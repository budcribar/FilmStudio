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
        Action<string>? onProgress = null)
    {
        _log.LogInformation("Fake audio generate duration={Duration}s", durationSeconds);
        var fixturePath = ResolveFixturePath();
        return Task.FromResult<string?>("fixture:" + fixturePath);
    }

    private static string ResolveFixturePath()
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "Fixtures", "music_tiny_2s.wav");
        if (File.Exists(path)) return path;
        return path; // path for error message if genuinely missing
    }
}
