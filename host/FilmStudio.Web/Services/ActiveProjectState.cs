using FilmStudio.Core.Models;

namespace FilmStudio.Web.Services;

/// <summary>
/// Circuit-scoped active project for nav gating (hide Adaptation/Scenes/etc. until chosen).
/// Also tracks which workflow steps are available for the active project.
/// </summary>
public sealed class ActiveProjectState
{
    public string? ProjectId { get; private set; }
    public string? Label { get; private set; }

    public bool HasProject => !string.IsNullOrWhiteSpace(ProjectId);

    /// <summary>Screenplay approved — Characters / cast work makes sense.</summary>
    public bool CanCharacters { get; private set; }

    /// <summary>Shot plan present — Scenes / clip gen available.</summary>
    public bool CanScenes { get; private set; }

    /// <summary>Same as CanScenes for review of generated clips.</summary>
    public bool CanReview { get; private set; }

    /// <summary>Operator hint when a nav item is disabled (short, no jargon).</summary>
    public string CharactersBlockedReason { get; private set; } = "Approve the screenplay first";
    public string ScenesBlockedReason { get; private set; } = "Finish the shot plan first";
    public string ReviewBlockedReason { get; private set; } = "Finish the shot plan first";

    public event Action? Changed;

    public void Set(string? projectId, string? label = null)
    {
        var id = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim();
        var lbl = string.IsNullOrWhiteSpace(label) ? id : label.Trim();
        if (string.Equals(ProjectId, id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Label, lbl, StringComparison.Ordinal))
            return;

        var projectChanged = !string.Equals(ProjectId, id, StringComparison.OrdinalIgnoreCase);
        ProjectId = id;
        Label = lbl;
        // Until RefreshReadinessAsync runs, assume blocked so nav stays greyed
        if (projectChanged)
            ClearReadiness();
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (ProjectId is null && Label is null) return;
        ProjectId = null;
        Label = null;
        ClearReadiness();
        Changed?.Invoke();
    }

    /// <summary>Load active project from the API (page load / after create).</summary>
    public async Task RefreshFromApiAsync(EngineApiClient engine, CancellationToken ct = default)
    {
        try
        {
            var projs = await engine.GetProjectsAsync(ct);
            var active = projs?.Active;
            if (active?.Id is { Length: > 0 } aid)
                Set(aid, active.Label ?? active.Title ?? aid);
            else if (projs?.Projects is { Count: > 0 })
            {
                // Prefer explicit active; if none, do not invent — user must pick on Studio
                Clear();
            }
            else
            {
                Clear();
            }

            await RefreshReadinessAsync(engine, ct);
        }
        catch
        {
            // API down — leave prior state (or empty)
        }
    }

    /// <summary>Recompute which workflow nav items are available for the active project.</summary>
    public async Task RefreshReadinessAsync(EngineApiClient engine, CancellationToken ct = default)
    {
        if (!HasProject || ProjectId is null)
        {
            if (CanCharacters || CanScenes || CanReview)
            {
                ClearReadiness();
                Changed?.Invoke();
            }
            return;
        }

        try
        {
            var dto = await engine.GetAdaptationAsync(ProjectId, ct);
            if (ApplyAdaptation(dto?.Adaptation))
                Changed?.Invoke();
        }
        catch
        {
            // Keep last known readiness if API blips
        }
    }

    /// <returns>True if any gate flag or reason text changed.</returns>
    private bool ApplyAdaptation(AdaptationStatus? a)
    {
        if (a is null)
        {
            if (!CanCharacters && !CanScenes && !CanReview)
                return false;
            ClearReadiness();
            return true;
        }

        // Characters: signed / ready screenplay (or legacy stage1)
        var screenplayReady = a.Screenplay.ReadyForShots || a.Screenplay.Signed ||
                              (a.Stage1.Present && a.Stage1.SceneCount > 0);
        var charactersReason = screenplayReady ? "" : "Approve the screenplay first";

        // Scenes / Review: shot plan with clips
        var shotsReady = a.Stage2.Stage2Ready && a.Stage2.Stage2Clips > 0;
        var shotsHint = a.Stage2.Stage2Stale
            ? "Update the shot plan first"
            : "Finish the shot plan first";
        var scenesReason = shotsReady ? "" : shotsHint;

        var changed =
            CanCharacters != screenplayReady ||
            CanScenes != shotsReady ||
            CanReview != shotsReady ||
            !string.Equals(CharactersBlockedReason, charactersReason, StringComparison.Ordinal) ||
            !string.Equals(ScenesBlockedReason, scenesReason, StringComparison.Ordinal) ||
            !string.Equals(ReviewBlockedReason, scenesReason, StringComparison.Ordinal);

        CanCharacters = screenplayReady;
        CharactersBlockedReason = charactersReason;
        CanScenes = shotsReady;
        CanReview = shotsReady;
        ScenesBlockedReason = scenesReason;
        ReviewBlockedReason = scenesReason;
        return changed;
    }

    private void ClearReadiness()
    {
        CanCharacters = false;
        CanScenes = false;
        CanReview = false;
        CharactersBlockedReason = "Approve the screenplay first";
        ScenesBlockedReason = "Finish the shot plan first";
        ReviewBlockedReason = "Finish the shot plan first";
    }
}
