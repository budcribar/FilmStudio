using System.Text.Json;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// The auto-inserted end-credits scene is a title card with no real cast, so it must be exempt from the
/// locked-character cast gate that <see cref="FilmJobService.RunBatchGenAsync"/> enforces before video gen.
/// These tests pin the smallest predicate the exemption rides on — <see cref="FilmJobService.IsCreditsScene"/> —
/// which reuses the same blueprint signal ProjectStore uses to derive <c>SceneSummary.IsCredits</c>
/// (the <c>is_credits</c> flag or a CREDITS scene heading), never a hardcoded scene number or title string.
/// </summary>
public class CreditsSceneCastGateTests
{
    private static JsonElement Scene(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void Credits_scene_flag_is_exempt()
    {
        var s = Scene("""{ "scene_number": 9, "is_credits": true, "scene_heading": "FADE OUT. END CREDITS" }""");
        Assert.True(FilmJobService.IsCreditsScene(s));
    }

    [Fact]
    public void Credits_scene_heading_is_exempt_even_without_flag()
    {
        // A blueprint that carries only the CREDITS heading (no explicit is_credits flag) is still exempt —
        // mirrors ProjectStore's IsCredits derivation.
        var s = Scene("""{ "scene_number": 9, "scene_heading": "FADE OUT. END CREDITS" }""");
        Assert.True(FilmJobService.IsCreditsScene(s));
    }

    [Fact]
    public void Credits_flag_as_string_is_exempt()
    {
        var s = Scene("""{ "scene_number": 9, "is_credits": "true" }""");
        Assert.True(FilmJobService.IsCreditsScene(s));
    }

    [Fact]
    public void Normal_scene_still_enforces_the_gate()
    {
        // A real story scene has no credits signal → predicate is false → the cast-lock gate stays in force.
        var s = Scene("""{ "scene_number": 3, "scene_heading": "INT. KITCHEN - DAY", "characters_on_screen": ["Alice"] }""");
        Assert.False(FilmJobService.IsCreditsScene(s));
    }

    [Fact]
    public void Missing_scene_is_not_treated_as_credits()
    {
        // FindScene returns null when a scene is absent from the blueprint; that must NOT bypass the gate.
        Assert.False(FilmJobService.IsCreditsScene(null));
    }
}
