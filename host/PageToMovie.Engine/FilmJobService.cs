using System.Collections.Concurrent;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using PageToMovie.Engine.Abstractions;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PageToMovie.Engine;

public interface IJobProgressSink
{
    Task OnJobUpdatedAsync(JobSnapshot snapshot, CancellationToken ct = default);
    Task OnJobLogAsync(string message, CancellationToken ct = default);
}

/// <summary>
/// Native C# film job orchestrator (no Python): Stage 1/2, book prepare,
/// character design, multi-ref video, remux/WIP with SignalR progress.
/// Phase C: multi-job concurrency via ApiWorkerPool, scene locks, metrics.
/// </summary>
public sealed class FilmJobService
{
    private static readonly AsyncLocal<JobRunState?> CurrentRun = new();
    private static readonly TimeSpan DefaultLockTtl = TimeSpan.FromHours(2);

    private readonly ProjectStore _projects;
    private readonly IVideoClient _grok;
    private readonly CharacterDesignService _characters;
    private readonly CharacterBookPlateService _plates;
    private readonly BookPrepareService _books;
    private readonly IChatClient _chat;
    private readonly Stage1Service _stage1;
    private readonly Stage2PlannerService _stage2;
    private readonly VoicePreviewService _voicePreview;
    private readonly ClipAutoReviewService _clipAutoReview;
    private readonly ReviewIndexService _reviewIndex;
    private readonly ProjectTelemetryService _telemetry;
    private readonly ProjectArtifactIndexService _artifactIndex;
    private readonly ReviewEventStore _learning;
    private readonly EditLogService _editLogs;
    private readonly ProjectRulesService _projectRules;
    private readonly CostReportService _costs;
    private readonly CreditsGeneratorService _credits;
    private readonly IJobStore _jobs;
    private readonly ILockService _locks;
    private readonly ApiWorkerPool _apiPool;
    private readonly YouTubeAuthService _youTube;
    private readonly IServerMetricsService _metrics;
    private readonly MediaProxyTicketStore _mediaProxy;
    private readonly PageToMovieOptions _opts;
    private readonly ILogger<FilmJobService> _log;
    private readonly ConcurrentQueue<string> _logLines = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobCts =
        new(StringComparer.OrdinalIgnoreCase);
    private IJobProgressSink? _sink;
    private readonly IUserContext _user;
    private readonly IUserApiKeyProvider _keys;
    private readonly ClipSidecarService? _sidecars;
    private readonly ClipDialogueVerificationService? _dialogueVerification;
    private readonly GlobalTimingCalibrationService? _timingCalibration;
    private readonly ActionCameraOverheadLedger? _timingLedger;
    private readonly AiActionOverheadClassifier? _timingClassifier;
    private readonly IHttpClientFactory _httpFactory;

    public FilmJobService(
        ProjectStore projects,
        IVideoClient grok,
        CharacterDesignService characters,
        CharacterBookPlateService plates,
        BookPrepareService books,
        IChatClient chat,
        Stage1Service stage1,
        Stage2PlannerService stage2,
        VoicePreviewService voicePreview,
        ClipAutoReviewService clipAutoReview,
        ReviewIndexService reviewIndex,
        ProjectTelemetryService telemetry,
        ProjectArtifactIndexService artifactIndex,
        ReviewEventStore learning,
        EditLogService editLogs,
        ProjectRulesService projectRules,
        CostReportService costs,
        CreditsGeneratorService credits,
        IJobStore jobs,
        ILockService locks,
        ApiWorkerPool apiPool,
        YouTubeAuthService youTube,
        IServerMetricsService metrics,
        MediaProxyTicketStore mediaProxy,
        IOptions<PageToMovieOptions> opts,
        ILogger<FilmJobService> log,
        IUserContext user,
        IUserApiKeyProvider keys,
        IHttpClientFactory httpFactory,
        ClipSidecarService? sidecars = null,
        ClipDialogueVerificationService? dialogueVerification = null,
        GlobalTimingCalibrationService? timingCalibration = null,
        ActionCameraOverheadLedger? timingLedger = null,
        AiActionOverheadClassifier? timingClassifier = null)
    {
        _httpFactory = httpFactory;
        _projects = projects;
        _grok = grok;
        _characters = characters;
        _plates = plates;
        _books = books;
        _chat = chat;
        _stage1 = stage1;
        _stage2 = stage2;
        _voicePreview = voicePreview;
        _clipAutoReview = clipAutoReview;
        _reviewIndex = reviewIndex;
        _telemetry = telemetry;
        _artifactIndex = artifactIndex;
        _learning = learning;
        _editLogs = editLogs;
        _projectRules = projectRules;
        _costs = costs;
        _credits = credits;
        _jobs = jobs;
        _locks = locks;
        _apiPool = apiPool;
        _youTube = youTube;
        _mediaProxy = mediaProxy;
        _metrics = metrics;
        _opts = opts.Value;
        _log = log;
        _user = user;
        _keys = keys;
        _sidecars = sidecars;
        _dialogueVerification = dialogueVerification;
        _timingCalibration = timingCalibration;
        _timingLedger = timingLedger;
        _timingClassifier = timingClassifier;
    }

    public void SetProgressSink(IJobProgressSink sink) => _sink = sink;

    /// <summary>
    /// Primary job for the current caller (Phase F: no global singleton job).
    /// Prefers this user's running job, else their most recent, else idle.
    /// </summary>
    public JobSnapshot GetSnapshot()
    {
        var userId = string.IsNullOrWhiteSpace(_user.UserId) ? null : _user.UserId;
        var primary = _jobs.GetPrimary(userId);
        if (primary is not null)
            return primary.ToSnapshot();
        // Fallback: active AsyncLocal run (background worker thread)
        if (CurrentRun.Value?.Snapshot is { } live &&
            !string.Equals(live.Status, "idle", StringComparison.OrdinalIgnoreCase))
            return Clone(live);
        return new JobSnapshot { Status = "idle", UserId = userId };
    }

