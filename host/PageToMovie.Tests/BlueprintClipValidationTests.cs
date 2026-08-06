using System.Text.Json;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class BlueprintClipValidationTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void CleanPlan_HasNoDuplicates()
    {
        var root = Parse("""
        { "scenes": [
            { "scene_number": 1, "veo_clips": [ { "clip_number": 1 }, { "clip_number": 2 } ] },
            { "scene_number": 2, "veo_clips": [ { "clip_number": 1 } ] }
        ] }
        """);

        Assert.Empty(BlueprintClipValidation.FindDuplicateClipNumbers(root));
        Assert.Null(BlueprintClipValidation.DescribeDuplicates(root));
    }

    [Fact]
    public void DuplicateClipNumber_IsReportedPerScene()
    {
        // Scene 1 has clip 2 twice (the "doubled scene" fault); scene 2 is clean.
        var root = Parse("""
        { "scenes": [
            { "scene_number": 1, "veo_clips": [ { "clip_number": 1 }, { "clip_number": 2 }, { "clip_number": 2 } ] },
            { "scene_number": 2, "veo_clips": [ { "clip_number": 1 } ] }
        ] }
        """);

        var dups = BlueprintClipValidation.FindDuplicateClipNumbers(root);
        Assert.Single(dups);
        Assert.Equal((1, 2), dups[0]);
        Assert.Equal("scene 1 clip 2", BlueprintClipValidation.DescribeDuplicates(root));
    }

    [Fact]
    public void WholeSceneDuplicated_ReportsEachRepeatedClip()
    {
        // The exact failure seen in the wild: the entire clip set appears twice.
        var root = Parse("""
        { "scenes": [
            { "scene_number": 3, "veo_clips": [
                { "clip_number": 1 }, { "clip_number": 2 }, { "clip_number": 3 },
                { "clip_number": 1 }, { "clip_number": 2 }, { "clip_number": 3 } ] }
        ] }
        """);

        var dups = BlueprintClipValidation.FindDuplicateClipNumbers(root);
        Assert.Equal(3, dups.Count);
        Assert.Contains((3, 1), dups);
        Assert.Contains((3, 2), dups);
        Assert.Contains((3, 3), dups);
    }

    [Fact]
    public void MalformedOrEmpty_DoesNotThrow()
    {
        Assert.Empty(BlueprintClipValidation.FindDuplicateClipNumbers(Parse("{}")));
        Assert.Empty(BlueprintClipValidation.FindDuplicateClipNumbers(Parse("""{ "scenes": [] }""")));
        Assert.Empty(BlueprintClipValidation.FindDuplicateClipNumbers(Parse("""{ "scenes": [ { "scene_number": 1 } ] }""")));
    }
}
