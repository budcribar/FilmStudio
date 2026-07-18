using FilmStudio.Engine;
using Xunit;

namespace FilmStudio.Tests;

public class FountainParserTests
{
    private const string BrickSteel = """
        Title:
            BRICK & STEEL
            FULL RETIRED
        Credit: Written by
        Author: Stu Maschwitz
        Source: Story by KTM
        Draft date: 1/20/2012
        Contact:
            Next Level Productions
            1588 Mission Dr.
            Solvang, CA 93463

        EXT. BRICK'S PATIO - DAY

        A gorgeous day. The sun is shining. But BRICK BRADDOCK, retired police detective, is sitting quietly, contemplating -- something.

        The SCREEN DOOR slides open and DICK STEEL, his former partner and fellow retiree, emerges with two cold beers.

        STEEL
        Beer's ready!

        BRICK
        Are they cold?

        STEEL
        Does a bear crap in the woods?

        Steel sits. They laugh at the dumb joke.

        STEEL
        (beer raised)
        To retirement.

        BRICK
        To retirement.

        They drink long and well from the beers.

        STEEL
        Screw retirement.

        BRICK ^
        Screw retirement.

        SMASH CUT TO:

        INT. TRAILER HOME - DAY

        This is the home of THE BOY BAND, AKA DAN and JACK.

        JACK
        (in Vietnamese, subtitled)
        *Did you know Brick and Steel are retired?*

        DAN
        Then let's retire them.
        _Permanently_.

        CUT TO:

        EXT. BRICK'S POOL - DAY #12A#

        Steel, in the middle of a heated phone call:

        STEEL
        They're coming out of the woodwork!

        .SNIPER SCOPE POV

        From what seems like only INCHES AWAY.  _Steel's face FILLS the *Leupold Mark 4* scope_.

        STEEL
        The man's a myth!

        .OPENING TITLES

        > BRICK BRADDOCK <
        > & DICK STEEL IN <

        > BURN TO PINK.

        > THE END <
        """;

    [Fact]
    public void Title_page_multiline_and_keys()
    {
        var r = FountainParser.Parse(BrickSteel);
        Assert.Contains("BRICK", r.TitlePage["Title"]);
        Assert.Equal("Stu Maschwitz", r.TitlePage["Author"].Trim());
        Assert.Contains("Next Level", r.TitlePage["Contact"]);
        Assert.Equal("1/20/2012", r.TitlePage["Draft date"].Trim());
    }

