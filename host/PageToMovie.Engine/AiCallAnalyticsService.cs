using System.Text.Json;
using PageToMovie.Core.Models;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Read side of the AI-call feedback loop: scans every project's telemetry/api_calls.jsonl and rolls
/// the records up into <see cref="AiCallAnalyticsDto"/> for the admin analytics page. Outcomes are
/// derived from the fields we already record (Ok / HttpStatus / Attempt / Error) until the unified
/// AiCallRecord lands with an explicit outcome enum. Read-only — never writes.
/// </summary>
public sealed class AiCallAnalyticsService
{
    private readonly ProjectStore _projects;
    private readonly ProjectTelemetryService _telemetry;
    private readonly ILogger<AiCallAnalyticsService> _log;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AiCallAnalyticsService(ProjectStore projects, ProjectTelemetryService telemetry, ILogger<AiCallAnalyticsService> log)
    {
        _projects = projects;
        _telemetry = telemetry;
        _log = log;
    }

    /// <param name="maxLinesPerProject">Tail cap per project so one huge log can't dominate the scan.</param>
    public async Task<AiCallAnalyticsDto> BuildAsync(int maxLinesPerProject = 4000, CancellationToken ct = default)
    {
        var dto = new AiCallAnalyticsDto { GeneratedAt = DateTimeOffset.UtcNow };
        var ops = new Dictionary<string, OpAcc>(StringComparer.OrdinalIgnoreCase);
        var models = new Dictionary<string, ModelAcc>(StringComparer.OrdinalIgnoreCase);
        long durationSum = 0; int durationCount = 0;
        var failures = new List<AiFailureSample>();

        IReadOnlyList<ProjectInfo> projects;
        try { projects = await _projects.ListProjectsAsync(ct).ConfigureAwait(false); }
        catch (Exception ex) { _log.LogWarning(ex, "ai-call analytics: project list failed"); return dto; }

        foreach (var p in projects)
        {
            ct.ThrowIfCancellationRequested();
            var id = p.Id;
            if (string.IsNullOrWhiteSpace(id)) continue;
            var path = _telemetry.ApiCallsPath(id);
            if (!File.Exists(path)) continue;
            dto.ProjectsScanned++;

            string[] lines;
            try { lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false); }
            catch (Exception ex) { _log.LogDebug(ex, "ai-call analytics: read failed {Path}", path); continue; }

            var start = Math.Max(0, lines.Length - maxLinesPerProject);
            for (var i = start; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                ApiCallTelemetry? rec;
                try { rec = JsonSerializer.Deserialize<ApiCallTelemetry>(line, JsonOpts); }
                catch { continue; }
                if (rec is null) continue;

                dto.TotalCalls++;
                if (rec.Fakes) dto.FakeCalls++;
                var cost = rec.ChargeUsd ?? rec.EstimatedUsd ?? 0;
                dto.TotalCostUsd += cost;
                if (rec.DurationMs is { } d) { durationSum += d; durationCount++; }

                var retried = rec.Ok && rec.Attempt is > 1;
                if (!rec.Ok) dto.FailedCalls++;
                else if (retried) dto.RetriedCalls++;
                else dto.OkCalls++;

                var op = string.IsNullOrWhiteSpace(rec.Kind) ? "(unknown)" : rec.Kind.Trim().ToLowerInvariant();
                var oa = ops.TryGetValue(op, out var e1) ? e1 : ops[op] = new OpAcc();
                oa.Calls++; oa.Cost += cost;
                if (rec.DurationMs is { } od) { oa.DurSum += od; oa.DurCount++; }
                if (!rec.Ok) oa.Failed++; else if (retried) oa.Retried++;

                var model = string.IsNullOrWhiteSpace(rec.Model) ? "(none)" : rec.Model.Trim();
                var ma = models.TryGetValue(model, out var e2) ? e2 : models[model] = new ModelAcc { Provider = rec.Provider ?? "" };
                ma.Calls++; ma.Cost += cost; if (!rec.Ok) ma.Failed++;

                if (!rec.Ok && failures.Count < 400)
                    failures.Add(new AiFailureSample
                    {
                        Ts = rec.Ts, Op = op, Model = model, HttpStatus = rec.HttpStatus,
                        FailureKind = ClassifyFailure(rec), ProjectId = id,
                        Error = Trim(rec.Error, 240),
                    });
            }
        }

        dto.AvgDurationMs = durationCount == 0 ? 0 : Math.Round((double)durationSum / durationCount, 0);
        dto.TotalCostUsd = Math.Round(dto.TotalCostUsd, 4);

