using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PageToMovie.Engine;

/// <summary>
/// Model-agnostic music client facade that resolves the appropriate music client by model id via models_catalog.json.
/// </summary>
public sealed class MultiProviderMusicClient : IMusicClient
{
    private readonly FalMusicClient _fal;
    private readonly PageToMovieOptions _opts;
    private readonly ILogger<MultiProviderMusicClient> _log;

    public MultiProviderMusicClient(
        FalMusicClient fal,
        IOptions<PageToMovieOptions> opts,
        ILogger<MultiProviderMusicClient> log)
    {
        _fal = fal;
        _opts = opts.Value;
        _log = log;
    }

    public bool IsConfigured => _fal.IsConfigured;

    public Task<byte[]?> GenerateMusicTrackAsync(
        string prompt,
        double durationSeconds,
        string? model = null,
        CancellationToken ct = default)
    {
        var targetModel = !string.IsNullOrWhiteSpace(model)
            ? model
            : _opts.BackgroundMusicModel;

        var entry = SupportedModelCatalog.ResolveOrDefault(targetModel, ModelCapability.Audio, "fal-ai/stable-audio");
        _log.LogInformation("Generating background music ({Duration:0.#}s) using {Model} ({Provider})", durationSeconds, entry.Id, entry.Provider);

        return entry.Provider switch
        {
            _ => _fal.GenerateMusicTrackAsync(prompt, durationSeconds, entry.EndpointPath, ct)
        };
    }
}
