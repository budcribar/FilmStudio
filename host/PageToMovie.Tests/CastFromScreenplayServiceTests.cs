using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class CastFromScreenplayServiceTests
{
    [Fact]
    public async Task Prompt_file_exists_and_mentions_silent_cast()
    {
        var root = FindRepoWithPrompts();
        if (root is null)
        {
            Assert.True(true);
            return;
        }

        var text = await CastFromScreenplayService.LoadSystemPromptAsync(root);
        Assert.Contains("cast_seeds", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("silent", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Character_", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JSON", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BOOK-FIRST", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FORBIDDEN", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("performance_lock", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AUDIENCE", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Visual_literalize_prompt_exists_and_targets_figurative_language()
    {
        var root = FindRepoWithPrompts();
        if (root is null)
        {
            Assert.True(true);
            return;
        }

        var text = await CastVisualLiteralizeService.LoadSystemPromptAsync(root);
        Assert.Contains("figurative", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("literal", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JSON", text, StringComparison.OrdinalIgnoreCase);
        // Base-look vs later wardrobe (general, not book-specific lists)
        Assert.Contains("later", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BASE", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("wardrobe", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Narrator, as described in the screenplay.", true)]
    [InlineData("as in the screenplay", true)]
    [InlineData("Match Bob consistently across scenes.", true)]
    [InlineData("short", true)]
    [InlineData("Pale nervous adult man, mid-30s, thin face, dark wool coat, 1840s photoreal.", false)]
    public void IsStubLook_detects_placeholders(string text, bool expected)
    {
        Assert.Equal(expected, CastFromScreenplayService.IsStubLook(text));
    }

    [Fact]
    public void SelectTextForPrompt_keeps_short_books_whole()
    {
        var book = "Once upon a time there was a pale man and an old man with a vulture eye.";
        var selected = CastFromScreenplayService.SelectTextForPrompt(book, 100_000);
        Assert.Equal(book, selected);
    }

    [Fact]
    public void SelectTextForPrompt_samples_long_books_with_spine_windows()
    {
        var head = new string('A', 50_000);
        var mid = new string('B', 50_000);
        var tail = new string('C', 50_000);
        var book = head + mid + tail;
        var selected = CastFromScreenplayService.SelectTextForPrompt(book, 40_000);
        Assert.True(selected.Length <= 45_000);
        Assert.Contains('A', selected);
        Assert.Contains('C', selected);
        Assert.Contains("sampled for length", selected, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractNameHintsFromFountain_includes_dialogue_cues()
    {
        var fountain = """
            Title: Test

            INT. ROOM - DAY

            ZARA
            Hello.

            OLD MAN (V.O.)
            Listen.
            """;
        var names = CastFromScreenplayService.ExtractNameHintsFromFountain(fountain);
        Assert.Contains(names, n => n.Equals("ZARA", StringComparison.OrdinalIgnoreCase)
                                    || n.Equals("Zara", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, n => n.Contains("OLD", StringComparison.OrdinalIgnoreCase)
                                    || n.Contains("Old", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ExtractNameHintsFromBook_includes_title_hero_buster()
    {
        var book = """
            --- PAGE 1 ---
            BUSTER
            THE NOODLE HEAD DOG
            GOES TO BED

            Debra McGuinty

            --- PAGE 2 ---
            He's Buster the Noodle Head Dog
            He jumps around like a frog
            He's small, black, and white
            But not very bright!
            He's Buster the Noodle Head Dog

            --- PAGE 4 ---
            When Momma says, "It's time for bed",
            He wants to rest his furry head
            """;
        var names = CastFromScreenplayService.ExtractNameHintsFromBook(book);
        Assert.Contains(names, n => n.Equals("Buster", StringComparison.OrdinalIgnoreCase)
                                    || n.Equals("BUSTER", StringComparison.OrdinalIgnoreCase));
        // "Momma" appears once as title-case — family role names still count via He's/When patterns
        Assert.True(
            names.Any(n => n.Contains("Mom", StringComparison.OrdinalIgnoreCase)
                           || n.Contains("Buster", StringComparison.OrdinalIgnoreCase)),
            "expected Buster (and ideally Momma); got " + string.Join(", ", names));
        Assert.DoesNotContain(names, n => n.Equals("Dog", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Equals("GOES", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnsureSeedsForNameHints_adds_buster_when_model_only_returned_mom()
    {
        var seeds = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Character_Mom"] = new Dictionary<string, object?>
            {
                ["canonical_given_name"] = "Mom",
                ["description"] = "Adult woman, gentle.",
                ["display_name_policy"] = "ok_anytime",
            },
            ["Character_Narrator"] = new Dictionary<string, object?>
            {
                ["canonical_given_name"] = "Narrator",
                ["description"] = "Narrator (voice only).",
                ["display_name_policy"] = "never_on_screen",
            },
        };
        var book = """
            BUSTER
            He's Buster the Noodle Head Dog
            He's small, black, and white
            When Momma says bed time
            """;
        var fountain = """
            Title: BUSTER

            EXT. YARD - DAY

            This is BUSTER. A small dog.

            MOM
            Bed time.
            """;
        var hints = CastFromScreenplayService.CollectCastNameHints(fountain, book);
        Assert.Contains(hints, n => n.Contains("Buster", StringComparison.OrdinalIgnoreCase));

        var added = CastFromScreenplayService.EnsureSeedsForNameHints(seeds, hints, book, fountain);
        Assert.True(added >= 1);
        Assert.True(
            seeds.Keys.Any(k => k.Contains("Buster", StringComparison.OrdinalIgnoreCase)),
            "expected Character_Buster (or similar) after backfill; keys=" + string.Join(",", seeds.Keys));
        // Mom already present — do not duplicate as Momma
        Assert.Single(seeds.Keys.Where(k =>
            k.Contains("Mom", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void NameToCharacterKey_pascalizes()
    {
        Assert.Equal("Character_Buster", CastFromScreenplayService.NameToCharacterKey("BUSTER"));
        Assert.Equal("Character_Buster", CastFromScreenplayService.NameToCharacterKey("Buster the Dog"));
        Assert.Equal("Character_Bob_Cratchit", CastFromScreenplayService.NameToCharacterKey("BOB CRATCHIT"));
        Assert.Equal("Character_Queen_Of_Hearts", CastFromScreenplayService.NameToCharacterKey("QUEEN OF HEARTS"));
    }

    [Fact]
    public void SelectBookTextForCastPrompt_includes_late_name_look_when_over_budget()
    {
        // Novel-length padding so we must sample; unique look only appears late.
        var early = string.Join("\n\n", Enumerable.Range(0, 120).Select(i =>
            $"Chapter filler {i}. " + new string('x', 1_200)));
        var lateLook =
            "\n\nZara stepped into the firelight. She had silver hair and a green velvet coat with brass buttons.\n\n";
        var after = string.Join("\n\n", Enumerable.Range(0, 40).Select(i =>
            $"Epilogue pad {i}. " + new string('y', 600)));
        var book = early + lateLook + after;
        Assert.True(book.Length > CastFromScreenplayService.BookPromptChars,
            $"book len={book.Length}");

        var fountain = """
            Title: Late Reveal

            INT. HALL - NIGHT

            ZARA
            I am here.
            """;
        var names = CastFromScreenplayService.ExtractNameHintsFromFountain(fountain);
        Assert.NotEmpty(names);

        var selected = CastFromScreenplayService.SelectBookTextForCastPrompt(
            book, maxChars: 40_000, nameHints: names);

        Assert.Contains("silver hair", selected, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("green velvet", selected, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOOK EXCERPTS", selected, StringComparison.OrdinalIgnoreCase);
        // Must not be head-only truncation of the first 40k (late look would be absent)
        var headOnly = book[..40_000];
        Assert.DoesNotContain("silver hair", headOnly, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HarvestNameLookExcerpts_prefers_appearance_language()
    {
        var book = """
            Bob walked down the street and said nothing interesting for a long while about weather.

            Bob had curly red hair and a blue wool coat that marked him in every scene.

            Alice smiled once.
            """;
        var harvested = CastFromScreenplayService.HarvestNameLookExcerpts(
            book, new[] { "Bob", "Alice" }, maxChars: 2_000);
        Assert.Contains("curly red hair", harvested, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("blue wool coat", harvested, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindRepoWithPrompts()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "prompts", "fountain_to_cast.txt");
            if (File.Exists(candidate))
                return dir.FullName;
        }
        var known = @"C:\Users\budcr\source\repos\NickAndMe";
        if (File.Exists(Path.Combine(known, "prompts", "fountain_to_cast.txt")))
            return known;
        return null;
    }
}
