using PageToMovie.Core.Models;

namespace PageToMovie.Engine.Abstractions;

/// <summary>
/// Provider-specific "apply sample → provider voice id + optional TTS preview" strategy.
/// Selected by <see cref="VoiceCloneApplyService"/> from the project voice model catalog entry.
/// </summary>
public interface IVoiceApplyStrategy
{
    /// <summary>Stable provider id (e.g. elevenlabs, fal).</summary>
    string ProviderId { get; }

    /// <summary>True when this strategy can execute (keys present or mock allowed).</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Whether this strategy should handle the resolved clone model.
    /// Strategies are ordered; first match wins.
    /// </summary>
    bool CanHandle(SupportedModelEntry? cloneModel);

    Task<VoiceApplyResult> ApplyAsync(VoiceApplyContext ctx, CancellationToken ct = default);
}

/// <summary>Inputs shared across all voice-apply strategies.</summary>
public sealed class VoiceApplyContext
{
    public required string ProjectId { get; init; }
    public required string CharKey { get; init; }
    public required string DisplayName { get; init; }
    /// <summary>Absolute path to voice_clone_sample.* on disk.</summary>
    public required string SamplePath { get; init; }
    public SupportedModelEntry? CloneModel { get; init; }
    public SupportedModelEntry? SpeakModel { get; init; }
    public string? PreviewText { get; init; }
    public string? VoiceLabel { get; init; }
}

/// <summary>Result of applying a clone (provider-agnostic).</summary>
public sealed class VoiceApplyResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public string? Message { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderVoiceId { get; init; }
    public string? ModelId { get; init; }
    public bool UsedMock { get; init; }
    public string? PreviewRelativePath { get; init; }
    public string? PreviewUrl { get; init; }
    public string? VoiceLabel { get; init; }
    public double? EstimatedCloneUsd { get; init; }
}
