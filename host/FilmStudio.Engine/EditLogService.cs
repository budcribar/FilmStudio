using System.Text.Json;
using FilmStudio.Core.Models;
using Microsoft.Extensions.Logging;

namespace FilmStudio.Engine;

/// <summary>Project edit/feedback log (edit_feedback_log.json) + clip/scene review state.</summary>
public sealed class EditLogService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly ProjectStore _projects;
    private readonly ILogger<EditLogService> _log;

    public EditLogService(ProjectStore projects, ILogger<EditLogService> log)
    {
        _projects = projects;
        _log = log;
    }

    public EditLogDocument Load(string projectId) =>
        LoadAsync(projectId).GetAwaiter().GetResult();

    public async Task<EditLogDocument> LoadAsync(string projectId, CancellationToken ct = default)
    {
        var path = await LogPathAsync(projectId, ct).ConfigureAwait(false);
        if (!File.Exists(path))
            return new EditLogDocument();
        try
        {
            await using var stream = File.OpenRead(path);
            var doc = await JsonSerializer.DeserializeAsync<EditLogDocument>(stream, JsonOpts, ct)
                .ConfigureAwait(false);
            return doc ?? new EditLogDocument();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to load edit log for {Project}", projectId);
            return new EditLogDocument();
        }
    }

    public void Save(string projectId, EditLogDocument doc) =>
        SaveAsync(projectId, doc).GetAwaiter().GetResult();

    public async Task SaveAsync(string projectId, EditLogDocument doc, CancellationToken ct = default)
    {
        var path = await LogPathAsync(projectId, ct).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        var json = JsonSerializer.Serialize(doc, JsonOpts) + "\n";
        await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
        File.Move(tmp, path, overwrite: true);
    }

    public EditLogEntry Add(
        string projectId,
        string entryType,
        string userNote,
        int? scene = null,
        int? clip = null,
        string? character = null,
        string actionTaken = "",
        string before = "",
        string after = "",
        string learningLayer = "clip") =>
        AddAsync(projectId, entryType, userNote, scene, clip, character, actionTaken, before, after, learningLayer)
            .GetAwaiter().GetResult();

    public async Task<EditLogEntry> AddAsync(
        string projectId,
        string entryType,
        string userNote,
        int? scene = null,
        int? clip = null,
        string? character = null,
        string actionTaken = "",
        string before = "",
        string after = "",
        string learningLayer = "clip",
        CancellationToken ct = default)
    {
        var doc = await LoadAsync(projectId, ct).ConfigureAwait(false);
        var entry = new EditLogEntry
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Ts = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            Type = entryType,
            LearningLayer = learningLayer,
            Scene = scene,
            Clip = clip,
            Character = character,
            UserNote = (userNote ?? "").Trim(),
            ActionTaken = actionTaken,
            Before = before,
            After = after,
            SuggestedRule = SuggestRule(entryType, userNote, scene, clip, character),
        };
        doc.Entries.Insert(0, entry);
        await SaveAsync(projectId, doc, ct).ConfigureAwait(false);
        return entry;
    }

    public EditLogEntry? Get(string projectId, string entryId) =>
        GetAsync(projectId, entryId).GetAwaiter().GetResult();

    public async Task<EditLogEntry?> GetAsync(
        string projectId,
        string entryId,
        CancellationToken ct = default)
    {
        var doc = await LoadAsync(projectId, ct).ConfigureAwait(false);
        return doc.Entries.FirstOrDefault(e =>
            string.Equals(e.Id, entryId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Pass/fail clip review status in pipeline_state.json + edit log.</summary>
    public void SetClipReview(
        string projectId,
        int scene,
        int clip,
        string status,
        string note = "") =>
        SetClipReviewAsync(projectId, scene, clip, status, note).GetAwaiter().GetResult();

    public async Task SetClipReviewAsync(
        string projectId,
        int scene,
        int clip,
        string status,
        string note = "",
        CancellationToken ct = default)
    {
        status = status.Trim().ToLowerInvariant();
        if (status is not ("pass" or "fail" or "pending"))
            throw new InvalidOperationException("status must be pass|fail|pending");

        var dir = await _projects.GetProjectDirAsync(projectId, ct).ConfigureAwait(false);
        var statePath = Path.Combine(dir, "pipeline_state.json");
        var state = await LoadStateAsync(statePath, ct).ConfigureAwait(false);
        var key = $"S{scene:D2}C{clip:D2}";
        if (!state.TryGetValue("clip_review", out var cr) || cr is not Dictionary<string, object?> reviews)
        {
            reviews = new Dictionary<string, object?>();
            state["clip_review"] = reviews;
        }
        reviews[key] = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["note"] = note ?? "",
            ["ts"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
        };
        await SaveStateAsync(statePath, state, ct).ConfigureAwait(false);

        await AddAsync(
            projectId,
            status == "pass" ? "clip_pass" : status == "fail" ? "clip_fail" : "clip_review",
            note is { Length: > 0 } ? note : status,
            scene: scene,
            clip: clip,
            actionTaken: $"review_status={status}",
            ct: ct).ConfigureAwait(false);
    }

    public void MarkSceneApproved(string projectId, int scene, string note = "") =>
        MarkSceneApprovedAsync(projectId, scene, note).GetAwaiter().GetResult();

    public async Task MarkSceneApprovedAsync(
        string projectId,
        int scene,
        string note = "",
        CancellationToken ct = default)
    {
        var dir = await _projects.GetProjectDirAsync(projectId, ct).ConfigureAwait(false);
        var statePath = Path.Combine(dir, "pipeline_state.json");
        var state = await LoadStateAsync(statePath, ct).ConfigureAwait(false);
        if (!state.TryGetValue("scene_review", out var sr) || sr is not Dictionary<string, object?> scenes)
        {
            scenes = new Dictionary<string, object?>();
            state["scene_review"] = scenes;
        }
        scenes[$"S{scene:D2}"] = new Dictionary<string, object?>
        {
            ["status"] = "approved",
            ["note"] = note ?? "",
            ["ts"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
        };
        await SaveStateAsync(statePath, state, ct).ConfigureAwait(false);
        await AddAsync(
            projectId,
            "scene_approve",
            note is { Length: > 0 } ? note : "Approved",
            scene: scene,
            actionTaken: "scene_review=approved",
            ct: ct).ConfigureAwait(false);
    }

    public void MarkSceneDirty(string projectId, int scene, string reason, string layer = "stage2") =>
        MarkSceneDirtyAsync(projectId, scene, reason, layer).GetAwaiter().GetResult();

    public async Task MarkSceneDirtyAsync(
        string projectId,
        int scene,
        string reason,
        string layer = "stage2",
        CancellationToken ct = default)
    {
        var dir = await _projects.GetProjectDirAsync(projectId, ct).ConfigureAwait(false);
        var statePath = Path.Combine(dir, "pipeline_state.json");
        var state = await LoadStateAsync(statePath, ct).ConfigureAwait(false);
        if (!state.TryGetValue("dirty_scenes", out var ds) || ds is not Dictionary<string, object?> dirty)
        {
            dirty = new Dictionary<string, object?>();
            state["dirty_scenes"] = dirty;
        }
        dirty[$"S{scene:D2}"] = new Dictionary<string, object?>
        {
            ["reason"] = reason,
            ["layer"] = layer,
            ["ts"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
        };
        await SaveStateAsync(statePath, state, ct).ConfigureAwait(false);
        await AddAsync(
            projectId,
            "scene_dirty",
            reason,
            scene: scene,
            actionTaken: $"dirty layer={layer}",
            learningLayer: layer,
            ct: ct).ConfigureAwait(false);
    }

    public Dictionary<string, string> GetClipReviewMap(string projectId) =>
        GetClipReviewMapAsync(projectId).GetAwaiter().GetResult();

    public async Task<Dictionary<string, string>> GetClipReviewMapAsync(
        string projectId,
        CancellationToken ct = default)
    {
        var dir = await _projects.GetProjectDirAsync(projectId, ct).ConfigureAwait(false);
        var statePath = Path.Combine(dir, "pipeline_state.json");
        var state = await LoadStateAsync(statePath, ct).ConfigureAwait(false);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (state.TryGetValue("clip_review", out var cr) && cr is Dictionary<string, object?> reviews)
        {
            foreach (var (k, v) in reviews)
            {
                if (v is Dictionary<string, object?> row &&
                    row.TryGetValue("status", out var st) && st is not null)
                    map[k] = st.ToString() ?? "";
            }
        }
        return map;
    }

    private async Task<string> LogPathAsync(string projectId, CancellationToken ct)
    {
        var dir = await _projects.GetProjectDirAsync(projectId, ct).ConfigureAwait(false);
        return Path.Combine(dir, "edit_feedback_log.json");
    }

    private static async Task<Dictionary<string, object?>> LoadStateAsync(
        string path,
        CancellationToken ct)
    {
        if (!File.Exists(path)) return new();
        try
        {
            var text = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return GrokChatClient.ParseJsonObject(text);
        }
        catch { return new(); }
    }

    private static async Task SaveStateAsync(
        string path,
        Dictionary<string, object?> state,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(state, JsonOpts) + "\n";
        await File.WriteAllTextAsync(path, json, ct).ConfigureAwait(false);
    }

    private static string SuggestRule(
        string entryType,
        string? note,
        int? scene,
        int? clip,
        string? character)
    {
        var loc = scene is int s
            ? (clip is int c ? $"S{s:D2}C{c}" : $"S{s:D2}")
            : character is { Length: > 0 } ? $"character {character}" : "general";
        if (entryType.Contains("fail", StringComparison.OrdinalIgnoreCase))
            return $"Review fail at {loc}: {(note ?? "").Trim()}".Trim();
        if (entryType.Contains("pass", StringComparison.OrdinalIgnoreCase))
            return $"Review pass at {loc}";
        return $"Note at {loc}: {(note ?? "").Trim()}".Trim();
    }
}
