using System.Text.Json;
using PageToMovie.Adaptation.Contracts;

namespace PageToMovie.Adaptation.Conversion;

/// <summary>Pure parse of model VISION_META JSON into <see cref="AdaptationVisionMeta"/>.</summary>
public static class AdaptationVisionMetaParser
{
    public const string MediumPhotoreal = "photoreal_live_action";
    public const string MediumIllustrated = "illustrated_picture_book";
    public const string MediumStylized3d = "stylized_3d_animated";
    public const string MediumOther = "other";

    public static AdaptationVisionMeta? ParseModelJson(string? raw)
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
            return new AdaptationVisionMeta
            {
                VisualMedium = med,
                RenderStyleLock = string.IsNullOrWhiteSpace(style) ? DefaultStyleLock(med) : style!.Trim(),
                Notes = notes,
                DecidedBy = "adaptation",
            };
        }
        catch
        {
            return null;
        }
    }

    public static string NormalizeMedium(string? raw)
    {
        var s = (raw ?? "").Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');
        if (s is "photoreal" or "photo_real" or "live_action" or "liveaction" or "photoreal_live_action"
            or "period_drama" or "gothic_live_action" or "mixed")
            return MediumPhotoreal;
        if (s is "illustrated" or "picture_book" or "picturebook" or "illustration"
            or "illustrated_picture_book" or "childrens_book" or "storybook")
            return MediumIllustrated;
        if (s is "stylized_3d" or "stylized_3d_animated" or "cg_animated" or "pixar" or "3d_animated")
            return MediumStylized3d;
        if (s is MediumPhotoreal or MediumIllustrated or MediumStylized3d or MediumOther)
            return s;
        if (s.Contains("picture") || s.Contains("illustrat") || s.Contains("cartoon") || s.Contains("storybook"))
            return MediumIllustrated;
        if (s.Contains("photoreal") || s.Contains("live_action") || s.Contains("live action") || s.Contains("period"))
            return MediumPhotoreal;
        return MediumOther;
    }

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
}
