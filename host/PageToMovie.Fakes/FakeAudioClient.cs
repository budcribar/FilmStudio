using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Fakes;

public sealed class FakeAudioClient : IAudioClient
{
    private readonly ILogger<FakeAudioClient> _log;

    public FakeAudioClient(ILogger<FakeAudioClient> log) => _log = log;

    public bool IsConfigured => true;

    public Task<byte[]> GenerateMusicTrackAsync(
        string prompt,
        int durationSeconds,
        string? model = null,
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake audio generate duration={Duration}s", durationSeconds);
        return Task.FromResult(Array.Empty<byte>());
    }
}
