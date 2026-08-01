using System.Text.Json;
using System.Text.Json.Serialization;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

/// <summary>
/// Structured visual medium decided at book→screenplay (adaptation) time.
/// Source of truth for photoreal vs illustrated — not regex over Fountain prose.
/// File: <c>source/vision_meta.json</c>.
/// </summary>
public static class ProjectVisionMeta
{
    public const string FileName = "vision_meta.json";
    public const string SchemaVersion = "vision_meta.v1";

    public const string MediumPhotoreal = "photoreal_live_action";
    public const string MediumIllustrated = "illustrated_picture_book";
    public const string MediumStylized3d = "stylized_3d_animated";
    public const string MediumOther = "other";

    public sealed class Document
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = ProjectVisionMeta.SchemaVersion;

        /// <summary>Machine enum: photoreal_live_action | illustrated_picture_book | stylized_3d_animated | other</summary>
        [JsonPropertyName("visual_medium")]
        public string VisualMedium { get; set; } = MediumPhotoreal;

        /// <summary>Full STYLE LOCK prose for image/video models.</summary>
        [JsonPropertyName("render_style_lock")]
        public string? RenderStyleLock { get; set; }

        [JsonPropertyName("performance_lock")]
        public string? PerformanceLock { get; set; }

        /// <summary>adaptation | cast_extract | user</summary>
        [JsonPropertyName("decided_by")]
        public string DecidedBy { get; set; } = "adaptation";

        [JsonPropertyName("decided_at")]
        public string? DecidedAt { get; set; }

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    public static string GetPath(string projectDir) =>
        Path.Combine(projectDir, "source", FileName);

