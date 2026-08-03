namespace PageToMovie.Adaptation.Contracts;

/// <summary>
/// Pure Stage‑1 convert inputs. No projectId, paths, or userId — Engine supplies book text
/// and injects <see cref="PageToMovie.Core.Abstractions.IChatClient"/>.
/// </summary>
public sealed class AdaptationRequest
{
    public required string BookText { get; init; }
    public string? Title { get; init; }
    public string? Author { get; init; }

    /// <summary>Null → use natural runtime from density.</summary>
    public int? TargetRuntimeMinutes { get; init; }

    /// <summary>Planning model id from catalog (caller resolved).</summary>
    public string ModelId { get; init; } = "";

    public double Temperature { get; init; } = 0.2;

    /// <summary>Provider-neutral reasoning hint (low/medium/high/max); null = provider default.</summary>
    public string? ReasoningEffort { get; init; }
}
