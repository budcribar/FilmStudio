using System.Text.Json;
using FilmStudio.Engine;
using Xunit;

namespace FilmStudio.Tests;

public class ClipDurationEstimatorTests
{
    [Fact]
    public void Short_dialogue_is_tighter_than_8s_default()
    {
        var d = ClipDurationEstimator.Estimate(
            dialogue: "Merry Christmas, Uncle!",
            visualOrAction: "Fred grins.",
            actionClass: "dialogue");
        Assert.InRange(d, ClipDurationEstimator.MinSeconds, 6);
    }

    [Fact]
    public void Long_dialogue_gets_more_time_but_stays_capped()
    {
        var line =
            "If I could work my will, every idiot who goes about with Merry Christmas on his lips " +
            "should be boiled with his own pudding and buried with a stake of holly through his heart.";
        var d = ClipDurationEstimator.Estimate(line, "Scrooge scowls.", "dialogue");
        Assert.True(d >= 6, $"expected longer clip, got {d}");
        Assert.True(d <= ClipDurationEstimator.MaxSeconds);
    }

    [Fact]
    public void Action_only_is_not_padded_to_ten()
    {
        var d = ClipDurationEstimator.Estimate(
            dialogue: "",
            visualOrAction: "Buster runs across the grass.",
            actionClass: "action");
        Assert.InRange(d, ClipDurationEstimator.ActionOnlyMinSeconds, 8);
    }

    [Fact]
    public void Allocate_does_not_inflate_dialogue_clips_to_fill_scene_budget()
    {
        var beats = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["dialogue"] = "Hi.",
                ["visual_event"] = "Wave.",
                ["action_class"] = "dialogue",
            },
            new()
            {
                ["dialogue"] = "Bye.",
                ["visual_event"] = "Exit.",
                ["action_class"] = "dialogue",
            },
        };
        var durs = ClipDurationEstimator.AllocateForBeats(beats, sceneTargetSeconds: 40);
        Assert.All(durs, d => Assert.True(d <= 6, $"dialogue clip padded to {d}"));
    }

    [Fact]
    public void EstimateForClip_reads_audio_payload()
    {
        var clip = JsonDocument.Parse("""
            {
              "duration_seconds": 10,
              "visual_prompt": "Momma points to the door.",
              "audio_payload": {
                "speaker": "Character_Momma",
                "dialogue": "A doggy goes outside.",
                "delivery": "on_camera"
              }
            }
            """).RootElement;
        var d = ClipDurationEstimator.EstimateForClip(clip);
        Assert.True(d < 10, "should not keep inflated plan for short dialogue");
        Assert.InRange(d, ClipDurationEstimator.MinSeconds, ClipDurationEstimator.MaxSeconds);
    }
}

public class ClipSilenceTrimmerTests
{
    [Fact]
    public void ComputeCutPoint_trims_trailing_silence()
    {
        // Speech until 5.0s, then silence to 8.0s
        var log = """
            [silencedetect @ 0x] silence_start: 5.0
            """;
        var cut = ClipSilenceTrimmer.ComputeCutPoint(log, totalDuration: 8.0, keepTailSeconds: 0.35);
        Assert.NotNull(cut);
        Assert.InRange(cut!.Value, 5.2, 5.6);
    }

    [Fact]
    public void ComputeCutPoint_skips_when_no_trailing_silence()
    {
        var log = """
            [silencedetect @ 0x] silence_start: 1.0
            [silencedetect @ 0x] silence_end: 1.5
            """;
        // speech resumes and continues to end — no open trailing silence
        var cut = ClipSilenceTrimmer.ComputeCutPoint(log, totalDuration: 6.0, keepTailSeconds: 0.35);
        Assert.Null(cut);
    }
}
