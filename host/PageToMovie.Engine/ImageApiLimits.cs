namespace PageToMovie.Engine;

/// <summary>
/// Provider-specific caps for multi-reference image edit / portrait seeds.
/// Keep UI and API aligned so we never offer more seeds than the active backend accepts.
/// </summary>
public static class ImageApiLimits
{
    public const string ProviderGrok = "grok";
    public const string ProviderGemini = "gemini";

    /// <summary>xAI Grok Imagine image edits: up to 3 reference images per request.</summary>
    public const int GrokMaxReferenceImages = 3;

    /// <summary>
    /// Gemini 3 image models: up to 14 reference images.
    /// Older Flash image paths are lower — still use 14 as soft max for selection ranking;
    /// actual client may clamp further if needed.
    /// </summary>
    public const int GeminiMaxReferenceImages = 14;

    public const int DefaultMaxReferenceImages = GrokMaxReferenceImages;

    /// <summary>
    /// Resolve provider id from model catalog only. Empty when unknown — never invents "grok".
    /// </summary>
    public static string ResolveProvider(string? imageProvider, string? imageModel)
    {
        var entry = PageToMovie.Core.Models.SupportedModelCatalog.Find(
            imageModel,
            PageToMovie.Core.Models.ModelCapability.Image)
            ?? PageToMovie.Core.Models.SupportedModelCatalog.Find(imageModel);
        if (entry is not null && !string.IsNullOrWhiteSpace(entry.ProviderId))
            return entry.ProviderId;

        if (!string.IsNullOrWhiteSpace(imageProvider)
            && PageToMovie.Core.Models.SupportedModelCatalog.IsKnownProviderId(imageProvider))
            return PageToMovie.Core.Models.SupportedModelCatalog.NormalizeProviderId(imageProvider);

        return "";
    }

    /// <summary>Hard max reference images for multi-image edit on this provider.</summary>
    public static int MaxReferenceImages(string? imageProvider, string? imageModel)
    {
        var entry = PageToMovie.Core.Models.SupportedModelCatalog.Find(
            imageModel, PageToMovie.Core.Models.ModelCapability.Image)
            ?? PageToMovie.Core.Models.SupportedModelCatalog.Find(imageModel);
        if (entry?.MaxReferenceImages is { } catalogMax && catalogMax > 0)
            return catalogMax;

        return ResolveProvider(imageProvider, imageModel) switch
        {
            ProviderGemini or "google" => GeminiMaxReferenceImages,
            ProviderGrok or "xai" => GrokMaxReferenceImages,
            _ => DefaultMaxReferenceImages,
        };
    }

    /// <summary>
    /// Clamp a requested max-refs to the active provider limit.
    /// </summary>
    public static int ClampMaxRefs(int requested, string? imageProvider, string? imageModel)
    {
        var cap = MaxReferenceImages(imageProvider, imageModel);
        if (requested <= 0)
            return cap;
        return Math.Clamp(requested, 1, cap);
    }
}
