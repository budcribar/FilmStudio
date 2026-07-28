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

    public DateTime VerifiedAt { get; set; } = DateTime.UtcNow;
}
