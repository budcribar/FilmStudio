using PageToMovie.Engine.ModelExecution;
using Xunit;

namespace PageToMovie.Tests;

public sealed class Stage2AggregateLifecycleTests
{
    [Fact]
    public void Recorded_partial_classifier_replay_fails_cross_references_then_accepts_repair()
    {
        var partial = RecordedPlan();
        var partialIssues = Stage2AggregateValidator.Validate(partial);

        Assert.Contains(partialIssues, issue => issue.Code == "beat_clip_mismatch");
        Assert.Contains(partialIssues, issue => issue.Code == "unknown_cast_reference");
        Assert.Contains(partialIssues, issue => issue.Code == "focus_not_on_screen");
        Assert.Contains(partialIssues, issue => issue.Code == "incomplete_dialogue_reference");
        Assert.Contains(partialIssues, issue => issue.Code == "invalid_continuity_reference");

        var scene = (Dictionary<string, object?>)((List<object?>)partial["scenes"]!)[0]!;
        scene["stage1_beat_map"] = new List<object?> { "s1_b1", "s1_b2" };
        var clips = ((List<object?>)scene["veo_clips"]!).Cast<Dictionary<string, object?>>().ToList();
        clips[0]["focus_keys"] = new List<object?> { "Character_Hero" };
        clips[1]["characters_on_screen"] = new List<object?> { "Character_Hero" };
        clips[1]["focus_keys"] = new List<object?> { "Character_Hero" };
        clips[1]["location_id"] = "Location_Home";
        clips[1]["audio_payload"] = new Dictionary<string, object?>
        {
            ["dialogue"] = "We should go.", ["speaker"] = "Character_Hero", ["delivery"] = "dialogue",
        };

        Assert.Empty(Stage2AggregateValidator.Validate(partial));
    }

    [Fact]
    public void Aggregate_provenance_records_model_fallback_and_attempts_when_exposed()
    {
        var provenance = Stage2AggregateValidator.BuildClassifierProvenance(new()
        {
            ["silent_beat"] = new Dictionary<string, object?>
            {
                ["enabled"] = true, ["model"] = "catalog-planning-model", ["ai_labels"] = 1,
                ["heuristic_fallback"] = 2, ["attempts"] = 2,
            },
            ["camera"] = null,
        });

        var silent = Assert.Single(provenance, item => item.Classifier == "silent_beat");
        Assert.Equal("mixed", silent.Source);
        Assert.Equal(2, silent.Attempts);
        Assert.Equal(1, silent.ModelResults);
        Assert.Equal(2, silent.FallbackResults);
        Assert.Equal("not_exposed", Assert.Single(provenance, item => item.Classifier == "camera").Source);
    }

    private static Dictionary<string, object?> RecordedPlan() => new()
    {
        ["stage2_meta"] = new Dictionary<string, object?>(),
        ["global_production_variables"] = new Dictionary<string, object?>
        {
            ["character_seed_tokens"] = new Dictionary<string, object?> { ["Character_Hero"] = new Dictionary<string, object?>() },
        },
        ["scenes"] = new List<object?>
        {
            new Dictionary<string, object?>
            {
                ["scene_number"] = 1,
                ["characters_on_screen"] = new List<object?> { "Character_Hero" },
                ["stage1_beat_map"] = new List<object?> { "s1_b1" },
                ["veo_clips"] = new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["clip_number"] = 1, ["location_id"] = "Location_Home",
                        ["veo_continuation_source"] = "none",
                        ["characters_on_screen"] = new List<object?> { "Character_Hero" },
                        ["focus_keys"] = new List<object?> { "Character_Missing" },
                        ["audio_payload"] = new Dictionary<string, object?>(),
                    },
                    new Dictionary<string, object?>
                    {
                        ["clip_number"] = 2, ["location_id"] = "Location_Elsewhere",
                        ["veo_continuation_source"] = "extend_previous",
                        ["characters_on_screen"] = new List<object?> { "Character_Missing" },
                        ["focus_keys"] = new List<object?> { "Character_Missing" },
                        ["audio_payload"] = new Dictionary<string, object?>
                        {
                            ["dialogue"] = "We should go.", ["speaker"] = "", ["delivery"] = "none",
                        },
                    },
                },
            },
        },
    };
}
