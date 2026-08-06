using System.Text.Json;
using System.Text.Json.Nodes;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// ClipKeying is the single source of truth for a clip's number across every gen/cost/read site.
/// Older shot plans (and some auto-inserted credits scenes) key the clip on legacy `clip_index` with
/// no `clip_number`; the fallback must resolve identically everywhere or such a clip becomes invisible
/// to generation ("not in blueprint — skip") and to cost ($0). Also guards the actual gen finder.
/// </summary>
public class ClipKeyingTests
{
    private static JsonElement El(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Element_prefers_clip_number() =>
        Assert.Equal(3, ClipKeying.ClipNumber(El("""{ "clip_number": 3, "clip_index": 9 }""")));

    [Fact]
    public void Element_falls_back_to_clip_index() =>
        Assert.Equal(2, ClipKeying.ClipNumber(El("""{ "clip_index": 2 }""")));

    [Fact]
    public void Element_zero_when_neither() =>
        Assert.Equal(0, ClipKeying.ClipNumber(El("""{ "visual_prompt": "x" }""")));

    [Fact]
    public void Node_prefers_clip_number() =>
        Assert.Equal(3, ClipKeying.ClipNumber(JsonNode.Parse("""{ "clip_number": 3, "clip_index": 9 }""")));

    [Fact]
    public void Node_falls_back_to_clip_index() =>
        Assert.Equal(2, ClipKeying.ClipNumber(JsonNode.Parse("""{ "clip_index": 2 }""")));

    [Fact]
    public void Node_minus_one_when_neither() =>
        Assert.Equal(-1, ClipKeying.ClipNumber(JsonNode.Parse("""{ "visual_prompt": "x" }""")));

    [Fact]
    public void FindClipInScene_locates_a_legacy_clip_index_clip()
    {
        // The exact bug: a credits clip keyed on clip_index only was invisible to the batch-gen
        // finder → "S02C1: not in blueprint — skip" → never generated.
        var scene = El("""
            {
              "scene_number": 2,
              "veo_clips": [ { "clip_index": 1, "is_credits": true, "visual_prompt": "END CREDITS" } ]
            }
            """);

        var clip = FilmJobService.FindClipInScene(scene, 1);

        Assert.NotNull(clip);
        Assert.True(clip!.Value.TryGetProperty("is_credits", out _));
    }
}
