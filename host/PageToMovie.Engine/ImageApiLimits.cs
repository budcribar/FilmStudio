namespace PageToMovie.Engine;

/// <summary>
/// Image multi-reference caps from the models catalog only.
/// Unknown model ids or missing <c>maxReferenceImages</c> are errors — never invent defaults.
/// </summary>
public static class ImageApiLimits
{
    public const string ProviderGrok = "grok";
    public const string ProviderGemini = "gemini";

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

    /// <summary>
    /// Max reference images for this image model from the catalog.
    /// Throws if <paramref name="imageModel"/> is missing, not in the catalog, or has no
    /// <c>maxReferenceImages</c> — never defaults by provider.
    /// </summary>
    public static int MaxReferenceImages(string? imageProvider, string? imageModel)
    {
        if (string.IsNullOrWhiteSpace(imageModel))
            throw new InvalidOperationException(
                "Image model is required for max reference images. " +
                "Open Settings → Studio coverage and choose an image model.");

        var entry = PageToMovie.Core.Models.SupportedModelCatalog.Find(
            imageModel.Trim(), PageToMovie.Core.Models.ModelCapability.Image)
            ?? PageToMovie.Core.Models.SupportedModelCatalog.Find(imageModel.Trim());

        if (entry is null)
            throw new InvalidOperationException(
                $"Image model '{imageModel}' is not in models_catalog.json. " +
                "Unknown models have no capabilities — pick a catalog image model in Settings.");

        if (entry.Capability != PageToMovie.Core.Models.ModelCapability.Image)
            throw new InvalidOperationException(
                $"Model '{entry.Id}' is catalogued as {entry.Capability}, not Image. " +
                "Choose an image model in Settings → Studio coverage.");

        if (entry.MaxReferenceImages is not { } catalogMax)
            throw new InvalidOperationException(
                $"Image model '{entry.Id}' has no maxReferenceImages in models_catalog.json. " +
                "Add the field to the catalog — do not invent a default.");

        return catalogMax;
    }

    /// <summary>
    /// Clamp a requested max-refs to the catalog limit for this image model.
    /// </summary>
    public static int ClampMaxRefs(int requested, string? imageProvider, string? imageModel)
    {
        var cap = MaxReferenceImages(imageProvider, imageModel);
        if (cap <= 0)
            return 0;
        if (requested <= 0)
            return cap;
        return Math.Clamp(requested, 1, cap);
    }
}
