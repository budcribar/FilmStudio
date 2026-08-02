using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Fakes;

public sealed class FakeLipSyncClient : ILipSyncClient
{
    private readonly ILogger<FakeLipSyncClient> _log;

    public FakeLipSyncClient(ILogger<FakeLipSyncClient> log) => _log = log;

    public bool IsConfigured => true;

    public Task<string?> GenerateLipSyncAsync(
        string videoPath,
        string audioPath,
        string? model = null,
        string syncMode = "cut_off",
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake lip-sync video={Video} audio={Audio} syncMode={SyncMode}", videoPath, audioPath, syncMode);
        onProgress?.Invoke("Fake lip-sync complete");
        var fixturePath = FakeGrokVideoClient.ResolveFixturePath("LoadLight", 2);
        return Task.FromResult<string?>("fixture:" + fixturePath);
    }
}
