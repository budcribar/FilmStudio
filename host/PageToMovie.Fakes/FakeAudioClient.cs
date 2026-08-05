using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Fakes;

public sealed class FakeAudioClient : IAudioClient
{
    private readonly ILogger<FakeAudioClient> _log;

    public FakeAudioClient(ILogger<FakeAudioClient> log) => _log = log;

    public bool IsConfigured => true;

    public Task<string?> GenerateMusicTrackAsync(
        string prompt,
        int durationSeconds,
        string? model = null,
        CancellationToken ct = default,
        Action<string>? onProgress = null,
        bool isVocal = false,
        string? lyrics = null)
    {
        ValidateVocalRequest(model, isVocal);
        _log.LogInformation(
            "Fake audio generate model={Model} duration={Duration}s vocal={IsVocal}",
            model ?? "(default)", durationSeconds, isVocal);
        var fixturePath = ResolveFixturePath();
        return Task.FromResult<string?>("fixture:" + fixturePath);
    }

    /// <summary>
    /// Catalog <see cref="SupportedModelEntry.SupportsVocals"/> only — no provider-id heuristic.
    /// </summary>
    public static void ValidateVocalRequest(string? model, bool isVocal)
    {
        if (!isVocal)
            return;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException(
                "Fake audio: vocal generation requires an audio model id with supportsVocals=true.");

        var entry = SupportedModelCatalog.Find(model.Trim(), ModelCapability.Audio);
        if (entry is null)
            throw new InvalidOperationException(
                $"Fake audio: model '{model}' is not in the catalog as Audio.");

        if (!entry.SupportsVocals)
        {
            throw new InvalidOperationException(
                $"Fake audio: model '{entry.Id}' has no vocal/sing capability (supportsVocals=false).");
        }
    }

    private static string ResolveFixturePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "Fixtures", "music_tiny_2s.wav");
        if (File.Exists(path)) return path;
        return path; // path for error message if genuinely missing
    }
}
