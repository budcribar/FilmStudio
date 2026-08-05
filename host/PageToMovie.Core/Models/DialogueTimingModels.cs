using System;
using System.Collections.Generic;

namespace PageToMovie.Core.Models;

/// <summary>
/// One reviewed dialogue line: the script (blueprint/fountain) text paired with what speech-to-text
/// actually heard in the matched speech window, plus per-word timings. Lets a reviewer see where the
/// script, the STT, and the window delineation disagree — and (Phase 2) correct whichever is wrong.
/// </summary>
public sealed class DialogueTimingRow
{
    public int Clip { get; set; }
    public string Speaker { get; set; } = "";

    /// <summary>Expected line text from the blueprint (what the script says).</summary>
    public string ScriptText { get; set; } = "";

    /// <summary>What Scribe (STT) transcribed for the matched window (empty when no window matched).</summary>
    public string HeardText { get; set; } = "";

    /// <summary>Matched speech window in the stitched scene timeline (0 = no window).</summary>
    public double WindowStartSec { get; set; }
    public double WindowEndSec { get; set; }

    /// <summary>Word overlap between script and heard text, 0..1 (a quick "how well do they agree").</summary>
    public double MatchScore { get; set; }

    /// <summary>Per-word STT timings, 0-based within the window.</summary>
    public List<VoiceCaptureWord> Words { get; set; } = new();

    public double DurationSec => Math.Max(0, WindowEndSec - WindowStartSec);
}

/// <summary>All reviewed dialogue rows for one scene, in script order.</summary>
public sealed class DialogueTimingScene
{
    public int Scene { get; set; }
    public double SceneDurationSec { get; set; }
    public List<DialogueTimingRow> Rows { get; set; } = new();
}

/// <summary>
/// Per-project cached dialogue-timing review across scenes, saved at
/// <c>assets/alignment/dialogue_timing.json</c>. Computed once per scene (STT is a paid pass), then
/// the review page reads from here; scenes are analyzed and saved independently.
/// </summary>
public sealed class DialogueTimingDoc
{
    public string SchemaVersion { get; set; } = "dialogue_timing.v1";
    public string ProjectId { get; set; } = "";
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public List<DialogueTimingScene> Scenes { get; set; } = new();
}