    [Fact]
    public void Scene_headings_forced_and_numbered()
    {
        var r = FountainParser.Parse(BrickSteel);
        var headings = r.Elements.Where(e => e.Type == FountainParser.ElementType.SceneHeading).ToList();
        Assert.Contains(headings, h => h.Text.Contains("PATIO", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headings, h => h.Text.Contains("SNIPER SCOPE POV", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headings, h => h.Text.Contains("OPENING TITLES", StringComparison.OrdinalIgnoreCase));
        var numbered = headings.First(h => h.Text.Contains("POOL", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("12A", numbered.Meta);
        Assert.DoesNotContain("#", numbered.Text);
    }

    [Fact]
    public void Scene_heading_recognized_with_page_tag_on_next_line()
    {
        // Book→Fountain pipeline puts = page N / [[page N]] under the heading (no blank).
        var text = """
            Title: Page Tags

            EXT. YARD - DAY
            = page 2
            [[page 2]]

            A dog hops.

            INT. ROOM - NIGHT
            = page 4
            [[page 4]]

            MOMMA
            Bedtime.
            """;
        var r = FountainParser.Parse(text);
        var headings = r.Elements.Where(e => e.Type == FountainParser.ElementType.SceneHeading).ToList();
        Assert.Equal(2, headings.Count);
        Assert.Contains(headings, h => h.Text.Contains("YARD", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headings, h => h.Text.Contains("ROOM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Character_dialogue_parenthetical_dual()
    {
        var r = FountainParser.Parse(BrickSteel);
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Character && e.Text == "STEEL");
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Parenthetical &&
                                         e.Text.Contains("beer raised", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Dialogue &&
                                         e.Text.Contains("Beer's ready", StringComparison.OrdinalIgnoreCase));
        var dual = r.Elements.First(e => e.Type == FountainParser.ElementType.Character && e.Text == "BRICK" &&
                                         e.Meta is not null && e.Meta.Contains("dual"));
        Assert.NotNull(dual);
    }

    [Fact]
    public void Transitions_centered_and_forced()
    {
        var r = FountainParser.Parse(BrickSteel);
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Transition &&
                                         e.Text.Contains("CUT TO", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Transition &&
                                         e.Text.Contains("BURN TO PINK", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Centered &&
                                         e.Text.Contains("THE END", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Centered &&
                                         e.Text.Contains("BRICK BRADDOCK", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Emphasis_stripped_in_dialogue_and_action()
    {
        var r = FountainParser.Parse(BrickSteel);
        var dialogue = r.Elements.First(e => e.Type == FountainParser.ElementType.Dialogue &&
                                             e.Text.Contains("Did you know", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("*", dialogue.Text);
        var action = r.Elements.First(e => e.Type == FountainParser.ElementType.Action &&
                                           e.Text.Contains("Leupold", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("*", action.Text);
        Assert.DoesNotContain("_", action.Text.Replace("Steel's", "X")); // underline markers gone
        Assert.Contains("FILLS", action.Text);
    }

    [Fact]
    public void Forced_action_and_character_and_lyrics()
    {
        var src = """
            INT. CASINO - NIGHT

            THE DEALER eyes the new player warily.

            !SCANNING THE AISLES…
            Where is that pit boss?

            @McCLANE
            Yippie ki-yay!

            ~Willy Wonka! The amazing chocolatier!
            """;
        var r = FountainParser.Parse(src);
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Action &&
                                         e.Text.Contains("SCANNING THE AISLES", StringComparison.OrdinalIgnoreCase));
        // Uppercase action forced — should NOT be character
        Assert.DoesNotContain(r.Elements, e => e.Type == FountainParser.ElementType.Character &&
                                               e.Text.Contains("SCANNING", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Character && e.Text == "McCLANE");
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Lyric &&
                                         e.Text.Contains("Willy Wonka", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Boneyard_and_notes_removed_or_captured()
    {
        var src = """
            INT. ROOM - DAY

            Hello[[secret note]].

            /*
            INT. HIDDEN - DAY
            SECRET
            Hidden dialogue.
            */

            EXT. YARD - DAY

            Outside.
            """;
        var r = FountainParser.Parse(src);
        Assert.DoesNotContain(r.Elements, e => e.Type == FountainParser.ElementType.SceneHeading &&
                                               e.Text.Contains("HIDDEN", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(r.Elements, e => e.Type == FountainParser.ElementType.Dialogue &&
                                               e.Text.Contains("Hidden", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Note &&
                                         e.Text.Contains("secret note", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.SceneHeading &&
                                         e.Text.Contains("YARD", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Sections_synopses_page_break()
    {
        var src = """
            # Act One

            = Set up the story.

            INT. HOUSE - DAY

            Action here.

            ===

            ## Act Two

            EXT. ROAD - NIGHT
            """;
        var r = FountainParser.Parse(src);
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Section && e.Text.Contains("Act One"));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Synopsis && e.Text.Contains("Set up"));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.PageBreak);
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Section && e.Meta == "2");
    }

    [Fact]
    public void Action_preserves_leading_indent()
    {
        var src = """
            INT. ROOM - DAY

            He opens the card:

                Scott --
                Jacob Billups

            He throws the card down.
            """;
        var r = FountainParser.Parse(src);
        var indented = r.Elements.Where(e => e.Type == FountainParser.ElementType.Action &&
                                             e.Text.Contains("Scott")).ToList();
        Assert.NotEmpty(indented);
        Assert.StartsWith("    ", indented[0].Text);
    }

    [Fact]
    public void Character_extension_mixed_case()
    {
        var src = """
            INT. HOME - DAY

            MOM (O. S.)
            Luke! Come down for supper!

            HANS (on the radio)
            What was it you said?
            """;
        var r = FountainParser.Parse(src);
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Character &&
                                         e.Text == "MOM" && e.Meta != null && e.Meta.Contains("O. S."));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Character &&
                                         e.Text == "HANS" && e.Meta != null && e.Meta.Contains("on the radio"));
    }

    [Fact]
    public void BuildStage1_from_full_sample()
    {
        var parsed = FountainParser.Parse(BrickSteel);
        var doc = FountainStage1Importer.BuildStage1(parsed);
        doc = Stage1Normalizer.Normalize(doc);

        Assert.Equal("stage1.v1", doc["schema_version"]?.ToString());
        Assert.Contains("BRICK", doc["movie_title"]?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);

        var scenes = doc["scenes"] as System.Collections.IList;
        Assert.NotNull(scenes);
        Assert.True(scenes!.Count >= 3);

        var gpv = doc["global_production_variables"] as Dictionary<string, object?>;
        var chars = gpv!["character_seed_tokens"] as Dictionary<string, object?>;
        Assert.True(chars!.Count >= 2);
    }

    [Fact]
    public void StripEmphasis_handles_escapes_and_space_rules()
    {
        // Escaped closing star → no italics; literal asterisks remain
        var escaped = FountainParser.StripEmphasis(@"Steel dialed *69 and then 23\*, done.");
        Assert.Equal("Steel dialed *69 and then 23*, done.", escaped);

        // Closed pair → italics stripped (Fountain: *69 and then 23*)
        var closed = FountainParser.StripEmphasis("He dialed *69 and then 23*, and hung up.");
        Assert.Equal("He dialed 69 and then 23, and hung up.", closed);

        // Both stars have space to the left of a would-be close → no emphasis
        var spaced = FountainParser.StripEmphasis("He dialed *69 and then *23, and hung up.");
        Assert.Contains("*69", spaced);
        Assert.Contains("*23", spaced);

        var bold = FountainParser.StripEmphasis("Then **retire** them.");
        Assert.Equal("Then retire them.", bold);

        var boldItalic = FountainParser.StripEmphasis("***bold italics***");
        Assert.Equal("bold italics", boldItalic);

        var underline = FountainParser.StripEmphasis("_Permanently_.");
        Assert.Equal("Permanently.", underline);

        // Nested: underline with italic inside
        var nested = FountainParser.StripEmphasis("_Steel's face FILLS the *Leupold Mark 4* scope_");
        Assert.Equal("Steel's face FILLS the Leupold Mark 4 scope", nested);
        Assert.DoesNotContain("*", nested);
        Assert.DoesNotContain("_", nested);
    }

    [Fact]
    public void Transition_trailing_spaces_after_colon_is_action()
    {
        // Fountain: spaces after colon prevent Transition (line no longer ends with colon)
        var src = """
            Action before.

            CUT TO:  

            EXT. YARD - DAY
            """;
        var r = FountainParser.Parse(src);
        Assert.DoesNotContain(r.Elements, e => e.Type == FountainParser.ElementType.Transition &&
                                               e.Text.Contains("CUT TO", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Action &&
                                         e.Text.Trim().StartsWith("CUT TO", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Scene_heading_prefixes_and_lowercase()
    {
        var src = """
            int. brick's pool - day

            Action.

            EXT. STREET - NIGHT

            More.

            INT./EXT. CAR - DAY

            Inside.

            I/E HOSPITAL - NIGHT

            Hall.

            EST. CITY - DAWN

            Wide.
            """;
        var r = FountainParser.Parse(src);
        var headings = r.Elements.Where(e => e.Type == FountainParser.ElementType.SceneHeading).Select(e => e.Text).ToList();
        Assert.True(headings.Count >= 5, string.Join(" | ", headings));
        Assert.Contains(headings, h => h.Contains("pool", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headings, h => h.Contains("STREET", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headings, h => h.Contains("CAR", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headings, h => h.Contains("HOSPITAL", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(headings, h => h.Contains("CITY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Ellipsis_action_not_forced_scene_heading()
    {
        var src = """
            EXT. FIELD - NIGHT

            ...where the carnival is parked.
            """;
        var r = FountainParser.Parse(src);
        Assert.DoesNotContain(r.Elements, e => e.Type == FountainParser.ElementType.SceneHeading &&
                                               e.Text.Contains("carnival", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Action &&
                                         e.Text.Contains("carnival", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Dialogue_two_space_blank_continues_block()
    {
        // Fountain line breaks: two spaces on "blank" line keep dialogue going
        var src = "INT. CASINO - NIGHT\n\nDEALER\nTen.\nFour.\n  \nHit or stand?\n\nMONKEY\nDude.\n";
        var r = FountainParser.Parse(src);
        var dealerDialogue = r.Elements
            .SkipWhile(e => !(e.Type == FountainParser.ElementType.Character && e.Text == "DEALER"))
            .Skip(1)
            .TakeWhile(e => e.Type is FountainParser.ElementType.Dialogue or FountainParser.ElementType.Parenthetical)
            .ToList();
        Assert.Contains(dealerDialogue, d => d.Text.Contains("Hit or stand", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Character && e.Text == "MONKEY");
    }

    [Fact]
    public void Dual_dialogue_caret_with_spaces()
    {
        var src = """
            INT. ROOM - DAY

            BRICK
            Screw retirement.

            STEEL  ^
            Screw retirement.
            """;
        var r = FountainParser.Parse(src);
        Assert.Contains(r.Elements, e => e.Type == FountainParser.ElementType.Character &&
                                         e.Text == "STEEL" &&
                                         e.Meta != null && e.Meta.Contains("dual"));
    }

    [Fact]
    public void Forced_scene_heading_with_scene_number()
    {
        var src = """
            .OPENING TITLES #A1#

            Action.
            """;
        var r = FountainParser.Parse(src);
        var h = r.Elements.First(e => e.Type == FountainParser.ElementType.SceneHeading);
        Assert.Equal("OPENING TITLES", h.Text);
        Assert.Equal("A1", h.Meta);
    }
}
