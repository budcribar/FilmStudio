using System;
using System.Collections.Generic;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// The fresh-video-gen locked-reference gate (FilmJobService.OnScreenVisualKeys, behind
/// EnsureFreshGenHasLockedRefs / MissingOnScreenLockKeys) decides which on-screen characters must
/// have a locked reference image. It must exclude voice-only roles AND group/ensemble cast — a group
/// has no single portrait identity to lock, so requiring a ref for it blocks generation forever.
/// Regression for the batch that generated clip 1 (Mary+Lamb) then failed every clip with "Children"
/// on screen: "Locked character reference images required for fresh video gen … Missing ref for:
/// Character_Children". (The pre-flight cast gate was exempted earlier; this deeper gate was missed.)
/// </summary>
public class FreshGenLockGateTests
{
    private static ClipVideoPromptBuilder.CharacterProfile Profile(string key, bool voiceOnly = false) =>
        new() { Key = key, VoiceOnly = voiceOnly };

    [Fact]
    public void OnScreenVisualKeys_excludes_groups_and_voice_only_keeps_individuals()
    {
        var built = new ClipVideoPromptBuilder.PromptBuildResult
        {
            OnScreenKeys = new[] { "Character_Mary", "Character_Children", "Character_Narrator" },
        };
        var profiles = new Dictionary<string, ClipVideoPromptBuilder.CharacterProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["Character_Mary"] = Profile("Character_Mary"),
            ["Character_Children"] = Profile("Character_Children"),
            ["Character_Narrator"] = Profile("Character_Narrator", voiceOnly: true),
        };

        var keys = FilmJobService.OnScreenVisualKeys(built, profiles);

        Assert.Contains("Character_Mary", keys);          // individual → still needs a locked ref
        Assert.DoesNotContain("Character_Children", keys); // group → exempt (no lockable identity)
        Assert.DoesNotContain("Character_Narrator", keys); // voice-only → exempt
    }

    [Fact]
    public void OnScreenVisualKeys_group_only_scene_needs_no_refs()
    {
        // A crowd/ensemble-only shot must not demand any locked reference (nothing to lock).
        var built = new ClipVideoPromptBuilder.PromptBuildResult
        {
            OnScreenKeys = new[] { "Character_Children", "Character_Crowd" },
        };
        var profiles = new Dictionary<string, ClipVideoPromptBuilder.CharacterProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["Character_Children"] = Profile("Character_Children"),
            ["Character_Crowd"] = Profile("Character_Crowd"),
        };

        Assert.Empty(FilmJobService.OnScreenVisualKeys(built, profiles));
    }
}
