using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

/// <summary>
/// Routes <see cref="IAudioClient"/> calls to the right concrete provider client based on the
/// requested <c>model</c>'s provider in <see cref="SupportedModelCatalog"/>. Simpler than
/// <see cref="MultiProviderVideoClient"/>: audio generation is one call (no separate submit/poll
/// interface methods, no request-id routing needed), so this just resolves the provider up front
/// and delegates the whole call.
/// </summary>
public sealed class MultiProviderAudioClient : IAudioClient
{
    private readonly FalAudioClient _fal;
    private readonly SunoClient _suno;
    private readonly AiMusicApiClient _aiMusicApi;

    public MultiProviderAudioClient(FalAudioClient fal, SunoClient suno, AiMusicApiClient aiMusicApi)
    {
        _fal = fal;
        _suno = suno;
        _aiMusicApi = aiMusicApi;
    }

    /// <summary>True when at least one provider has an API key configured.</summary>
    public bool IsConfigured => _fal.IsConfigured || _suno.IsConfigured || _aiMusicApi.IsConfigured;

    public Task<string?> GenerateMusicTrackAsync(
        string prompt,
        int durationSeconds,
        string? model = null,
        CancellationToken ct = default,
        Action<string>? onProgress = null)
    {
        var provider = SupportedModelCatalog.ResolveOrDefault(model, ModelCapability.Audio, "fal-ai/stable-audio").Provider;
        IAudioClient client = provider switch
        {
            ModelProviderFamily.Suno => _suno,
            ModelProviderFamily.AiMusicApi => _aiMusicApi,
            _ => _fal,
        };
        return client.GenerateMusicTrackAsync(prompt, durationSeconds, model, ct, onProgress);
    }
}
