using PageToMovie.Core.Models;

namespace PageToMovie.Web.Services;

/// <summary>
/// Scoped active project for nav gating, readiness flags, and a shared load lifecycle
/// (<see cref="IsLoading"/> / <see cref="IsReady"/> / <see cref="EnsureLoadedAsync"/>).
/// </summary>
public sealed class ActiveProjectState
{
    public string? ProjectId { get; private set; }
    public string? Label { get; private set; }
    public string? ParentProjectId { get; private set; }
    public string StudioPath { get; private set; } = ProjectStudioPaths.Full;
    public PageToMovie.Core.Models.AdaptationStatus? Status { get; private set; }

    public bool HasProject => !string.IsNullOrWhiteSpace(ProjectId);
    public bool IsLoading { get; private set; }
    public bool IsReady { get; private set; }
    public string? LoadError { get; private set; }

    public bool IsSimpleVoice => ProjectStudioPaths.IsSimpleVoice(StudioPath);

    public bool CanCharacters { get; private set; }
    public bool CanScenes { get; private set; }
    public bool CanReview { get; private set; }
    public bool CanEstimate { get; private set; }

    public string CharactersBlockedReason { get; private set; } = "Approve the screenplay first";
    public string ScenesBlockedReason { get; private set; } = "Finish the shot plan first";
    public string ReviewBlockedReason { get; private set; } = "Finish the shot plan first";
    public string EstimateBlockedReason { get; private set; } = "Finish importing the book and approve the screenplay first";

    public event Action? Changed;

    public void Set(
        string? projectId,
        string? label = null,
        string? parentProjectId = null,
        string? studioPath = null)
    {
        var id = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim();
        var lbl = string.IsNullOrWhiteSpace(label) ? id : label.Trim();
        var parentId = string.IsNullOrWhiteSpace(parentProjectId) ? null : parentProjectId.Trim();
        var path = ProjectStudioPaths.Normalize(studioPath);
        if (string.Equals(ProjectId, id, StringComparison.Ordinal)
            && string.Equals(Label, lbl, StringComparison.Ordinal)
            && string.Equals(ParentProjectId, parentId, StringComparison.Ordinal)
            && string.Equals(StudioPath, path, StringComparison.Ordinal))
            return;

        var projectChanged = !string.Equals(ProjectId, id, StringComparison.Ordinal);
        ProjectId = id;
        Label = lbl;
        ParentProjectId = parentId;
        StudioPath = path;
        if (id is null)
        {
            ClearReadiness();
            IsLoading = false;
            IsReady = false;
            LoadError = null;
        }
        else if (projectChanged)
        {
            IsReady = false;
            LoadError = null;
            ClearReadiness();
        }
        Changed?.Invoke();
    }

    public void Clear()
    {
        ProjectId = null;
        Label = null;
        ParentProjectId = null;
        StudioPath = ProjectStudioPaths.Full;
        IsLoading = false;
        IsReady = false;
        LoadError = null;
        ClearReadiness();
        Changed?.Invoke();
    }

    public async Task RefreshFromServerAsync(EngineApiClient engine, CancellationToken ct = default)
    {
        try
        {
            await EnsureLoadedAsync(engine, force: true, ct: ct);
        }
        catch
        {
            IsLoading = false;
            Changed?.Invoke();
        }
    }

    public async Task RefreshReadinessAsync(EngineApiClient engine, CancellationToken ct = default)
    {
        if (!HasProject || ProjectId is null)
        {
            ClearReadiness();
            IsReady = false;
            Changed?.Invoke();
            return;
        }

        try
        {
            var status = await engine.GetAdaptationAsync(ProjectId, ct);
            ApplyFromStatusPayload(status);
            IsReady = true;
            LoadError = null;
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }

        Changed?.Invoke();
    }

    private Task? _ensureLoadTask;
    private readonly object _ensureLoadGate = new();

