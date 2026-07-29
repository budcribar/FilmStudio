namespace PageToMovie.Engine.Abstractions;

/// <summary>
/// Model-agnostic client for background music generation (Fal.ai Stable Audio, etc.).
/// </summary>
public interface IMusicClient
{
    bool IsConfigured { get; }

    Task<byte[]?> GenerateMusicTrackAsync(
        string prompt,
        double durationSeconds,
        string? model = null,
        CancellationToken ct = default);
}
