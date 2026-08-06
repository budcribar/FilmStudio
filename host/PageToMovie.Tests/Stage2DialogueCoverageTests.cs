using System.Collections.Generic;
using System.Linq;
using PageToMovie.Engine;
using PageToMovie.Engine.ModelExecution;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// The Stage-2 middle-layer guard: every spoken line in the approved screenplay must survive shot
/// planning into some clip's <c>audio_payload</c>. These lock the detector against the ways a line
/// can be silenced (a clip that exists but says nothing) or dropped (no clip at all), while proving
/// it does NOT false-positive on sanitization or dialogue split across clips.
/// </summary>
public class Stage2DialogueCoverageTests
{
    // --- tiny builders for the Dictionary/List shapes GetScenes/GetList require ---
    private static List<object?> L(params object?[] items) => items.ToList();

    private static Dictionary<string, object?> Beat(string id, string speaker, string dialogue) => new()
    {
        ["beat_id"] = id,
        ["speaker"] = speaker,
        ["dialogue"] = dialogue,
    };

    private static Dictionary<string, object?> Stage1Scene(int n, params Dictionary<string, object?>[] beats) => new()
    {
        ["scene_number"] = n,
        ["story_beats"] = beats.Cast<object?>().ToList(),
    };

    private static Dictionary<string, object?> Clip(string beatId, string speaker, string dialogue, string delivery = "speak")
    {
        var ap = new Dictionary<string, object?>
        {
            ["delivery"] = delivery,
            ["speaker"] = speaker,
            // The plan always stores speech-safe text — mirror that so the test exercises parity.
            ["dialogue"] = ClipVideoPromptBuilder.SanitizeSpokenDialogue(dialogue),
        };
        return new Dictionary<string, object?>
        {
            ["stage1_beat_id"] = beatId,
            ["audio_payload"] = ap,
        };
    }

    private static Dictionary<string, object?> PlanScene(int n, params Dictionary<string, object?>[] clips) => new()
    {
        ["scene_number"] = n,
        ["veo_clips"] = clips.Cast<object?>().ToList(),
    };

    private static Dictionary<string, object?> Wrap(string key, params Dictionary<string, object?>[] scenes) => new()
    {
        [key] = scenes.Cast<object?>().ToList(),
    };

    private static Dictionary<string, object?> Plan(params Dictionary<string, object?>[] scenes)
    {
        var p = Wrap("scenes", scenes);
        p["stage2_meta"] = new Dictionary<string, object?>();
        return p;
    }

    [Fact]
    public void All_lines_covered_reports_no_gaps()
    {
        var stage1 = Wrap("scenes",
            Stage1Scene(1, Beat("b1", "NARRATOR", "Mary had a little lamb."),
                            Beat("b2", "TEACHER", "You cannot bring that here.")));
        var plan = Plan(
            PlanScene(1, Clip("b1", "NARRATOR", "Mary had a little lamb."),
                         Clip("b2", "TEACHER", "You cannot bring that here.")));

        var r = Stage2DialogueCoverage.Verify(stage1, plan);

        Assert.Equal(2, r.ExpectedLines);
        Assert.Equal(2, r.CoveredLines);
        Assert.False(r.HasGaps);
        Assert.Empty(r.Issues);
    }

    [Fact]
    public void Silenced_clip_line_is_flagged_as_present_but_unspoken()
    {
        // The clip for b2 exists but its audio is delivery:"none" → it says nothing.
        var stage1 = Wrap("scenes",
            Stage1Scene(1, Beat("b1", "NARRATOR", "Mary had a little lamb."),
                            Beat("b2", "TEACHER", "You cannot bring that here.")));
        var plan = Plan(
            PlanScene(1, Clip("b1", "NARRATOR", "Mary had a little lamb."),
                         Clip("b2", "TEACHER", "You cannot bring that here.", delivery: "none")));

        var r = Stage2DialogueCoverage.Verify(stage1, plan);

        var gap = Assert.Single(r.Gaps);
        Assert.Equal(1, gap.Scene);
        Assert.Equal("b2", gap.BeatId);
        Assert.Equal("beat_present_but_unspoken", gap.Diagnosis);
        Assert.Equal(ModelValidationSeverity.Warning, Assert.Single(r.Issues).Severity);
    }