    public async Task EnsureLoadedAsync(EngineApiClient engine, bool force = false, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ProjectId))
        {
            IsLoading = false;
            IsReady = false;
            LoadError = null;
            return;
        }

        if (IsReady && !force && LoadError is null)
            return;

        Task loadTask;
        lock (_ensureLoadGate)
        {
            if (!force && _ensureLoadTask is { IsCompleted: false })
                loadTask = _ensureLoadTask;
            else
                loadTask = _ensureLoadTask = EnsureLoadedCoreAsync(engine);
        }

        // Waiters can cancel waiting without canceling the shared load.
        if (ct.CanBeCanceled)
            await loadTask.WaitAsync(ct).ConfigureAwait(false);
        else
            await loadTask.ConfigureAwait(false);
    }

    private async Task EnsureLoadedCoreAsync(EngineApiClient engine)
    {
        IsLoading = true;
        LoadError = null;
        Changed?.Invoke();
        try
        {
            await RefreshReadinessAsync(engine).ConfigureAwait(false);
            IsReady = true;
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
            IsReady = false;
        }
        finally
        {
            IsLoading = false;
            Changed?.Invoke();
            lock (_ensureLoadGate)
            {
                if (_ensureLoadTask?.IsCompleted == true)
                    _ensureLoadTask = null;
            }
        }
    }

    /// <summary>
    /// Maps the web client's adaptation status DTO into nav gate flags.
    /// Uses JSON so we do not fight Core.Models.AdaptationStatus vs Web DTO twin types.
    /// </summary>
    private void ApplyFromStatusPayload(object? statusObj)
    {
        Status = null;
        if (statusObj is null)
        {
            ClearReadiness();
            return;
        }

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(statusObj);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            static bool PropBool(System.Text.Json.JsonElement el, string camel, string pascal)
            {
                if (el.TryGetProperty(camel, out var v) && v.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
                    return v.GetBoolean();
                if (el.TryGetProperty(pascal, out v) && v.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False)
                    return v.GetBoolean();
                return false;
            }

            static int PropInt(System.Text.Json.JsonElement el, string camel, string pascal)
            {
                if (el.TryGetProperty(camel, out var v) && v.TryGetInt32(out var i)) return i;
                if (el.TryGetProperty(pascal, out v) && v.TryGetInt32(out i)) return i;
                return 0;
            }

            var screenplayReady = false;
            if (root.TryGetProperty("screenplay", out var sp) || root.TryGetProperty("Screenplay", out sp))
            {
                screenplayReady = PropBool(sp, "readyForShots", "ReadyForShots")
                    || PropBool(sp, "signed", "Signed");
            }
            if (root.TryGetProperty("stage1", out var s1) || root.TryGetProperty("Stage1", out s1))
            {
                if (PropBool(s1, "present", "Present") && PropInt(s1, "sceneCount", "SceneCount") > 0)
                    screenplayReady = true;
            }

            var shotsReady = false;
            var stage2Stale = false;
            if (root.TryGetProperty("stage2", out var s2) || root.TryGetProperty("Stage2", out s2))
            {
                shotsReady = PropBool(s2, "stage2Ready", "Stage2Ready")
                    && PropInt(s2, "stage2Clips", "Stage2Clips") > 0;
                stage2Stale = PropBool(s2, "stage2Stale", "Stage2Stale");
            }

            var castReady = true;
            if (root.TryGetProperty("cast", out var ca) || root.TryGetProperty("Cast", out ca))
                castReady = PropBool(ca, "readyForShots", "ReadyForShots");

            CanCharacters = screenplayReady;
            CharactersBlockedReason = screenplayReady ? "" : "Approve the screenplay first";
            CanScenes = shotsReady;
            CanReview = shotsReady;
            CanEstimate = screenplayReady;
            EstimateBlockedReason = screenplayReady
                ? ""
                : "Finish importing the book and approve the screenplay first";

            if (shotsReady)
                ScenesBlockedReason = castReady
                    ? ""
                    : "Approve every character voice + locked image before generating video";
            else if (stage2Stale)
                ScenesBlockedReason = "Update the shot plan first";
            else if (!castReady)
                ScenesBlockedReason = "Finish characters (voice + locked image), then the shot plan";
            else
                ScenesBlockedReason = "Finish the shot plan first";
            ReviewBlockedReason = ScenesBlockedReason;
        }
        catch
        {
            ClearReadiness();
        }
    }

    private void ClearReadiness()
    {
        Status = null;
        CanCharacters = false;
        CanScenes = false;
        CanReview = false;
        CanEstimate = false;
        CharactersBlockedReason = "Approve the screenplay first";
        ScenesBlockedReason = "Finish the shot plan first";
        ReviewBlockedReason = "Finish the shot plan first";
        EstimateBlockedReason = "Finish importing the book and approve the screenplay first";
    }
}