        dto.Ops = ops.Select(kv => new AiOpStat
        {
            Op = kv.Key, Calls = kv.Value.Calls, Retried = kv.Value.Retried, Failed = kv.Value.Failed,
            CostUsd = Math.Round(kv.Value.Cost, 4),
            AvgDurationMs = kv.Value.DurCount == 0 ? 0 : Math.Round((double)kv.Value.DurSum / kv.Value.DurCount, 0),
        }).OrderByDescending(o => o.Calls).ToList();

        dto.Models = models.Select(kv => new AiModelStat
        {
            Model = kv.Key, Provider = kv.Value.Provider, Calls = kv.Value.Calls, Failed = kv.Value.Failed,
            CostUsd = Math.Round(kv.Value.Cost, 4),
        }).OrderByDescending(m => m.Calls).ToList();

        dto.RecentFailures = failures.OrderByDescending(f => f.Ts ?? DateTimeOffset.MinValue).Take(25).ToList();
        dto.Learnings = BuildLearnings(dto);
        dto.WindowNote = $"Last {maxLinesPerProject:N0} calls/project across {dto.ProjectsScanned} project(s)"
            + (dto.FakeCalls == dto.TotalCalls && dto.TotalCalls > 0 ? " — all fakes-mode calls" : "");
        return dto;
    }

    private static List<string> BuildLearnings(AiCallAnalyticsDto d)
    {
        var outp = new List<string>();
        if (d.TotalCalls == 0) { outp.Add("No AI calls have been logged yet — run the pipeline (or a live generation) to collect data."); return outp; }

        outp.Add($"{d.SuccessRatePct}% of {d.TotalCalls:N0} calls succeeded ({d.RetriedCalls:N0} only after a retry).");

        var worstOp = d.Ops.Where(o => o.Calls >= 3).OrderByDescending(o => o.FailPct).FirstOrDefault();
        if (worstOp is { FailPct: > 0 })
            outp.Add($"Highest failure rate: “{worstOp.Op}” at {worstOp.FailPct}% ({worstOp.Failed}/{worstOp.Calls}).");

        var retryHeavy = d.Ops.Where(o => o.Calls >= 3).OrderByDescending(o => o.RetryPct).FirstOrDefault();
        if (retryHeavy is { RetryPct: > 0 })
            outp.Add($"Most retry-dependent: “{retryHeavy.Op}” needed a retry {retryHeavy.RetryPct}% of the time.");

        var costliest = d.Ops.OrderByDescending(o => o.CostUsd).FirstOrDefault();
        if (costliest is { CostUsd: > 0 })
            outp.Add($"Costliest operation: “{costliest.Op}” at ${costliest.CostUsd:N2}.");

        var slowest = d.Ops.Where(o => o.Calls >= 3).OrderByDescending(o => o.AvgDurationMs).FirstOrDefault();
        if (slowest is { AvgDurationMs: > 0 })
            outp.Add($"Slowest operation: “{slowest.Op}” averaging {slowest.AvgDurationMs:N0} ms.");

        if (d.FakeCalls > 0 && d.FakeCalls < d.TotalCalls)
            outp.Add($"{d.FakeCalls:N0} of {d.TotalCalls:N0} calls were fakes-mode (excluded from real spend).");

        return outp;
    }

    /// <summary>Best-effort failure taxonomy from today's fields — a placeholder for the unified outcome enum.</summary>
    private static string ClassifyFailure(ApiCallTelemetry r)
    {
        if (r.HttpStatus is 429) return "rate_limited";
        if (r.HttpStatus is >= 500) return "provider_error";
        var e = (r.Error ?? "").ToLowerInvariant();
        if (e.Length == 0) return r.HttpStatus is > 0 ? $"http_{r.HttpStatus}" : "error";
        if (e.Contains("timeout") || e.Contains("timed out")) return "timeout";
        if (e.Contains("could not read the portrait") || e.Contains("blind")) return "vision_blind";
        if (e.Contains("does not match the project style")) return "validation_reject";
        if (e.Contains("parse") || e.Contains("unreadable") || e.Contains("json")) return "parse_error";
        if (e.Contains("cancel")) return "cancelled";
        return "error";
    }

    private static string Trim(string? s, int max) => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

    private sealed class OpAcc { public int Calls; public int Retried; public int Failed; public double Cost; public long DurSum; public int DurCount; }
    private sealed class ModelAcc { public string Provider = ""; public int Calls; public int Failed; public double Cost; }
}
