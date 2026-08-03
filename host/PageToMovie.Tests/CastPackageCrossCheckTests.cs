using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public sealed class CastPackageCrossCheckTests
{
    private const string MaryFountain = """
        Title: MARY HAD A LITTLE LAMB

        FADE IN:

        EXT. COUNTRY LANE - DAY

        MARY, a young girl with brown braids, walks with her LAMB.

        MARY
        Come along.

        INT. SCHOOLHOUSE - DAY

        The TEACHER stands at the front. ELI and CLARA watch the lamb.

        ELI
        Why does he follow her?

        CLARA
        What makes the lamb love Mary so?

        TEACHER
        Oh, Mary loves the lamb, you know.

        FADE OUT.
        """;

    private const string FullCast = """
        {
          "schema_version": "cast_seeds.v1",
          "character_seed_tokens": {
            "Character_Mary": {
              "canonical_given_name": "Mary",
              "description": "A young girl with brown braids, a blue pinafore, white apron, and straw bonnet.",
              "visual_lock": "brown braids, blue pinafore, white apron, straw bonnet, school-age girl",
              "wardrobe_lock": "blue pinafore, white apron, straw bonnet",
              "species_kind": "human",
              "display_name_policy": "ok_anytime"
            },
            "Character_Eli": {
              "canonical_given_name": "Eli",
              "description": "A freckled boy about eight in a brown waistcoat over a cream shirt.",
              "visual_lock": "freckled boy, short auburn hair, brown waistcoat",
              "wardrobe_lock": "brown waistcoat, cream shirt, dark trousers",
              "species_kind": "human",
              "display_name_policy": "ok_anytime"
            },
            "Character_Clara": {
              "canonical_given_name": "Clara",
              "description": "A girl about eight with dark curls tied with a yellow ribbon.",
              "visual_lock": "dark shoulder-length curls, yellow ribbon",
              "wardrobe_lock": "muted green pinafore, white blouse",
              "species_kind": "human",
              "display_name_policy": "ok_anytime"
            },
            "Character_Teacher": {
              "canonical_given_name": "Teacher",
              "description": "A middle-aged woman in a dark gray dress with a white collar and pinned brown hair.",
              "visual_lock": "middle-aged woman, pinned brown hair, dark gray dress, white collar",
              "species_kind": "human",
              "display_name_policy": "ok_anytime"
            },
            "Character_Lamb": {
              "canonical_given_name": "Lamb",
              "description": "A small lamb with snowy fleece and a red ribbon at its neck.",
              "visual_lock": "snowy fleece, red neck ribbon, small lamb",
              "species_kind": "animal",
              "display_name_policy": "ok_anytime"
            }
          }
        }
        """;

    [Fact]
    public void Speakers_include_named_children_and_teacher()
    {
        var speakers = CastPackageCrossCheck.ExtractSpeakers(MaryFountain);
        Assert.Contains("MARY", speakers);
        Assert.Contains("ELI", speakers);
        Assert.Contains("CLARA", speakers);
        Assert.Contains("TEACHER", speakers);
    }

    [Fact]
    public void Full_cast_package_scores_high()
    {
        var report = CastPackageCrossCheck.Evaluate(MaryFountain, FullCast);
        Assert.True(report.Ok, string.Join("; ", report.Failures));
        Assert.True(report.Score >= 85, $"score={report.Score}");
        Assert.Contains("Character_Eli", report.MatchedKeys);
        Assert.Contains("Character_Clara", report.MatchedKeys);
    }

    [Fact]
    public void Missing_cast_file_fails_hard()
    {
        var report = CastPackageCrossCheck.Evaluate(MaryFountain, castSeedsJson: null);
        Assert.False(report.Ok);
        Assert.Equal(0, report.Score);
        Assert.Contains(report.Failures, f => f.Contains("cast_seeds", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Partial_cast_missing_children_fails_membership()
    {
        var partial = """
            {
              "schema_version": "cast_seeds.v1",
              "character_seed_tokens": {
                "Character_Mary": {
                  "canonical_given_name": "Mary",
                  "description": "A young girl with brown braids and a blue pinafore.",
                  "visual_lock": "brown braids, blue pinafore",
                  "species_kind": "human",
                  "display_name_policy": "ok_anytime"
                }
              }
            }
            """;
        var report = CastPackageCrossCheck.Evaluate(MaryFountain, partial);
        Assert.False(report.Ok);
        Assert.Contains(report.Failures, f => f.Contains("ELI", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Failures, f => f.Contains("CLARA", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Failures, f => f.Contains("TEACHER", StringComparison.OrdinalIgnoreCase));
    }
}
