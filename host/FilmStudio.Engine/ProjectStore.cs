using System.Text.Json;
using FilmStudio.Core.Models;
using FilmStudio.Core.Options;
using Microsoft.Extensions.Options;

namespace FilmStudio.Engine;

public sealed class ProjectStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly FilmStudioOptions _opts;
    private string _activeProjectId = "";

    public ProjectStore(IOptions<FilmStudioOptions> opts)
    {
        _opts = opts.Value;
        var root = ResolveWorkspaceRoot();
        var ws = Path.Combine(root, "projects", "workspace.json");
        if (File.Exists(ws))
        {
            try
            {
                var state = JsonSerializer.Deserialize<WorkspaceState>(File.ReadAllText(ws), JsonOpts);
                _activeProjectId = state?.ActiveProject ?? "";
            }
            catch { /* ignore */ }
        }
    }

    public string WorkspaceRoot => ResolveWorkspaceRoot();

    public string ActiveProjectId =>
        string.IsNullOrWhiteSpace(_activeProjectId)
            ? ListProjects().FirstOrDefault()?.Id ?? ""
            : _activeProjectId;

    public IReadOnlyList<ProjectInfo> ListProjects()
    {
        var projectsDir = Path.Combine(WorkspaceRoot, "projects");
        if (!Directory.Exists(projectsDir))
            return Array.Empty<ProjectInfo>();

        var list = new List<ProjectInfo>();
        foreach (var dir in Directory.GetDirectories(projectsDir))
        {
            var id = Path.GetFileName(dir);
            if (string.Equals(id, "workspace.json", StringComparison.OrdinalIgnoreCase))
                continue;
            var metaPath = Path.Combine(dir, "project.json");
            string? title = null;
            string? label = null;
            if (File.Exists(metaPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
                    if (doc.RootElement.TryGetProperty("title", out var t))
                        title = t.GetString();
                    if (doc.RootElement.TryGetProperty("label", out var l))
                        label = l.GetString();
                }
                catch { /* ignore */ }
            }
            list.Add(new ProjectInfo
            {
                Id = id,
                Title = title,
                Label = label ?? title ?? id,
                Path = dir,
            });
        }
        return list.OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public ProjectInfo? GetProject(string projectId)
    {
        return ListProjects().FirstOrDefault(p =>
            string.Equals(p.Id, projectId, StringComparison.OrdinalIgnoreCase));
    }

    public ProjectInfo Activate(string projectId)
    {
        var p = GetProject(projectId)
            ?? throw new InvalidOperationException($"Unknown project: {projectId}");
        _activeProjectId = p.Id;
        var wsPath = Path.Combine(WorkspaceRoot, "projects", "workspace.json");
        Directory.CreateDirectory(Path.GetDirectoryName(wsPath)!);
        File.WriteAllText(
            wsPath,
            JsonSerializer.Serialize(new WorkspaceState { ActiveProject = p.Id }, JsonOpts));
        return p;
    }

    public string GetProjectDir(string projectId)
    {
        var p = GetProject(projectId)
            ?? throw new InvalidOperationException($"Unknown project: {projectId}");
        return p.Path;
    }

    public string? FindBlueprintPath(string projectId)
    {
        var dir = GetProjectDir(projectId);
        var configPath = Path.Combine(dir, "pipeline_config.json");
        var name = "blueprint.clips.grok.json";
        if (File.Exists(configPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                if (doc.RootElement.TryGetProperty("blueprint_file", out var bf))
                {
                    var n = bf.GetString();
                    if (!string.IsNullOrWhiteSpace(n))
                        name = n;
                }
            }
            catch { /* ignore */ }
        }
        foreach (var candidate in new[]
                 {
                     name,
                     "blueprint.clips.grok.json",
                     "nickandme.clips.grok.json",
                 })
        {
            var full = Path.Combine(dir, candidate);
            if (File.Exists(full))
                return full;
        }
        return null;
    }

    public JsonDocument? LoadBlueprint(string projectId)
    {
        var path = FindBlueprintPath(projectId);
        if (path is null)
            return null;
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    public string ConfigPath(string projectId) =>
        Path.Combine(GetProjectDir(projectId), "pipeline_config.json");

    public Dictionary<string, JsonElement> GetConfig(string projectId)
    {
        var path = ConfigPath(projectId);
        if (!File.Exists(path))
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var dict = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in doc.RootElement.EnumerateObject())
            dict[p.Name] = p.Value.Clone();
        return dict;
    }

    public Dictionary<string, JsonElement> SaveConfig(string projectId, JsonElement updates)
    {
        var path = ConfigPath(projectId);
        Dictionary<string, object?> merged = new(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(path))
        {
            using var existing = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var p in existing.RootElement.EnumerateObject())
                merged[p.Name] = JsonSerializer.Deserialize<object>(p.Value.GetRawText());
        }

        if (updates.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in updates.EnumerateObject())
                merged[p.Name] = JsonSerializer.Deserialize<object>(p.Value.GetRawText());
        }

        var json = JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json + "\n");
        return GetConfig(projectId);
    }

    /// <summary>
    /// Character seeds from blueprint, falling back to Stage 1 scenes.json.
    /// </summary>
    public IReadOnlyList<CharacterSummary> ListCharacters(string projectId)
    {
        var seeds = LoadCharacterSeeds(projectId);
        var projectDir = GetProjectDir(projectId);
        var rows = new List<CharacterSummary>();
        foreach (var (key, info) in seeds)
        {
            var voiceOnly = IsVoiceOnly(key, info);
            var display = info.TryGetProperty("canonical_given_name", out var cn) &&
                          cn.GetString() is { Length: > 0 } cname
                ? cname
                : (info.TryGetProperty("voice_label", out var vl) && vl.GetString() is { Length: > 0 } lab
                    ? lab
                    : key.Replace("Character_", "").Replace("_", " "));

            var refName = GuessRefFileName(key, info);
            var refPath = Path.Combine(projectDir, "assets", "characters", refName);
            var hasRef = !voiceOnly && File.Exists(refPath);

            var bookRefs = new List<string>();
            if (info.TryGetProperty("design_reference_images", out var dri) &&
                dri.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in dri.EnumerateArray())
                {
                    var s = x.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        bookRefs.Add(s!);
                }
            }

            var wardrobe = new List<string>();
            if (info.TryGetProperty("wardrobe_always", out var wa) &&
                wa.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in wa.EnumerateArray())
                {
                    var s = x.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        wardrobe.Add(s!);
                }
            }

            rows.Add(new CharacterSummary
            {
                Key = key,
                DisplayName = display,
                Description = info.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
                VisualLock = info.TryGetProperty("visual_lock", out var v) ? v.GetString() ?? "" : "",
                VoiceProfile = info.TryGetProperty("voice_profile", out var vp) ? vp.GetString() ?? "" : "",
                VoiceLabel = info.TryGetProperty("voice_label", out var vlab) ? vlab.GetString() ?? "" : "",
                VoiceOnly = voiceOnly,
                Locked = voiceOnly
                    ? !string.IsNullOrWhiteSpace(
                        info.TryGetProperty("voice_profile", out var vpr) ? vpr.GetString() : null)
                    : hasRef,
                RefFileName = hasRef ? refName : null,
                RefUrl = hasRef
                    ? $"/api/projects/{Uri.EscapeDataString(projectId)}/characters/{Uri.EscapeDataString(key)}/ref"
                    : null,
                WardrobeAlways = wardrobe,
                DesignReferenceImages = bookRefs,
                AgeBand = info.TryGetProperty("age_band", out var ab) ? ab.GetString() : null,
            });
        }

        return rows
            .OrderBy(r => r.Key.EndsWith("_Young") ? 1 : r.Key.EndsWith("_Teen") ? 2 : 0)
            .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string? ResolveCharacterRefPath(string projectId, string charKey)
    {
        var seeds = LoadCharacterSeeds(projectId);
        if (!seeds.TryGetValue(charKey, out var info))
            return null;
        if (IsVoiceOnly(charKey, info))
            return null;
        var refName = GuessRefFileName(charKey, info);
        var full = Path.Combine(GetProjectDir(projectId), "assets", "characters", refName);
        return File.Exists(full) ? full : null;
    }

    /// <summary>Light scene list from Stage 2 blueprint + on-disk clip counts.</summary>
    public IReadOnlyList<SceneSummary> ListScenes(string projectId)
    {
        using var bp = LoadBlueprint(projectId);
        if (bp is null ||
            !bp.RootElement.TryGetProperty("scenes", out var scenesEl) ||
            scenesEl.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SceneSummary>();
        }

        var projectDir = GetProjectDir(projectId);
        var videoDir = Path.Combine(projectDir, "assets", "video");
        var scenesDir = Path.Combine(projectDir, "assets", "scenes");
        var videoIndex = IndexDirFiles(videoDir);
        var scenesIndex = IndexDirFiles(scenesDir);

        var rows = new List<SceneSummary>();
        foreach (var s in scenesEl.EnumerateArray())
        {
            if (!s.TryGetProperty("scene_number", out var snEl) || !snEl.TryGetInt32(out var sn))
                continue;

            var clips = s.TryGetProperty("veo_clips", out var vc) && vc.ValueKind == JsonValueKind.Array
                ? vc.EnumerateArray().ToList()
                : new List<JsonElement>();
            var nClips = clips.Count;
            var onDisk = 0;
            foreach (var c in clips)
            {
                var cn = c.TryGetProperty("clip_number", out var cnEl) && cnEl.TryGetInt32(out var n) ? n : 0;
                if (cn <= 0) continue;
                if (ClipOnDisk(videoIndex, sn, cn))
                    onDisk++;
            }

            var compositeName = $"scene_{sn:D2}_complete.mp4";
            var compositeOk =
                scenesIndex.TryGetValue(compositeName, out var csz) && csz >= 1024 ||
                videoIndex.TryGetValue(compositeName, out var vsz) && vsz >= 1024;

            double? dur = null;
            if (s.TryGetProperty("total_estimated_duration_seconds", out var dEl))
            {
                if (dEl.TryGetDouble(out var dd)) dur = dd;
                else if (dEl.TryGetInt32(out var di)) dur = di;
            }

            var chars = new List<string>();
            if (s.TryGetProperty("characters_on_screen", out var cos) && cos.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in cos.EnumerateArray())
                {
                    var name = x.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        chars.Add(name!);
                }
            }

            var complete = nClips > 0 && onDisk >= nClips;
            var status = nClips == 0 || onDisk == 0
                ? "empty"
                : complete ? "complete" : "partial";

            rows.Add(new SceneSummary
            {
                SceneNumber = sn,
                Setting = s.TryGetProperty("setting", out var set) ? set.GetString() ?? "" : "",
                ClipCount = nClips,
                ClipsOnDisk = onDisk,
                ClipsComplete = complete,
                DurationSeconds = dur,
                CompositeExists = compositeOk,
                CharactersOnScreen = chars,
                Status = status,
            });
        }

        return rows.OrderBy(r => r.SceneNumber).ToList();
    }

    public SceneDetail? GetSceneDetail(string projectId, int sceneNumber)
    {
        using var bp = LoadBlueprint(projectId);
        if (bp is null)
            return null;

        JsonElement? sceneEl = null;
        if (bp.RootElement.TryGetProperty("scenes", out var scenesEl) &&
            scenesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in scenesEl.EnumerateArray())
            {
                if (s.TryGetProperty("scene_number", out var snEl) &&
                    snEl.TryGetInt32(out var sn) &&
                    sn == sceneNumber)
                {
                    sceneEl = s.Clone();
                    break;
                }
            }
        }

        if (sceneEl is null)
            return null;

        var sEl = sceneEl.Value;
        var projectDir = GetProjectDir(projectId);
        var videoDir = Path.Combine(projectDir, "assets", "video");
        var scenesDir = Path.Combine(projectDir, "assets", "scenes");
        var videoIndex = IndexDirFiles(videoDir);
        var scenesIndex = IndexDirFiles(scenesDir);

        var clips = new List<ClipSummary>();
        if (sEl.TryGetProperty("veo_clips", out var vc) && vc.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in vc.EnumerateArray())
            {
                var cn = c.TryGetProperty("clip_number", out var cnEl) && cnEl.TryGetInt32(out var n) ? n : 0;
                if (cn <= 0) continue;

                var fileName = $"scene_{sceneNumber:D2}_clip_{cn:D2}.mp4";
                var onDisk = ClipOnDisk(videoIndex, sceneNumber, cn);
                long size = 0;
                if (onDisk && videoIndex.TryGetValue(fileName, out var sz))
                    size = sz;

                var dialogue = "";
                string? speaker = null;
                string? delivery = null;
                if (c.TryGetProperty("audio_payload", out var ap) && ap.ValueKind == JsonValueKind.Object)
                {
                    if (ap.TryGetProperty("dialogue", out var d))
                        dialogue = d.GetString() ?? "";
                    if (ap.TryGetProperty("speaker", out var sp))
                        speaker = sp.GetString();
                    if (ap.TryGetProperty("delivery", out var del))
                        delivery = del.GetString();
                }

                var dur = 0;
                if (c.TryGetProperty("duration_seconds", out var dEl) && dEl.TryGetInt32(out var ds))
                    dur = ds;

                clips.Add(new ClipSummary
                {
                    ClipNumber = cn,
                    Timestamp = c.TryGetProperty("timestamp", out var ts) ? ts.GetString() ?? "" : "",
                    DurationSeconds = dur,
                    Continuation = c.TryGetProperty("veo_continuation_source", out var cont)
                        ? cont.GetString() ?? "none"
                        : "none",
                    PrimarySubject = c.TryGetProperty("primary_subject", out var ps)
                        ? ps.GetString() ?? ""
                        : "",
                    VisualPrompt = c.TryGetProperty("visual_prompt", out var vp) ? vp.GetString() ?? "" : "",
                    NegativePrompt = c.TryGetProperty("negative_prompt", out var np) ? np.GetString() ?? "" : "",
                    Dialogue = dialogue,
                    Speaker = speaker,
                    Delivery = delivery,
                    OnDisk = onDisk,
                    SizeBytes = size,
                    FileName = onDisk ? fileName : null,
                    VideoUrl = onDisk
                        ? $"/api/projects/{Uri.EscapeDataString(projectId)}/scenes/{sceneNumber}/clips/{cn}/video"
                        : null,
                });
            }
        }

        clips = clips.OrderBy(c => c.ClipNumber).ToList();
        var onDiskCount = clips.Count(c => c.OnDisk);

        var compositeName = $"scene_{sceneNumber:D2}_complete.mp4";
        var compositeOk =
            scenesIndex.TryGetValue(compositeName, out var csz) && csz >= 1024 ||
            videoIndex.TryGetValue(compositeName, out var vsz) && vsz >= 1024;

        double? durTotal = null;
        if (sEl.TryGetProperty("total_estimated_duration_seconds", out var td))
        {
            if (td.TryGetDouble(out var dd)) durTotal = dd;
            else if (td.TryGetInt32(out var di)) durTotal = di;
        }

        var chars = new List<string>();
        if (sEl.TryGetProperty("characters_on_screen", out var cos) && cos.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in cos.EnumerateArray())
            {
                var name = x.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    chars.Add(name!);
            }
        }

        var locs = new List<string>();
        if (sEl.TryGetProperty("location_ids", out var lids) && lids.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in lids.EnumerateArray())
            {
                var name = x.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    locs.Add(name!);
            }
        }

        return new SceneDetail
        {
            SceneNumber = sceneNumber,
            Setting = sEl.TryGetProperty("setting", out var set) ? set.GetString() ?? "" : "",
            DurationSeconds = durTotal,
            ClipCount = clips.Count,
            ClipsOnDisk = onDiskCount,
            CompositeExists = compositeOk,
            CompositeUrl = compositeOk
                ? $"/api/projects/{Uri.EscapeDataString(projectId)}/scenes/{sceneNumber}/composite"
                : null,
            CharactersOnScreen = chars,
            LocationIds = locs,
            PrimaryLocationId = sEl.TryGetProperty("primary_location_id", out var pl)
                ? pl.GetString()
                : null,
            Clips = clips,
        };
    }

    public string? ResolveClipVideoPath(string projectId, int sceneNumber, int clipNumber)
    {
        var path = Path.Combine(
            GetProjectDir(projectId),
            "assets",
            "video",
            $"scene_{sceneNumber:D2}_clip_{clipNumber:D2}.mp4");
        return File.Exists(path) && new FileInfo(path).Length >= 1024 ? path : null;
    }

    public string? ResolveCompositePath(string projectId, int sceneNumber)
    {
        var dir = GetProjectDir(projectId);
        foreach (var candidate in new[]
                 {
                     Path.Combine(dir, "assets", "scenes", $"scene_{sceneNumber:D2}_complete.mp4"),
                     Path.Combine(dir, "assets", "video", $"scene_{sceneNumber:D2}_complete.mp4"),
                 })
        {
            if (File.Exists(candidate) && new FileInfo(candidate).Length >= 1024)
                return candidate;
        }
        return null;
    }

    private static Dictionary<string, long> IndexDirFiles(string dir)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(dir))
            return map;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                try
                {
                    var info = new FileInfo(f);
                    map[info.Name] = info.Length;
                }
                catch { /* skip */ }
            }
        }
        catch { /* skip */ }
        return map;
    }

    private static bool ClipOnDisk(Dictionary<string, long> videoIndex, int scene, int clip)
    {
        var name = $"scene_{scene:D2}_clip_{clip:D2}.mp4";
        return videoIndex.TryGetValue(name, out var sz) && sz >= 1024;
    }

    private Dictionary<string, JsonElement> LoadCharacterSeeds(string projectId)
    {
        // Prefer blueprint, then scenes.json
        try
        {
            using var bp = LoadBlueprint(projectId);
            if (bp is not null &&
                bp.RootElement.TryGetProperty("global_production_variables", out var gpv) &&
                gpv.TryGetProperty("character_seed_tokens", out var seeds) &&
                seeds.ValueKind == JsonValueKind.Object)
            {
                var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var p in seeds.EnumerateObject())
                    dict[p.Name] = p.Value.Clone();
                if (dict.Count > 0)
                    return dict;
            }
        }
        catch { /* fall through */ }

        var scenesPath = Path.Combine(GetProjectDir(projectId), "scenes.json");
        var alt = Path.Combine(GetProjectDir(projectId), "nickandme.scenes.json");
        var path = File.Exists(scenesPath) ? scenesPath : (File.Exists(alt) ? alt : null);
        if (path is null)
            return new Dictionary<string, JsonElement>();

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.TryGetProperty("global_production_variables", out var g2) &&
            g2.TryGetProperty("character_seed_tokens", out var s2) &&
            s2.ValueKind == JsonValueKind.Object)
        {
            var dict = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var p in s2.EnumerateObject())
                dict[p.Name] = p.Value.Clone();
            return dict;
        }
        return new Dictionary<string, JsonElement>();
    }

    private static bool IsVoiceOnly(string key, JsonElement info)
    {
        if (key.Contains("Narrator", StringComparison.OrdinalIgnoreCase))
            return true;
        if (info.TryGetProperty("display_name_policy", out var pol))
        {
            var p = pol.GetString() ?? "";
            if (p.Contains("never", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static string GuessRefFileName(string key, JsonElement info)
    {
        if (info.TryGetProperty("reference_image_placeholder", out var ph))
        {
            var name = Path.GetFileName(ph.GetString() ?? "");
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        // Character_Buster → buster_ref.png
        var shortName = key.Replace("Character_", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        return $"{shortName}_ref.png";
    }

    private string ResolveWorkspaceRoot()
    {
        if (!string.IsNullOrWhiteSpace(_opts.WorkspaceRoot) &&
            Directory.Exists(_opts.WorkspaceRoot))
        {
            return Path.GetFullPath(_opts.WorkspaceRoot);
        }

        // host/FilmStudio.Engine → host → repo
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "projects")) &&
                Directory.Exists(Path.Combine(dir.FullName, "renderer")))
            {
                return dir.FullName;
            }
            // running from host/FilmStudio.Api/bin/...
            if (dir.Name.Equals("host", StringComparison.OrdinalIgnoreCase) &&
                dir.Parent is not null &&
                Directory.Exists(Path.Combine(dir.Parent.FullName, "projects")))
            {
                return dir.Parent.FullName;
            }
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }
}
