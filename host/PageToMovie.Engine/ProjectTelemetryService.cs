using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using PageToMovie.Core.Utils;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Append-only project telemetry under <c>projects/{id}/telemetry/</c>:
/// <list type="bullet">
/// <item><c>api_calls.jsonl</c> — live model/API calls (full prompts)</item>
/// <item><c>media_ops.jsonl</c> — optional condensed local media ops (legacy name was ffmpeg.jsonl)</item>
/// </list>
/// Project id from <see cref="UseProject"/> scope, else <see cref="ProjectStore.ActiveProjectId"/>.
/// Native server ffmpeg is gone; media_ops is rarely written.
/// </summary>
public sealed class ProjectTelemetryService
{
    private static readonly AsyncLocal<string?> ScopedProjectId = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private static readonly Regex ProgressFluff = new(
        @"^(frame=|fps=|bitrate=|total_size=|out_time|dup=|drop=|speed=|progress=)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ProjectStore _projects;
    private readonly UserDatabaseService? _userDb;
    private readonly ILogger<ProjectTelemetryService> _log;

    public ProjectTelemetryService(
        ProjectStore projects,
        ILogger<ProjectTelemetryService> log,
        UserDatabaseService? userDb = null)
    {
        _projects = projects;
        _log = log;
        _userDb = userDb;
    }

    /// <summary>Bind telemetry writes to a project for the current async flow.</summary>
    public IDisposable UseProject(string projectId)
    {
        var prev = ScopedProjectId.Value;
        ScopedProjectId.Value = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim();
        return new ScopePop(() => ScopedProjectId.Value = prev);
    }

    public string? CurrentProjectId =>
        !string.IsNullOrWhiteSpace(ScopedProjectId.Value)
            ? ScopedProjectId.Value
            : string.IsNullOrWhiteSpace(_projects.ActiveProjectId)
                ? null
                : _projects.ActiveProjectId;

    public string TelemetryDir(string projectId) =>
        Path.Combine(_projects.GetProjectDir(projectId), "telemetry");

    public string ApiCallsPath(string projectId) =>
        Path.Combine(TelemetryDir(projectId), "api_calls.jsonl");

    /// <summary>Preferred path for local media-op telemetry (replaces ffmpeg.jsonl).</summary>
    public string MediaOpsPath(string projectId) =>
        Path.Combine(TelemetryDir(projectId), "media_ops.jsonl");

    /// <summary>Legacy alias for <see cref="MediaOpsPath"/>.</summary>
    [Obsolete("Use MediaOpsPath — server no longer runs native ffmpeg.")]
    public string FfmpegPath(string projectId) => MediaOpsPath(projectId);

    public async Task LogApiCallAsync(ApiCallTelemetry rec, CancellationToken ct = default)
    {
        var projectId = rec.ProjectId ?? CurrentProjectId;
        rec.UserId ??= UserApiCallScope.UserId;
        if (rec.Ts is null)
            rec.Ts = DateTimeOffset.UtcNow;
        if (string.IsNullOrWhiteSpace(rec.Provider) && !string.IsNullOrWhiteSpace(rec.Model))
            rec.Provider = TryProviderForModel(rec.Model, rec.Kind);
        rec.EstimatedUsd ??= EstimateListRateUsd(rec);

        // Project jsonl (full prompts) when a project is in scope.
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            rec.ProjectId = projectId;
            try
            {
                await AppendJsonlAsync(ApiCallsPath(projectId), rec, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "api_calls append failed for {ProjectId}", projectId);
            }
        }
        else
        {
            _log.LogDebug("api_calls project skip — no project id (kind={Kind})", rec.Kind);
        }

        // Always attribute to user SQLite when we know who paid / whose key ran.
        if (_userDb is not null && !string.IsNullOrWhiteSpace(rec.UserId))
        {
            try
            {
                await _userDb.InsertUserApiCallAsync(rec, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "user_api_calls insert failed for {UserId}", rec.UserId);
            }
        }
    }

