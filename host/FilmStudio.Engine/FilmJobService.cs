using System.Collections.Concurrent;
using System.Text.Json;
using FilmStudio.Core.Models;
using FilmStudio.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FilmStudio.Engine;

public interface IJobProgressSink
{
    Task OnJobUpdatedAsync(JobSnapshot snapshot, CancellationToken ct = default);
    Task OnJobLogAsync(string message, CancellationToken ct = default);
}

/// <summary>
/// C# film job orchestrator: loads blueprint clips and generates missing ones via Grok.
/// Full Python feature parity (multi-ref, identity packer, WIP remux) remains available via Python;
/// this is the native backend path for multi-user / Blazor / SignalR.
/// </summary>
public sealed class FilmJobService
{
    private readonly ProjectStore _projects;
    private readonly GrokVideoClient _grok;
    private readonly FilmStudioOptions _opts;
    private readonly ILogger<FilmJobService> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentQueue<string> _logLines = new();
    private CancellationTokenSource? _cts;
    private JobSnapshot _snapshot = new() { Status = "idle" };
    private IJobProgressSink? _sink;

    public FilmJobService(
        ProjectStore projects,
        GrokVideoClient grok,
        IOptions<FilmStudioOptions> opts,
        ILogger<FilmJobService> log)
    {
        _projects = projects;
        _grok = grok;
        _opts = opts.Value;
        _log = log;
    }

    public void SetProgressSink(IJobProgressSink sink) => _sink = sink;

    public JobSnapshot GetSnapshot() => Clone(_snapshot);

    public bool IsRunning =>
        string.Equals(_snapshot.Status, "running", StringComparison.OrdinalIgnoreCase);

    public async Task CancelAsync()
    {
        _cts?.Cancel();
        await AppendLogAsync("Cancel requested…");
        await UpdateAsync(s => s.Message = "Cancel requested — finishing current step if possible…");
    }

