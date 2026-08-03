using PageToMovie.Adaptation;
using PageToMovie.Adaptation.Validation;
using PageToMovie.Core.Models;
using Xunit;

namespace PageToMovie.Tests;

public sealed class CastKindClassifierTests
{
    [Theory]
    [InlineData("Character_Children", "Children", null, true)]
    [InlineData("Character_Crowd", "Crowd", null, true)]
    [InlineData("Character_Mary", "Mary", null, false)]
    [InlineData("Character_Teacher", "Teacher", null, false)]
    [InlineData("Character_Kids", "Kids", "individual", false)] // explicit individual wins
    [InlineData("Character_X", "X", "group", true)]
    [InlineData("Character_Classmates", "Classmates", null, true)]
    public void IsGroup_classifies_expected(string key, string display, string? castKind, bool expected)
    {
        Assert.Equal(expected, CastKindClassifier.IsGroup(key, display, castKind, description: null));
    }

    [Fact]
    public void IsVoiceOnlyPolicy_never_on_screen()
    {
        Assert.True(CastKindClassifier.IsVoiceOnlyPolicy("never_on_screen"));
        Assert.False(CastKindClassifier.IsVoiceOnlyPolicy("ok_anytime"));
        Assert.False(CastKindClassifier.IsVoiceOnlyPolicy(null));
    }

    [Fact]
    public void Cast_judge_flags_group_keys_on_Mary_style_package()
    {
        var fountain = """
            Title: Mary

            FADE IN:

            EXT. YARD - DAY

            CHILDREN
            What makes the lamb love Mary so?

            TEACHER
            Oh, Mary loves the lamb, you know.

            FADE OUT.
            """;
        var cast = """
            {
              "character_seed_tokens": {
                "Character_Children": {
                  "canonical_given_name": "Children",
                  "description": "Small group of school-age children in simple period play clothes.",
                  "visual_lock": "several young classmates, eager faces",
                  "species_kind": "human",
                  "cast_kind": "group"
                },
                "Character_Teacher": {
                  "canonical_given_name": "Teacher",
                  "description": "Adult woman in plain gray dress with hair in a bun.",
                  "visual_lock": "gray dress, hair in a bun",
                  "species_kind": "human"
                }
              }
            }
            """;
        var report = new AdaptationService().CrossCheckCast(fountain, cast, bookText: "Mary had a little lamb. The eager children cry.");
        Assert.True(report.Ok, string.Join("; ", report.Failures));
        Assert.Contains("Character_Children", report.GroupCastKeys);
        Assert.DoesNotContain("Character_Teacher", report.GroupCastKeys);
        Assert.True(report.MembershipScore >= 99);
        Assert.True(report.NoInventedNames);
    }
}
