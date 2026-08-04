using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// HealSceneCastFromClips unions each clip's on-screen cast into the scene cast, so a model that
/// under-lists the scene cast (e.g. omitting the lead) does not hard-fail the clip⊆scene validation.
/// Reproduces the Mary4 shot-plan block: clips list Character_Lamb / Character_Mary, scene cast omitted them.
/// </summary>
public sealed class Stage2SceneCastHealTests
{
    [Fact]
    public void Heal_unions_clip_cast_into_scene_cast()
    {
        var plan = new Dictionary<string, object?>
        {
            ["scenes"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["scene_number"] = 1,
                    ["characters_on_screen"] = new List<object?> { "Character_Teacher" }, // lead omitted by model
                    ["veo_clips"] = new List<object?>
                    {
                        new Dictionary<string, object?>
                        {
                            ["clip_number"] = 1,
                            ["characters_on_screen"] = new List<object?> { "Character_Mary", "Character_Lamb" },
                        },
                    },
                },
            },
        };

        Stage2PlannerService.HealSceneCastFromClips(plan);

        var scene = (Dictionary<string, object?>)((List<object?>)plan["scenes"]!)[0]!;
        var sceneCast = ((List<object?>)scene["characters_on_screen"]!).Select(x => x?.ToString()).ToList();
        Assert.Contains("Character_Mary", sceneCast);
        Assert.Contains("Character_Lamb", sceneCast);
        Assert.Contains("Character_Teacher", sceneCast); // original retained
    }

    [Fact]
    public void Heal_leaves_consistent_plan_unchanged()
    {
        var plan = new Dictionary<string, object?>
        {
            ["scenes"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["scene_number"] = 1,
                    ["characters_on_screen"] = new List<object?> { "Character_Mary" },
                    ["veo_clips"] = new List<object?>
                    {
                        new Dictionary<string, object?>
                        {
                            ["clip_number"] = 1,
                            ["characters_on_screen"] = new List<object?> { "Character_Mary" },
                        },
                    },
                },
            },
        };

        Stage2PlannerService.HealSceneCastFromClips(plan);

        var scene = (Dictionary<string, object?>)((List<object?>)plan["scenes"]!)[0]!;
        var sceneCast = ((List<object?>)scene["characters_on_screen"]!).Select(x => x?.ToString()).ToList();
        Assert.Single(sceneCast);
        Assert.Equal("Character_Mary", sceneCast[0]);
    }
}
