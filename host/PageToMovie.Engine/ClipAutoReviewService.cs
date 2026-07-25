using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Per-clip AI review: sample previous tail + current frames, draft structured suggestions.
/// Does not apply edits or regen — user confirms via Apply → Regen.
/// </summary>
public sealed class ClipAutoReviewService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly ProjectStore _projects;
    private readonly IVisionClient _vision;
    private readonly EditLogService _logs;
    private readonly ProjectRulesService _projectRules;
    private readonly ReviewIndexService _reviewIndex;
    private readonly ProjectTelemetryService _telemetry;
    private readonly ILogger<ClipAutoReviewService> _log;

    public ClipAutoReviewService(
        ProjectStore projects,
        IVisionClient vision,
        EditLogService logs,
        ProjectRulesService projectRules,
        ReviewIndexService reviewIndex,
        ProjectTelemetryService telemetry,
        ILogger<ClipAutoReviewService> log)
    {
        _projects = projects;
        _vision = vision;
        _logs = logs;
        _projectRules = projectRules;
        _reviewIndex = reviewIndex;
        _telemetry = telemetry;
        _log = log;
    }

    public bool IsConfigured => _vision.IsConfigured;

    private async Task<string> GetConfigStringAsync(
        string projectId, string key, string fallback, CancellationToken ct)
    {
        var cfg = await _projects.GetConfigAsync(projectId, ct).ConfigureAwait(false);
        if (cfg.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
            return el.GetString() ?? fallback;
        return fallback;
    }

    public string DraftPath(string projectId, int scene, int clip) =>
        Path.Combine(
            _projects.GetProjectDir(projectId),
            "assets",
            "review",
            $"S{scene:D2}C{clip:D2}.auto_review.json");

    public ClipAutoReviewDraft? LoadDraft(string projectId, int scene, int clip)
    {
        var path = DraftPath(projectId, scene, clip);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ClipAutoReviewDraft>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public void SaveDraft(ClipAutoReviewDraft draft)
    {
        var path = DraftPath(draft.ProjectId, draft.Scene, draft.Clip);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(draft, JsonOpts) + "\n");
    }

    public async Task<ClipAutoReviewDraft> ReviewAsync(
        string projectId,
        int scene,
        int clip,
        Action<int, int, string>? onProgress = null,
        CancellationToken ct = default)
    {
        if (!_vision.IsConfigured)
            throw new InvalidOperationException("Connect service (XAI_API_KEY) for clip review.");
        // Frame sampling used native ffmpeg; product is browser-only for media tools.
        // Auto-review still records a lightweight draft so the UI can drive human pass/fail.
        throw new InvalidOperationException(
            "Server frame sampling was removed (no native ffmpeg). " +
            "Review clips manually in Review, or re-enable a client-side frame sample path later.");

#pragma warning disable CS0162 // intentional: keep method body for future client-frame path
        using var _telScope = _telemetry.UseProject(projectId);
        var projectDir = _projects.GetProjectDir(projectId);
        var videoDir = Path.Combine(projectDir, "assets", "video");
        var clipPath = Path.Combine(videoDir, $"scene_{scene:D2}_clip_{clip:D2}.mp4");
        if (!File.Exists(clipPath) || new FileInfo(clipPath).Length < 512)
            throw new InvalidOperationException($"Clip not on disk: S{scene:D2}C{clip:D2}");

        onProgress?.Invoke(5, 100, "Loading clip plan…");
        var plan = LoadClipPlan(projectId, scene, clip);
        var profiles = _projects.LoadCharacterPromptProfiles(projectId);

        string? prevPath = null;
        if (clip > 1)
        {
            var cand = Path.Combine(videoDir, $"scene_{scene:D2}_clip_{clip - 1:D2}.mp4");
            if (File.Exists(cand) && new FileInfo(cand).Length >= 512)
                prevPath = cand;
        }

        var workDir = Path.Combine(projectDir, "assets", "review", $"_frames_S{scene:D2}C{clip:D2}");
        try
        {
            if (Directory.Exists(workDir))
            {
                try { Directory.Delete(workDir, recursive: true); } catch { /* */ }
            }
            Directory.CreateDirectory(workDir);

            var images = new List<(string Path, string Label)>();
            if (prevPath is not null)
            {
                onProgress?.Invoke(15, 100, "Sampling end of previous clip…");
                var prevFrames = await ExtractTailFramesAsync(prevPath, workDir, "prev", count: 3, ct);
                foreach (var f in prevFrames)
                    images.Add((f, "PREVIOUS_CLIP_TAIL"));
            }
            else
            {
                onProgress?.Invoke(15, 100, clip == 1
                    ? "First clip — no previous for continuity…"
                    : "Previous clip missing — reviewing this clip only…");
            }

            onProgress?.Invoke(35, 100, "Sampling this clip…");
            var curFrames = await ExtractSpanFramesAsync(clipPath, workDir, "cur", ct);
            foreach (var f in curFrames)
                images.Add((f, "CURRENT_CLIP"));

            if (images.Count == 0)
                throw new InvalidOperationException("Could not extract frames from clip video.");

            // PR3: keep 2–4 current-clip frames for humans/export (before temp workDir is deleted)
            IReadOnlyList<string> durableFrames = Array.Empty<string>();
            try
            {
                durableFrames = _reviewIndex.PersistDurableFrames(
                    projectId, scene, clip, curFrames, maxFrames: 4);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Persist durable frames skipped S{Scene}C{Clip}", scene, clip);
            }

            onProgress?.Invoke(55, 100, "AI reviewing continuity and quality…");
            var prompt = BuildReviewPrompt(scene, clip, plan, profiles, images, prevPath is not null);
            // Project-scoped rules only (checklist lives in embedded clip_auto_review.txt).
            try
            {
                var rules = _projectRules.GetActiveRulesBlock(projectId);
                if (!string.IsNullOrWhiteSpace(rules))
                    prompt += "\n\n" + rules.Trim();
            }
            catch { /* non-fatal */ }
            var imagePaths = images.Select(i => i.Path).ToList();
            var qualityModel = await GetConfigStringAsync(projectId, "quality_model_name", "grok-4.5", ct);
            var raw = await _vision.CompleteWithImagesAsync(
                prompt,
                imagePaths,
                model: qualityModel,
                detail: "low",
                ct: ct);

            onProgress?.Invoke(85, 100, "Parsing suggestions…");
            var draft = ParseDraft(raw, projectId, scene, clip, plan, profiles, prevPath is not null);
            draft.GeneratedAt = DateTimeOffset.UtcNow;
            SaveDraft(draft);

            await TryLogAsync(projectId, scene, clip, draft, ct);
            try
            {
                await _logs.RecordAutoReviewAsync(
                    projectId, scene, clip,
                    draft.Suggestion, draft.Category, draft.Note, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "RecordAutoReview for assembly gate skipped");
            }

            try
            {
                _reviewIndex.UpsertClip(projectId, scene, clip, durableFrames, draft);
            }
            catch (Exception ex)
            {
                _log.LogDebug(ex, "Review index upsert skipped S{Scene}C{Clip}", scene, clip);
            }

            onProgress?.Invoke(100, 100, "Review draft ready");
            return draft;
        }
        finally
        {
            try
            {
                if (Directory.Exists(workDir))
                    Directory.Delete(workDir, recursive: true);
            }
            catch { /* best effort */ }
        }
    }

    /// <summary>Write accepted suggestion values into cast seeds / blueprint clip (with before/after log).</summary>
    public void ApplySuggestions(
        string projectId,
        int scene,
        int clip,
        IReadOnlyList<ClipAutoReviewApplyItem> items)
    {
        if (items is null || items.Count == 0)
            throw new InvalidOperationException("No suggestions selected to apply.");

        var plan = LoadClipPlan(projectId, scene, clip);
        var profiles = _projects.LoadCharacterPromptProfiles(projectId);
        var beforeParts = new List<string>();
        var afterParts = new List<string>();

        foreach (var item in items)
        {
            var layer = (item.Layer ?? "clip").Trim().ToLowerInvariant();
            var field = (item.Field ?? "").Trim().ToLowerInvariant();
            var value = item.Value ?? "";
            var before = "";

            if (layer == "character" && !string.IsNullOrWhiteSpace(item.CharKey))
            {
                profiles.TryGetValue(item.CharKey, out var p);
                before = field switch
                {
                    "description" => p?.Description ?? "",
                    "visual_lock" => p?.VisualLock ?? "",
                    "voice_profile" => p?.VoiceProfile ?? "",
                    _ => "",
                };
                switch (field)
                {
                    case "voice_profile":
                        _projects.UpdateCharacterSeedText(projectId, item.CharKey, voiceProfile: value);
                        break;
                    case "description":
                        _projects.UpdateCharacterSeedText(projectId, item.CharKey, description: value);
                        break;
                    case "visual_lock":
                        _projects.UpdateCharacterSeedText(projectId, item.CharKey, visualLock: value);
                        break;
                    default:
                        _log.LogWarning("Unknown character field {Field}", field);
                        break;
                }
                beforeParts.Add($"{item.CharKey}.{field}: {Trim(before, 400)}");
                afterParts.Add($"{item.CharKey}.{field}: {Trim(value, 400)}");
            }
            else if (layer == "clip" && field is "visual_prompt" or "prompt")
            {
                before = plan.VisualPrompt;
                _projects.UpdateClipVisualPrompt(projectId, scene, clip, value);
                beforeParts.Add($"clip.visual_prompt: {Trim(before, 600)}");
                afterParts.Add($"clip.visual_prompt: {Trim(value, 600)}");
                plan.VisualPrompt = value;
            }
        }

        var draft = LoadDraft(projectId, scene, clip);
        if (draft is not null)
        {
            draft.RawSummary = (draft.RawSummary ?? "") + "\n[applied " + DateTimeOffset.UtcNow.ToString("O") + "]";
            SaveDraft(draft);
        }

        try
        {
            _logs.AddAsync(
                projectId,
                "auto_review_apply",
                $"Applied {items.Count} suggestion(s) to S{scene:D2}C{clip:D2}",
                scene: scene,
                clip: clip,
                actionTaken: "apply_suggestions",
                before: string.Join("\n---\n", beforeParts),
                after: string.Join("\n---\n", afterParts),
                category: draft?.Category,
                suggestionCount: items.Count).GetAwaiter().GetResult();
        }
        catch { /* non-fatal */ }
    }

    private async Task TryLogAsync(
        string projectId, int scene, int clip, ClipAutoReviewDraft draft, CancellationToken ct)
    {
        try
        {
            await _logs.AddAsync(
                projectId,
                "auto_review",
                draft.Note.Length > 0 ? draft.Note : draft.Suggestion,
                scene: scene,
                clip: clip,
                actionTaken: $"suggestion={draft.Suggestion};category={draft.Category};confidence={draft.Confidence}",
                category: draft.Category,
                suggestion: draft.Suggestion,
                confidence: draft.Confidence,
                continuity: draft.Continuity,
                suggestionCount: draft.Suggestions.Count,
                ct: ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "auto_review log skip");
        }
    }

    /// <summary>Test/helper: load planned clip fields from Stage 2 blueprint (<c>veo_clips</c>).</summary>
    public static ClipPlanSnapshot LoadClipPlanForTests(
        ProjectStore projects, string projectId, int scene, int clip)
    {
        var plan = LoadClipPlanCore(projects, projectId, scene, clip, log: null);
        return new ClipPlanSnapshot(plan.VisualPrompt, plan.Dialogue, plan.Speaker, plan.Delivery);
    }

    public readonly record struct ClipPlanSnapshot(
        string VisualPrompt, string Dialogue, string Speaker, string Delivery);

    private ClipPlan LoadClipPlan(string projectId, int scene, int clip) =>
        LoadClipPlanCore(_projects, projectId, scene, clip, _log);

    private static ClipPlan LoadClipPlanCore(
        ProjectStore projects,
        string projectId,
        int scene,
        int clip,
        ILogger? log)
    {
        var plan = new ClipPlan();
        try
        {
            using var doc = projects.LoadBlueprintAsync(projectId).GetAwaiter().GetResult();
            if (doc is null) return plan;
            var root = doc.RootElement;
            if (!root.TryGetProperty("scenes", out var scenes) || scenes.ValueKind != JsonValueKind.Array)
                return plan;
            foreach (var s in scenes.EnumerateArray())
            {
                if (!s.TryGetProperty("scene_number", out var sn) || !sn.TryGetInt32(out var n) || n != scene)
                    continue;
                // Canonical Stage 2 key is veo_clips
                if (!s.TryGetProperty("veo_clips", out var clips) || clips.ValueKind != JsonValueKind.Array)
                {
                    if (!s.TryGetProperty("clips", out clips) || clips.ValueKind != JsonValueKind.Array)
                        break;
                }
                foreach (var c in clips.EnumerateArray())
                {
                    if (!c.TryGetProperty("clip_number", out var cn) || !cn.TryGetInt32(out var cnum) || cnum != clip)
                        continue;
                    plan.VisualPrompt = c.TryGetProperty("visual_prompt", out var vp) ? vp.GetString() ?? "" : "";
                    if (c.TryGetProperty("audio_payload", out var ap) && ap.ValueKind == JsonValueKind.Object)
                    {
                        plan.Dialogue = ap.TryGetProperty("dialogue", out var d) ? d.GetString() ?? "" : "";
                        plan.Speaker = ap.TryGetProperty("speaker", out var sp) ? sp.GetString() ?? "" : "";
                        plan.Delivery = ap.TryGetProperty("delivery", out var del) ? del.GetString() ?? "" : "";
                    }
                    if (c.TryGetProperty("characters_present", out var cp) && cp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var x in cp.EnumerateArray())
                        {
                            var k = x.GetString();
                            if (!string.IsNullOrWhiteSpace(k)) plan.Characters.Add(k!);
                        }
                    }
                    return plan;
                }
            }
        }
        catch (Exception ex)
        {
            log?.LogDebug(ex, "LoadClipPlan failed");
        }
        return plan;
    }

    private static string BuildReviewPrompt(
        int scene,
        int clip,
        ClipPlan plan,
        IReadOnlyDictionary<string, ClipVideoPromptBuilder.CharacterProfile> profiles,
        List<(string Path, string Label)> images,
        bool hasPrev)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a film QC assistant for a short children's/book adaptation.");
        sb.AppendLine($"Review clip S{scene:D2}C{clip:D2}.");
        sb.AppendLine();
        sb.AppendLine("Images are labeled in order:");
        for (var i = 0; i < images.Count; i++)
            sb.AppendLine($"  IMAGE_{i + 1}: {images[i].Label}");
        if (hasPrev)
            sb.AppendLine("PREVIOUS_CLIP_TAIL frames are the END of the prior clip — judge continuity into CURRENT_CLIP (especially its START).");
        else
            sb.AppendLine("No previous clip tail available.");
        sb.AppendLine();
        sb.AppendLine("Planned visual_prompt:");
        sb.AppendLine(plan.VisualPrompt.Length > 0 ? plan.VisualPrompt : "(missing)");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(plan.Dialogue))
        {
            sb.AppendLine($"Dialogue speaker={plan.Speaker} delivery={plan.Delivery}:");
            sb.AppendLine($"\"{plan.Dialogue}\"");
        }
        else
            sb.AppendLine("No dialogue on this clip.");
        sb.AppendLine();
        if (plan.Characters.Count > 0)
        {
            sb.AppendLine("Cast present:");
            foreach (var key in plan.Characters)
            {
                profiles.TryGetValue(key, out var p);
                var name = p?.DisplayName ?? key;
                var look = p?.Description ?? "";
                var voice = p?.VoiceProfile ?? "";
                // Token-accurate (was character-count Trim): this line becomes part of the
                // vision-review prompt, unlike the other Trim(...) calls in this file (those
                // trim audit-log diffs / stored response summaries, not outgoing prompt text).
                sb.AppendLine(
                    $"- {key} ({name}) look: {PromptTokenizer.TruncateToTokens(look, 50)} " +
                    $"voice: {PromptTokenizer.TruncateToTokens(voice, 30)}");
            }
        }
        sb.AppendLine();
        sb.Append(LoadAutoReviewRulesBlock());
        return sb.ToString();
    }

    /// <summary>Checklist + JSON schema from <c>prompts/clip_auto_review.txt</c> (embed or override).</summary>
    public static string LoadAutoReviewRulesBlock()
    {
        try
        {
            var text = PromptFiles.ReadAsync("prompts/clip_auto_review.txt").GetAwaiter().GetResult();
            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim() + "\n";
        }
        catch
        {
            /* fall through to built-in fallback */
        }

        // Minimal fallback if embed/override missing (tests without resources).
        return """
            CHECKLIST (fail when confidence high; put the primary issue in category):
            1) IDENTITY — faces match cast Character_*; no role swap/merge.
            2) PROMPT COMPLETENESS — flag stub/truncated plan text; rewrite visual_prompt if needed.
            3) SILENCE vs EXPRESSION — no mid-shout / open-mouth yell when silent or no dialogue.
            4) ADDRESS / GAZE — honor PROJECT performance rules (confessional vs observational).
            5) STYLE + WARDROBE — match project medium and cast period/wardrobe locks.
            6) FACE READABILITY — fail beauty-blank or wrong-emotion face when confidence high.
            Also: continuity from prev tail, lip/speech vs dialogue, empty/dead frames, wrong action.
            Respond with JSON ONLY (no markdown):
            {
              "suggestion": "pass"|"fail"|"unclear",
              "category": "continuity"|"wrong_look"|"wrong_style"|"wrong_voice"|"silent"|"framing"|"other",
              "confidence": "high"|"medium"|"low",
              "continuity": "ok"|"jump"|"unclear"|"n/a",
              "note": "one short human-readable review note covering the main checklist hit",
              "suggestions": []
            }
            Rules: prefer clip visual_prompt rewrites. Keep Character_* keys. Empty suggestions[] if pass.

            """;
    }

    private static ClipAutoReviewDraft ParseDraft(
        string raw,
        string projectId,
        int scene,
        int clip,
        ClipPlan plan,
        IReadOnlyDictionary<string, ClipVideoPromptBuilder.CharacterProfile> profiles,
        bool hasPrev)
    {
        var draft = new ClipAutoReviewDraft
        {
            ProjectId = projectId,
            Scene = scene,
            Clip = clip,
            IncludedPreviousTail = hasPrev,
            RawSummary = Trim(raw, 2000),
        };

        try
        {
            var start = raw.IndexOf('{');
            var end = raw.LastIndexOf('}');
            if (start < 0 || end <= start)
            {
                draft.Suggestion = "unclear";
                draft.Note = "Could not parse review response.";
                return draft;
            }

            using var doc = JsonDocument.Parse(raw[start..(end + 1)]);
            var root = doc.RootElement;
            draft.Suggestion = GetStr(root, "suggestion", "unclear").ToLowerInvariant();
            draft.Category = GetStr(root, "category", "other").ToLowerInvariant();
            draft.Confidence = GetStr(root, "confidence", "medium").ToLowerInvariant();
            draft.Continuity = GetStr(root, "continuity", hasPrev ? "unclear" : "n/a").ToLowerInvariant();
            draft.Note = GetStr(root, "note", "");

            if (root.TryGetProperty("suggestions", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var layer = GetStr(item, "layer", "clip").ToLowerInvariant();
                    var field = GetStr(item, "field", "");
                    if (string.IsNullOrWhiteSpace(field) && item.TryGetProperty("suggested_value", out _))
                        field = layer == "character" ? "voice_profile" : "visual_prompt";
                    var charKey = GetStr(item, "char_key", "") is { Length: > 0 } ck ? ck : null;
                    if (charKey is null && item.TryGetProperty("charKey", out var ck2))
                        charKey = ck2.GetString();

                    var suggested = GetStr(item, "suggested_value", "");
                    if (string.IsNullOrWhiteSpace(suggested))
                        suggested = GetStr(item, "suggestedValue", "");
                    if (string.IsNullOrWhiteSpace(suggested)) continue;

                    var current = "";
                    if (layer == "clip" && field is "visual_prompt" or "prompt")
                        current = plan.VisualPrompt;
                    else if (layer == "character" && charKey is not null &&
                             profiles.TryGetValue(charKey, out var p))
                    {
                        current = field switch
                        {
                            "description" => p.Description,
                            "visual_lock" => p.VisualLock,
                            "voice_profile" => p.VoiceProfile,
                            _ => "",
                        };
                    }

                    var include = true;
                    if (item.TryGetProperty("include_by_default", out var ib) &&
                        ib.ValueKind is JsonValueKind.False)
                        include = false;
                    if (item.TryGetProperty("includeByDefault", out var ib2) &&
                        ib2.ValueKind is JsonValueKind.False)
                        include = false;

                    draft.Suggestions.Add(new ClipAutoReviewSuggestion
                    {
                        Layer = layer is "character" or "scene" ? layer : "clip",
                        Field = field,
                        CharKey = charKey,
                        Label = GetStr(item, "label", field),
                        CurrentValue = current,
                        SuggestedValue = suggested,
                        IncludeByDefault = include,
                        Rationale = GetStr(item, "rationale", ""),
                    });
                }
            }
        }
        catch (Exception)
        {
            draft.Suggestion = "unclear";
            if (string.IsNullOrWhiteSpace(draft.Note))
                draft.Note = "Review response parse error.";
        }

        return draft;
    }

    private async Task<List<string>> ExtractTailFramesAsync(
        string videoPath, string workDir, string prefix, int count, CancellationToken ct)
    {
        // Last ~1.5s at ~2 fps
        var pattern = Path.Combine(workDir, $"{prefix}_%02d.jpg");
        var args =
            $"-y -sseof -1.5 -i \"{videoPath}\" -vf fps=2 -frames:v {count} -q:v 5 \"{pattern}\"";
        await RunFfmpegAsync(args, ct);
        return Directory.GetFiles(workDir, $"{prefix}_*.jpg").OrderBy(f => f).ToList();
    }

    private async Task<List<string>> ExtractSpanFramesAsync(
        string videoPath, string workDir, string prefix, CancellationToken ct)
    {
        // ~3 frames across the clip (start-ish, mid, end-ish)
        var pattern = Path.Combine(workDir, $"{prefix}_%02d.jpg");
        var args =
            $"-y -i \"{videoPath}\" -vf \"fps=1/2\" -frames:v 3 -q:v 5 \"{pattern}\"";
        await RunFfmpegAsync(args, ct);
        var files = Directory.GetFiles(workDir, $"{prefix}_*.jpg").OrderBy(f => f).ToList();
        if (files.Count > 0) return files;

        // Fallback: single frame at 0.5s
        var one = Path.Combine(workDir, $"{prefix}_01.jpg");
        await RunFfmpegAsync($"-y -ss 0.5 -i \"{videoPath}\" -frames:v 1 -q:v 5 \"{one}\"", ct);
        return File.Exists(one) ? new List<string> { one } : new List<string>();
    }

    private Task RunFfmpegAsync(string args, CancellationToken ct)
    {
        _ = args;
        _ = ct;
        throw new InvalidOperationException("Native ffmpeg removed — cannot sample frames on the server.");
    }

    private static string GetStr(JsonElement el, string name, string fallback)
    {
        if (!el.TryGetProperty(name, out var p)) return fallback;
        return p.ValueKind == JsonValueKind.String ? (p.GetString() ?? fallback) : fallback;
    }

    private static string Trim(string s, int n) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..n];

    private sealed class ClipPlan
    {
        public string VisualPrompt { get; set; } = "";
        public string Dialogue { get; set; } = "";
        public string Speaker { get; set; } = "";
        public string Delivery { get; set; } = "";
        public List<string> Characters { get; } = new();
    }
}
