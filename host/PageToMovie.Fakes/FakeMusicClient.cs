using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Fakes;

public sealed class FakeMusicClient : IMusicClient
{
    private readonly ILogger<FakeMusicClient> _log;

    public FakeMusicClient(ILogger<FakeMusicClient> log) => _log = log;

    public bool IsConfigured => true;

    public Task<byte[]?> GenerateMusicTrackAsync(
        string prompt,
        double durationSeconds,
        string? model = null,
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake music generate duration={Duration}s", durationSeconds);
        return Task.FromResult<byte[]?>(Array.Empty<byte>());
    }
}
