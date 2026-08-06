using System.Collections.Generic;
using System.Text.Json.Nodes;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// "Voice-only" (never on screen → no portrait, stripped visual_lock) must be decided by the cast
/// seed's display_name_policy per story, NEVER by the key name "Narrator". An on-camera / POV narrator
/// (e.g. Tell-Tale Heart's confessor, policy "ok_anytime") is a real character with a locked look; a
/// pure off-screen narrator (e.g. Mary's, "never_on_screen") is voice-only. Regression for the two
/// sites (Stage1Normalizer, CharacterBookPlateService) that used to force voice-only by name and strip
/// an on-camera narrator's visual identity.
/// </summary>
public class NarratorVoiceOnlyPolicyTests
{
    private static Dictionary<string, object?> NormalizeNarrator(string policy)
    {
        var data = new Dictionary<string, object?>
        {
            ["global_production_variables"] = new Dictionary<string, object?>
            {
                ["character_seed_tokens"] = new Dictionary<string, object?>
                {
                    ["Character_Narrator"] = new Dictionary<string, object?>
                    {
                        ["display_name_policy"] = policy,
                        ["visual_lock"] = "same weary man in a dark coat, hollow eyes",
                        ["wardrobe_always"] = new List<object?> { "dark coat" },
                        ["description"] = "a weary confessor",
                        ["species_kind"] = "human",
                    },
                },
            },
            ["scenes"] = new List<object?>(),
        };

        var result = Stage1Normalizer.Normalize(data);
        var gpv = (Dictionary<string, object?>)result["global_production_variables"]!;
        var seeds = (Dictionary<string, object?>)gpv["character_seed_tokens"]!;
        return (Dictionary<string, object?>)seeds["Character_Narrator"]!;
    }

    [Fact]
    public void Stage1Normalizer_keeps_on_camera_narrators_visual_identity()
    {
        var narrator = NormalizeNarrator("ok_anytime"); // on-camera / POV narrator

        Assert.True(narrator.ContainsKey("visual_lock"));
        Assert.False(string.IsNullOrWhiteSpace(narrator["visual_lock"]?.ToString()));
    }

    [Fact]
    public void Stage1Normalizer_strips_voice_only_narrators_visual_identity()
    {
        var narrator = NormalizeNarrator("never_on_screen"); // pure off-screen V.O.

        Assert.False(narrator.ContainsKey("visual_lock"));
        Assert.False(narrator.ContainsKey("wardrobe_always"));
    }

    [Fact]
    public void BookPlateGate_gives_on_camera_narrator_a_portrait_but_not_a_voice_only_one()
    {
        // On-camera narrator (policy ok_anytime) is NOT voice-only → gets book plates / a portrait.
        var onCamera = new JsonObject { ["display_name_policy"] = "ok_anytime" };
        Assert.False(CharacterBookPlateService.IsVoiceOnly("Character_Narrator", onCamera));

        // Pure off-screen narrator (never_on_screen) is voice-only → skipped for plates.
        var offScreen = new JsonObject { ["display_name_policy"] = "never_on_screen" };
        Assert.True(CharacterBookPlateService.IsVoiceOnly("Character_Narrator", offScreen));
    }
}