    private static string? TryProviderForModel(string model, string? kind)
    {
        try
        {
            var cap = kind?.ToLowerInvariant() switch
            {
                "image" or "image_edit" => ModelCapability.Image,
                "video" or "video_extend" or "video_poll" => ModelCapability.Video,
                "vision" => ModelCapability.Vision,
                "audio" or "tts" or "music" => ModelCapability.Audio,
                "voice" => ModelCapability.Voice,
                _ => ModelCapability.Chat,
            };
            return SupportedModelCatalog.Find(model, cap)?.ProviderId
                   ?? SupportedModelCatalog.Find(model)?.ProviderId;
        }
        catch { return null; }
    }

    /// <summary>Catalog list-rate estimate — not a provider invoice line.</summary>
    public static double? EstimateListRateUsd(ApiCallTelemetry rec)
    {
        try
        {
            var model = rec.Model ?? "";
            var kind = (rec.Kind ?? "").ToLowerInvariant();
            if (kind is "image" or "image_edit")
            {
                var entry = SupportedModelCatalog.Find(model, ModelCapability.Image)
                            ?? SupportedModelCatalog.ResolveOrDefault(model, ModelCapability.Image);
                var unit = entry.ImageCostPerImage ?? 0.02;
                var n = Math.Max(1, rec.ImageCount ?? 1);
                return Math.Round(unit * n, 6);
            }
            if (kind is "video" or "video_extend")
            {
                var entry = SupportedModelCatalog.Find(model, ModelCapability.Video)
                            ?? SupportedModelCatalog.ResolveOrDefault(model, ModelCapability.Video);
                var res = rec.Resolution ?? "480p";
                double rate = 0.05;
                if (entry.VideoCostPerSecondByResolution is { } table)
                {
                    if (!table.TryGetValue(res, out rate))
                        rate = table.Values.DefaultIfEmpty(0.05).First();
                }
                var sec = rec.DurationSec ?? 6;
                return Math.Round(rate * sec, 6);
            }
            // chat / vision / other text: token rates or char/4 proxy
            var chat = SupportedModelCatalog.Find(model, ModelCapability.Chat)
                       ?? SupportedModelCatalog.Find(model, ModelCapability.Vision)
                       ?? SupportedModelCatalog.ResolveOrDefault(model, ModelCapability.Chat);
            var inPerM = chat.InputCostPerMillionTokens ?? 0;
            var outPerM = chat.OutputCostPerMillionTokens ?? 0;
            if (inPerM <= 0 && outPerM <= 0)
                return null;
            var inTok = rec.InputTokens
                        ?? Math.Max(0, (rec.PromptChars ?? ((rec.SystemPrompt?.Length ?? 0) + (rec.UserPrompt?.Length ?? 0))) / 4);
            var outTok = rec.OutputTokens
                         ?? Math.Max(0, (rec.ResponseChars ?? (rec.ResponsePreview?.Length ?? 0)) / 4);
            var usd = (inTok / 1_000_000.0) * inPerM + (outTok / 1_000_000.0) * outPerM;
            return usd > 0 ? Math.Round(usd, 6) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Append a condensed local media op.</summary>
    public async Task LogMediaOpAsync(FfmpegOpTelemetry rec, CancellationToken ct = default)
    {
        var projectId = rec.ProjectId ?? CurrentProjectId;
        if (string.IsNullOrWhiteSpace(projectId))
        {
            _log.LogDebug("media_ops skip — no project id (op={Op})", rec.Op);
            return;
        }

        rec.ProjectId = projectId;
        if (rec.Ts is null)
            rec.Ts = DateTimeOffset.UtcNow;

        try
        {
            await AppendJsonlAsync(MediaOpsPath(projectId), rec, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "media_ops append failed for {ProjectId}", projectId);
        }
    }

    /// <summary>
    /// Build condensed media-op telemetry from raw process log + args.
    /// Drops frame/fps spam; keeps interesting lines + sparse progress samples.
    /// </summary>
    public static FfmpegOpTelemetry CondenseMediaOp(
        string op,
        string args,
        IReadOnlyList<string>? inputs,
        string? output,
        int exitCode,
        bool timedOut,
        long wallMs,
        string? rawLog,
        string? ffmpegExe = null,
        int? scene = null,
        int? includedCount = null,
        int? excludedCount = null,
        string? fallback = null,
        string? projectId = null)
    {
        var interesting = new List<string>();
        var progressSamples = new List<string>();
        string? lastTime = null;
        string? lastSpeed = null;

        if (!string.IsNullOrEmpty(rawLog))
        {
            foreach (var line in rawLog.Split('\n'))
            {
                var t = line.Trim();
                if (t.Length == 0) continue;

                if (t.StartsWith("out_time=", StringComparison.OrdinalIgnoreCase) ||
                    t.Contains("time=", StringComparison.OrdinalIgnoreCase))
                {
                    lastTime = t.Length > 120 ? t[..120] : t;
                    // Sample sparsely: keep first, then every ~8th-ish by counting
                    if (progressSamples.Count == 0 ||
                        progressSamples.Count < 8 && progressSamples.Count % 2 == 0)
                    {
                        if (progressSamples.Count == 0 ||
                            !string.Equals(progressSamples[^1], lastTime, StringComparison.Ordinal))
                            progressSamples.Add(lastTime);
                    }
                    continue;
                }

                if (t.StartsWith("speed=", StringComparison.OrdinalIgnoreCase))
                {
                    lastSpeed = t;
                    continue;
                }

                if (ProgressFluff.IsMatch(t) && !IsInterestingLogLine(t))
                    continue;

                if (IsInterestingLogLine(t))
                {
                    interesting.Add(t.Length > 300 ? t[..300] : t);
                    if (interesting.Count >= 40) break;
                }
            }
        }

        // Cap progress samples
        if (progressSamples.Count > 8)
        {
            progressSamples = new List<string>
            {
                progressSamples[0],
                progressSamples[progressSamples.Count / 4],
                progressSamples[progressSamples.Count / 2],
                progressSamples[progressSamples.Count * 3 / 4],
                progressSamples[^1],
            };
        }

        if (lastTime is not null &&
            (progressSamples.Count == 0 || progressSamples[^1] != lastTime))
            progressSamples.Add(lastTime);

        return new FfmpegOpTelemetry
        {
            ProjectId = projectId,
            Op = op,
            Args = args,
            Inputs = inputs?.ToList(),
            Output = output,
            ExitCode = exitCode,
            TimedOut = timedOut,
            WallMs = wallMs,
            ToolPath = ffmpegExe,
            FfmpegPath = ffmpegExe,
            Scene = scene,
            IncludedCount = includedCount,
            ExcludedCount = excludedCount,
            Fallback = fallback,
            Progress = progressSamples.Count > 0 ? progressSamples : null,
            StderrInteresting = interesting.Count > 0 ? interesting : null,
            Stats = lastTime is not null || lastSpeed is not null
                ? new Dictionary<string, object?>
                {
                    ["lastTime"] = lastTime,
                    ["lastSpeed"] = lastSpeed,
                }
                : null,
        };
    }

    /// <summary>Legacy alias for <see cref="CondenseMediaOp"/>.</summary>
    [Obsolete("Use CondenseMediaOp.")]
    public static FfmpegOpTelemetry CondenseFfmpegOp(
        string op,
        string args,
        IReadOnlyList<string>? inputs,
        string? output,
        int exitCode,
        bool timedOut,
        long wallMs,
        string? rawLog,
        string? ffmpegExe = null,
        int? scene = null,
        int? includedCount = null,
        int? excludedCount = null,
        string? fallback = null,
        string? projectId = null) =>
        CondenseMediaOp(
            op, args, inputs, output, exitCode, timedOut, wallMs, rawLog,
            ffmpegExe, scene, includedCount, excludedCount, fallback, projectId);

    public static bool IsInterestingLogLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        if (line.Contains("error", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.Contains("warning", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.Contains("failed", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.Contains("Invalid", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.Contains("No such", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.Contains("Conversion failed", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.Contains("not found", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.Contains("Error opening", StringComparison.OrdinalIgnoreCase)) return true;
        if (line.StartsWith("ffmpeg version", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    // Per-path gate, kept on this instance (not JsonlStore's internal one) so a future reader of
    // api_calls.jsonl/media_ops.jsonl can coordinate against the exact same lock instance a write
    // is using (see MtimeValidatedFileCache<T, RealSemaphore>) instead of a read landing mid-append.
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileAsyncLocks =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task AppendJsonlAsync(string path, object rec, CancellationToken ct = default)
    {
        var gate = _fileAsyncLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await JsonlStore.AppendAsync(path, rec, JsonOpts, gate, ct).ConfigureAwait(false);
    }

    private sealed class ScopePop : IDisposable
    {
        private readonly Action _onDispose;
        private int _done;
        public ScopePop(Action onDispose) => _onDispose = onDispose;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
                _onDispose();
        }
    }
}

/// <summary>One live API call (full prompts on disk for project review).</summary>
public sealed class ApiCallTelemetry
{
    public DateTimeOffset? Ts { get; set; }
    public string? ProjectId { get; set; }
    /// <summary>Signed-in user who triggered the call (BYOK / cost attribution).</summary>
    public string? UserId { get; set; }
    /// <summary>Provider id: grok, gemini, openai, anthropic, fal, …</summary>
    public string? Provider { get; set; }
    /// <summary>List-rate USD estimate at call time (catalog). Not a provider invoice.</summary>
    public double? EstimatedUsd { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    /// <summary>video | video_extend | video_poll | image | image_edit | vision | chat | tts | …</summary>
    public string Kind { get; set; } = "";
    public string? Endpoint { get; set; }
    public string? Model { get; set; }
    public int? HttpStatus { get; set; }
    public string? RequestId { get; set; }
    public string? Error { get; set; }
    public long? DurationMs { get; set; }
    public int? Scene { get; set; }
    public int? Clip { get; set; }
    public string? CharKey { get; set; }
    /// <summary>
    /// Call purpose tag. Chat: <c>book_to_fountain</c>, <c>cast_from_screenplay</c>, …
    /// (see <c>ChatCallModes</c>). Video: <c>fresh</c> | <c>video-extend</c> | <c>reseed</c> | …
    /// </summary>
    public string? Mode { get; set; }
    public string? Prompt { get; set; }
    public string? SystemPrompt { get; set; }
    public string? UserPrompt { get; set; }
    public string? ResponsePreview { get; set; }
    public List<string>? ReferenceImagePaths { get; set; }
    public bool? RefsAttached { get; set; }
    public string? Resolution { get; set; }
    public double? DurationSec { get; set; }
    public int? Attempt { get; set; }
    public string? JobId { get; set; }
    public bool Fakes { get; set; }
    public int? ImageCount { get; set; }
    public int? PromptChars { get; set; }
    public int? ResponseChars { get; set; }
    public bool Ok { get; set; } = true;
}

/// <summary>One condensed local media operation (historical name: ffmpeg op).</summary>
public sealed class FfmpegOpTelemetry
{
    public DateTimeOffset? Ts { get; set; }
    public string? ProjectId { get; set; }
    public string Op { get; set; } = "";
    public string? Args { get; set; }
    public List<string>? Inputs { get; set; }
    public string? Output { get; set; }
    public int ExitCode { get; set; }
    public bool TimedOut { get; set; }
    public long WallMs { get; set; }
    /// <summary>Tool binary path if any (legacy field name FfmpegPath).</summary>
    public string? ToolPath { get; set; }
    public string? FfmpegPath { get; set; }
    public int? Scene { get; set; }
    public int? IncludedCount { get; set; }
    public int? ExcludedCount { get; set; }
    public string? Fallback { get; set; }
    public List<string>? Progress { get; set; }
    public List<string>? StderrInteresting { get; set; }
    public Dictionary<string, object?>? Stats { get; set; }
    public bool Ok => !TimedOut && ExitCode == 0;
}
