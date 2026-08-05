using System;
using System.Collections.Generic;

namespace PageToMovie.Core.Models;

/// <summary>
/// One candidate dialogue phrase for the "listen-and-copy" voice capture: a detected narrator window,
/// Scribe-verified against the expected blueprint line. Confident phrases give both the capture
/// material and a reliable line↔window mapping the dub overlay can trust.
/// </summary>
public sealed class VoiceCapturePhrase
{
    public int Scene { get; set; }
    public int Clip { get; set; }

    /// <summary>Detected speech window in the stitched scene's timeline.</summary>
    public double WindowStartSec { get; set; }
    public double WindowEndSec { get; set; }

    /// <summary>Expected narrator line text from the blueprint.</summary>
    public string Text { get; set; } = "";

    /// <summary>What Scribe transcribed for this window (for reference / read-along).</summary>
    public string? TranscribedText { get; set; }

    /// <summary>Word-overlap between the transcript and the expected line, 0..1.</summary>
    public double MatchScore { get; set; }

    /// <summary>True when <see cref="MatchScore"/> clears the confidence threshold — the window
    /// genuinely contains this line, so it's usable both as capture material and as a trusted
    /// line↔window mapping for the overlay.</summary>
    public bool Confident { get; set; }

    /// <summary>Crude expressiveness proxy: max − mean loudness (dB) of the segment. 0 if unmeasured.</summary>
    public double DynamicRangeDb { get; set; }

    /// <summary>Selection rank among confident phrases (0 = best). -1 = not selected.</summary>
    public int Rank { get; set; } = -1;

    /// <summary>Per-word timings from Scribe (seconds relative to the window start), so the capture
    /// teleprompter can pace each word exactly like the narrator — lingering on stretched words and
    /// zipping through quick ones — instead of an even glide. Null/empty ⇒ fall back to even spacing.</summary>
    public List<VoiceCaptureWord>? Words { get; set; }

    public double DurationSec => Math.Max(0, WindowEndSec - WindowStartSec);
}

/// <summary>One transcribed word with its timing within the phrase window (seconds from window start).</summary>
public sealed class VoiceCaptureWord
{
    public string Text { get; set; } = "";
    public double StartSec { get; set; }
    public double EndSec { get; set; }
}

/// <summary>
/// Per-project cached phrase set for voice capture, computed once (STT is a once-per-book step) and
/// saved at <c>assets/voice_capture/phrases.json</c>. Holds every verified candidate plus the
/// selected pool; the capture UI and the dub overlay read from here.
/// </summary>
public sealed class VoiceCapturePhrases
{
    public string SchemaVersion { get; set; } = "voice_capture.v1";
    public string ProjectId { get; set; } = "";
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Word-overlap threshold used to mark a phrase confident (informational).</summary>
    public double ConfidenceThreshold { get; set; } = 0.7;

    public List<VoiceCapturePhrase> Phrases { get; set; } = new();
}