    public static Document? TryRead(string projectDir)
    {
        var path = GetPath(projectDir);
        if (!File.Exists(path)) return null;
        try
        {
            var doc = JsonSerializer.Deserialize<Document>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (doc is null || string.IsNullOrWhiteSpace(doc.VisualMedium))
                return null;
            doc.VisualMedium = NormalizeMedium(doc.VisualMedium);
            return doc;
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string projectDir, Document doc)
    {
        Directory.CreateDirectory(Path.Combine(projectDir, "source"));
        doc.SchemaVersion = SchemaVersion;
        doc.VisualMedium = NormalizeMedium(doc.VisualMedium);
        doc.DecidedAt = DateTimeOffset.UtcNow.ToString("o");
        if (string.IsNullOrWhiteSpace(doc.RenderStyleLock))
            doc.RenderStyleLock = DefaultStyleLock(doc.VisualMedium);
        var path = GetPath(projectDir);
        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
        File.WriteAllText(path, json);
    }

    public static string NormalizeMedium(string? raw)
    {
        var s = (raw ?? "").Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        if (s is "photoreal" or "photo_real" or "live_action" or "liveaction" or "photoreal_live_action"
            or "period_drama" or "gothic_live_action")
            return MediumPhotoreal;
        if (s is "illustrated" or "picture_book" or "picturebook" or "illustration"
            or "illustrated_picture_book" or "childrens_book" or "storybook")
            return MediumIllustrated;
        if (s is "stylized_3d" or "stylized_3d_animated" or "cg_animated" or "pixar" or "3d_animated")
            return MediumStylized3d;
        if (s is MediumPhotoreal or MediumIllustrated or MediumStylized3d or MediumOther)
            return s;
        // Free text from model
        if (s.Contains("picture") || s.Contains("illustrat") || s.Contains("cartoon") || s.Contains("storybook"))
            return MediumIllustrated;
        if (s.Contains("photoreal") || s.Contains("live_action") || s.Contains("live action") || s.Contains("period"))
            return MediumPhotoreal;
        return MediumOther;
    }

    public static bool PrefersIllustrated(string? visualMedium) =>
        NormalizeMedium(visualMedium) is MediumIllustrated or MediumStylized3d;

    public static string DefaultStyleLock(string visualMedium) => NormalizeMedium(visualMedium) switch
    {
        MediumIllustrated =>
            "STYLE LOCK: stylized animated children's picture-book look for ALL on-screen cast " +
            "(animals and humans share the same medium) -- not photoreal, not live-action",
        MediumStylized3d =>
            "STYLE LOCK: stylized 3D animated children's feature look — coherent CG medium for all cast; " +
            "not photoreal live-action, not flat 2D doodle",
        _ =>
            "STYLE LOCK: photoreal live-action continuity portrait — naturalistic face and wardrobe, " +
            "period-appropriate when the story implies it. NOT cartoon, NOT illustration, NOT anime",
    };

    /// <summary>
    /// Ask the planning model once at adaptation time for structured medium metadata.
    /// Fountain prose is not parsed; the model returns JSON only.
    /// </summary>
    public static async Task<Document> DecideAtAdaptationAsync(
        string projectDir,
        string title,
        string bookText,
        string fountainText,
        IChatClient chat,
        string model,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        onProgress?.Invoke("Deciding film visual medium (structured metadata)…");

        var bookSample = bookText.Length > 6_000 ? bookText[..6_000] + "\n[[truncated]]" : bookText;
        var fountainSample = fountainText.Length > 4_000 ? fountainText[..4_000] + "\n[[truncated]]" : fountainText;

        var system =
            "You classify the visual medium for a film adaptation. Return JSON only (no markdown).\n" +
            "Schema:\n" +
            "{\n" +
            "  \"visual_medium\": \"photoreal_live_action\" | \"illustrated_picture_book\" | \"stylized_3d_animated\" | \"other\",\n" +
            "  \"render_style_lock\": \"STYLE LOCK: … one sentence medium for portraits and clips …\",\n" +
            "  \"notes\": \"optional short rationale\"\n" +
            "}\n" +
            "Rules:\n" +
            "- Decide from STORY CONTENT (genre, illustrated children's book vs literary prose vs live-action drama).\n" +
            "- Do NOT use file type. Classic short stories / gothic / period literary → photoreal_live_action.\n" +
            "- Animal picture books / painted children's stories → illustrated_picture_book.\n" +
            "- One medium for the whole film.";

        var user =
            $"Title: {title}\n\n--- BOOK (sample) ---\n{bookSample}\n\n--- SCREENPLAY (sample) ---\n{fountainSample}\n";

        var raw = await chat.CompleteAsync(
            system,
            user,
            model: model,
            ct: ct,
            mode: ChatCallModes.VisionMetaAdaptation).ConfigureAwait(false);

        var doc = ParseModelJson(raw) ?? new Document
        {
            VisualMedium = MediumPhotoreal,
            RenderStyleLock = DefaultStyleLock(MediumPhotoreal),
            Notes = "fallback: model JSON unparseable",
        };
        doc.DecidedBy = "adaptation";
        Write(projectDir, doc);
        onProgress?.Invoke($"Visual medium: {doc.VisualMedium}");
        return doc;
    }

    public static Document? ParseModelJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = raw.Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNl = t.IndexOf('\n');
            if (firstNl > 0) t = t[(firstNl + 1)..];
            var fence = t.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) t = t[..fence];
            t = t.Trim();
        }
        try
        {
            using var jd = JsonDocument.Parse(t);
            var root = jd.RootElement;
            var medium = root.TryGetProperty("visual_medium", out var m) ? m.GetString() : null;
            var style = root.TryGetProperty("render_style_lock", out var s) ? s.GetString() : null;
            var notes = root.TryGetProperty("notes", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(medium) && string.IsNullOrWhiteSpace(style))
                return null;
            var med = NormalizeMedium(medium);
            return new Document
            {
                VisualMedium = med,
                RenderStyleLock = string.IsNullOrWhiteSpace(style) ? DefaultStyleLock(med) : style!.Trim(),
                Notes = notes,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Upsert from cast extract when adaptation metadata is missing.</summary>
    public static void UpsertFromCast(string projectDir, string? renderStyleLock, string? performanceLock)
    {
        if (string.IsNullOrWhiteSpace(renderStyleLock) && string.IsNullOrWhiteSpace(performanceLock))
            return;
        var existing = TryRead(projectDir);
        // Do not overwrite adaptation decision with cast unless missing.
        if (existing is not null &&
            string.Equals(existing.DecidedBy, "adaptation", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(existing.RenderStyleLock))
        {
            if (!string.IsNullOrWhiteSpace(performanceLock) && string.IsNullOrWhiteSpace(existing.PerformanceLock))
            {
                existing.PerformanceLock = performanceLock.Trim();
                Write(projectDir, existing);
            }
            return;
        }

        var med = existing?.VisualMedium ?? MediumPhotoreal;
        if (!string.IsNullOrWhiteSpace(renderStyleLock))
        {
            var r = renderStyleLock!;
            if (r.Contains("picture", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("illustrat", StringComparison.OrdinalIgnoreCase) ||
                r.Contains("cartoon", StringComparison.OrdinalIgnoreCase))
                med = MediumIllustrated;
            else if (r.Contains("photoreal", StringComparison.OrdinalIgnoreCase) ||
                     r.Contains("live-action", StringComparison.OrdinalIgnoreCase) ||
                     r.Contains("live action", StringComparison.OrdinalIgnoreCase))
                med = MediumPhotoreal;
        }

        Write(projectDir, new Document
        {
            VisualMedium = med,
            RenderStyleLock = renderStyleLock?.Trim() ?? existing?.RenderStyleLock ?? DefaultStyleLock(med),
            PerformanceLock = performanceLock?.Trim() ?? existing?.PerformanceLock,
            DecidedBy = existing is null ? "cast_extract" : existing.DecidedBy,
            Notes = existing?.Notes,
        });
    }
}
