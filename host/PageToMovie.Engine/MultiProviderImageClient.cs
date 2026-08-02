using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

/// <summary>
/// Routes <see cref="IImageClient"/> calls to the right concrete provider client based on
/// the requested <c>model</c>'s provider in <see cref="SupportedModelCatalog"/> — callers
/// (character portrait generation) keep calling one <see cref="IImageClient"/> and never
/// need to know which backend actually served the request. Empty model throws (Settings required).
/// </summary>
public sealed class MultiProviderImageClient : IImageClient
{
    private readonly GrokImageClient _grok;
    private readonly GeminiImageClient _gemini;
    private readonly FalImageClient _fal;

    public MultiProviderImageClient(GrokImageClient grok, GeminiImageClient gemini, FalImageClient fal)
    {
        _grok = grok;
        _gemini = gemini;
        _fal = fal;
    }

    /// <summary>True when at least one provider has an API key configured.</summary>
    public bool IsConfigured => _grok.IsConfigured || _gemini.IsConfigured || _fal.IsConfigured;

    public Task<IReadOnlyList<byte[]>> GenerateVariantsAsync(
        string prompt,
        int n = 3,
        string aspectRatio = "1:1",
        string? model = null,
        CancellationToken ct = default) =>
        Resolve(model).GenerateVariantsAsync(prompt, n, aspectRatio, model, ct);

    public Task<IReadOnlyList<byte[]>> EditVariantsAsync(
        string prompt,
        IReadOnlyList<string> referenceImagePaths,
        int n = 3,
        string aspectRatio = "1:1",
        string? model = null,
        int maxRefs = 0,
        string? costumeRefPath = null,
        bool illustratedMedium = true,
        Action<string>? onProgress = null,
        CancellationToken ct = default) =>
        Resolve(model).EditVariantsAsync(
            prompt, referenceImagePaths, n, aspectRatio, model, maxRefs, costumeRefPath, illustratedMedium,
            onProgress, ct);

    private IImageClient Resolve(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException(
                "Image: model is required. Open Settings and choose an Image generation model.");
        var entry = SupportedModelCatalog.Find(model, ModelCapability.Image) ?? SupportedModelCatalog.Find(model);
        if (entry is null || !entry.Enabled)
            throw new InvalidOperationException(
                $"Image: model '{model}' is not in the models catalog (or is disabled). Open Settings and pick a current model.");
        return entry.Provider switch
        {
            ModelProviderFamily.Google => _gemini,
            ModelProviderFamily.Fal => _fal,
            _ => _grok,
        };
    }
}