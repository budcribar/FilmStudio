using System.Text.Json;

namespace PageToMovie.Adaptation.Contracts;

/// <summary>
/// Single source of truth for the visual-medium tokens, their default STYLE LOCK prose, and the
/// shared VISION_META JSON parse prologue. Referenced by both
/// <see cref="Conversion.AdaptationVisionMetaParser"/> (Adaptation) and <c>ProjectVisionMeta</c>
/// (Engine, which references Adaptation).
///
/// Note: medium *normalization* is intentionally NOT shared — the two callers diverge
/// (the adaptation parser maps "mixed"→photoreal with no "auto"; the Engine store recognizes
/// "auto"/empty and has no "mixed"), so each keeps its own <c>NormalizeMedium</c>.
/// </summary>
public static class VisualMediumStyles
{
    public const string MediumPhotoreal = "photoreal_live_action";
    public const string MediumIllustrated = "illustrated_picture_book";
    public const string MediumStylized3d = "stylized_3d_animated";
    public const string MediumOther = "other";

    public const string PhotorealStyleLock =
        "STYLE LOCK: photoreal live-action continuity portrait — naturalistic face and wardrobe, " +
        "period-appropriate when the story implies it. NOT cartoon, NOT illustration, NOT anime";

    public const string IllustratedStyleLock =
        "STYLE LOCK: stylized animated children's picture-book look for ALL on-screen cast " +
        "(animals and humans share the same medium) -- not photoreal, not live-action";

    public const string Stylized3dStyleLock =
        "STYLE LOCK: stylized 3D animated children's feature look — coherent CG medium for all cast; " +
        "not photoreal live-action, not flat 2D doodle";

    /// <summary>Default STYLE LOCK prose for an already-normalized medium token.</summary>
    public static string StyleLockFor(string normalizedMedium) => normalizedMedium switch
    {
        MediumIllustrated => IllustratedStyleLock,
        MediumStylized3d => Stylized3dStyleLock,
        _ => PhotorealStyleLock,
    };

    /// <summary>Strips a leading/trailing ``` (optionally ```json) code fence from a model reply.</summary>
    public static string StripJsonFence(string trimmed)
    {
        var t = trimmed;
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNl = t.IndexOf('\n');
            if (firstNl > 0) t = t[(firstNl + 1)..];
            var fence = t.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) t = t[..fence];
            t = t.Trim();
        }
        return t;
    }

    /// <summary>
    /// Shared VISION_META parse prologue: trims/fence-strips <paramref name="raw"/>, parses the JSON,
    /// and reads the <c>visual_medium</c> / <c>render_style_lock</c> / <c>notes</c> fields. Returns null
    /// for blank input, unparseable JSON, or when both medium and style are absent — exactly matching the
    /// callers' original guard. Each caller applies its own medium normalization and result type.
    /// </summary>
    public static (string? Medium, string? Style, string? Notes)? ParseVisionFields(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = StripJsonFence(raw.Trim());
        try
        {
            using var jd = JsonDocument.Parse(t);
            var root = jd.RootElement;
            var medium = root.TryGetProperty("visual_medium", out var m) ? m.GetString() : null;
            var style = root.TryGetProperty("render_style_lock", out var s) ? s.GetString() : null;
            var notes = root.TryGetProperty("notes", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(medium) && string.IsNullOrWhiteSpace(style))
                return null;
            return (medium, style, notes);
        }
        catch
        {
            return null;
        }
    }
}