    public async Task StartSceneGenAsync(StartSceneGenRequest req)
    {
        if (!await _gate.WaitAsync(0))
            throw new InvalidOperationException("A generation job is already running.");

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await RunSceneGenAsync(req, ct);
            }
            finally
            {
                _gate.Release();
            }
        }, CancellationToken.None);

        await Task.CompletedTask;
    }

    public async Task StartBatchGenAsync(StartBatchGenRequest req)
    {
        if (req.Scenes is null || req.Scenes.Count == 0)
            throw new InvalidOperationException("At least one scene is required.");

        if (!await _gate.WaitAsync(0))
            throw new InvalidOperationException("A generation job is already running.");

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await RunBatchGenAsync(req, ct);
            }
            finally
            {
                _gate.Release();
            }
        }, CancellationToken.None);

        await Task.CompletedTask;
    }

    private async Task RunBatchGenAsync(StartBatchGenRequest req, CancellationToken ct)
    {
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        _projects.Activate(projectId);

        var scenes = req.Scenes.Distinct().OrderBy(s => s).ToList();
        _snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "batch",
            ProjectId = projectId,
            Message = $"Batch: {scenes.Count} scene(s)…",
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
        await PublishAsync();

        try
        {
            if (!_grok.IsConfigured)
                throw new InvalidOperationException("XAI_API_KEY is not set.");

            using var bp = _projects.LoadBlueprint(projectId)
                ?? throw new InvalidOperationException(
                    $"No Stage 2 blueprint for project {projectId}. Run Stage 2 first.");

            var projectDir = _projects.GetProjectDir(projectId);
            Directory.CreateDirectory(Path.Combine(projectDir, "assets", "video"));

            // Pre-count work units
            var work = new List<(int Scene, int Clip, JsonElement ClipEl)>();
            foreach (var sn in scenes)
            {
                var sceneEl = FindScene(bp.RootElement, sn);
                if (sceneEl is null)
                {
                    await AppendLogAsync($"Scene {sn}: not in blueprint — skip");
                    continue;
                }
                if (!sceneEl.Value.TryGetProperty("veo_clips", out var clipsEl) ||
                    clipsEl.ValueKind != JsonValueKind.Array)
                {
                    await AppendLogAsync($"Scene {sn}: no veo_clips — skip");
                    continue;
                }

                foreach (var c in clipsEl.EnumerateArray())
                {
                    var cn = c.TryGetProperty("clip_number", out var n) && n.TryGetInt32(out var v) ? v : 0;
                    if (cn <= 0) continue;
                    var path = Path.Combine(projectDir, "assets", "video", $"scene_{sn:D2}_clip_{cn:D2}.mp4");
                    var missing = !File.Exists(path) || new FileInfo(path).Length < 1024;
                    if (!req.OnlyMissing || missing)
                        work.Add((sn, cn, c.Clone()));
                }
            }

            if (work.Count == 0)
            {
                await AppendLogAsync("Batch: nothing to generate (only_missing).");
                await FinishAsync("done", "No clips to generate");
                return;
            }

            await UpdateAsync(s =>
            {
                s.Total = work.Count;
                s.Index = 0;
                s.Message = $"Batch: {work.Count} clip(s) across {scenes.Count} scene(s)";
            });
            await AppendLogAsync(_snapshot.Message!);

            var done = 0;
            var failed = 0;
            for (var i = 0; i < work.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (sn, cn, clip) = work[i];
                await UpdateAsync(s =>
                {
                    s.Index = i + 1;
                    s.Scene = sn;
                    s.Clip = cn;
                    s.Message = $"Generating S{sn:D2} C{cn} ({i + 1}/{work.Count})…";
                });
                await AppendLogAsync(_snapshot.Message!);

                try
                {
                    await GenerateOneClipAsync(projectDir, sn, cn, clip, ct);
                    done++;
                    await AppendLogAsync($"Done S{sn:D2} C{cn}");
                }
                catch (OperationCanceledException)
                {
                    await FinishAsync("cancelled", "Cancelled by user");
                    return;
                }
                catch (Exception ex)
                {
                    failed++;
                    _log.LogError(ex, "Clip S{Scene}C{Clip} failed", sn, cn);
                    await AppendLogAsync($"Failed S{sn:D2} C{cn}: {ex.Message}");
                }
            }

            var status = failed > 0 && done == 0 ? "error" : "done";
            var msg = failed > 0
                ? $"Batch finished with errors ({done} ok, {failed} failed)"
                : $"Batch finished ({done} clip(s))";
            await FinishAsync(status, msg, failed > 0 ? msg : null);
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Cancelled by user");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Batch gen failed");
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }

    private async Task RunSceneGenAsync(StartSceneGenRequest req, CancellationToken ct)
    {
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        _projects.Activate(projectId);

        _snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "scene",
            ProjectId = projectId,
            Scene = req.Scene,
            Message = "Starting…",
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
        await PublishAsync();

        try
        {
            if (!_grok.IsConfigured)
                throw new InvalidOperationException("XAI_API_KEY is not set.");

            using var bp = _projects.LoadBlueprint(projectId)
                ?? throw new InvalidOperationException(
                    $"No Stage 2 blueprint for project {projectId}. Run Stage 2 first.");

            var sceneEl = FindScene(bp.RootElement, req.Scene)
                ?? throw new InvalidOperationException($"Scene {req.Scene} not in blueprint.");

            if (!sceneEl.TryGetProperty("veo_clips", out var clipsEl) ||
                clipsEl.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"Scene {req.Scene} has no veo_clips.");
            }

            var clips = clipsEl.EnumerateArray().ToList();
            var projectDir = _projects.GetProjectDir(projectId);
            var videoDir = Path.Combine(projectDir, "assets", "video");
            Directory.CreateDirectory(videoDir);

            var todo = new List<(int ClipNum, JsonElement Clip)>();
            foreach (var c in clips)
            {
                var cn = c.TryGetProperty("clip_number", out var n) && n.TryGetInt32(out var v) ? v : 0;
                if (cn <= 0) continue;
                var path = Path.Combine(videoDir, $"scene_{req.Scene:D2}_clip_{cn:D2}.mp4");
                var missing = !File.Exists(path) || new FileInfo(path).Length < 1024;
                if (!req.OnlyMissing || missing)
                    todo.Add((cn, c.Clone()));
            }

            if (todo.Count == 0)
            {
                await AppendLogAsync($"Scene {req.Scene}: nothing to generate (only_missing).");
                await FinishAsync("done", "No clips to generate");
                return;
            }

            var startMsg = $"Scene {req.Scene}: {todo.Count} clip(s)";
            await UpdateAsync(s =>
            {
                s.Total = todo.Count;
                s.Index = 0;
                s.Message = startMsg;
            });
            await AppendLogAsync(startMsg);

            var done = 0;
            var failed = 0;
            for (var i = 0; i < todo.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (cn, clip) = todo[i];
                await UpdateAsync(s =>
                {
                    s.Index = i + 1;
                    s.Clip = cn;
                    s.Message = $"Generating S{req.Scene:D2} C{cn} ({i + 1}/{todo.Count})…";
                });
                await AppendLogAsync(_snapshot.Message!);

                try
                {
                    await GenerateOneClipAsync(projectDir, req.Scene, cn, clip, ct);
                    done++;
                    await AppendLogAsync($"Done S{req.Scene:D2} C{cn}");
                }
                catch (OperationCanceledException)
                {
                    await FinishAsync("cancelled", "Cancelled by user");
                    return;
                }
                catch (Exception ex)
                {
                    failed++;
                    _log.LogError(ex, "Clip S{Scene}C{Clip} failed", req.Scene, cn);
                    await AppendLogAsync($"Failed S{req.Scene:D2} C{cn}: {ex.Message}");
                }
            }

            var status = failed > 0 && done == 0 ? "error" : "done";
            var msg = failed > 0
                ? $"Finished with errors ({done} ok, {failed} failed)"
                : $"Generation finished ({done} clip(s))";
            await FinishAsync(status, msg, failed > 0 ? msg : null);
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Cancelled by user");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Scene gen failed");
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }

    private async Task GenerateOneClipAsync(
        string projectDir,
        int scene,
        int clip,
        JsonElement clipEl,
        CancellationToken ct)
    {
        var prompt = clipEl.TryGetProperty("visual_prompt", out var vp)
            ? vp.GetString() ?? ""
            : "";
        if (string.IsNullOrWhiteSpace(prompt))
            throw new InvalidOperationException("clip missing visual_prompt");

        // Style lock reinforce for humans (port of product rule)
        if (prompt.Contains("Character_Mom", StringComparison.Ordinal) ||
            prompt.Contains("Character_Daddy", StringComparison.Ordinal) ||
            prompt.Contains("Character_Mom", StringComparison.OrdinalIgnoreCase))
        {
            if (!prompt.Contains("STYLE LOCK", StringComparison.OrdinalIgnoreCase))
            {
                prompt =
                    "STYLE LOCK: stylized 3D animated children's picture-book CG " +
                    "(same render family as the cartoon dog) -- not photoreal, not live-action. " +
                    prompt;
            }
        }

        if (prompt.Length > 4000)
            prompt = prompt[..3990] + "…";

        var duration = _opts.DefaultDurationSeconds;
        if (clipEl.TryGetProperty("duration_seconds", out var d) && d.TryGetInt32(out var ds))
            duration = Math.Clamp(ds, 1, 15);
        // ref-to-video max often 10; text-only keep config default
        duration = Math.Min(duration, 10);

        var model = _opts.DefaultModel;
        var resolution = _opts.DefaultResolution;

        await AppendLogAsync($"  [Grok] Submit S{scene:D2}C{clip} duration={duration}s res={resolution}");
        var requestId = await _grok.SubmitGenerationAsync(prompt, duration, resolution, model, ct);
        await AppendLogAsync($"  [Grok] request_id={requestId}");

        var url = await _grok.PollForVideoUrlAsync(
            requestId,
            msg => { _ = AppendLogAsync($"  [Grok] {msg}"); },
            ct);

        var outPath = Path.Combine(
            projectDir, "assets", "video", $"scene_{scene:D2}_clip_{clip:D2}.mp4");
        await _grok.DownloadToFileAsync(url, outPath, ct);
        await AppendLogAsync($"  [Grok] saved {outPath}");
    }

    private static JsonElement? FindScene(JsonElement root, int sceneNum)
    {
        if (!root.TryGetProperty("scenes", out var scenes) ||
            scenes.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var s in scenes.EnumerateArray())
        {
            if (s.TryGetProperty("scene_number", out var n) && n.TryGetInt32(out var sn) && sn == sceneNum)
                return s;
        }
        return null;
    }

    private async Task AppendLogAsync(string message)
    {
        _logLines.Enqueue(message);
        await UpdateAsync(s =>
        {
            s.Log.Add(message);
            if (s.Log.Count > 80)
                s.Log = s.Log.TakeLast(80).ToList();
            s.Message = message;
        });
        if (_sink is not null)
            await _sink.OnJobLogAsync(message);
    }

    private async Task UpdateAsync(Action<JobSnapshot> mutate)
    {
        mutate(_snapshot);
        await PublishAsync();
    }

    private async Task FinishAsync(string status, string message, string? error = null)
    {
        await UpdateAsync(s =>
        {
            s.Status = status;
            s.Message = message;
            s.Error = error;
            s.FinishedAt = DateTimeOffset.UtcNow;
        });
        await AppendLogAsync(message);
    }

    private async Task PublishAsync()
    {
        if (_sink is not null)
            await _sink.OnJobUpdatedAsync(Clone(_snapshot));
    }

    private static JobSnapshot Clone(JobSnapshot s) => new()
    {
        Status = s.Status,
        Kind = s.Kind,
        Message = s.Message,
        ProjectId = s.ProjectId,
        Scene = s.Scene,
        Clip = s.Clip,
        Index = s.Index,
        Total = s.Total,
        Log = s.Log.ToList(),
        Error = s.Error,
        StartedAt = s.StartedAt,
        FinishedAt = s.FinishedAt,
    };
}
