using System.Security.Cryptography;
using System.Text;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Fakes;

/// <summary>
/// Offline fake for <see cref="IVoiceClient"/> (ElevenLabs clone + TTS). Deterministic, no network:
/// clone returns a stable mock voice id derived from the sample bytes, TTS returns a tiny valid WAV
/// (the shared music fixture when present, else a synthesized silent clip). Swapped in under
/// PageToMovie:UseFakes so voice-clone / dialogue-TTS flows never reach ElevenLabs.
/// </summary>
public sealed class FakeVoiceClient : IVoiceClient
{
    private readonly ILogger<FakeVoiceClient> _log;

    public FakeVoiceClient(ILogger<FakeVoiceClient> log) => _log = log;

    public bool IsConfigured => true;

    public string ProviderId => "elevenlabs";

    public Task<VoiceCloneResult> CreateCloneAsync(
        string displayName,
        byte[] sampleAudio,
        string sampleFileName,
        CancellationToken ct = default)
    {
        var seed = sampleAudio is { Length: > 0 }
            ? Convert.ToHexString(SHA256.HashData(sampleAudio))[..12].ToLowerInvariant()
            : Guid.NewGuid().ToString("N")[..12];
        _log.LogInformation("Fake voice clone name={Name} voiceId=fake_{Seed}", displayName, seed);
        return Task.FromResult(new VoiceCloneResult
        {
            Ok = true,
            ProviderVoiceId = "fake_clone_" + seed,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Fake clone" : displayName.Trim(),
            UsedMock = true,
        });
    }

    public Task<VoiceTtsResult> TextToSpeechAsync(
        string providerVoiceId,
        string text,
        string? modelId = null,
        CancellationToken ct = default)
    {
        _log.LogInformation("Fake TTS voiceId={VoiceId} textLen={Len}", providerVoiceId, text?.Length ?? 0);
        return Task.FromResult(new VoiceTtsResult
        {
            Ok = true,
            AudioBytes = ResolveAudioBytes(),
            ContentType = "audio/mpeg",
            FileExtension = ".mp3",
            UsedMock = true,
        });
    }

    public Task<IReadOnlyList<VoiceCatalogEntry>> ListVoicesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<VoiceCatalogEntry> voices = new[]
        {
            new VoiceCatalogEntry { ProviderVoiceId = "fake_premade_narrator", Name = "Fake Narrator", Category = "premade" },
            new VoiceCatalogEntry { ProviderVoiceId = "fake_premade_dialogue", Name = "Fake Dialogue", Category = "premade" },
        };
        return Task.FromResult(voices);
    }

    private static byte[] ResolveAudioBytes()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "music_tiny_2s.wav");
        if (File.Exists(fixture))
        {
            try { return File.ReadAllBytes(fixture); }
            catch { /* fall through to synthesized clip */ }
        }
        return SynthesizeSilentWav();
    }

    /// <summary>Minimal valid 16-bit mono PCM WAV (~0.1s of silence) — no fixture dependency.</summary>
    private static byte[] SynthesizeSilentWav()
    {
        const int sampleRate = 8000;
        const int samples = 800; // 0.1s
        var dataBytes = samples * 2;
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataBytes);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);              // fmt chunk size
        w.Write((short)1);        // PCM
        w.Write((short)1);        // mono
        w.Write(sampleRate);
        w.Write(sampleRate * 2);  // byte rate
        w.Write((short)2);        // block align
        w.Write((short)16);       // bits per sample
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataBytes);
        w.Write(new byte[dataBytes]);
        w.Flush();
        return ms.ToArray();
    }
}
