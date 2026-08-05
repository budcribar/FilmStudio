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
    /// Mirrors Scenes.SelectedAudioModelCanSing: only suno / aimusicapi / elevenlabs providers
    /// may request vocals. Catalog has no supportsVocals field yet — providerId is the source of truth.
    /// </summary>
    public static void ValidateVocalRequest(string? model, bool isVocal)
    {
        if (!isVocal)
            return;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException(
                "Fake audio: vocal generation requires an audio model id (suno / aimusicapi / elevenlabs).");

        var entry = SupportedModelCatalog.Find(model.Trim(), ModelCapability.Audio);
        var provider = (entry?.ProviderId ?? entry?.Provider.ToString() ?? "").ToLowerInvariant();
        // Provider enum may stringify differently; also check id prefixes.
        var id = model.Trim().ToLowerInvariant();
        var canSing =
            provider is "suno" or "aimusicapi" or "elevenlabs" ||
            id.StartsWith("suno", StringComparison.Ordinal) ||
            id.StartsWith("aimusicapi", StringComparison.Ordinal) ||
            id.StartsWith("elevenlabs", StringComparison.Ordinal);
        if (!canSing)
        {
            throw new InvalidOperationException(
                $"Fake audio: model '{model}' has no vocal/sing capability " +
                $"(provider '{provider ?? "unknown"}'). Use suno, aimusicapi, or elevenlabs-music.");
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
