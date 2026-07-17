namespace FilmStudio.Engine.Abstractions;

/// <summary>xAI (or fake) video generate / poll / download.</summary>
public interface IGrokVideoClient
{
    bool IsConfigured { get; }

    Task<string> SubmitGenerationAsync(
        string prompt,
        int durationSeconds,
        string resolution,
        string model,
        CancellationToken ct,
        IReadOnlyList<string>? referenceImagePaths = null);

    Task<string> PollForVideoUrlAsync(
        string requestId,
        Action<string>? onProgress,
        CancellationToken ct);

    Task DownloadToFileAsync(string url, string destPath, CancellationToken ct);
}

/// <summary>xAI (or fake) image generate / edit.</summary>
public interface IGrokImageClient
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<byte[]>> GenerateVariantsAsync(
        string prompt,
        int n = 3,
        string aspectRatio = "1:1",
        string? model = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<byte[]>> EditVariantsAsync(
        string prompt,
        IReadOnlyList<string> referenceImagePaths,
        int n = 3,
        string aspectRatio = "1:1",
        string? model = null,
        int maxRefs = 0,
        Action<string>? onProgress = null,
        CancellationToken ct = default);
}

/// <summary>xAI (or fake) chat completions.</summary>
public interface IGrokChatClient
{
    bool IsConfigured { get; }

    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        string model = "grok-4.5",
        double temperature = 0.2,
        CancellationToken ct = default);
}

/// <summary>xAI (or fake) vision (transcribe / classify).</summary>
public interface IGrokVisionClient
{
    bool IsConfigured { get; }

    Task<string> TranscribePageAsync(
        string imagePath,
        int page,
        string model = "grok-4.5",
        CancellationToken ct = default);

    Task<CharacterPageClassification> ClassifyCharactersOnImageAsync(
        string imagePath,
        int page,
        IReadOnlyList<CharacterClassifyHint> cast,
        string model = "grok-4.5",
        CancellationToken ct = default);
}
