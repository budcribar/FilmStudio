using PageToMovie.Adaptation.Contracts;

namespace PageToMovie.Adaptation.Conversion;

/// <summary>Pure parse of model VISION_META JSON into <see cref="AdaptationVisionMeta"/>.</summary>
public static class AdaptationVisionMetaParser
{
    public const string MediumPhotoreal = VisualMediumStyles.MediumPhotoreal;
    public const string MediumIllustrated = VisualMediumStyles.MediumIllustrated;
    public const string MediumStylized3d = VisualMediumStyles.MediumStylized3d;
    public const string MediumOther = VisualMediumStyles.MediumOther;

    public static AdaptationVisionMeta? ParseModelJson(string? raw)
    {
        if (VisualMediumStyles.ParseVisionFields(raw) is not { } fields) return null;
        var (medium, style, notes) = fields;
        var med = NormalizeMedium(medium);
        return new AdaptationVisionMeta
        {
            VisualMedium = med,
            RenderStyleLock = string.IsNullOrWhiteSpace(style) ? DefaultStyleLock(med) : style!.Trim(),
            Notes = notes,
            DecidedBy = "adaptation",
        };
    }

    // NormalizeMedium is intentionally NOT shared with ProjectVisionMeta: this parser maps "mixed"
    // to photoreal and has no "auto" handling (the model always returns a concrete medium here).
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

    public static string DefaultStyleLock(string visualMedium) =>
        VisualMediumStyles.StyleLockFor(NormalizeMedium(visualMedium));
}
