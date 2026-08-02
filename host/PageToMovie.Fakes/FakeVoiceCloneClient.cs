using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Fakes;

public sealed class FakeVoiceCloneClient : IVoiceCloneClient
{
    private readonly ILogger<FakeVoiceCloneClient> _log;

    public FakeVoiceCloneClient(ILogger<FakeVoiceCloneClient> log) => _log = log;

    public bool IsConfigured => true;

    public Task<string?> CloneVoiceAsync(
        string sampleAudioPath,
        string? model = null,
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake voice clone sample={Sample}", sampleAudioPath);
        return Task.FromResult<string?>("fake-voice-" + Guid.NewGuid().ToString("N")[..12]);
    }

    public Task<string?> SynthesizeSpeechAsync(
        string text,
        string voiceId,
        string? model = null,
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake speech synthesis voiceId={VoiceId} textLen={Len}", voiceId, text?.Length ?? 0);
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "music_tiny_2s.wav");
        return Task.FromResult<string?>("fixture:" + fixturePath);
    }
}
