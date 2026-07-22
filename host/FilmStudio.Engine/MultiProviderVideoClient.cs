using FilmStudio.Core.Models;
using FilmStudio.Engine.Abstractions;

namespace FilmStudio.Engine;

/// <summary>
/// Routes <see cref="IVideoClient"/> calls to the right concrete provider client based on
/// the requested <c>model</c>'s provider in <see cref="SupportedModelCatalog"/>. Submit / poll /
/// download are three separate calls in this interface, and Grok's and Gemini's request ids
/// have different shapes (Grok: short opaque id; Gemini: an operation resource path) — rather
/// than keep a requestId→provider lookup table (extra state to go stale across restarts), this
/// tags the id itself with a small provider prefix on submit and strips it again on poll, so
/// routing stays correct with no server-side memory of in-flight jobs.
/// </summary>
public sealed class MultiProviderVideoClient : IVideoClient
{
    private const string GrokPrefix = "grok:";
    private const string GeminiPrefix = "gemini:";

    private readonly GrokVideoClient _grok;
    private readonly GeminiVideoClient _gemini;

    public MultiProviderVideoClient(GrokVideoClient grok, GeminiVideoClient gemini)
    {
        _grok = grok;
        _gemini = gemini;
    }

    /// <summary>True when at least one provider has an API key configured.</summary>
    public bool IsConfigured => _grok.IsConfigured || _gemini.IsConfigured;

    public async Task<string> SubmitGenerationAsync(
        string prompt,
        int durationSeconds,
        string resolution,
        string model,
        CancellationToken ct,
        IReadOnlyList<string>? referenceImagePaths = null,
        string? startFrameImagePath = null,
        string? continueFromVideoPath = null)
    {
        var provider = SupportedModelCatalog.ResolveOrDefault(model, ModelCapability.Video).Provider;
        if (provider == ModelProviderFamily.Google)
        {
            var id = await _gemini.SubmitGenerationAsync(
                prompt, durationSeconds, resolution, model, ct,
                referenceImagePaths, startFrameImagePath, continueFromVideoPath).ConfigureAwait(false);
            return GeminiPrefix + id;
        }

        var grokId = await _grok.SubmitGenerationAsync(
            prompt, durationSeconds, resolution, model, ct,
            referenceImagePaths, startFrameImagePath, continueFromVideoPath).ConfigureAwait(false);
        return GrokPrefix + grokId;
    }

    public Task<string> PollForVideoUrlAsync(string requestId, Action<string>? onProgress, CancellationToken ct)
    {
        var (client, id) = Resolve(requestId);
        return client.PollForVideoUrlAsync(id, onProgress, ct);
    }

    public Task DownloadToFileAsync(string url, string destPath, CancellationToken ct)
    {
        // The URL returned by PollForVideoUrlAsync already tells us nothing about provider —
        // both providers' download implementations only differ in auth header, and each one's
        // EnsureAuth() is a no-op if its own env key isn't set, so trying Grok's client first
        // and falling back to Gemini's on failure is safe and avoids needing a third id tag.
        return DownloadWithFallbackAsync(url, destPath, ct);
    }

    private async Task DownloadWithFallbackAsync(string url, string destPath, CancellationToken ct)
    {
        try
        {
            await _grok.DownloadToFileAsync(url, destPath, ct).ConfigureAwait(false);
        }
        catch when (_gemini.IsConfigured)
        {
            await _gemini.DownloadToFileAsync(url, destPath, ct).ConfigureAwait(false);
        }
    }

    private (IVideoClient Client, string Id) Resolve(string requestId)
    {
        var (provider, id) = ParseTaggedRequestId(requestId);
        return provider == ModelProviderFamily.Google ? (_gemini, id) : (_grok, id);
    }

    /// <summary>
    /// Splits a dispatcher-tagged request id back into (provider, original id). Untagged ids
    /// (e.g. held by a caller from before this dispatcher existed) are treated as Grok's, since
    /// Grok ids never contained a colon prefix. Public so tests can exercise the tagging
    /// round-trip without constructing the full client graph.
    /// </summary>
    public static (ModelProviderFamily Provider, string Id) ParseTaggedRequestId(string requestId)
    {
        if (requestId.StartsWith(GeminiPrefix, StringComparison.Ordinal))
            return (ModelProviderFamily.Google, requestId[GeminiPrefix.Length..]);
        if (requestId.StartsWith(GrokPrefix, StringComparison.Ordinal))
            return (ModelProviderFamily.Xai, requestId[GrokPrefix.Length..]);
        return (ModelProviderFamily.Xai, requestId);
    }
}
