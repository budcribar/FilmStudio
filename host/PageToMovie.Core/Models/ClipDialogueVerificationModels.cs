using System;

namespace PageToMovie.Core.Models;

/// <summary>
/// Result model for automated clip dialogue & character speaker verification.
/// </summary>
public sealed class ClipDialogueVerificationResult
{
    public int SceneNumber { get; set; }
    public int ClipNumber { get; set; }
    public string ExpectedSpeaker { get; set; } = "";
    public string ExpectedDialogue { get; set; } = "";
    public string? DetectedSpeaker { get; set; }
    public string? TranscribedDialogue { get; set; }

    /// <summary>Dialogue match similarity score (0.0 to 1.0).</summary>
    public double DialogueAccuracyScore { get; set; }

    /// <summary>True if detected speaker matches expected speaker plate identity.</summary>
    public bool SpeakerMatch { get; set; }

    /// <summary>Status: verified, mismatch, speaker_swap, no_speech.</summary>
    public string Status { get; set; } = "verified";

    /// <summary>Summary notes from multimodal AI evaluation.</summary>
    public string SummaryNote { get; set; } = "";

    /// <summary>Estimated / planned clip duration (seconds).</summary>
    public double EstimatedDurationSeconds { get; set; }

    /// <summary>Estimated speech dialogue time (seconds).</summary>
    public double SpeechDurationSeconds { get; set; }

    /// <summary>Estimated action & camera movement overhead time (seconds).</summary>
    public double ActionDurationSeconds { get; set; }

    /// <summary>Actual measured clip MP4 duration (seconds).</summary>
    public double ActualDurationSeconds { get; set; }

    /// <summary>True if spoken dialogue was cut off mid-delivery.</summary>
    public bool SpeechTruncated { get; set; }

    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}