    [Fact]
    public void Beat_with_no_clip_is_flagged_as_absent_from_plan()
    {
        // The teacher's last line's beat never became a clip at all.
        var stage1 = Wrap("scenes",
            Stage1Scene(1, Beat("b1", "NARRATOR", "Mary had a little lamb."),
                            Beat("b2", "TEACHER", "Out you go, little lamb.")));
        var plan = Plan(
            PlanScene(1, Clip("b1", "NARRATOR", "Mary had a little lamb.")));

        var r = Stage2DialogueCoverage.Verify(stage1, plan);

        var gap = Assert.Single(r.Gaps);
        Assert.Equal("b2", gap.BeatId);
        Assert.Equal("beat_absent_from_plan", gap.Diagnosis);
    }

    [Fact]
    public void Sanitization_and_punctuation_do_not_cause_false_gaps()
    {
        // Raw screenplay line vs the speech-safe text stored in the plan must still match.
        var raw = "It's 100% true — she said, \"Don't!\"";
        var stage1 = Wrap("scenes", Stage1Scene(1, Beat("b1", "GIRL", raw)));
        var plan = Plan(PlanScene(1, Clip("b1", "GIRL", raw)));

        var r = Stage2DialogueCoverage.Verify(stage1, plan);

        Assert.Equal(1, r.CoveredLines);
        Assert.False(r.HasGaps);
    }

    [Fact]
    public void Two_hander_secondary_line_counts_as_covered()
    {
        var stage1 = Wrap("scenes",
            Stage1Scene(1, Beat("b1", "MARY", "Please let him stay."),
                            Beat("b2", "TEACHER", "Absolutely not.")));
        // Both lines coalesced onto one two-hander clip (primary + secondary in one audio_payload).
        var ap = new Dictionary<string, object?>
        {
            ["delivery"] = "speak",
            ["speaker"] = "MARY",
            ["dialogue"] = ClipVideoPromptBuilder.SanitizeSpokenDialogue("Please let him stay."),
            ["secondary_speaker"] = "TEACHER",
            ["secondary_dialogue"] = ClipVideoPromptBuilder.SanitizeSpokenDialogue("Absolutely not."),
        };
        var clip = new Dictionary<string, object?> { ["stage1_beat_id"] = "b1", ["audio_payload"] = ap };
        var plan = Plan(PlanScene(1, clip));

        var r = Stage2DialogueCoverage.Verify(stage1, plan);

        Assert.Equal(2, r.ExpectedLines);
        Assert.Equal(2, r.CoveredLines);
        Assert.False(r.HasGaps);
    }

    [Fact]
    public void Line_split_across_two_clips_in_the_scene_is_covered()
    {
        // One screenplay line whose delivery was split across two clips still counts (scene-level blob).
        var stage1 = Wrap("scenes",
            Stage1Scene(1, Beat("b1", "NARRATOR", "Everywhere that Mary went the lamb was sure to go.")));
        var plan = Plan(
            PlanScene(1, Clip("b1", "NARRATOR", "Everywhere that Mary went"),
                         Clip("b1", "NARRATOR", "the lamb was sure to go.")));

        var r = Stage2DialogueCoverage.Verify(stage1, plan);

        Assert.Equal(1, r.CoveredLines);
        Assert.False(r.HasGaps);
    }

    [Fact]
    public void Verify_does_not_mutate_its_inputs()
    {
        var stage1 = Wrap("scenes", Stage1Scene(1, Beat("b1", "NARRATOR", "A line.")));
        var plan = Plan(PlanScene(1, Clip("b1", "NARRATOR", "A different unmatched thing.", delivery: "none")));

        var scene0 = (Dictionary<string, object?>)((List<object?>)stage1["scenes"]!)[0]!;
        var beat = (Dictionary<string, object?>)((List<object?>)scene0["story_beats"]!)[0]!;

        var r = Stage2DialogueCoverage.Verify(stage1, plan);

        Assert.True(r.HasGaps); // sanity: this scenario is a real gap
        Assert.Equal("A line.", beat["dialogue"]); // input untouched
    }
}