    public Task<JobSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(GetSnapshot());
    }

    public JobSnapshot? GetJob(string jobId) => _jobs.Get(jobId)?.ToSnapshot();

    public IReadOnlyList<JobSnapshot> ListJobs(string? userId = null, string? projectId = null, int take = 50) =>
        _jobs.List(userId, projectId, take).Select(j => j.ToSnapshot()).ToList();

    public bool IsRunning => _jobs.CountRunning() > 0;

    /// <summary>O(1) count of jobs currently running (hot path for /api/capacity).</summary>
    public int RunningCount => _jobs.CountRunning();

    public CapacityOptions Capacity => _opts.Capacity ?? new CapacityOptions();

    public ILockService Locks => _locks;

    public IServerMetricsService Metrics => _metrics;

    /// <summary>
    /// Cancel one job by id, or cancel active jobs in scope.
    /// </summary>
    /// <param name="jobId">When set, cancel only this job (ownership is enforced at the API).</param>
    /// <param name="userId">
    /// When canceling without <paramref name="jobId"/> and <paramref name="cancelAllUsers"/> is false,
    /// only cancel jobs owned by this user. Required for bulk cancel unless canceling all users.
    /// </param>
    /// <param name="cancelAllUsers">
    /// When true (admin only at API), cancel every active job regardless of owner.
    /// </param>
    /// <returns>Number of jobs that were marked cancelled / had CTS cancelled.</returns>
    public Task<int> CancelAsync(
        string? jobId = null,
        string? userId = null,
        bool cancelAllUsers = false)
    {
        if (!string.IsNullOrWhiteSpace(jobId))
        {
            var n = CancelOneJob(jobId!) ? 1 : 0;
            return Task.FromResult(n);
        }

        // Refuse unscoped bulk cancel — callers must pass userId or cancelAllUsers.
        if (!cancelAllUsers && string.IsNullOrWhiteSpace(userId))
            return Task.FromResult(0);

        var cancelled = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Prefer store records (have UserId) over bare CTS keys.
        var records = cancelAllUsers
            ? _jobs.List(userId: null, take: 200)
            : _jobs.List(userId: userId, take: 200);

        foreach (var rec in records)
        {
            if (!IsActiveJobStatus(rec.Status))
                continue;
            if (!seen.Add(rec.JobId))
                continue;
            if (CancelOneJob(rec.JobId))
                cancelled++;
        }

        // CTS entries that might lack a store row (edge case)
        foreach (var kv in _jobCts.ToArray())
        {
            if (!seen.Add(kv.Key))
                continue;
            var rec = _jobs.Get(kv.Key);
            if (!IsInBulkCancelScope(rec?.UserId, userId, cancelAllUsers))
                continue;
            if (CancelOneJob(kv.Key))
                cancelled++;
        }

        return Task.FromResult(cancelled);
    }

    /// <summary>Whether a job owner matches bulk-cancel scope (unit-tested).</summary>
    public static bool IsInBulkCancelScope(
        string? jobUserId,
        string? requestUserId,
        bool cancelAllUsers)
    {
        if (cancelAllUsers)
            return true;
        if (string.IsNullOrWhiteSpace(requestUserId))
            return false;
        return string.Equals(jobUserId, requestUserId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActiveJobStatus(string? status) =>
        string.Equals(status, "running", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase);

    private bool CancelOneJob(string jobId)
    {
        var storeHit = _jobs.TryCancel(jobId);
        var ctsHit = false;
        if (_jobCts.TryGetValue(jobId, out var cts))
        {
            try
            {
                cts.Cancel();
                ctsHit = true;
            }
            catch
            {
                /* ignore */
            }
        }
        return storeHit || ctsHit;
    }

    private void EnsureCanStart(string? userId)
    {
        var cap = Capacity;
        // Soft gate: running at global max still allows queue until per-user max;
        // worker pool will wait for a slot. Reject only when user queue is full.
        if (!string.IsNullOrWhiteSpace(userId) &&
            _jobs.CountQueuedForUser(userId!) >= Math.Max(1, cap.MaxQueuePerUser))
        {
            _metrics.NoteCapacityReject();
            throw new CapacityRejectedException(
                $"User queue full: MaxQueuePerUser={cap.MaxQueuePerUser}.");
        }

        // Hard reject if global running already >> 2x cap (runaway protection)
        var running = _jobs.CountRunning();
        var max = Math.Max(1, cap.MaxVideoInFlight);
        if (running >= max + Math.Max(1, cap.MaxQueuePerUser))
        {
            _metrics.NoteCapacityReject();
            throw new CapacityRejectedException(
                $"At capacity: running={running}, MaxVideoInFlight={max}.");
        }
    }

    private JobSnapshot Snapshot
    {
        get => CurrentRun.Value?.Snapshot
               ?? throw new InvalidOperationException("No active job run context.");
        set
        {
            var run = CurrentRun.Value
                      ?? throw new InvalidOperationException("No active job run context.");
            run.Snapshot = value;
        }
    }

    private string? ActiveJobId
    {
        get => CurrentRun.Value?.ActiveJobId;
        set
        {
            if (CurrentRun.Value is not null)
                CurrentRun.Value.ActiveJobId = value;
        }
    }

    /// <summary>
    /// Promote pre-created queued job to running (or create if none). Publishes SignalR.
    /// </summary>
    private void RegisterActiveJob()
    {
        var run = CurrentRun.Value
                  ?? throw new InvalidOperationException("No active job run context.");
        if (string.IsNullOrWhiteSpace(Snapshot.UserId))
            Snapshot.UserId = run.UserId;
        Snapshot.QueuedAt ??= run.QueuedAt;
        Snapshot.StartedAt ??= DateTimeOffset.UtcNow;
        Snapshot.Status = "running";
        run.StartedAt = Snapshot.StartedAt;

        if (!string.IsNullOrWhiteSpace(run.ActiveJobId))
        {
            // Promote existing queued → running
            Snapshot.JobId = run.ActiveJobId;
            _jobs.Update(run.ActiveJobId, rec =>
            {
                rec.Status = "running";
                rec.Kind = Snapshot.Kind;
                rec.Message = Snapshot.Message;
                rec.ProjectId = Snapshot.ProjectId;
                rec.UserId = Snapshot.UserId;
                rec.CharKey = Snapshot.CharKey;
                rec.Scene = Snapshot.Scene;
                rec.Clip = Snapshot.Clip;
                rec.Index = Snapshot.Index;
                rec.Total = Snapshot.Total;
                rec.Log = Snapshot.Log.ToList();
                rec.StartedAt = Snapshot.StartedAt;
                rec.QueuedAt = Snapshot.QueuedAt ?? rec.QueuedAt;
            });
            foreach (var res in run.HeldLocks)
            {
                var existing = _locks.Get(res);
                if (existing is not null &&
                    string.Equals(existing.UserId, run.UserId, StringComparison.OrdinalIgnoreCase))
                {
                    _locks.TryAcquire(res, run.UserId, DefaultLockTtl, existing.Reason, run.ActiveJobId);
                }
            }
            _metrics.NoteJobStarted(Snapshot.Kind ?? "job", run.UserId, run.QueuedAt);
            _ = PublishAsync();
            return;
        }

        // Fallback: create running job when no pre-queued record
        var recNew = _jobs.Create(new JobRecord
        {
            Status = Snapshot.Status,
            Kind = Snapshot.Kind,
            ProjectId = Snapshot.ProjectId,
            UserId = Snapshot.UserId,
            CharKey = Snapshot.CharKey,
            Scene = Snapshot.Scene,
            Clip = Snapshot.Clip,
            Message = Snapshot.Message,
            Index = Snapshot.Index,
            Total = Snapshot.Total,
            QueuedAt = run.QueuedAt,
            StartedAt = Snapshot.StartedAt ?? DateTimeOffset.UtcNow,
            Log = Snapshot.Log.ToList(),
        });
        ActiveJobId = recNew.JobId;
        Snapshot.JobId = recNew.JobId;
        Snapshot.QueuedAt = recNew.QueuedAt;
        _jobCts[recNew.JobId] = run.Cts;
        foreach (var res in run.HeldLocks)
        {
            var existing = _locks.Get(res);
            if (existing is not null &&
                string.Equals(existing.UserId, run.UserId, StringComparison.OrdinalIgnoreCase))
            {
                _locks.TryAcquire(res, run.UserId, DefaultLockTtl, existing.Reason, recNew.JobId);
            }
        }
        _metrics.NoteJobStarted(Snapshot.Kind ?? "job", run.UserId, run.QueuedAt);
        _ = PublishAsync();
    }

    private sealed class JobEnqueueMeta
    {
        public string? Kind { get; set; }
        public string? ProjectId { get; set; }
        public int? Scene { get; set; }
        public int? Clip { get; set; }
        public string? CharKey { get; set; }
        public string Message { get; set; } = "Queued — waiting for worker…";
    }

    /// <summary>
    /// Phase 2: accept job as <c>queued</c> immediately, wait for locks + worker slot, then run.
    /// Hard 409 only when user queue is full, or <paramref name="failIfLocked"/> and lock held by other.
    /// </summary>
    private Task<JobSnapshot> StartBackgroundJobAsync(
        Func<CancellationToken, Task> work,
        JobEnqueueMeta meta,
        IReadOnlyList<string>? lockResources = null,
        string? lockReason = null,
        bool failIfLocked = false)
    {
        var userId = string.IsNullOrWhiteSpace(_user.UserId) ? "local" : _user.UserId.Trim();
        EnsureCanStart(userId);

        var resources = (lockResources ?? Array.Empty<string>())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Hard reject only when client asks FailIfLocked and lock is held by someone else
        if (failIfLocked)
        {
            foreach (var res in resources)
            {
                var held = _locks.Get(res);
                if (held is null) continue;
                if (string.Equals(held.UserId, userId, StringComparison.OrdinalIgnoreCase))
                    continue;
                _metrics.NoteLockConflict();
                throw new LockConflictException(res, held.UserId, held.ExpiresAt);
            }
        }

        var apiKey = !string.IsNullOrWhiteSpace(_user.RequestApiKey)
            ? _user.RequestApiKey
            : _keys.GetKey(userId, "grok");
        var geminiKey = _keys.GetKey(userId, "gemini");
        var anthropicKey = _keys.GetKey(userId, "anthropic");

        var queuedAt = DateTimeOffset.UtcNow;
        var cts = new CancellationTokenSource();
        var kind = meta.Kind ?? "job";
        var rec = _jobs.Create(new JobRecord
        {
            Status = "queued",
            Kind = kind,
            ProjectId = meta.ProjectId,
            UserId = userId,
            CharKey = meta.CharKey,
            Scene = meta.Scene,
            Clip = meta.Clip,
            Message = meta.Message,
            QueuedAt = queuedAt,
            Log = new List<string> { meta.Message },
        });

        var run = new JobRunState
        {
            UserId = userId,
            ApiKey = apiKey,
            GeminiApiKey = geminiKey,
            AnthropicApiKey = anthropicKey,
            QueuedAt = queuedAt,
            Cts = cts,
            ActiveJobId = rec.JobId,
            HeldLocks = new List<string>(),
            Snapshot = rec.ToSnapshot(),
            PendingLockResources = resources,
            LockReason = lockReason,
        };
        _jobCts[rec.JobId] = cts;
        _metrics.NoteJobQueued(kind, userId);
        _ = PublishSnapshotAsync(run.Snapshot);

        _ = Task.Run(async () =>
        {
            CurrentRun.Value = run;
            using (ApiKeyScope.Push(run.ApiKey, run.GeminiApiKey, run.AnthropicApiKey))
            {
                var startedAt = DateTimeOffset.UtcNow;
                var success = false;
                try
                {
                    // Wait for locks (queued stays visible via SignalR messages)
                    await WaitForLocksAsync(run, cts.Token);

                    await UpdateQueuedMessageAsync(run, "Waiting for worker slot…");

                    async Task RunWorkAsync(CancellationToken ct)
                    {
                        using var linked = CancellationTokenSource.CreateLinkedTokenSource(run.Cts.Token, ct);
                        // Bind api_calls telemetry to this job's project for the async flow
                        using var tel = !string.IsNullOrWhiteSpace(meta.ProjectId)
                            ? _telemetry.UseProject(meta.ProjectId!)
                            : null;
                        await work(linked.Token);
                    }

                    await _apiPool.RunAsync(userId, RunWorkAsync, run.Cts.Token);

                    var status = CurrentRun.Value?.Snapshot.Status;
                    success = string.Equals(status, "done", StringComparison.OrdinalIgnoreCase);
                }
                catch (OperationCanceledException)
                {
                    try
                    {
                        if (CurrentRun.Value?.Snapshot is { } s &&
                            !string.Equals(s.Status, "cancelled", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(s.Status, "done", StringComparison.OrdinalIgnoreCase))
                        {
                            await FinishAsync("cancelled", "Cancelled by user");
                        }
                    }
                    catch { /* ignore */ }
                }
                catch (LockConflictException ex)
                {
                    _metrics.NoteLockConflict();
                    try
                    {
                        await FinishAsync("error", ex.Message, ex.Message);
                    }
                    catch { /* ignore */ }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Background job failed");
                    try
                    {
                        if (CurrentRun.Value?.Snapshot is { } s &&
                            (string.Equals(s.Status, "running", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(s.Status, "queued", StringComparison.OrdinalIgnoreCase)))
                        {
                            await FinishAsync("error", ex.Message, ex.Message);
                        }
                    }
                    catch { /* ignore */ }
                }
                finally
                {
                    var kindDone = CurrentRun.Value?.Snapshot.Kind ?? kind;
                    var q = run.QueuedAt;
                    var st = run.StartedAt ?? startedAt;
                    var snapStatus = CurrentRun.Value?.Snapshot.Status;
                    if (string.Equals(snapStatus, "done", StringComparison.OrdinalIgnoreCase))
                        success = true;
                    else if (string.Equals(snapStatus, "error", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(snapStatus, "cancelled", StringComparison.OrdinalIgnoreCase))
                        success = false;

                    _metrics.NoteJobFinished(kindDone, userId, success, q, st);

                    foreach (var res in run.HeldLocks)
                        _locks.Release(res, userId);

                    if (!string.IsNullOrWhiteSpace(run.ActiveJobId))
                    {
                        _jobCts.TryRemove(run.ActiveJobId, out _);
                        _locks.ReleaseAllForJob(run.ActiveJobId);
                    }

                    CurrentRun.Value = null;
                }
            }
        }, CancellationToken.None);

        return Task.FromResult(rec.ToSnapshot());
    }

    private async Task WaitForLocksAsync(JobRunState run, CancellationToken ct)
    {
        var resources = run.PendingLockResources;
        if (resources.Count == 0)
            return;

        await UpdateQueuedMessageAsync(run, "Waiting for resource lock…");

        while (!ct.IsCancellationRequested)
        {
            // Cancelled while queued?
            var job = !string.IsNullOrEmpty(run.ActiveJobId) ? _jobs.Get(run.ActiveJobId) : null;
            if (job is not null &&
                string.Equals(job.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
            {
                throw new OperationCanceledException("Job cancelled");
            }

            var acquired = new List<string>();
            string? blockedResource = null;
            string? blockedOwner = null;
            foreach (var res in resources)
            {
                if (_locks.TryAcquire(res, run.UserId, DefaultLockTtl, run.LockReason, run.ActiveJobId))
                {
                    acquired.Add(res);
                    continue;
                }

                var holder = _locks.Get(res);
                if (holder is not null &&
                    string.Equals(holder.UserId, run.UserId, StringComparison.OrdinalIgnoreCase))
                {
                    // Already ours
                    acquired.Add(res);
                    continue;
                }

                blockedResource = res;
                blockedOwner = holder?.UserId;
                break;
            }

            if (blockedResource is null)
            {
                run.HeldLocks = acquired;
                await UpdateQueuedMessageAsync(run, "Lock acquired — waiting for worker…");
                return;
            }

            foreach (var a in acquired)
                _locks.Release(a, run.UserId);

            var msg = string.IsNullOrEmpty(blockedOwner)
                ? $"Waiting for lock {blockedResource}…"
                : $"Waiting for lock (held by {blockedOwner})…";
            await UpdateQueuedMessageAsync(run, msg);
            await Task.Delay(300, ct);
        }

        throw new OperationCanceledException("Cancelled while waiting for lock");
    }

    private async Task UpdateQueuedMessageAsync(JobRunState run, string message)
    {
        if (string.IsNullOrEmpty(run.ActiveJobId)) return;
        run.Snapshot.Message = message;
        run.Snapshot.Status = "queued";
        if (run.Snapshot.Log.Count == 0 || run.Snapshot.Log[^1] != message)
        {
            run.Snapshot.Log.Add(message);
            if (run.Snapshot.Log.Count > 120)
                run.Snapshot.Log = run.Snapshot.Log.TakeLast(120).ToList();
        }
        _jobs.Update(run.ActiveJobId, rec =>
        {
            if (string.Equals(rec.Status, "cancelled", StringComparison.OrdinalIgnoreCase))
                return;
            rec.Status = "queued";
            rec.Message = message;
            rec.Log = run.Snapshot.Log.ToList();
        });
        await PublishSnapshotAsync(run.Snapshot);
    }

    private async Task PublishSnapshotAsync(JobSnapshot snap)
    {
        if (_sink is not null)
            await _sink.OnJobUpdatedAsync(Clone(snap));
    }

    public Task<JobSnapshot> StartSceneGenAsync(StartSceneGenRequest req)
    {
        if (req.Scene <= 0)
            throw new InvalidOperationException("scene required");
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        return StartBackgroundJobAsync(
            ct => RunSceneGenAsync(req, projectId, ct),
            new JobEnqueueMeta
            {
                Kind = "scene",
                ProjectId = projectId,
                Scene = req.Scene,
                Clip = req.Clip,
                Message = $"Queued scene S{req.Scene:D2} gen…",
            },
            lockResources: new[] { LockKeys.Scene(projectId, req.Scene) },
            lockReason: $"scene gen S{req.Scene:D2}",
            failIfLocked: req.FailIfLocked);
    }

    public Task<JobSnapshot> StartBatchGenAsync(StartBatchGenRequest req)
    {
        var hasClips = req.Clips is { Count: > 0 };
        if ((req.Scenes is null || req.Scenes.Count == 0) && !hasClips)
            throw new InvalidOperationException("At least one scene or clip is required.");
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        var sceneNumbers = hasClips
            ? req.Clips!.Select(c => c.Scene)
            : req.Scenes ?? new List<int>();
        var locks = sceneNumbers
            .Where(s => s > 0)
            .Select(s => LockKeys.Scene(projectId, s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var queuedMsg = hasClips
            ? $"Queued batch gen ({req.Clips!.Count} clip(s))…"
            : $"Queued batch gen ({req.Scenes!.Count} scenes)…";
        return StartBackgroundJobAsync(
            ct => RunBatchGenAsync(req, projectId, ct),
            new JobEnqueueMeta
            {
                Kind = "batch",
                ProjectId = projectId,
                Message = queuedMsg,
            },
            lockResources: locks,
            lockReason: "batch scene gen",
            failIfLocked: req.FailIfLocked);
    }

    /// <summary>Book → Fountain draft + approve. Requires XAI_API_KEY.</summary>
    public Task<JobSnapshot> StartStage1Async(StartStage1Request req)
    {
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        return StartBackgroundJobAsync(
            ct => RunStage1Async(req, projectId, ct),
            new JobEnqueueMeta
            {
                Kind = "stage1",
                ProjectId = projectId,
                Message = "Queued Stage 1…",
            },
            lockResources: new[] { LockKeys.Stage(projectId) },
            lockReason: "stage1");
    }

    /// <summary>Stage 2 planner (Fountain → blueprint). Deterministic C#; no API key.</summary>
    public Task<JobSnapshot> StartStage2Async(StartStage2Request req)
    {
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        return StartBackgroundJobAsync(
            ct => RunStage2Async(req, projectId, ct),
            new JobEnqueueMeta
            {
                Kind = "stage2",
                ProjectId = projectId,
                Message = "Queued Stage 2…",
            },
            lockResources: new[] { LockKeys.Stage(projectId) },
            lockReason: "stage2");
    }

    /// <summary>C# PDF extract + optional Grok vision OCR → book_full.txt (prepare only).</summary>
    public Task<JobSnapshot> StartBookPrepareAsync(StartBookPrepareRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProjectId))
            throw new InvalidOperationException("projectId required");
        return StartBackgroundJobAsync(
            ct => RunBookPrepareAsync(req, ct),
            new JobEnqueueMeta
            {
                Kind = "book_prepare",
                ProjectId = req.ProjectId,
                Message = "Queued book prepare…",
            },
            lockResources: new[] { LockKeys.Stage(req.ProjectId) },
            lockReason: "book prepare");
    }

    /// <summary>
    /// Full import path: prepare book text (unless skipped) then book→Fountain draft.
    /// Use for PDF/TXT Import; Screenplay “draft from book” can set <see cref="StartBookImportRequest.SkipPrepare"/>.
    /// </summary>
    public Task<JobSnapshot> StartBookImportAsync(StartBookImportRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProjectId))
            throw new InvalidOperationException("projectId required");
        return StartBackgroundJobAsync(
            ct => RunBookImportAsync(req, ct),
            new JobEnqueueMeta
            {
                Kind = "book_import",
                ProjectId = req.ProjectId,
                Message = req.SkipPrepare
                    ? "Queued screenplay draft from book…"
                    : "Queued book import (prepare + screenplay)…",
            },
            lockResources: new[] { LockKeys.Stage(req.ProjectId) },
            lockReason: "book import");
    }

    private sealed class JobRunState
    {
        public JobSnapshot Snapshot { get; set; } = new() { Status = "idle" };
        public string? ActiveJobId { get; set; }
        public CancellationTokenSource Cts { get; set; } = new();
        public string UserId { get; set; } = "local";
        public string? ApiKey { get; set; }
        public string? GeminiApiKey { get; set; }
        public string? AnthropicApiKey { get; set; }
        public DateTimeOffset QueuedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? StartedAt { get; set; }
        public List<string> HeldLocks { get; set; } = new();
        public List<string> PendingLockResources { get; set; } = new();
        public string? LockReason { get; set; }
        public SemaphoreSlim SnapLock { get; } = new(1, 1);
    }

    private async Task RunBookPrepareAsync(StartBookPrepareRequest req, CancellationToken ct)
    {
        var projectId = req.ProjectId;
        await _projects.RequireProjectAsync(projectId, ct);
        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "book_prepare",
            ProjectId = projectId,
            Message = "Preparing book (PDF extract / vision OCR)…",
            Index = 0,
            Total = 3,
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
                RegisterActiveJob();
        await PublishAsync();

        try
        {
            await AppendLogAsync("Book prepare (C# PdfPig + optional Grok vision)");
            var result = await _books.PrepareAsync(
                projectId,
                forceExtract: req.ForceExtract,
                forceVision: req.ForceVision,
                autoVision: req.AutoVision,
                visionModel: string.IsNullOrWhiteSpace(req.VisionModel) ? "grok-4.5" : req.VisionModel,
                onProgress: line =>
                {
                    _ = AppendLogAsync(line);
                    if (line.Contains("Extract", StringComparison.OrdinalIgnoreCase))
                        _ = UpdateAsync(s => { s.Index = 1; s.Message = line; });
                    else if (line.Contains("Vision", StringComparison.OrdinalIgnoreCase) ||
                             line.Contains("page", StringComparison.OrdinalIgnoreCase))
                        _ = UpdateAsync(s => { s.Index = Math.Max(s.Index, 2); s.Message = line; });
                    else
                        _ = UpdateAsync(s => s.Message = line);
                },
                ct: ct);

            await UpdateAsync(s => s.Index = 3);
            var msg = result.ReadyForStage1
                ? $"Book ready · {result.TextWords} words · quality={result.TextQuality} · {result.TextEngine}"
                : $"Book prepared but Stage 1 not ready · {result.Strategy}: {result.StrategyReason}";
            await FinishAsync(result.Ok ? "done" : "error", msg, result.Ok ? null : msg);
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Cancelled by user");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Book prepare failed");
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }

    private async Task RunBookImportAsync(StartBookImportRequest req, CancellationToken ct)
    {
        var projectId = req.ProjectId;
        await _projects.RequireProjectAsync(projectId, ct).ConfigureAwait(false);

        // Progress: 0–4 prepare, 5–10 adapt (chunk messages bump index)
        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "book_import",
            ProjectId = projectId,
            Message = req.SkipPrepare
                ? "Writing screenplay from book…"
                : "Importing book (prepare + screenplay)…",
            Index = 0,
            Total = 10,
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
        RegisterActiveJob();
        await PublishAsync().ConfigureAwait(false);

        try
        {
            // Ambient job scope is pushed before this runs — log which key source is active.
            var keyHint = !string.IsNullOrWhiteSpace(ApiKeyScope.Current)
                ? "personal/scope"
                : !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XAI_API_KEY"))
                    ? "server XAI_API_KEY env"
                    : "none";
            await AppendLogAsync($"AI key source for import: {keyHint}").ConfigureAwait(false);

            if (!_chat.IsConfigured)
            {
                await FinishAsync("error",
                    "API key missing. A Grok key is set in Configuration only if it decrypts for this user. " +
                    "Re-save the key after each redeploy unless Railway has a Volume at /data. " +
                    "Or set server env XAI_API_KEY.",
                    "API key missing. Re-save Grok key in Configuration (needs Volume at /data) or set XAI_API_KEY env.").ConfigureAwait(false);
                return;
            }

            var projectDir = await _projects.GetProjectDirAsync(projectId, ct).ConfigureAwait(false);
            var bookPath = Path.Combine(projectDir, "source", "book_full.txt");
            var needPrepare = !req.SkipPrepare;

            // TXT may already have book_full after upload; still allow force extract for PDF
            if (needPrepare && File.Exists(bookPath) && !req.ForceExtract && !req.ForceVision)
            {
                // Light skip if text already good and not forcing — still run prepare for PDF path consistency
                // Import always sets ForceExtract=true for PDF; SkipPrepare for re-draft only.
            }

            if (needPrepare)
            {
                await AppendLogAsync("Phase 1: prepare book text").ConfigureAwait(false);
                await UpdateAsync(s =>
                {
                    s.Index = 1;
                    s.Message = "Reading book…";
                }).ConfigureAwait(false);

                var prep = await _books.PrepareAsync(
                    projectId,
                    forceExtract: req.ForceExtract,
                    forceVision: req.ForceVision,
                    autoVision: req.AutoVision,
                    visionModel: string.IsNullOrWhiteSpace(req.VisionModel) ? "grok-4.5" : req.VisionModel,
                    onProgress: line =>
                    {
                        _ = AppendLogAsync(line);
                        _ = UpdateAsync(s =>
                        {
                            s.Message = line;
                            if (line.Contains("Extract", StringComparison.OrdinalIgnoreCase))
                                s.Index = Math.Max(s.Index, 2);
                            else if (line.Contains("Vision", StringComparison.OrdinalIgnoreCase) ||
                                     line.Contains("page", StringComparison.OrdinalIgnoreCase))
                                s.Index = Math.Max(s.Index, 3);
                            else
                                s.Index = Math.Max(s.Index, 2);
                        });
                    },
                    ct: ct).ConfigureAwait(false);

                if (!prep.Ok)
                {
                    await FinishAsync("error", prep.StrategyReason ?? "Book prepare failed",
                        prep.StrategyReason ?? "Book prepare failed").ConfigureAwait(false);
                    return;
                }

                await AppendLogAsync(
                    $"Book text ready · {prep.TextWords} words · {prep.TextEngine}").ConfigureAwait(false);
            }
            else
            {
                await AppendLogAsync("Skipping prepare — using existing book text").ConfigureAwait(false);
            }

            if (!File.Exists(bookPath))
            {
                await FinishAsync("error", "No book text after prepare",
                    "No book text after prepare").ConfigureAwait(false);
                return;
            }

            await UpdateAsync(s =>
            {
                s.Index = 5;
                s.Message = "Writing screenplay draft…";
            }).ConfigureAwait(false);
            await AppendLogAsync("Phase 2: book → Fountain screenplay").ConfigureAwait(false);

            if (!_chat.IsConfigured)
            {
                await FinishAsync("error", "Chat service not configured",
                    "Chat service not configured").ConfigureAwait(false);
                return;
            }

            var model = string.IsNullOrWhiteSpace(req.Model) ? "grok-4.5" : req.Model.Trim();
            var save = await ScreenplayService.CreateDraftFromBookAsync(
                _projects,
                projectId,
                _chat,
                model: model,
                onProgress: line =>
                {
                    _ = AppendLogAsync(line);
                    _ = UpdateAsync(s =>
                    {
                        s.Message = line;
                        // Map adapt progress into 5–9
                        if (line.Contains("chunk", StringComparison.OrdinalIgnoreCase))
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(
                                line, @"(\d+)\s*/\s*(\d+)");
                            if (m.Success &&
                                int.TryParse(m.Groups[1].Value, out var cur) &&
                                int.TryParse(m.Groups[2].Value, out var tot) &&
                                tot > 0)
                            {
                                s.Index = 5 + (int)Math.Round(4.0 * Math.Clamp(cur, 0, tot) / tot);
                            }
                            else
                                s.Index = Math.Max(s.Index, 6);
                        }
                        else if (line.Contains("Merge", StringComparison.OrdinalIgnoreCase) ||
                                 line.Contains("Stitch", StringComparison.OrdinalIgnoreCase))
                            s.Index = Math.Max(s.Index, 9);
                        else if (line.Contains("repair", StringComparison.OrdinalIgnoreCase) ||
                                 line.Contains("retry", StringComparison.OrdinalIgnoreCase))
                            s.Index = Math.Max(s.Index, 8);
                        else
                            s.Index = Math.Max(s.Index, 6);
                    });
                },
                ct: ct).ConfigureAwait(false);

            if (!save.Ok)
            {
                await FinishAsync("error", save.Error ?? "Screenplay draft failed",
                    save.Error ?? "Screenplay draft failed").ConfigureAwait(false);
                return;
            }

            await UpdateAsync(s => s.Index = 10).ConfigureAwait(false);
            await FinishAsync(
                "done",
                save.Message ?? "Screenplay draft ready — review and approve").ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Cancelled by user").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Book import failed");
            await FinishAsync("error", ex.Message, ex.Message).ConfigureAwait(false);
        }
    }

    /// <summary>Generate portrait variants via C# Grok image API.</summary>
    public Task<JobSnapshot> StartCharacterVariantsAsync(StartCharacterVariantsRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CharKey))
            throw new InvalidOperationException("charKey required");
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        return StartBackgroundJobAsync(
            ct => RunCharacterVariantsAsync(req, projectId, ct),
            new JobEnqueueMeta
            {
                Kind = "character_variants",
                ProjectId = projectId,
                CharKey = req.CharKey,
                Message = $"Queued portrait gen for {req.CharKey}…",
            },
            lockResources: new[] { LockKeys.Character(projectId, req.CharKey) },
            lockReason: $"char variants {req.CharKey}");
    }

    /// <summary>End-credits title card via video gen (client saves credits.mp4).</summary>
    public Task<JobSnapshot> StartCreditsGenAsync(string projectId, string? resolution = null)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            projectId = _projects.ActiveProjectId;
        if (string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException("projectId required");
        return StartBackgroundJobAsync(
            ct => RunCreditsGenAsync(projectId, resolution, ct),
            new JobEnqueueMeta
            {
                Kind = "credits",
                ProjectId = projectId,
                Message = "Queued end-credits plate…",
            },
            lockResources: new[] { LockKeys.Wip(projectId) },
            lockReason: "credits gen");
    }

    private async Task RunCreditsGenAsync(string projectId, string? resolution, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);
        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "credits",
            ProjectId = projectId,
            Message = "Generating end-credits plate…",
            Index = 0,
            Total = 100,
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
        RegisterActiveJob();
        await PublishAsync();
        try
        {
            await AppendLogAsync("Building cinematic credits title card (video API)…");
            var handoff = await _credits.GenerateCreditsForClientAsync(
                projectId,
                resolution,
                msg => { _ = AppendLogAsync("  " + msg); },
                ct).ConfigureAwait(false);

            if (handoff is null)
            {
                await FinishAsync("done", "Credits already present or disabled");
                return;
            }

            await UpdateAsync(s =>
            {
                s.ClientMediaUrl = handoff.ClientMediaUrl;
                s.ClientRelativePath = handoff.ClientRelativePath;
                s.Index = 100;
            });
            await AppendLogAsync($"Credits ready → {handoff.ClientRelativePath}");
            await FinishAsync("done", "Credits plate ready — save to media folder");
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Cancelled by user");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Credits gen failed");
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }

    /// <summary>
    /// Short Grok video with VOICE LOCK + dialogue, extract MP3 for Characters Play sample.
    /// </summary>
    public Task<JobSnapshot> StartVoicePreviewAsync(StartVoicePreviewRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CharKey))
            throw new InvalidOperationException("charKey required");
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        return StartBackgroundJobAsync(
            ct => RunVoicePreviewAsync(req, projectId, ct),
            new JobEnqueueMeta
            {
                Kind = "voice-preview",
                ProjectId = projectId,
                CharKey = req.CharKey,
                Message = req.Force
                    ? $"Queued voice regenerate for {req.CharKey}…"
                    : $"Queued voice sample for {req.CharKey}…",
            },
            lockResources: new[] { LockKeys.Character(projectId, req.CharKey) },
            lockReason: $"voice preview {req.CharKey}");
    }

    /// <summary>AI per-clip review (frames + prev tail) → draft suggestions for Apply → Regen.</summary>
    public Task<JobSnapshot> StartClipAutoReviewAsync(StartClipAutoReviewRequest req)
    {
        if (req.Scene <= 0 || req.Clip <= 0)
            throw new InvalidOperationException("scene and clip required");
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        return StartBackgroundJobAsync(
            ct => RunClipAutoReviewAsync(req, projectId, ct),
            new JobEnqueueMeta
            {
                Kind = "clip-auto-review",
                ProjectId = projectId,
                Scene = req.Scene,
                Clip = req.Clip,
                Message = $"Queued AI review S{req.Scene:D2}C{req.Clip:D2}…",
            },
            lockResources: new[] { LockKeys.Scene(projectId, req.Scene) },
            lockReason: $"auto-review S{req.Scene:D2}C{req.Clip:D2}");
    }

    /// <summary>
    /// Batch AI review (server walk). Prefer client-orchestrated batch: browser samples frames
    /// per clip then calls single auto-review. Server batch cannot sample video (browser frames required).
    /// </summary>
    public Task<JobSnapshot> StartClipAutoReviewBatchAsync(StartClipAutoReviewBatchRequest req)
    {
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        if (string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException("projectId required");

        // Server no longer extracts frames; batch must be driven from the browser Review page.
        throw new InvalidOperationException(
            "Batch auto-review must run from the browser (samples frames with ffmpeg.wasm). " +
            "Use Review → Auto-review all.");
    }

    private async Task RunClipAutoReviewAsync(StartClipAutoReviewRequest req, string projectId, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);

        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "clip-auto-review",
            ProjectId = projectId,
            Scene = req.Scene,
            Clip = req.Clip,
            Message = $"Reviewing S{req.Scene:D2}C{req.Clip:D2}…",
            Index = 0,
            Total = 100,
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
        RegisterActiveJob();
        await PublishAsync();

        try
        {
            var frameCount = req.Frames?.Count ?? 0;
            await AppendLogAsync(
                frameCount > 0
                    ? $"AI review = {frameCount} browser frame(s) → vision (key stays on server) → draft"
                    : "AI review requires browser-sampled frames (no server ffmpeg)");
            var draft = await _clipAutoReview.ReviewAsync(
                projectId,
                req.Scene,
                req.Clip,
                onProgress: (index, total, line) =>
                {
                    _ = AppendLogAsync(line);
                    _ = UpdateAsync(s =>
                    {
                        s.Index = Math.Clamp(index, 0, Math.Max(1, total));
                        s.Total = Math.Max(1, total);
                        s.Message = line;
                    });
                },
                ct: ct,
                clientFrames: req.Frames);

            await AppendLogAsync(
                $"Draft: {draft.Suggestion}/{draft.Category} · {draft.Suggestions.Count} suggestion(s)");
            await FinishAsync(
                "done",
                $"Review ready S{req.Scene:D2}C{req.Clip:D2} — {draft.Suggestion} ({draft.Suggestions.Count} suggestions)");
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Clip review cancelled");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Clip auto-review failed S{Scene}C{Clip}", req.Scene, req.Clip);
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }

    private async Task RunClipAutoReviewBatchAsync(StartClipAutoReviewBatchRequest req, string projectId, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);

        var coords = _reviewIndex.ListOnDiskClipCoords(projectId, req.Scene)
            .Where(c => !req.OnlyMissing || !_reviewIndex.HasDraft(projectId, c.Scene, c.Clip))
            .ToList();

        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "clip-auto-review-batch",
            ProjectId = projectId,
            Scene = req.Scene is int s0 && s0 > 0 ? s0 : null,
            Message = coords.Count == 0
                ? "No clips to auto-review"
                : $"Batch reviewing {coords.Count} clip(s)…",
            Index = 0,
            Total = Math.Max(1, coords.Count),
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
        RegisterActiveJob();
        await PublishAsync();

        try
        {
            if (coords.Count == 0)
            {
                try { await _reviewIndex.RebuildAsync(projectId, req.Scene, ct); } catch { /* non-fatal */ }
                await FinishAsync("done", "Batch auto-review: nothing to do (no missing drafts)");
                return;
            }

            await AppendLogAsync(
                $"Batch auto-review: {coords.Count} clip(s)" +
                (req.OnlyMissing ? " (only missing drafts)" : " (all)") +
                (req.Scene is int sn && sn > 0 ? $" scene S{sn:D2}" : ""));

            var ok = 0;
            var failed = 0;
            for (var i = 0; i < coords.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (scene, clip) = coords[i];
                await UpdateAsync(s =>
                {
                    s.Index = i;
                    s.Total = coords.Count;
                    s.Scene = scene;
                    s.Clip = clip;
                    s.Message = $"Reviewing S{scene:D2}C{clip:D2} ({i + 1}/{coords.Count})…";
                });
                await AppendLogAsync($"--- S{scene:D2}C{clip:D2} ({i + 1}/{coords.Count}) ---");

                try
                {
                    var draft = await _clipAutoReview.ReviewAsync(
                        projectId,
                        scene,
                        clip,
                        onProgress: (index, total, line) =>
                        {
                            _ = AppendLogAsync($"  {line}");
                        },
                        ct: ct);
                    ok++;
                    await AppendLogAsync(
                        $"  → {draft.Suggestion}/{draft.Category} · {draft.Suggestions.Count} suggestion(s)");
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    failed++;
                    _log.LogWarning(ex, "Batch auto-review failed S{Scene}C{Clip}", scene, clip);
                    await AppendLogAsync($"  → ERROR: {ex.Message}");
                }
            }

            try
            {
                var index = await _reviewIndex.RebuildAsync(projectId, req.Scene, ct: ct);
                await AppendLogAsync(
                    $"Review index rebuilt: {index.Clips.Count} row(s) → assets/review/index.json");
            }
            catch (Exception ex)
            {
                await AppendLogAsync($"Review index rebuild skipped: {ex.Message}");
            }

            await FinishAsync(
                "done",
                $"Batch auto-review done: {ok} ok, {failed} failed of {coords.Count}");
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Batch auto-review cancelled");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Batch auto-review failed for {ProjectId}", projectId);
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }

    private async Task RunVoicePreviewAsync(StartVoicePreviewRequest req, string projectId, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);

        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "voice-preview",
            ProjectId = projectId,
            CharKey = req.CharKey,
            Message = req.Force
                ? $"Regenerating voice for {req.CharKey}…"
                : $"Generating voice sample for {req.CharKey}…",
            Index = 0,
            Total = 100,
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
        RegisterActiveJob();
        await PublishAsync();

        try
        {
            await AppendLogAsync(
                "Voice sample = short film video (voice style + dialogue), kept as MP4");

            var path = await _voicePreview.GenerateAsync(
                projectId,
                req.CharKey,
                req.VoiceProfile,
                req.VoiceLabel,
                req.DisplayName,
                req.SampleText,
                force: req.Force,
                onProgress: (index, total, line) =>
                {
                    _ = AppendLogAsync(line);
                    _ = UpdateAsync(s =>
                    {
                        s.Index = Math.Clamp(index, 0, Math.Max(1, total));
                        s.Total = Math.Max(1, total);
                        s.Message = line;
                    });
                },
                ct: ct);

            await AppendLogAsync($"Saved {Path.GetFileName(path)}");
            await FinishAsync("done", $"Voice sample ready for {req.CharKey}");
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Voice sample cancelled");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Voice preview failed for {Char}", req.CharKey);
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }

    /// <summary>
    /// Grok vision: classify book images → which characters appear, write plates to scenes.json.
    /// Cancellable. Falls back to heuristics if no API key.
    /// </summary>
    public Task<JobSnapshot> StartSortCharacterPlatesAsync(AttachCharacterPlatesRequest req)
    {
        var projectId = string.IsNullOrWhiteSpace(req.ProjectId)
            ? _projects.ActiveProjectId
            : req.ProjectId;
        return StartBackgroundJobAsync(
            ct => RunSortCharacterPlatesAsync(req, projectId, ct),
            new JobEnqueueMeta
            {
                Kind = "character-plates",
                ProjectId = projectId,
                Message = "Queued character plate sort…",
            },
            lockResources: new[] { LockKeys.Stage(projectId) },
            lockReason: "character plates");
    }

    private async Task RunSortCharacterPlatesAsync(AttachCharacterPlatesRequest req, string projectId, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);

        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "character-plates",
            ProjectId = projectId,
            Message = req.UseGrok
                ? "Sorting book images onto characters with Grok vision…"
                : "Sorting book images onto characters (heuristic)…",
            Index = 0,
            Total = Math.Max(1, req.MaxImages),
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
                RegisterActiveJob();
        await PublishAsync();

        try
        {
            await AppendLogAsync(
                req.UseGrok
                    ? "Character plate sort (Grok vision classifies who appears on each page)"
                    : "Character plate sort (heuristic only)");

            var result = await _plates.AttachAsync(
                projectId,
                force: true, // job is always an explicit re-sort from UI
                copyIntoAssets: req.CopyIntoAssets,
                onlyCharKey: req.CharKey,
                useGrok: req.UseGrok,
                visionModel: string.IsNullOrWhiteSpace(req.VisionModel) ? "grok-4.5" : req.VisionModel,
                maxImages: req.MaxImages > 0 ? req.MaxImages : 32,
                onProgress: line =>
                {
                    _ = AppendLogAsync(line);
                    // "Grok vision 3/20: …"
                    var m = System.Text.RegularExpressions.Regex.Match(
                        line, @"Grok vision\s+(\d+)/(\d+)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success &&
                        int.TryParse(m.Groups[1].Value, out var i) &&
                        int.TryParse(m.Groups[2].Value, out var t))
                    {
                        _ = UpdateAsync(s =>
                        {
                            s.Index = i;
                            s.Total = t;
                            s.Message = line;
                        });
                    }
                    else
                        _ = UpdateAsync(s => s.Message = line);
                },
                ct: ct);

            if (result.AlreadySorted)
            {
                await FinishAsync("done", $"Already sorted ({result.SortedAt})");
                return;
            }

            if (!result.Ok && !string.IsNullOrEmpty(result.Reason))
            {
                await FinishAsync("error", result.Reason, result.Reason);
                return;
            }

            await UpdateAsync(s =>
            {
                s.Index = Math.Max(s.Index, result.ImagesClassified);
                if (result.ImagesClassified > 0)
                    s.Total = Math.Max(s.Total, result.ImagesClassified);
            });
            await AppendLogAsync(
                $"method={result.Method} updated={result.CharactersUpdated} " +
                $"skipped={result.CharactersSkipped} classified={result.ImagesClassified} " +
                $"text_skipped={result.ImagesSkippedText}");
            await FinishAsync(
                "done",
                $"Plates sorted ({result.Method}): {result.CharactersUpdated} character(s) updated");
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Cancelled by user");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Character plate sort failed");
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }

    /// <summary>Lock/unlock character reference images (locks run vision style gate).</summary>
    public async Task<string> RunCharacterDesignActionAsync(
        string projectId,
        string action,
        string charKey,
        int variantIndex = 1,
        string? imagePath = null,
        CancellationToken ct = default)
    {
        if (IsRunning)
            throw new InvalidOperationException("A generation job is already running.");

        ct.ThrowIfCancellationRequested();
        return action switch
        {
            "lock-variant" =>
                await _characters.LockVariantAsync(
                    projectId, charKey, Math.Clamp(variantIndex, 1, 3), ct).ConfigureAwait(false),
            "lock-image" when !string.IsNullOrWhiteSpace(imagePath) =>
                await _characters.LockFromPathAsync(
                    projectId,
                    charKey,
                    ResolveLockImagePath(projectId, imagePath!),
                    ct).ConfigureAwait(false),
            "lock-bookref" =>
                await _characters.LockBookRefAsync(
                    projectId, charKey, Math.Max(0, variantIndex), ct).ConfigureAwait(false),
            "unlock" =>
                _characters.Unlock(projectId, charKey)
                    ? $"Unlocked {charKey} — previous lock kept as variant 1 (best so far)"
                    : $"No locked ref for {charKey}",
            _ => throw new InvalidOperationException($"Unknown character action: {action}"),
        };
    }

    private string ResolveLockImagePath(string projectId, string imagePath)
    {
        if (File.Exists(imagePath))
            return Path.GetFullPath(imagePath);
        var projectDir = _projects.GetProjectDir(projectId);
        var cand = Path.Combine(projectDir, imagePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(cand))
            return Path.GetFullPath(cand);
        throw new InvalidOperationException($"Image not found: {imagePath}");
    }

    private async Task RunCharacterVariantsAsync(StartCharacterVariantsRequest req, string projectId, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);

        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "character",
            ProjectId = projectId,
            CharKey = req.CharKey,
            Message = $"Generating portraits for {req.CharKey}…",
            Index = 0,
            Total = 3,
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
                RegisterActiveJob();
        await PublishAsync();

        try
        {
            await AppendLogAsync($"Character design (C# / Grok image API) for {req.CharKey}");
            await UpdateAsync(s => s.Message = "Resolving refs + design prompt…");

            var result = await _characters.GenerateVariantsAsync(
                projectId,
                req.CharKey,
                n: req.Count,
                seedOptions: req,
                onProgress: line =>
                {
                    _ = AppendLogAsync(line);
                    var idx = TryParseVariantProgress(line);
                    if (idx > 0)
                        _ = UpdateAsync(s => { s.Index = idx; s.Message = line; });
                    else if (line.Contains("generating", StringComparison.OrdinalIgnoreCase))
                    {
                        // "generating 1 variant(s)" / "generating 3 variants"
                        var m = System.Text.RegularExpressions.Regex.Match(line, @"generating\s+(\d+)");
                        if (m.Success && int.TryParse(m.Groups[1].Value, out var total) && total > 0)
                            _ = UpdateAsync(s => { s.Total = total; s.Message = line; });
                        else
                            _ = UpdateAsync(s => s.Message = line);
                    }
                    else if (line.Contains("edit variant", StringComparison.OrdinalIgnoreCase) ||
                             line.Contains("Grok", StringComparison.OrdinalIgnoreCase) ||
                             line.Contains("book ref", StringComparison.OrdinalIgnoreCase) ||
                             line.Contains("ref image", StringComparison.OrdinalIgnoreCase))
                        _ = UpdateAsync(s =>
                        {
                            s.Index = Math.Max(s.Index, 1);
                            s.Message = line;
                        });
                },
                ct: ct);

            await UpdateAsync(s =>
            {
                s.Index = result.Paths.Count;
                s.Total = Math.Max(s.Total, result.Paths.Count);
            });
            await AppendLogAsync(
                $"mode={result.Mode} · {result.Paths.Count} file(s)" +
                (result.BookRefs.Count > 0
                    ? $" · book refs: {string.Join(", ", result.BookRefs)}"
                    : ""));
            await FinishAsync(
                "done",
                $"Variants ready for {req.CharKey} ({result.Mode}, {result.Paths.Count} image(s))");
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Cancelled by user");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Character variants failed");
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }

    private static int TryParseVariantProgress(string line)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            line, @"variant[_\s-]*0*([1-3])", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
            return n;
        m = System.Text.RegularExpressions.Regex.Match(line, @"\b([1-3])\s*/\s*3\b");
        if (m.Success && int.TryParse(m.Groups[1].Value, out n))
            return n;
        m = System.Text.RegularExpressions.Regex.Match(
            line, @"saved variant\s+([1-3])", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out n))
            return n;
        return 0;
    }

    private async Task RunStage1Async(StartStage1Request req, string projectId, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);

        // Progress: 10 fixed phases (same scale as book_import) so the UI bar never sticks at
        // the "Total=0 → 35%" placeholder during a long single-pass adapt call.
        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "stage1",
            ProjectId = projectId,
            Message = "Building screenplay…",
            Index = 0,
            Total = 10,
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
                RegisterActiveJob();
        await PublishAsync();

        try
        {
            await AppendLogAsync("Screenplay: book → draft → approve");
            // Sequential progress pump — no GetAwaiter; preserves line order for SignalR
            var progress = System.Threading.Channels.Channel.CreateUnbounded<string>(
                new System.Threading.Channels.UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false,
                });
            var progressPump = Task.Run(async () =>
            {
                await foreach (var line in progress.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                    await ReportStage1ProgressAsync(line).ConfigureAwait(false);
            }, CancellationToken.None);

            Stage1Result result;
            try
            {
                result = await _stage1.RunAsync(
                    projectId,
                    chunkPages: Math.Clamp(req.ChunkPages, 5, 30),
                    totalMinutes: req.TotalMinutes,
                    model: string.IsNullOrWhiteSpace(req.Model) ? "grok-4.5" : req.Model,
                    resume: req.Resume,
                    maxChunks: req.MaxChunks,
                    onProgress: line => progress.Writer.TryWrite(line),
                    ct: ct);
            }
            finally
            {
                progress.Writer.TryComplete();
                try { await progressPump.ConfigureAwait(false); }
                catch (OperationCanceledException) { /* job cancelled */ }
            }

            var msg =
                $"Screenplay ready: {result.SceneCount} scenes · {result.CharacterCount} cast · " +
                $"{result.LocationCount} locations · V.O. {result.VoCueCount}/{result.TotalDialogueCues} ({result.VoPercent}%)";
            if (result.TotalDialogueCues > 0 && result.VoPercent >= 45)
                msg += " — narration-heavy (clip gen will lean on V.O.)";
            if (result.HardErrors.Count > 0)
                msg += $" · {result.HardErrors.Count} issue(s)";
            await FinishAsync(result.Ok || result.SceneCount > 0 ? "done" : "error", msg,
                result.Ok ? null : string.Join("; ", result.HardErrors.Take(3)));
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Cancelled by user");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Stage 1 failed");
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }

    private async Task RunStage2Async(StartStage2Request req, string projectId, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);

        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "stage2",
            ProjectId = projectId,
            Message = "Building shot plan…",
            Index = 0,
            Total = 10,
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
                RegisterActiveJob();
        await PublishAsync();

        try
        {
            await AppendLogAsync("Building shot plan from screenplay");
            ct.ThrowIfCancellationRequested();
            var resolution = await ResolveVideoResolutionAsync(projectId, req.Resolution, ct);
            var result = await _stage2.PlanAsync(
                projectId,
                resolution: resolution,
                scenes: string.IsNullOrWhiteSpace(req.Scenes) ? "all" : req.Scenes,
                onProgress: line =>
                {
                    _ = AppendLogAsync(line);
                    _ = UpdateAsync(s =>
                    {
                        s.Message = line;
                        s.Total = Math.Max(s.Total, 10);
                        // "Planning N scene(s)" / "Scene N…" — map into 1–9
                        var mPlan = System.Text.RegularExpressions.Regex.Match(
                            line, @"Planning\s+(\d+)\s+scene", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (mPlan.Success && int.TryParse(mPlan.Groups[1].Value, out var nScenes) && nScenes > 0)
                        {
                            s.Index = Math.Max(s.Index, 1);
                            return;
                        }
                        var mSc = System.Text.RegularExpressions.Regex.Match(
                            line, @"Scene\s+(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (mSc.Success && int.TryParse(mSc.Groups[1].Value, out var sn) && sn > 0)
                        {
                            // Approximate: scene numbers climb; keep under 9 until merge/done
                            s.Index = Math.Max(s.Index, Math.Min(8, 1 + sn));
                            return;
                        }
                        if (line.Contains("Merged", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("Backed up", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("complete", StringComparison.OrdinalIgnoreCase))
                            s.Index = Math.Max(s.Index, 9);
                        else
                            s.Index = Math.Max(s.Index, 1);
                    });
                },
                ct: ct);

            await FinishAsync(
                "done",
                $"Stage 2 complete: {result.SceneCount} scenes · {result.ClipCount} clips · ~{result.DurationSeconds}s");
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "Cancelled by user");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Stage 2 failed");
            await FinishAsync("error", ex.Message, ex.Message);
        }
    }


    public Task<JobSnapshot> StartYouTubeUploadAsync(StartYouTubeUploadRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProjectId))
            throw new InvalidOperationException("projectId required");
        var projectId = req.ProjectId;
        return StartBackgroundJobAsync(
            ct => RunYouTubeUploadAsync(req, projectId, ct),
            new JobEnqueueMeta
            {
                Kind = "youtube_upload",
                ProjectId = projectId,
                Message = "Queued YouTube upload…",
            },
            lockResources: new[] { LockKeys.YouTube(projectId) },
            lockReason: "youtube upload");
    }

    private async Task RunYouTubeUploadAsync(StartYouTubeUploadRequest req, string projectId, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);

        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "youtube_upload",
            ProjectId = projectId,
            Message = "Connecting to YouTube…",
            Index = 0,
            Total = 100,
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
        RegisterActiveJob();
        await PublishAsync();

        try
        {
            var path = _projects.ResolveWipMoviePath(projectId);
            if (path is null || !File.Exists(path))
            {
                var pDir = _projects.GetProjectDir(projectId);
                var altWip = Path.Combine(pDir, "assets", "video", "wip_movie.mp4");
                if (File.Exists(altWip)) path = altWip;
            }
            if (path is null || !File.Exists(path))
                throw new InvalidOperationException("No WIP movie file found on server — publish Demo from a browser stitch first.");

            var youtube = await _youTube.GetServiceAsync(ct)
                ?? throw new InvalidOperationException("YouTube is not connected — connect it from Review first.");

            var title = string.IsNullOrWhiteSpace(req.Title) ? $"{projectId} — WIP" : req.Title.Trim();
            var privacy = req.PrivacyStatus is "private" or "unlisted" or "public"
                ? req.PrivacyStatus
                : "unlisted";

            var video = new Video
            {
                Snippet = new VideoSnippet
                {
                    Title = title,
                    Description = req.Description ?? "",
                    CategoryId = "1", // Film & Animation
                },
                Status = new VideoStatus { PrivacyStatus = privacy },
            };

            var bytes = new FileInfo(path).Length;
            await AppendLogAsync($"Uploading {Path.GetFileName(path)} ({bytes / (1024 * 1024)} MB, {privacy})…");

            await using var stream = File.OpenRead(path);
            var upload = youtube.Videos.Insert(video, "snippet,status", stream, "video/mp4");
            string? videoId = null;
            upload.ResponseReceived += v => videoId = v.Id;
            upload.ProgressChanged += p =>
            {
                var pct = bytes > 0 ? (int)Math.Clamp(p.BytesSent * 100 / bytes, 0, 100) : 0;
                _ = UpdateAsync(s =>
                {
                    s.Index = pct;
                    s.Total = 100;
                    s.Message = p.Status switch
                    {
                        UploadStatus.Uploading => $"Uploading… {pct}%",
                        UploadStatus.Completed => "Upload complete — finalizing…",
                        UploadStatus.Failed => $"Upload failed: {p.Exception?.Message}",
                        _ => s.Message,
                    };
                });
            };

            var result = await upload.UploadAsync(ct);
            if (result.Status != UploadStatus.Completed || videoId is null)
            {
                if (ct.IsCancellationRequested)
                    throw new OperationCanceledException(ct);
                var errDetail = result.Exception is Google.GoogleApiException gerr
                    ? $"Google API {gerr.HttpStatusCode}: {gerr.Message} — {gerr.Error?.Message}"
                    : result.Exception?.Message ?? $"YouTube upload status: {result.Status}";
                await AppendLogAsync($"❌ YouTube upload failed: {errDetail}");
                throw result.Exception ?? new InvalidOperationException($"YouTube upload failed: {errDetail}");
            }

            var url = $"https://youtu.be/{videoId}";
            await _projects.SaveYouTubeUploadInfoAsync(projectId, new YouTubeUploadInfo
            {
                VideoId = videoId,
                Url = url,
                Title = title,
                PrivacyStatus = privacy,
                UploadedAt = DateTimeOffset.UtcNow,
            }, ct);

            // Best-effort cleanup of temporary staged MP4 to conserve server disk space
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to clean up temporary staged movie file {Path} after YouTube upload", path);
            }

            await FinishAsync("done", $"Uploaded to YouTube: {url}");
        }
        catch (OperationCanceledException)
        {
            await FinishAsync("cancelled", "YouTube upload cancelled");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "YouTube upload failed for {ProjectId}", projectId);
            var errMessage = ex is Google.GoogleApiException gex
                ? $"Google API Error ({gex.HttpStatusCode}): {gex.Message} — {gex.Error?.Message}"
                : ex.Message;
            await AppendLogAsync($"❌ YouTube upload exception: {errMessage}");
            await FinishAsync("error", errMessage, errMessage);
        }
    }

    private async Task RunBatchGenAsync(StartBatchGenRequest req, string projectId, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);

        var hasClips = req.Clips is { Count: > 0 };
        var scenes = (hasClips ? req.Clips!.Select(c => c.Scene) : req.Scenes)
            .Distinct().OrderBy(s => s).ToList();
        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "batch",
            ProjectId = projectId,
            Message = hasClips
                ? $"Batch: {req.Clips!.Count} clip(s)…"
                : $"Batch: {scenes.Count} scene(s)…",
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
                RegisterActiveJob();
        await PublishAsync();

        try
        {
            await EnsureVideoProviderConfiguredAsync(projectId, ct).ConfigureAwait(false);

            using var bp = await _projects.LoadBlueprintAsync(projectId, ct)
                ?? throw new InvalidOperationException(
                    $"No Stage 2 blueprint for project {projectId}. Run Stage 2 first.");

            if (req.RequireLockedCharacters)
            {
                // Project-wide first (all cast voice + locked images), then per-scene mentions.
                EnsureCastReadyForVideo(projectId);
                foreach (var sn in scenes)
                    EnsureSceneCharactersLocked(projectId, sn);
            }

            var projectDir = _projects.GetProjectDir(projectId);
            Directory.CreateDirectory(Path.Combine(projectDir, "assets", "video"));

            // Pre-count work units
            var work = new List<(int Scene, int Clip, JsonElement ClipEl)>();
            if (hasClips)
            {
                // Explicit multi-select of specific clips — always force-regen (ignore OnlyMissing),
                // same as single-clip regen.
                foreach (var target in req.Clips!.OrderBy(c => c.Scene).ThenBy(c => c.Clip))
                {
                    var sceneEl = FindScene(bp.RootElement, target.Scene);
                    if (sceneEl is null)
                    {
                        await AppendLogAsync($"Scene {target.Scene}: not in blueprint — skip");
                        continue;
                    }
                    var clipEl = FindClipInScene(sceneEl.Value, target.Clip);
                    if (clipEl is null)
                    {
                        await AppendLogAsync($"S{target.Scene:D2}C{target.Clip}: not in blueprint — skip");
                        continue;
                    }
                    work.Add((Scene: target.Scene, Clip: target.Clip, ClipEl: clipEl.Value.Clone()));
                }
            }
            else
            {
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
                        var missing = !ClipPresentOnServerOrClient(path);
                        if (!req.OnlyMissing || missing)
                            work.Add((Scene: sn, Clip: cn, ClipEl: c.Clone()));
                    }
                }
            }

            if (work.Count == 0)
            {
                await AppendLogAsync("Batch: nothing to generate (only_missing).");
                await FinishAsync("done", "No clips to generate");
                return;
            }

            // Fail before any API spend if the selected video model cannot do multi-clip / plates.
            await EnsureVideoModelCapabilitiesAsync(
                    projectId,
                    needContinue: work.Any(w => w.Clip > 1),
                    needReferenceImages: req.RequireLockedCharacters,
                    ct)
                .ConfigureAwait(false);

            var resolution = await ResolveVideoResolutionAsync(projectId, req.Resolution, ct);
            await UpdateAsync(s =>
            {
                s.Total = work.Count;
                s.Index = 0;
                s.Message = $"Batch: {work.Count} clip(s) across {scenes.Count} scene(s) @ {resolution}";
            });
            await AppendLogAsync(Snapshot.Message!);

            var done = 0;
            var failed = 0;
            // Per-scene (LastGeneratedClip, CarryoverPaddingSec) — batch work can interleave scenes,
            // so the padding nudge from one scene's overrun must never leak into a different scene.
            var sceneCarryover = new Dictionary<int, (int LastClip, double PaddingSec)>();
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
                await AppendLogAsync(Snapshot.Message!);

                try
                {
                    // Previous clip element in same scene (for prompt context)
                    JsonElement? prevClipEl = null;
                    if (cn > 1)
                    {
                        var sceneEl = FindScene(bp.RootElement, sn);
                        if (sceneEl is not null)
                            prevClipEl = FindClipInScene(sceneEl.Value, cn - 1);
                    }

                    var prior = sceneCarryover.TryGetValue(sn, out var p) ? p : (LastClip: 0, PaddingSec: 0.0);
                    var incomingPadding = ResolveIncomingDurationPadding(cn, prior.LastClip, prior.PaddingSec);
                    var overrun = await GenerateOneClipAsync(
                        projectId, projectDir, sn, cn, clip, resolution, ct,
                        previousClipEl: prevClipEl,
                        blueprintRoot: bp.RootElement,
                        incomingDurationPaddingSec: incomingPadding);
                    sceneCarryover[sn] = (cn, overrun);
                    done++;
                    // Fresh clips x/y + status pills while batch is still running.
                    _projects.InvalidateSceneListCache(projectId);
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

            var status = failed > 0 && done == 0 ? "error"
                : failed > 0 ? "partial"
                : "done";
            var msg = status switch
            {
                "error" => $"Batch failed ({failed} clip(s) failed, none ok)",
                "partial" => $"Batch partial ({done} ok, {failed} failed)",
                _ => $"Batch finished ({done} clip(s))",
            };
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

    private async Task RunSceneGenAsync(StartSceneGenRequest req, string projectId, CancellationToken ct)
    {
        await _projects.RequireProjectAsync(projectId, ct);

        Snapshot = new JobSnapshot
        {
            Status = "running",
            Kind = "scene",
            ProjectId = projectId,
            Scene = req.Scene,
            Message = "Starting…",
            StartedAt = DateTimeOffset.UtcNow,
            Log = new List<string>(),
        };
                RegisterActiveJob();
        await PublishAsync();

        try
        {
            await EnsureVideoProviderConfiguredAsync(projectId, ct).ConfigureAwait(false);

            using var bp = await _projects.LoadBlueprintAsync(projectId, ct)
                ?? throw new InvalidOperationException(
                    $"No Stage 2 blueprint for project {projectId}. Run Stage 2 first.");

            var sceneEl = FindScene(bp.RootElement, req.Scene)
                ?? throw new InvalidOperationException($"Scene {req.Scene} not in blueprint.");

            if (req.RequireLockedCharacters)
            {
                EnsureCastReadyForVideo(projectId);
                EnsureSceneCharactersLocked(projectId, req.Scene);
            }

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
                if (req.Clip is int onlyClip && onlyClip > 0 && cn != onlyClip)
                    continue;
                var path = Path.Combine(videoDir, $"scene_{req.Scene:D2}_clip_{cn:D2}.mp4");
                var missing = !ClipPresentOnServerOrClient(path);
                if (!req.OnlyMissing || missing)
                    todo.Add((cn, c.Clone()));
            }

            if (todo.Count == 0)
            {
                await AppendLogAsync($"Scene {req.Scene}: nothing to generate (only_missing).");
                await FinishAsync("done", "No clips to generate");
                return;
            }

            // Fail before any API spend if the selected video model cannot do multi-clip / plates.
            await EnsureVideoModelCapabilitiesAsync(
                    projectId,
                    needContinue: todo.Any(t => t.ClipNum > 1),
                    needReferenceImages: req.RequireLockedCharacters,
                    ct)
                .ConfigureAwait(false);

            var resolution = await ResolveVideoResolutionAsync(projectId, req.Resolution, ct);
            var startMsg = $"Scene {req.Scene}: {todo.Count} clip(s) @ {resolution}";
            await UpdateAsync(s =>
            {
                s.Total = todo.Count;
                s.Index = 0;
                s.Message = startMsg;
            });
            await AppendLogAsync(startMsg);

            var done = 0;
            var failed = 0;
            var lastGeneratedClipNum = 0;
            var carryoverPaddingSec = 0.0;
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
                await AppendLogAsync(Snapshot.Message!);

                try
                {
                    JsonElement? prevClipEl = null;
                    if (cn > 1)
                    {
                        foreach (var (pcn, pclip) in todo)
                        {
                            if (pcn == cn - 1) { prevClipEl = pclip; break; }
                        }
                        // Also scan full scene clips for prev not in todo
                        if (prevClipEl is null)
                            prevClipEl = FindClipInScene(sceneEl, cn - 1);
                    }

                    var incomingPadding = ResolveIncomingDurationPadding(cn, lastGeneratedClipNum, carryoverPaddingSec);
                    carryoverPaddingSec = await GenerateOneClipAsync(
                        projectId, projectDir, req.Scene, cn, clip, resolution, ct,
                        previousClipEl: prevClipEl,
                        blueprintRoot: bp.RootElement,
                        incomingDurationPaddingSec: incomingPadding);
                    lastGeneratedClipNum = cn;
                    done++;
                    // Fresh clips x/y + status pills while scene gen is still running.
                    _projects.InvalidateSceneListCache(projectId);
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
                    // Full-scene sequential gen: later clips need previous on disk — stop after first fail.
                    // Single-clip regen (req.Clip set) keeps trying only that one clip (already filtered).
                    if (req.Clip is null or <= 0 && i + 1 < todo.Count)
                    {
                        await AppendLogAsync(
                            "Stopping scene gen after first clip failure " +
                            $"(remaining {todo.Count - i - 1} clip(s) need previous video).");
                        break;
                    }
                }
            }

            // partial = some clips ok, some failed (not "done" — remux/continue need a clear signal)
            var status = failed > 0 && done == 0 ? "error"
                : failed > 0 ? "partial"
                : "done";
            var msg = status switch
            {
                "error" => $"Scene gen failed ({failed} clip(s) failed, none ok)",
                "partial" => $"Scene gen partial ({done} ok, {failed} failed)",
                _ => $"Generation finished ({done} clip(s))",
            };
            await FinishAsync(status, msg, failed > 0 ? msg : null);

            // P0 learning: single-clip regen (typical after auto-review apply)
            if (req.Clip is int regenClip && regenClip > 0)
            {
                try
                {
                    await _learning.AppendAsync(new ReviewLearningEvent
                    {
                        ProjectId = projectId,
                        Type = "regen_after_review",
                        Scene = req.Scene,
                        Clip = regenClip,
                        Note = msg,
                        Outcome = status,
                        JobId = Snapshot.JobId,
                        ActionTaken = $"gen clip force only_missing={req.OnlyMissing}",
                    }).ConfigureAwait(false);
                }
                catch { /* non-fatal */ }
            }
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

    /// <summary>
    /// Before a regen overwrites a previously-rendered clip, copy it (plus its duration sidecar)
    /// into assets/video/_backup/ so a bad regen can be restored by hand. Keeps only the
    /// immediately-previous version — not unbounded history.
    /// </summary>
    private static void BackupExistingClipFile(string outPath, int scene, int clip)
    {
        if (!File.Exists(outPath)) return;
        try
        {
            var videoDir = Path.GetDirectoryName(outPath)!;
            var backupDir = Path.Combine(videoDir, "_backup");
            Directory.CreateDirectory(backupDir);
            var backupPath = Path.Combine(backupDir, $"scene_{scene:D2}_clip_{clip:D2}.mp4");
            File.Copy(outPath, backupPath, overwrite: true);

            var sidecar = outPath + ".duration.json";
            if (File.Exists(sidecar))
                File.Copy(sidecar, backupPath + ".duration.json", overwrite: true);
        }
        catch
        {
            // Best-effort safety net — never block a regen because the backup copy failed.
        }
    }

    /// <summary>
    /// Ceiling on how much extra duration a measured overrun on the previous clip in the same
    /// continuation chain can add to the next clip's request. Bounds a single anomalous measurement
    /// from ballooning every subsequent clip's requested duration.
    /// </summary>
    private const double MaxCarryoverDurationPaddingSec = 2.0;

    /// <summary>
    /// How much of the previous clip's measured duration overrun should carry forward as padding for
    /// <paramref name="clipNum"/>. Only non-zero when <paramref name="lastGeneratedClipNum"/> is truly
    /// the immediately preceding clip number (no gap) — a gap (e.g. only-missing regen skipped one)
    /// means there's no real adjacency to reconcile against, so start fresh at zero.
    /// </summary>
    public static double ResolveIncomingDurationPadding(
        int clipNum, int lastGeneratedClipNum, double lastOverrunSec) =>
        clipNum == lastGeneratedClipNum + 1 ? lastOverrunSec : 0.0;

    /// <summary>
    /// Seconds a just-finished clip's real measured duration overran what was requested, clamped to
    /// <see cref="MaxCarryoverDurationPaddingSec"/>. Zero for non-continuation models (Constraint 3 —
    /// only continuation chains get the free same-scene reconciliation) or when the clip ran at/under
    /// its requested duration (never carry forward a negative "padding").
    /// </summary>
    public static double ComputeCarryoverOverrunSec(
        bool supportsContinue, double probedDurationSec, int requestedDurationSec) =>
        supportsContinue
            ? Math.Clamp(probedDurationSec - requestedDurationSec, 0.0, MaxCarryoverDurationPaddingSec)
            : 0.0;

    /// <summary>
    /// Applies carried-forward padding to a clip's requested duration, never exceeding the resolved
    /// model's absolute ceiling (<paramref name="absMaxSeconds"/>).
    /// </summary>
    public static int ApplyIncomingDurationPadding(
        int durationSeconds, double incomingDurationPaddingSec, int absMaxSeconds) =>
        incomingDurationPaddingSec > 0
            ? Math.Min(absMaxSeconds, durationSeconds + (int)Math.Ceiling(incomingDurationPaddingSec))
            : durationSeconds;

    /// <summary>
    /// Generates one clip. Returns the seconds this clip's actual measured duration overran its
    /// requested duration (0 when not applicable) — for continuation-chain models, the caller feeds
    /// this back in as <paramref name="incomingDurationPaddingSec"/> for the next clip in the same
    /// scene, since clip N+1 already can't start before clip N is on disk (free reconciliation,
    /// no added wall-clock cost). Never used to retroactively correct this clip itself — duration is
    /// billed/quantized per provider, so padding the next request is cheaper than any fix-up here.
    /// </summary>
    private async Task<double> GenerateOneClipAsync(
        string projectId,
        string projectDir,
        int scene,
        int clip,
        JsonElement clipEl,
        string resolution,
        CancellationToken ct,
        JsonElement? previousClipEl = null,
        JsonElement? blueprintRoot = null,
        double incomingDurationPaddingSec = 0.0)
    {
        var profiles = _projects.LoadCharacterPromptProfiles(projectId);
        var videoDir = Path.Combine(projectDir, "assets", "video");
        var overrunSec = 0.0;

        // Previous clip in this scene — Imagine /videos/extensions continues from that video.
        // Clip 2+ requires previous on disk (no gaps). Cast-set changes reseed fresh+refs (PR2).
        string? prevVisual = null;
        string? prevVideoPath = null;
        // Disposable working copy of prev for silence-trim / extend — never rewrite clip N-1 on disk.
        string? prevExtendWorkTemp = null;
        var cont = clipEl.TryGetProperty("veo_continuation_source", out var ce)
            ? (ce.GetString() ?? "none")
            : "none";
        var wantContinue =
            string.Equals(cont, "extend_previous", StringComparison.OrdinalIgnoreCase) ||
            clip > 1;

        string? prevOnDisk = null;
        if (clip > 1)
        {
            prevOnDisk = Path.Combine(
                projectDir, "assets", "video", $"scene_{scene:D2}_clip_{clip - 1:D2}.mp4");
            var prevBytesOnServer = File.Exists(prevOnDisk) && new FileInfo(prevOnDisk).Length >= 1024;
            // Client-storage is the primary path now (server MP4s get pruned within minutes of the
            // browser confirming a synced save — see ServerMediaPruningService), so "previous clip
            // exists" must also accept its .client.json marker, not just raw bytes still on server disk.
            if (!prevBytesOnServer && !ClipPresentOnServerOrClient(prevOnDisk))
            {
                throw new InvalidOperationException(
                    $"Generate S{scene:D2}C{clip - 1:D2} first — later clips continue from the previous video.");
            }

            if (prevBytesOnServer)
            {
                // Breath-tail silence trim for extend input only. Mutating prevOnDisk in place used to
                // permanently shorten a finished clip when this job then failed/cancelled before C_N
                // was written (no backup of N-1). Work on a throwaway copy instead.
                prevExtendWorkTemp = Path.Combine(
                    projectDir, "assets", "video", $"_prev_extend_s{scene:D2}c{clip:D2}.mp4");
                File.Copy(prevOnDisk, prevExtendWorkTemp, overwrite: true);
                prevVideoPath = prevExtendWorkTemp;
            }
            // else: previous clip already synced to the client and was pruned server-side. That's
            // fine — prevVideoPath stays null, and generation below always does a fresh gen with
            // locked reference images regardless (no server-side video-extend since ffmpeg left).
        }

        if (previousClipEl is { } prevEl &&
            prevEl.TryGetProperty("visual_prompt", out var pvp))
            prevVisual = pvp.GetString();

        if (prevVisual is null && wantContinue && blueprintRoot is { } root)
            prevVisual = FindClipVisualInBlueprint(root, scene, clip - 1);

        // PR2: reseed with locked refs when on-screen cast set changes (API drops refs on extend).
        var reseedFresh = false;
        // Imagine /videos/extensions rejects input video longer than 15s.
        // Bad extension-tail trims (or re-extend chains) can leave a prev clip over that cap —
        // clamp to the last ≤15s so continuity still uses the ending frames.
        string? extendInputTemp = null;
        try
        {
            // No native ffmpeg: never video-extend (cannot split prev+new). Fresh gen + locked plates.
            // Gated on `clip > 1` (not `prevVideoPath is not null`): the previous clip may only exist
            // via its .client.json marker now (synced to the client, pruned server-side), in which case
            // prevVideoPath is already null above — this is still a continuation clip either way.
            if (clip > 1)
            {
                reseedFresh = true;
                prevVideoPath = null;
                await AppendLogAsync(
                    $"  [Continuity] S{scene:D2}C{clip:D2} fresh gen with locked refs " +
                    "(no server video-extend; browser stitch for play/export)");
            }

            if (prevVideoPath is not null && _opts.IdentityReseedOnCastChange)
            {
                var curKeys = ClipVideoPromptBuilder.ResolveOnScreenCharacterKeys(clipEl)
                    .Where(k => !(profiles.TryGetValue(k, out var cp) && cp.VoiceOnly))
                    .Select(k => k)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var prevKeys = previousClipEl is { } pe
                    ? ClipVideoPromptBuilder.ResolveOnScreenCharacterKeys(pe)
                        .Where(k => !(profiles.TryGetValue(k, out var pp) && pp.VoiceOnly))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                        .ToList()
                    : new List<string>();
                if (prevKeys.Count > 0 && !OnScreenSetsEqual(curKeys, prevKeys))
                {
                    reseedFresh = true;
                    await AppendLogAsync(
                        $"  [Identity] Cast set changed " +
                        $"[{string.Join(", ", prevKeys)}] → [{string.Join(", ", curKeys)}] — " +
                        "fresh gen with locked refs (not video-extend)");
                    prevVideoPath = null; // API: attach refs
                    // Keep prevVisual for continuity prose only
                }
            }

            // Silent → first spoken/VO: video-extend often clips the opening word (mouth stays closed
            // from the prior silent clip). Require prev on disk for order, but gen fresh + plates.
            if (prevVideoPath is not null)
            {
                JsonElement? prevMeta = previousClipEl;
                if (prevMeta is null && blueprintRoot is { } br)
                    prevMeta = FindClipElementInBlueprint(br, scene, clip - 1);
                if (prevMeta is { } pm && ClipHasSpokenAudio(clipEl) && !ClipHasSpokenAudio(pm))
                {
                    reseedFresh = true;
                    prevVideoPath = null;
                    await AppendLogAsync(
                        $"  [Speech] S{scene:D2}C{clip:D2} is first spoken after silence — " +
                        "fresh gen with locked refs (not video-extend) so the opening word is not clipped");
                }
            }

            if (prevVideoPath is not null)
            {
                await AppendLogAsync(
                    $"  [Continuity] Imagine video-extend from S{scene:D2}C{clip - 1:D2} " +
                    $"({Path.GetFileName(prevVideoPath)})");
            }
            else if (reseedFresh && prevOnDisk is not null)
            {
                await AppendLogAsync(
                    $"  [Identity] Reseed S{scene:D2}C{clip:D2} after S{scene:D2}C{clip - 1:D2} " +
                    "(locked character refs attached)");
            }

            string? styleHead = null;
            try
            {
                var rules = _projectRules.GetActiveRulesBlock(projectId);
                if (!string.IsNullOrWhiteSpace(rules))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(
                        rules, @"STYLE LOCK:\s*([^\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (m.Success)
                        styleHead = "STYLE LOCK: " + m.Groups[1].Value.Trim().TrimEnd('.', ' ');
                }
            }
            catch { /* non-fatal */ }

            var built = ClipVideoPromptBuilder.Build(
                clipEl,
                projectDir,
                characters: profiles,
                previousClipVisualPrompt: prevVisual,
                previousClipVideoPath: prevVideoPath,
                startFrameImagePath: null,
                maxRefs: 5,
                styleHead: styleHead,
                resolution: resolution);

            if (string.IsNullOrWhiteSpace(built.Prompt))
                throw new InvalidOperationException("clip missing visual_prompt");

            // Fresh / reseed: every on-screen cast key must have a locked ref attached
            if (prevVideoPath is null)
                EnsureFreshGenHasLockedRefs(projectId, projectDir, built, profiles);
            else
            {
                // Extend still requires locks on disk even when API cannot attach them
                EnsureOnScreenLocksExist(projectId, projectDir, built, profiles);
            }

            // Approved project-scoped house rules (learning). Global clip gen rules live in
            // embedded prompts/clip_gen_rules.txt and are composed inside ClipVideoPromptBuilder.
            try
            {
                var rules = _projectRules.GetActiveRulesBlock(projectId);
                if (!string.IsNullOrWhiteSpace(rules))
                {
                    built = built.WithPrompt(
                        built.Prompt.TrimEnd() + "\n\n" + rules.Trim(),
                        " · project-rules");
                }
            }
            catch { /* non-fatal */ }

            // Pre-budget to xAI video ~4096 char hard cap (strip HOUSE RULES / project rules first).
            // Avoids a guaranteed first-attempt 400 on every clip.
            var preLen = built.Prompt.Length;
            var fitted = ClipVideoPromptBuilder.FitPromptToVideoBudget(built.Prompt);
            if (fitted.Length < preLen)
            {
                built = built.WithPrompt(fitted, $" · pre-budget {preLen}→{fitted.Length}");
                await AppendLogAsync(
                    $"  [Prompt] pre-budget {preLen}→{fitted.Length} chars (video hard cap {ClipVideoPromptBuilder.VideoPromptHardCapChars})");
            }

            // Persist + log full prompt for evaluation (admin logs surface this)
            await WriteAndLogPromptAsync(projectId, projectDir, scene, clip, built, ct).ConfigureAwait(false);

            if (built.Prompt.Contains("VOICE LOCK", StringComparison.OrdinalIgnoreCase))
                await AppendLogAsync("  [Voice] VOICE LOCK from character profile");
            if (built.ReferenceImagePaths.Count > 0)
                await AppendLogAsync(
                    $"  [Refs] attached={built.RefsAttachedToApi} count={built.ReferenceImagePaths.Count}: " +
                    string.Join(", ", built.ReferenceImagePaths.Select(Path.GetFileName)));
            else if (prevVideoPath is not null)
                await AppendLogAsync("  [Refs] video-extend — locked plates not attached to API (IDENTITY text only)");

            var model = await ResolveVideoModelAsync(projectId, ct);
            if (string.IsNullOrWhiteSpace(resolution))
                resolution = await ResolveVideoResolutionAsync(projectId, null, ct);

            // Only continuation-chain models get carried-forward padding: clip N+1 already can't
            // start before clip N is on disk for these, so reconciling against N's real measurement
            // costs nothing extra. Non-continuation models don't have that same-scene coupling.
            var supportsContinue = SupportedModelCatalog.ResolveOrDefault(model, ModelCapability.Video).SupportsVideoContinue;

            // Dialogue-aware duration (tight for short lines — billed per second), clamped to the
            // actually-selected model's own duration caps (SupportedModelCatalog) instead of a
            // hardcoded provider assumption.
            var (durMin, durMax, durAbsMax) = ClipDurationEstimator.ResolveBoundsForModel(model);
            var duration = ClipDurationEstimator.EstimateForClip(clipEl, durMin, durMax, durAbsMax);
            if (supportsContinue && incomingDurationPaddingSec > 0)
            {
                var padded = ApplyIncomingDurationPadding(duration, incomingDurationPaddingSec, durAbsMax);
                await AppendLogAsync(
                    $"  [Duration] +{incomingDurationPaddingSec:F1}s carried from previous clip's overrun -> {duration}s to {padded}s");
                duration = padded;
            }
            await AppendLogAsync($"  [Duration] estimated {duration}s (dialogue-aware, max {durMax}s, model={model})");
            // Extension / ref: new portion typically max 10s
            if (prevVideoPath is not null || built.ReferenceImagePaths.Count > 0)
                duration = Math.Min(duration, 10);

            var modeLabel = prevVideoPath is not null ? "video-extend" : built.Mode;
            await AppendLogAsync(
                $"  [Grok] Submit S{scene:D2}C{clip} duration={duration}s res={resolution} " +
                $"model={model} mode={modeLabel} {built.PromptLogSummary}");

            // Prefer official video continue; character refs only on fresh gens (API: no mix)
            var requestId = await _grok.SubmitGenerationAsync(
                built.Prompt,
                duration,
                resolution,
                model,
                ct,
                referenceImagePaths: prevVideoPath is null && built.ReferenceImagePaths.Count > 0
                    ? built.ReferenceImagePaths
                    : null,
                startFrameImagePath: null,
                continueFromVideoPath: prevVideoPath);
            await AppendLogAsync($"  [Grok] request_id={requestId}");

            var url = await _grok.PollForVideoUrlAsync(
                requestId,
                msg => { _ = AppendLogAsync($"  [Grok] {msg}"); },
                ct);

            // Save MP4 file to server project directory so client media sync delivers MP4 files to client folder
            var mp4Path = Path.Combine(videoDir, $"scene_{scene:D2}_clip_{clip:D2}.mp4");
            try
            {
                var http = _httpFactory.CreateClient("media-proxy");
                var bytes = await http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
                if (bytes.Length > 0)
                {
                    await File.WriteAllBytesAsync(mp4Path, bytes, ct).ConfigureAwait(false);
                    await AppendLogAsync($"  [Media] Saved {bytes.Length} bytes to {Path.GetFileName(mp4Path)}");

                    // Trigger 100% automated background clip dialogue & speaker verification.
                    // Telemetry recording below awaits this (if started) so DialogueTruncated
                    // reflects the real Expected-vs-Heard result instead of staying hardcoded false.
                    Task<ClipDialogueVerificationResult?>? dialogueVerificationTask = null;
                    if (_dialogueVerification is not null && _dialogueVerification.IsConfigured)
                    {
                        var projId = Snapshot.ProjectId ?? projectId ?? _projects.ActiveProjectId;
                        dialogueVerificationTask = Task.Run(async () =>
                        {
                            try
                            {
                                return await _dialogueVerification.VerifyClipDialogueAsync(projId, scene, clip, ct: CancellationToken.None).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _log.LogWarning(ex, "Background dialogue verification failed for S{Scene:D2}C{Clip:D2}", scene, clip);
                                return null;
                            }
                        });
                    }

                    // Probe the real rendered duration once — used both to carry a same-scene
                    // continuation-chain padding nudge into the next clip (below) and, if timing
                    // calibration is configured, for telemetry.
                    var probedSec = Mp4DurationReader.TryReadSeconds(mp4Path) ?? (double)duration;
                    overrunSec = ComputeCarryoverOverrunSec(supportsContinue, probedSec, duration);

                    // Record dynamic cut timing telemetry into SQLite database for continuous server learning
                    if (_timingCalibration is not null)
                    {
                        var projId = Snapshot.ProjectId ?? projectId ?? _projects.ActiveProjectId;

                        // 1. Extract dialogue text & word count from clip blueprint
                        string dialogueText = "";
                        if (clipEl.TryGetProperty("audio_payload", out var ap) && ap.ValueKind == JsonValueKind.Object &&
                            ap.TryGetProperty("dialogue", out var dEl))
                        {
                            dialogueText = dEl.GetString() ?? "";
                        }
                        int wordCount = string.IsNullOrWhiteSpace(dialogueText)
                            ? 0
                            : dialogueText.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;

                        // 2. Extract camera movement category from blueprint or visual prompt
                        string camCat = "cam_push_in";
                        if (clipEl.TryGetProperty("camera", out var camEl) && camEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(camEl.GetString()))
                            camCat = camEl.GetString()!;
                        else if (clipEl.TryGetProperty("camera_category", out var ccEl) && ccEl.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(ccEl.GetString()))
                            camCat = ccEl.GetString()!;

                        // 3. Dynamically classify scene action category via AiActionOverheadClassifier
                        var promptToAnalyze = built.Prompt ?? "";
                        string actCat = "act_generic_action";
                        if (_timingClassifier is not null)
                        {
                            var estimation = _timingClassifier.ClassifyNovelAction(promptToAnalyze, null);
                            if (!string.IsNullOrWhiteSpace(estimation.MatchCategoryId))
                                actCat = estimation.MatchCategoryId;
                        }

                        // 4. Calculate measured camera and physical action overheads
                        double camOverhead = _timingLedger?.GetOverheadSec(camCat, 1.6) ?? 1.6;
                        double netSpeechSec = wordCount > 0 ? (wordCount / 2.6) : 0.0;
                        double measuredActOverhead = Math.Max(0.5, Math.Round(probedSec - camOverhead - netSpeechSec, 2));

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                var dialogueTruncated = false;
                                if (dialogueVerificationTask is not null)
                                {
                                    var verification = await dialogueVerificationTask.ConfigureAwait(false);
                                    if (verification is not null)
                                        dialogueTruncated = ClipDialogueVerificationService.LooksTruncated(verification);
                                }

                                await _timingCalibration.RecordCutTelemetryAsync(
                                    projectId: projId,
                                    sceneNumber: scene,
                                    videoModelId: model,
                                    videoModelVersion: "v1",
                                    evaluatorModelId: "grok-4.5",
                                    evaluatorModelVersion: "v1",
                                    cameraCategory: camCat,
                                    actionCategory: actCat,
                                    wordCount: wordCount,
                                    estimatedDurationSec: (double)duration,
                                    clipDurationSec: probedSec,
                                    measuredCamOverheadSec: camOverhead,
                                    measuredActionOverheadSec: measuredActOverhead,
                                    dialogueTruncated: dialogueTruncated).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                _log.LogWarning(ex, "Background timing telemetry logging failed for S{Scene:D2}C{Clip:D2}", scene, clip);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Could not save MP4 bytes to server project directory for S{Scene:D2}C{Clip:D2}", scene, clip);
            }

            var relPath = MediaRegistryService.ClipRelativePath(scene, clip);
            var ticket = _mediaProxy.Issue(url, TimeSpan.FromMinutes(45));
            var clientUrl = $"/api/media/proxy/{ticket}";
            await UpdateAsync(s =>
            {
                s.ClientMediaUrl = clientUrl;
                s.ClientRelativePath = relPath;
                s.Scene = scene;
                s.Clip = clip;
            });
            await AppendLogAsync(
                $"  [Grok] video ready for client save → {relPath} (not stored on server disk)");

            if (_sidecars is not null)
            {
                try
                {
                    var projDir = _projects.GetProjectDir(Snapshot.ProjectId ?? projectId ?? _projects.ActiveProjectId);
                    await _sidecars.WriteSidecarAsync(
                        projDir,
                        scene,
                        clip,
                        prompt: built.Prompt,
                        scriptText: "",
                        model: model,
                        resolution: resolution,
                        durationSeconds: (double)duration,
                        sha256: "",
                        sizeBytes: 0,
                        ct: ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Could not write clip sidecar for S{Scene:D2}C{Clip:D2}", scene, clip);
                }
            }

            // Cost uses requested duration (no server file to probe until client registers).
            var costDurationSec = (double)duration;

            try
            {
                var costProjectId = Snapshot.ProjectId ?? projectId ?? _projects.ActiveProjectId;
                await _costs.RecordVideoGenerationAsync(
                    costProjectId,
                    scene,
                    clip,
                    costDurationSec,
                    resolution,
                    model,
                    hasRefImage: built.ReferenceImagePaths.Count > 0 || prevVideoPath is not null,
                    isExtend: prevVideoPath is not null,
                    requestId: requestId,
                    requestedDurationSec: duration,
                    userId: Snapshot.UserId ?? _user.UserId,
                    ct: ct);
                await AppendLogAsync(
                    $"  [Cost] tracked list-rate for S{scene:D2}C{clip} ({costDurationSec:F2}s)");
            }
            catch (Exception ex)
            {
                await AppendLogAsync($"  [Cost] ledger write skipped: {ex.Message}");
            }
        }
        finally
        {
            if (extendInputTemp is not null)
            {
                try { File.Delete(extendInputTemp); } catch { /* ignore */ }
            }
            if (prevExtendWorkTemp is not null)
            {
                try { File.Delete(prevExtendWorkTemp); } catch { /* ignore */ }
            }
        }

        return overrunSec;
    }

    private async Task WriteAndLogPromptAsync(
        string projectId,
        string projectDir,
        int scene,
        int clip,
        ClipVideoPromptBuilder.PromptBuildResult built,
        CancellationToken ct)
    {
        try
        {
            var dir = Path.Combine(projectDir, "assets", "video", "prompts");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"S{scene:D2}C{clip:D2}.txt");
            var header =
                $"# S{scene:D2}C{clip:D2}  {built.PromptLogSummary}\n" +
                $"# projectId: {projectId}\n" +
                $"# mode: {built.Mode}\n" +
                $"# castCount: {built.CastCount}\n" +
                $"# onScreen: {string.Join(", ", built.OnScreenKeys)}\n" +
                $"# refs: {string.Join(", ", built.ReferenceImagePaths.Select(Path.GetFileName))}\n" +
                $"# refsAttachedToApi: {built.RefsAttachedToApi}\n" +
                $"# startFrame: {built.StartFrameImagePath ?? "(none)"}\n" +
                $"# characters: {string.Join(", ", built.CharacterKeys)}\n\n";
            await File.WriteAllTextAsync(path, header + built.Prompt, ct).ConfigureAwait(false);

            var metaPath = Path.Combine(dir, $"S{scene:D2}C{clip:D2}.meta.json");
            ArchiveClipPromptHistory(projectDir, scene, clip, metaPath);
            var meta = new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
                ["scene"] = scene,
                ["clip"] = clip,
                ["mode"] = built.Mode,
                ["castCount"] = built.CastCount,
                ["onScreenKeys"] = built.OnScreenKeys.ToList(),
                ["characterKeys"] = built.CharacterKeys.ToList(),
                ["refs"] = built.ReferenceImagePaths.Select(Path.GetFileName).ToList(),
                ["refsAttachedToApi"] = built.RefsAttachedToApi,
                ["styleHead"] = built.StyleHead,
                ["castCountLine"] = built.CastCountLine,
                ["actionText"] = built.ActionText,
                // Full prompt body on disk for manual / external AI review (PR5 project-local data)
                ["prompt"] = built.Prompt,
                ["promptLen"] = built.Prompt.Length,
                ["promptLogSummary"] = built.PromptLogSummary,
                ["startFrame"] = built.StartFrameImagePath,
                ["builtAtUtc"] = DateTimeOffset.UtcNow.ToString("o"),
            };
            var metaJson = System.Text.Json.JsonSerializer.Serialize(meta, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            }) + "\n";
            await File.WriteAllTextAsync(metaPath, metaJson, ct).ConfigureAwait(false);

            await AppendLogAsync(
                $"  [Prompt] saved {Path.GetRelativePath(projectDir, path)} + meta " +
                $"({built.Prompt.Length} chars, castCount={built.CastCount})");
            await AppendLogAsync("--- PROMPT BEGIN ---");
            const int chunk = 3500;
            for (var i = 0; i < built.Prompt.Length; i += chunk)
            {
                var len = Math.Min(chunk, built.Prompt.Length - i);
                await AppendLogAsync(built.Prompt.Substring(i, len));
            }
            await AppendLogAsync("--- PROMPT END ---");
        }
        catch (Exception ex)
        {
            await AppendLogAsync($"  [Prompt] log failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Before a clip's prompt meta is overwritten by a fresh generation, copy the previous
    /// version into assets/video/history/ so ClipPromptCompareViewer has a prior prompt to show
    /// alongside whatever prior video pagetomovie-media.js archived client-side. Best-effort.
    /// </summary>
    private static void ArchiveClipPromptHistory(string projectDir, int scene, int clip, string metaPath)
    {
        try
        {
            if (!File.Exists(metaPath)) return;
            var historyDir = Path.Combine(projectDir, "assets", "video", "history");
            Directory.CreateDirectory(historyDir);
            var dest = Path.Combine(
                historyDir,
                $"scene_{scene:D2}_clip_{clip:D2}_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.meta.json");
            File.Copy(metaPath, dest, overwrite: false);
        }
        catch
        {
            // never block a regeneration on history archiving
        }
    }

    /// <summary>Archived prompt versions for one clip (newest first), for ClipPromptCompareViewer.</summary>
    public static List<ClipPromptHistoryEntry> ListClipPromptHistory(string projectDir, int scene, int clip)
    {
        var result = new List<ClipPromptHistoryEntry>();
        var historyDir = Path.Combine(projectDir, "assets", "video", "history");
        if (!Directory.Exists(historyDir)) return result;

        var prefix = $"scene_{scene:D2}_clip_{clip:D2}_";
        foreach (var file in Directory.GetFiles(historyDir, $"{prefix}*.meta.json"))
        {
            try
            {
                var name = Path.GetFileName(file);
                var stamp = name[prefix.Length..^".meta.json".Length];
                if (!long.TryParse(stamp, out var ms)) continue;

                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                string? prompt = root.TryGetProperty("prompt", out var p) ? p.GetString() : null;
                result.Add(new ClipPromptHistoryEntry
                {
                    TimestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(ms),
                    Prompt = prompt ?? "",
                    VideoRelativePath = $"assets/video/history/scene_{scene:D2}_clip_{clip:D2}_{ms}.mp4",
                });
            }
            catch
            {
                // skip unreadable/corrupt history entry
            }
        }

        result.Sort((a, b) => b.TimestampUtc.CompareTo(a.TimestampUtc));
        return result;
    }

    public sealed class ClipPromptHistoryEntry
    {
        public DateTimeOffset TimestampUtc { get; set; }
        public string Prompt { get; set; } = "";
        /// <summary>Relative path under the project dir — client checks its own media folder for this.</summary>
        public string VideoRelativePath { get; set; } = "";
    }

    /// <summary>
    /// Probe final clip length (MP4 box parse) and write duration sidecar for cost ledger.
    /// </summary>
    private async Task<double?> EnsureClipDurationSidecarAsync(
        string videoPath,
        int scene,
        int clip,
        CancellationToken ct)
    {
        if (!File.Exists(videoPath))
            return null;
        try
        {
            var sec = Mp4DurationReader.TryReadSeconds(videoPath);
            if (sec is > 0)
            {
                await MediaDurationProbe.WriteDurationSidecarAsync(videoPath, sec.Value, ct)
                    .ConfigureAwait(false);
                await AppendLogAsync(
                    $"  [Duration] S{scene:D2}C{clip:D2} sidecar {sec.Value:F2}s");
                return sec.Value;
            }
        }
        catch (Exception ex)
        {
            await AppendLogAsync($"  [Duration] sidecar skip: {ex.Message}");
        }

        return null;
    }

    private static bool OnScreenSetsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count) return false;
        // Inputs are expected already sorted ignore-case; still compare as sets.
        var setA = new HashSet<string>(a, StringComparer.OrdinalIgnoreCase);
        return setA.SetEquals(b);
    }

    /// <summary>
    /// Video-extend cannot attach plates to the API, but locked refs must still exist on disk
    /// so CHARACTER VARIABLES / future reseeds stay authoritative.
    /// </summary>
    private void EnsureOnScreenLocksExist(
        string projectId,
        string projectDir,
        ClipVideoPromptBuilder.PromptBuildResult built,
        IReadOnlyDictionary<string, ClipVideoPromptBuilder.CharacterProfile> profiles)
    {
        var missing = MissingOnScreenLockKeys(projectId, projectDir, built, profiles);
        if (missing.Count == 0) return;

        throw new InvalidOperationException(
            "Locked character reference images required on disk before video-extend " +
            "(identity continuity even though the API cannot attach plates). " +
            $"Missing ref for: {string.Join(", ", missing)}. " +
            "Open Characters → generate + lock a portrait for each on-screen role.");
    }

    /// <summary>
    /// On fresh (non-extend) gens, every non-voice-only character in the clip prompt must have
    /// a locked ref image actually attached — prevents identity drift across clips.
    /// </summary>
    private void EnsureFreshGenHasLockedRefs(
        string projectId,
        string projectDir,
        ClipVideoPromptBuilder.PromptBuildResult built,
        IReadOnlyDictionary<string, ClipVideoPromptBuilder.CharacterProfile> profiles)
    {
        var missing = MissingOnScreenLockKeys(projectId, projectDir, built, profiles);
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Locked character reference images required for fresh video gen (avoids face drift). " +
                $"Missing ref for: {string.Join(", ", missing)}. " +
                "Open Characters → generate + lock a portrait for each on-screen role.");
        }

        var onScreen = OnScreenVisualKeys(built, profiles);
        if (onScreen.Count > 0 && built.ReferenceImagePaths.Count == 0)
        {
            throw new InvalidOperationException(
                "Fresh video gen built a prompt with on-screen cast but attached 0 reference images. " +
                "Lock portraits under Characters and retry.");
        }
    }

    private static List<string> OnScreenVisualKeys(
        ClipVideoPromptBuilder.PromptBuildResult built,
        IReadOnlyDictionary<string, ClipVideoPromptBuilder.CharacterProfile> profiles)
    {
        return (built.OnScreenKeys.Count > 0 ? built.OnScreenKeys : built.CharacterKeys)
            .Where(k => !(profiles.TryGetValue(k, out var p) && p.VoiceOnly))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private List<string> MissingOnScreenLockKeys(
        string projectId,
        string projectDir,
        ClipVideoPromptBuilder.PromptBuildResult built,
        IReadOnlyDictionary<string, ClipVideoPromptBuilder.CharacterProfile> profiles)
    {
        var onScreen = OnScreenVisualKeys(built, profiles);
        var missing = new List<string>();
        foreach (var key in onScreen)
        {
            var path = ClipVideoPromptBuilder.ResolveCharacterRefPathPublic(projectDir, key)
                       ?? _projects.ResolveCharacterRefPath(projectId, key);
            if (path is null || !File.Exists(path))
                missing.Add(key);
        }
        return missing;
    }

    private static string? FindClipVisualInBlueprint(JsonElement root, int scene, int clipNum)
    {
        try
        {
            var c = FindClipElementInBlueprint(root, scene, clipNum);
            if (c is { } clip && clip.TryGetProperty("visual_prompt", out var vp))
                return vp.GetString();
        }
        catch { /* ignore */ }
        return null;
    }

    private static JsonElement? FindClipElementInBlueprint(JsonElement root, int scene, int clipNum)
    {
        try
        {
            if (!root.TryGetProperty("scenes", out var scenes) ||
                scenes.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var s in scenes.EnumerateArray())
            {
                if (!s.TryGetProperty("scene_number", out var sn) || !sn.TryGetInt32(out var n) || n != scene)
                    continue;
                return FindClipInScene(s, clipNum);
            }
        }
        catch { /* ignore */ }
        return null;
    }

    /// <summary>
    /// True when the clip has spoken dialogue or VO text (not silent establish).
    /// </summary>
    internal static bool ClipHasSpokenAudio(JsonElement clipEl)
    {
        if (!clipEl.TryGetProperty("audio_payload", out var ap) ||
            ap.ValueKind != JsonValueKind.Object)
            return false;
        var dialogue = ap.TryGetProperty("dialogue", out var d) ? d.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(dialogue))
            return false;
        var delivery = (ap.TryGetProperty("delivery", out var del) ? del.GetString() ?? "none" : "none")
            .Trim().ToLowerInvariant();
        if (delivery is "none" or "")
            return false;
        return Stage2PlannerService.IsOnCameraDelivery(delivery) ||
               delivery is "voiceover_internal" or "internal" or "narration" or "vo" or "thought" or
                   "voiceover" or "voice_over" or "off_camera" or "offcamera";
    }

    private static JsonElement? FindClipInScene(JsonElement sceneEl, int clipNum)
    {
        if (!sceneEl.TryGetProperty("veo_clips", out var clips) ||
            clips.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var c in clips.EnumerateArray())
        {
            if (c.TryGetProperty("clip_number", out var cn) && cn.TryGetInt32(out var n) && n == clipNum)
                return c;
        }
        return null;
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

    /// <summary>
    /// Prefer explicit request resolution, else project Configuration, else app default —
    /// then guard against mixing resolutions within one project (see
    /// <see cref="GetLockedResolutionAsync"/>).
    /// </summary>
    private async Task<string> ResolveVideoResolutionAsync(
        string projectId,
        string? requested,
        CancellationToken ct)
    {
        string resolution;
        if (!string.IsNullOrWhiteSpace(requested))
        {
            resolution = NormalizeResolution(requested);
        }
        else
        {
            resolution = null!;
            try
            {
                var cfg = await _projects.GetConfigAsync(projectId, ct).ConfigureAwait(false);
                if (cfg.TryGetValue("resolution", out var el))
                {
                    var fromCfg = el.ValueKind switch
                    {
                        JsonValueKind.String => el.GetString(),
                        JsonValueKind.Number => el.ToString(),
                        _ => null,
                    };
                    if (!string.IsNullOrWhiteSpace(fromCfg))
                        resolution = NormalizeResolution(fromCfg);
                }
            }
            catch
            {
                // fall through to app default
            }

            resolution ??= NormalizeResolution(
                string.IsNullOrWhiteSpace(_opts.DefaultResolution) ? "480p" : _opts.DefaultResolution);
        }

        var locked = await GetLockedResolutionAsync(projectId, ct).ConfigureAwait(false);
        if (locked is not null && !string.Equals(locked, resolution, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"This project's existing clips are {locked} — generating at {resolution} would mix " +
                $"resolutions in one movie. Delete the existing clips first, or generate at {locked}.");
        }

        return resolution;
    }

    /// <summary>
    /// The resolution already used by this project's on-disk clips, if consistent — guards
    /// against accidentally mixing resolutions within one project. Null when there are no
    /// on-disk clips yet, or existing data doesn't settle on one value (fail-open: never
    /// block generation on ambiguous or missing cost-ledger history).
    /// </summary>
    public async Task<string?> GetLockedResolutionAsync(string projectId, CancellationToken ct = default)
    {
        try
        {
            var onDisk = _reviewIndex.ListOnDiskClipCoords(projectId);
            var ledger = await _costs.GetCostLedgerAsync(projectId, ct).ConfigureAwait(false);
            return DetermineLockedResolution(onDisk, ledger);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Pure decision: given which (scene, clip) pairs are on disk and the project's cost
    /// ledger, what resolution (if any) is this project locked to? Null when there are no
    /// on-disk clips, or the ledger doesn't settle on one consistent value for them
    /// (fail-open — ambiguous/missing history never blocks generation).
    /// </summary>
    public static string? DetermineLockedResolution(
        IEnumerable<(int Scene, int Clip)> onDiskClips,
        IEnumerable<CostEvent> costLedger)
    {
        var onDisk = onDiskClips as ICollection<(int Scene, int Clip)> ?? onDiskClips.ToList();
        if (onDisk.Count == 0)
            return null;

        var onDiskSet = onDisk.ToHashSet();
        var resolutions = costLedger
            .Where(e => e.Scene is int s && e.Clip is int c &&
                        onDiskSet.Contains((s, c)) &&
                        !string.IsNullOrWhiteSpace(e.Resolution))
            .Select(e => NormalizeResolution(e.Resolution))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return resolutions.Count == 1 ? resolutions[0] : null;
    }

    /// <summary>
    /// Fail closed before video spend when the selected model lacks continue/refs required for this job.
    /// </summary>
    private async Task EnsureVideoModelCapabilitiesAsync(
        string projectId,
        bool needContinue,
        bool needReferenceImages,
        CancellationToken ct)
    {
        if (!needContinue && !needReferenceImages)
            return;

        var modelId = await ResolveVideoModelAsync(projectId, ct).ConfigureAwait(false);
        var entry = SupportedModelCatalog.ResolveOrDefault(modelId, ModelCapability.Video);
        if (needContinue && !entry.SupportsVideoContinue)
        {
            throw new InvalidOperationException(
                $"Video model '{entry.Id}' does not support clip-to-clip continue " +
                "(required for clip 2+). Switch project video model to grok-imagine-video " +
                "(or another model with video-extend). " +
                (string.IsNullOrWhiteSpace(entry.Notes) ? "" : entry.Notes));
        }

        if (needReferenceImages && !entry.SupportsReferenceImages)
        {
            throw new InvalidOperationException(
                $"Video model '{entry.Id}' cannot attach locked character reference plates. " +
                "Switch project video model to grok-imagine-video, or disable the cast lock gate " +
                "only if you accept identity drift. " +
                (string.IsNullOrWhiteSpace(entry.Notes) ? "" : entry.Notes));
        }
    }

    /// <summary>
    /// Project <c>model_name</c> → catalog (endpoint/keys), else host <see cref="PageToMovieOptions.DefaultModel"/>.
    /// </summary>
    private async Task<string> ResolveVideoModelAsync(string projectId, CancellationToken ct)
    {
        string? fromCfg = null;
        try
        {
            var cfg = await _projects.GetConfigAsync(projectId, ct).ConfigureAwait(false);
            if (cfg.TryGetValue("model_name", out var el) && el.ValueKind == JsonValueKind.String)
                fromCfg = el.GetString();
        }
        catch
        {
            /* use default */
        }

        var resolved = SupportedModelCatalog.ResolveOrDefault(
            fromCfg,
            ModelCapability.Video,
            fallbackId: _opts.DefaultModel);
        return resolved.Id;
    }

    private static string NormalizeResolution(string? value)
    {
        var v = (value ?? "720p").Trim().ToLowerInvariant();
        return v switch
        {
            "480" or "480p" => "480p",
            "720" or "720p" => "720p",
            "1080" or "1080p" => "1080p",
            _ => v.EndsWith('p') ? v : $"{v}p",
        };
    }

    /// <summary>
    /// Require env keys for the project's selected video model (not a hardcoded XAI_API_KEY message).
    /// MultiProvider IsConfigured is true if either provider has a key — that misdirects Gemini-only setups.
    /// </summary>
    private async Task EnsureVideoProviderConfiguredAsync(string projectId, CancellationToken ct)
    {
        var modelId = await ResolveVideoModelAsync(projectId, ct).ConfigureAwait(false);
        var entry = SupportedModelCatalog.ResolveOrDefault(modelId, ModelCapability.Video);

        // Ambient per-user keys count as configured (personal BYOK or server env via scope).
        var ambient = entry.Provider switch
        {
            ModelProviderFamily.Xai => ApiKeyScope.Current,
            ModelProviderFamily.Google => ApiKeyScope.CurrentGemini,
            ModelProviderFamily.Anthropic => ApiKeyScope.CurrentAnthropic,
            _ => null,
        };
        if (!string.IsNullOrWhiteSpace(ambient))
            return;

        var missing = SupportedModelCatalog.MissingEnvKeys(entry);
        if (missing.Count == 0)
            return;

        var keys = string.Join(" / ", missing);
        throw new InvalidOperationException(
            $"{keys} is not set (required for video model '{entry.Id}' / {entry.ProviderId}). " +
            "Add a personal key in Configuration, or set the server environment variable.");
    }

    /// <summary>
    /// Project-wide spend gate: every cast seed must have an approved voice profile and
    /// (for on-screen roles) a locked ref image before any video generation.
    /// </summary>
    private void EnsureCastReadyForVideo(string projectId)
    {
        var missing = _projects.GetCastNotReadyForVideo(projectId);
        if (missing.Count == 0)
            return;

        var detail = string.Join("; ", missing);
        throw new InvalidOperationException(
            "Cast not ready for video gen — approve voice and locked image for every character first " +
            $"(avoids wasting API spend). Missing: {detail}. " +
            "Open Characters → set voice, then generate + lock a portrait. " +
            "Voice-only roles (e.g. Narrator) need a voice profile only.");
    }

    /// <summary>
    /// Scene-level safety net for on-screen keys mentioned in the blueprint that may not
    /// appear in cast seeds (still require a locked ref if they are not voice-only).
    /// </summary>
    private void EnsureSceneCharactersLocked(string projectId, int sceneNumber)
    {
        var unlocked = _projects.GetUnlockedOnScreenCharacters(projectId, sceneNumber);
        if (unlocked.Count == 0)
            return;

        var names = string.Join(", ", unlocked);
        throw new InvalidOperationException(
            $"Scene {sceneNumber}: locked character refs required before video gen. " +
            $"Missing lock(s): {names}. " +
            "Open Characters → lock a book plate or generate + lock a portrait. " +
            "(Only true voice-only roles skip images.)");
    }
    private async Task ReportStage1ProgressAsync(string line)
    {
        // Single UpdateAsync so Index/Total + log stay atomic (no race losing counters).
        // Keep Total on a 10-step phase scale so single-pass adapt still moves the bar
        // (legacy chunk-only counters left Total=0 → UI stuck at 35%).
        await UpdateAsync(s =>
        {
            if (s.Log.Count == 0 || s.Log[^1] != line)
            {
                s.Log.Add(line);
                if (s.Log.Count > 120)
                    s.Log = s.Log.TakeLast(120).ToList();
            }
            s.Message = line;
            s.Total = Math.Max(s.Total, 10);

            // Multi-chunk adapt: map chunk i/N into phases 4–8
            var m = System.Text.RegularExpressions.Regex.Match(
                line, @"chunk\s+(\d+)\s*/\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success &&
                int.TryParse(m.Groups[1].Value, out var idx) &&
                int.TryParse(m.Groups[2].Value, out var tot) &&
                tot > 0)
            {
                var chunkDone = line.Contains("done", StringComparison.OrdinalIgnoreCase);
                var frac = chunkDone
                    ? Math.Clamp((double)idx / tot, 0, 1)
                    : Math.Clamp((idx - 1.0) / tot, 0, 1);
                s.Index = Math.Max(s.Index, 4 + (int)Math.Round(4.0 * frac));
                return;
            }

            // Vision prepare: page i/N → phases 1–3
            var mVis = System.Text.RegularExpressions.Regex.Match(
                line, @"(?:Grok vision|Reading page|page)\s+(\d+)\s*/\s*(\d+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mVis.Success &&
                int.TryParse(mVis.Groups[1].Value, out var vi) &&
                int.TryParse(mVis.Groups[2].Value, out var vt) &&
                vt > 0)
            {
                var frac = Math.Clamp((vi - 1.0) / vt, 0, 1);
                s.Index = Math.Max(s.Index, 1 + (int)Math.Round(2.0 * frac));
                return;
            }

            if (line.Contains("Screenplay ready", StringComparison.OrdinalIgnoreCase))
                s.Index = Math.Max(s.Index, 10);
            else if (line.Contains("approving", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Fountain draft saved", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("plate", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Attaching", StringComparison.OrdinalIgnoreCase))
                s.Index = Math.Max(s.Index, 9);
            else if (line.Contains("Merge", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Stitch", StringComparison.OrdinalIgnoreCase))
                s.Index = Math.Max(s.Index, 8);
            else if (line.Contains("repair", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("retry", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Refin", StringComparison.OrdinalIgnoreCase))
                s.Index = Math.Max(s.Index, 7);
            else if (line.Contains("single pass", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Adapting book", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Book split", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("multi-chunk", StringComparison.OrdinalIgnoreCase))
                s.Index = Math.Max(s.Index, 4);
            else if (line.Contains("Target runtime", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("building Fountain", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Writing screenplay", StringComparison.OrdinalIgnoreCase))
                s.Index = Math.Max(s.Index, 3);
            else if (line.Contains("prepare", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Extract", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Vision", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("book text", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Checking book", StringComparison.OrdinalIgnoreCase))
                s.Index = Math.Max(s.Index, 1);
            else
                s.Index = Math.Max(s.Index, 1);
        });
        if (_sink is not null)
            await _sink.OnJobLogAsync(line);
    }

    private async Task AppendLogAsync(string message)
    {
        _logLines.Enqueue(message);
        await UpdateAsync(s =>
        {
            // Avoid duplicate consecutive lines (AppendLog after Update that already set Message)
            if (s.Log.Count == 0 || s.Log[^1] != message)
            {
                s.Log.Add(message);
                if (s.Log.Count > 120)
                    s.Log = s.Log.TakeLast(120).ToList();
            }
            s.Message = message;
        });
        if (_sink is not null)
            await _sink.OnJobLogAsync(message);
    }

    private async Task UpdateAsync(Action<JobSnapshot> mutate)
    {
        var run = CurrentRun.Value;
        if (run is null) return;
        await run.SnapLock.WaitAsync();
        try
        {
            mutate(run.Snapshot);
            if (!string.IsNullOrEmpty(run.ActiveJobId))
            {
                _jobs.Update(run.ActiveJobId, rec =>
                {
                    rec.Status = run.Snapshot.Status;
                    rec.Kind = run.Snapshot.Kind;
                    rec.Message = run.Snapshot.Message;
                    rec.ProjectId = run.Snapshot.ProjectId;
                    rec.UserId = run.Snapshot.UserId;
                    rec.CharKey = run.Snapshot.CharKey;
                    rec.Scene = run.Snapshot.Scene;
                    rec.Clip = run.Snapshot.Clip;
                    rec.Index = run.Snapshot.Index;
                    rec.Total = run.Snapshot.Total;
                    rec.Log = run.Snapshot.Log.ToList();
                    rec.Error = run.Snapshot.Error;
                    rec.StartedAt = run.Snapshot.StartedAt;
                    rec.FinishedAt = run.Snapshot.FinishedAt;
                    rec.ClientMediaUrl = run.Snapshot.ClientMediaUrl;
                    rec.ClientRelativePath = run.Snapshot.ClientRelativePath;
                    if (run.Snapshot.JobId is null)
                        run.Snapshot.JobId = rec.JobId;
                });
            }
            await PublishAsync();
        }
        finally
        {
            run.SnapLock.Release();
        }
    }

    private async Task FinishAsync(string status, string message, string? error = null)
    {
        string? projectId = null;
        string? kind = null;
        await UpdateAsync(s =>
        {
            s.Status = status;
            s.Message = message;
            s.Error = error;
            s.FinishedAt = DateTimeOffset.UtcNow;
            if (s.Total > 0 && status == "done")
                s.Index = s.Total;
            projectId = s.ProjectId;
            kind = s.Kind;
        });
        await AppendLogAsync(message);

        // Scene list cache: clip/composite counts change on gen/remux/stage done
        if (status is "done" or "error" or "cancelled")
        {
            if (string.IsNullOrWhiteSpace(projectId))
                projectId = CurrentRun.Value?.Snapshot.ProjectId;
            _projects.InvalidateSceneListCache(projectId);
        }

        // PR4.5b: keep ARTIFACTS.md / artifact_index.json current after pipeline work
        if (status == "done" &&
            !string.IsNullOrWhiteSpace(projectId) &&
            ShouldRefreshArtifactIndex(kind))
        {
            await TryRefreshArtifactIndexAsync(projectId!).ConfigureAwait(false);
        }
    }

    /// <summary>Server MP4 bytes or client-folder marker (.client.json).</summary>
    private static bool ClipPresentOnServerOrClient(string mp4Path) =>
        (File.Exists(mp4Path) && new FileInfo(mp4Path).Length >= 1024) ||
        File.Exists(mp4Path + ".client.json");

    private static bool ShouldRefreshArtifactIndex(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return false;
        return kind is
            "remux" or
            "gen-scene" or
            "gen-batch" or
            "clip-auto-review" or
            "clip-auto-review-batch" or
            "stage2" or
            "character-variants";
    }

    private async Task TryRefreshArtifactIndexAsync(string projectId)
    {
        try
        {
            var doc = await _artifactIndex.RebuildAsync(projectId).ConfigureAwait(false);
            await AppendLogAsync(
                $"  [Artifacts] map updated — readyForManualFinalReview={doc.ReadyForManualFinalReview} " +
                $"(ARTIFACTS.md, artifact_index.json, telemetry snapshots)");
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Artifact index rebuild skipped for {ProjectId}", projectId);
            await AppendLogAsync($"  [Artifacts] map refresh skipped: {ex.Message}");
        }
    }

    private async Task PublishAsync()
    {
        if (_sink is null) return;
        var run = CurrentRun.Value;
        if (run is null) return;
        await _sink.OnJobUpdatedAsync(Clone(run.Snapshot));
    }

    private static JobSnapshot Clone(JobSnapshot s) => new()
    {
        JobId = s.JobId,
        Status = s.Status,
        Kind = s.Kind,
        Message = s.Message,
        ProjectId = s.ProjectId,
        UserId = s.UserId,
        CharKey = s.CharKey,
        Scene = s.Scene,
        Clip = s.Clip,
        Index = s.Index,
        Total = s.Total,
        Log = s.Log.ToList(),
        Error = s.Error,
        QueuedAt = s.QueuedAt,
        StartedAt = s.StartedAt,
        FinishedAt = s.FinishedAt,
    };
}
