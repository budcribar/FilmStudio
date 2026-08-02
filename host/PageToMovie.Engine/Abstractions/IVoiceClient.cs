namespace PageToMovie.Engine.Abstractions;

/// <summary>
/// Voice clone + TTS (ElevenLabs today). Sample audio stays project-local;
/// provider returns a reusable <see cref="VoiceCloneResult.ProviderVoiceId"/>.
/// </summary>
public interface IVoiceClient
{
    bool IsConfigured { get; }

    /// <summary>Provider id for cost / settings (e.g. elevenlabs).</summary>
    string ProviderId { get; }

    /// <summary>Create or refresh a clone from sample bytes (wav/mp3/webm).</summary>
    Task<VoiceCloneResult> CreateCloneAsync(
        string displayName,
        byte[] sampleAudio,
        string sampleFileName,
        CancellationToken ct = default);

    /// <summary>Synthesize speech with a provider voice id.</summary>
    Task<VoiceTtsResult> TextToSpeechAsync(
        string providerVoiceId,
        string text,
        string? modelId = null,
        CancellationToken ct = default);

    /// <summary>List available voices (premade + cloned when key present).</summary>
    Task<IReadOnlyList<VoiceCatalogEntry>> ListVoicesAsync(CancellationToken ct = default);
}

public sealed class VoiceCloneResult
{
    public bool Ok { get; init; }
    public string? ProviderVoiceId { get; init; }
    public string? DisplayName { get; init; }
    public string? Error { get; init; }
    public bool UsedMock { get; init; }
}

public sealed class VoiceTtsResult
{
    public bool Ok { get; init; }
    public byte[]? AudioBytes { get; init; }
    public string ContentType { get; init; } = "audio/mpeg";
    public string? FileExtension { get; init; } = ".mp3";
    public string? Error { get; init; }
    public bool UsedMock { get; init; }
}

public sealed class VoiceCatalogEntry
{
    public string ProviderVoiceId { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Category { get; init; }
    public string? PreviewUrl { get; init; }
    public bool IsCloned { get; init; }
}
