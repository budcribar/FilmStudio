using System;
using System.Collections.Generic;

namespace PageToMovie.Core.Models;

/// <summary>
/// Where a speech segment's start/end timestamps came from. The product default is
/// <see cref="Silence"/> — client-side ffmpeg silence detection, which is local and free.
/// STT/transcript word alignment is an optional, paid enhancement that can plug in later
/// without changing the persisted shape.
/// </summary>
public static class SpeechTimestampSource
{
    /// <summary>Client ffmpeg <c>silencedetect</c> non-silent windows (primary, free).</summary>
    public const string Silence = "silence";

    /// <summary>Speech-to-text word/segment timestamps (optional, paid). Not wired by default.</summary>
    public const string Transcript = "transcript";

    /// <summary>Duration-model estimate — no detection has run yet (placeholder).</summary>
    public const string Estimate = "estimate";

    /// <summary>Operator hand-edit.</summary>
    public const string Manual = "manual";

    public static bool IsDetected(string? source) =>
        string.Equals(source, Silence, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(source, Transcript, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(source, Manual, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// One spoken line inside a clip: which character says it, the (blueprint-known) text, when it
/// happens in the clip, where the timestamps came from, and — once synthesized — the relative
/// path of the cloned-voice audio that should replace it.
/// </summary>
public sealed class SpeechSegment
{
    /// <summary>Ordinal of this line within the clip (0-based), stable across runs.</summary>
    public int Index { get; set; }

    /// <summary>Blueprint character key that speaks this line (e.g. Character_Narrator).</summary>
    public string CharacterKey { get; set; } = "";

    /// <summary>Expected spoken dialogue (from the blueprint, not guessed from audio).</summary>
    public string DialogueText { get; set; } = "";

    /// <summary>Segment start, seconds into the clip.</summary>
    public double StartSec { get; set; }

    /// <summary>Segment end, seconds into the clip.</summary>
    public double EndSec { get; set; }

    /// <summary>One of <see cref="SpeechTimestampSource"/>.</summary>
    public string Source { get; set; } = SpeechTimestampSource.Estimate;

    /// <summary>
    /// Project-relative path of the cloned-voice TTS rendered for this line, once the server job
    /// has produced it (e.g. assets/audio/revoice/scene_01_clip_02_seg_00.mp3). Null until synthesized.
    /// </summary>
    public string? VoiceAudioRelativePath { get; set; }

    public double DurationSec => Math.Max(0, EndSec - StartSec);
}

/// <summary>Per-clip alignment: the ordered speech segments detected/known for one clip.</summary>
public sealed class ClipSpeechAlignment
{
    public int Scene { get; set; }
    public int Clip { get; set; }

    /// <summary>Measured clip length (seconds) when known — used to clamp/fit segments.</summary>
    public double ClipDurationSeconds { get; set; }

    public List<SpeechSegment> Segments { get; set; } = new();

    /// <summary>True when every segment has real (non-estimate) detected timestamps.</summary>
    public bool IsDetected =>
        Segments.Count > 0 && Segments.TrueForAll(s => SpeechTimestampSource.IsDetected(s.Source));
}

/// <summary>
/// Persisted, per-project voice-substitution alignment. Lives at
/// <c>assets/alignment/voice_alignment.json</c> so it travels with the project on export/import.
/// Records, per clip, which character says what and when, plus which cloned-voice audio replaces it.
/// A re-run reuses this file's detected timestamps and skips re-detection.
/// </summary>
public sealed class ProjectVoiceAlignment
{
    public string SchemaVersion { get; set; } = "voice_alignment.v1";
    public string ProjectId { get; set; } = "";

    /// <summary>Character whose voice this alignment was last built for (informational).</summary>
    public string? CharKey { get; set; }

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    public List<ClipSpeechAlignment> Clips { get; set; } = new();

    public ClipSpeechAlignment? Find(int scene, int clip) =>
        Clips.Find(c => c.Scene == scene && c.Clip == clip);
}

/// <summary>
/// Server-side, movie-wide "substitute my cloned voice" job request. Reuses the same voice
/// machinery as speak-batch, but walks every clip in the movie, associates each dialogue line
/// with its speaker, synthesizes cloned-voice TTS per line, and maintains the persisted alignment.
/// </summary>
public sealed class StartVoiceSubstitutionRequest
{
    public string ProjectId { get; set; } = "";

    /// <summary>Character seed that owns the clone whose voice is being substituted in.</summary>
    public string CharKey { get; set; } = "Character_Narrator";

    /// <summary>
    /// When true (default), only replace lines spoken by <see cref="CharKey"/> (or narrator).
    /// When false, every dialogue line in the movie is re-voiced with <see cref="CharKey"/>'s clone.
    /// </summary>
    public bool NarratorOnly { get; set; } = true;

    /// <summary>Skip lines whose cloned-voice audio already exists on disk.</summary>
    public bool OnlyMissing { get; set; } = true;

    /// <summary>Max concurrent TTS provider calls (clamped 1–8). Default 3.</summary>
    public int MaxParallel { get; set; } = 3;

    /// <summary>Optional catalog speak-model id override; empty uses project voice model / seed provider.</summary>
    public string? Model { get; set; }

    public bool FailIfLocked { get; set; }
}

/// <summary>
/// Client → server payload: the raw non-silent windows a browser detected for one clip via ffmpeg
/// silence detection. The server matches these windows onto the clip's known dialogue lines (single
/// source of the matching logic) and persists the result into <see cref="ProjectVoiceAlignment"/> so
/// future runs skip re-detection.
/// </summary>
public sealed class ClipTimestampUpdate
{
    public int Scene { get; set; }
    public int Clip { get; set; }
    public double ClipDurationSeconds { get; set; }

    /// <summary>Detected non-silent (speech) windows in clip time, in order.</summary>
    public List<SpeechWindow> Windows { get; set; } = new();
}

/// <summary>One detected non-silent window in clip time.</summary>
public sealed class SpeechWindow
{
    public double StartSec { get; set; }
    public double EndSec { get; set; }
}
