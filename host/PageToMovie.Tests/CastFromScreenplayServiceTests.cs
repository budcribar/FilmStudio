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
    public void EnrichStubLooksFromSources_fills_model_seed_only_does_not_add_cast()
    {
        // Model already chose Buster + Mom; Buster has a stub look — enrich from book.
        var seeds = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Character_Mom"] = new Dictionary<string, object?>
            {
                ["canonical_given_name"] = "Mom",
                ["description"] = "Adult woman, gentle smile, soft brown hair, cardigan.",
                ["visual_lock"] = "Same soft brown hair and cardigan every scene.",
                ["display_name_policy"] = "ok_anytime",
            },
            ["Character_Buster"] = new Dictionary<string, object?>
            {
                ["canonical_given_name"] = "Buster",
                ["description"] = "as described in the screenplay",
                ["visual_lock"] = "",
                ["display_name_policy"] = "ok_anytime",
            },
        };
        var book = """
            He's Buster the Noodle Head Dog.

            He's small, black, and white with floppy ears and a soft rounded head.

            When Momma says bed time he wants to rest his furry head.
            """;
        var beforeKeys = seeds.Keys.OrderBy(k => k).ToList();
        var n = CastFromScreenplayService.EnrichStubLooksFromSources(seeds, book, fountainText: null);
        Assert.True(n >= 1);
        Assert.Equal(beforeKeys, seeds.Keys.OrderBy(k => k).ToList());
        var buster = (Dictionary<string, object?>)seeds["Character_Buster"]!;
        var desc = buster["description"]?.ToString() ?? "";
        Assert.False(CastFromScreenplayService.IsStubLook(desc));
        Assert.True(
            desc.Contains("black", StringComparison.OrdinalIgnoreCase) ||
            desc.Contains("Buster", StringComparison.OrdinalIgnoreCase) ||
            desc.Contains("floppy", StringComparison.OrdinalIgnoreCase),
            "expected look text from book; got: " + desc);
        // Must not invent kitchen/backyard cast
        Assert.DoesNotContain(seeds.Keys, k => k.Contains("Kitchen", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(seeds.Keys, k => k.Contains("Backyard", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EnrichStubLooksFromSources_does_not_add_missing_heroes()
    {
        // Model forgot Buster — we do NOT invent him via heuristics.
        var seeds = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Character_Mom"] = new Dictionary<string, object?>
            {
                ["canonical_given_name"] = "Mom",
                ["description"] = "Adult woman.",
                ["display_name_policy"] = "ok_anytime",
            },
        };
        var book = "He's Buster the Noodle Head Dog. Small black and white dog.";
        var n = CastFromScreenplayService.EnrichStubLooksFromSources(seeds, book, null);
        Assert.Equal(0, n);
        Assert.Single(seeds);
        Assert.DoesNotContain(seeds.Keys, k => k.Contains("Buster", StringComparison.OrdinalIgnoreCase));
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
    public void SelectBookTextForCastPrompt_without_hints_uses_spine_only()
    {
        var early = string.Join("\n\n", Enumerable.Range(0, 120).Select(i =>
            $"Chapter filler {i}. " + new string('x', 1_200)));
        var lateLook =
            "\n\nZara stepped into the firelight. She had silver hair and a green velvet coat with brass buttons.\n\n";
        var after = string.Join("\n\n", Enumerable.Range(0, 40).Select(i =>
            $"Epilogue pad {i}. " + new string('y', 600)));
        var book = early + lateLook + after;
        Assert.True(book.Length > CastFromScreenplayService.BookPromptChars);

        // Production cast prompt path: no name-list guessing.
        var selected = CastFromScreenplayService.SelectBookTextForCastPrompt(
            book, maxChars: 40_000, nameHints: null);
        Assert.Contains("NARRATIVE SPINE", selected, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LOOK EXCERPTS", selected, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("spine only", selected, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectBookTextForCastPrompt_with_model_names_can_pull_late_looks()
    {
        // After model chooses Zara, look harvest may use her name — not cast inventing.
        var early = string.Join("\n\n", Enumerable.Range(0, 120).Select(i =>
            $"Chapter filler {i}. " + new string('x', 1_200)));
        var lateLook =
            "\n\nZara stepped into the firelight. She had silver hair and a green velvet coat with brass buttons.\n\n";
        var after = string.Join("\n\n", Enumerable.Range(0, 40).Select(i =>
            $"Epilogue pad {i}. " + new string('y', 600)));
        var book = early + lateLook + after;

        var selected = CastFromScreenplayService.SelectBookTextForCastPrompt(
            book, maxChars: 40_000, nameHints: new[] { "Zara" });

        Assert.Contains("silver hair", selected, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("green velvet", selected, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LOOK EXCERPTS", selected, StringComparison.OrdinalIgnoreCase);
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



    [Fact]
    public void DiscoverCandidateNames_finds_silent_and_titled_heroes()
    {
        const string fountain = """
            Title: BUSTER THE NOODLE HEAD DOG
            
            EXT. BACKYARD - DAY
            
            BUSTER bounds across the grass.
            
            NARRATOR (V.O.)
            He's Buster the Noodle Head Dog.
            
            INT. BEDROOM - NIGHT
            
            DADDY reading in bed. MOM enters.
            """;

        const string book = """
            BUSTER THE NOODLE HEAD DOG
            When Momma says it's time for bed,
            To get to Mom and Daddy's room.
            """;

        var candidates = CastFromScreenplayService.DiscoverCandidateNames(fountain, book);
        Assert.Contains("Buster", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Daddy", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Mom", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Narrator", candidates, StringComparer.OrdinalIgnoreCase);
        // Scene-heading places and stage verbs must never become cast candidates
        Assert.DoesNotContain("Backyard", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bedroom", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Day", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Night", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bounds", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Leaps", candidates, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoverCandidateNames_ignores_slugline_locations_and_action_verbs()
    {
        const string fountain = """
            INT. KITCHEN - DAY

            BUSTER LEAPS onto the table. BOUNDS OUT of the room.

            EXT. HALL - NIGHT

            MOMMA
            Come back!
            """;

        var candidates = CastFromScreenplayService.DiscoverCandidateNames(fountain, bookText: null);
        Assert.Contains("Buster", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Momma", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kitchen", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hall", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Leaps", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bounds", candidates, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Out", candidates, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ScrubFalseCastSeeds_removes_places_and_keeps_real_cast()
    {
        const string fountain = """
            EXT. BACKYARD - DAY
            BUSTER runs.
            INT. KITCHEN - NIGHT
            MOMMA
            Dinner!
            """;
        var seeds = new Dictionary<string, object?>
        {
            ["Character_Buster"] = new Dictionary<string, object?> { ["canonical_given_name"] = "Buster" },
            ["Character_Momma"] = new Dictionary<string, object?> { ["canonical_given_name"] = "Momma" },
            ["Character_Backyard"] = new Dictionary<string, object?> { ["canonical_given_name"] = "Backyard" },
            ["Character_Kitchen"] = new Dictionary<string, object?> { ["canonical_given_name"] = "Kitchen" },
            ["Character_Leaps"] = new Dictionary<string, object?> { ["canonical_given_name"] = "Leaps" },
        };

        var removed = CastFromScreenplayService.ScrubFalseCastSeeds(seeds, fountain, "Buster the dog. Momma smiles.");
        Assert.True(removed >= 3);
        Assert.True(seeds.ContainsKey("Character_Buster"));
        Assert.True(seeds.ContainsKey("Character_Momma"));
        Assert.False(seeds.ContainsKey("Character_Backyard"));
        Assert.False(seeds.ContainsKey("Character_Kitchen"));
        Assert.False(seeds.ContainsKey("Character_Leaps"));
    }

    [Fact]
    public void EnsureDiscoveredCastMembers_adds_missing_silent_hero_and_dad()
    {
        var seeds = new Dictionary<string, object?>
        {
            ["Character_Mom"] = new Dictionary<string, object?> { ["canonical_given_name"] = "Mom" },
            ["Character_Narrator"] = new Dictionary<string, object?> { ["canonical_given_name"] = "Narrator" }
        };

        var candidates = new[] { "Buster", "Daddy", "Mom", "Narrator" };
        const string book = "Buster the small black and white dog. Daddy reading in bed.";
        const string fountain = "BUSTER bounds. DADDY reading.";

        var added = CastFromScreenplayService.EnsureDiscoveredCastMembers(seeds, candidates, book, fountain);
        Assert.Equal(2, added);
        Assert.True(seeds.ContainsKey("Character_Buster"));
        Assert.True(seeds.ContainsKey("Character_Daddy"));
    }

    [Fact]
    public void EnsureDiscoveredCastMembers_does_not_force_locations()
    {
        var seeds = new Dictionary<string, object?>
        {
            ["Character_Buster"] = new Dictionary<string, object?> { ["canonical_given_name"] = "Buster" },
        };
        var added = CastFromScreenplayService.EnsureDiscoveredCastMembers(
            seeds,
            new[] { "Kitchen", "Backyard", "Leaps", "Buster" },
            bookText: "Buster the dog",
            fountainText: "INT. KITCHEN - DAY\nBUSTER runs.");
        Assert.Equal(0, added);
        Assert.False(seeds.ContainsKey("Character_Kitchen"));
        Assert.False(seeds.ContainsKey("Character_Backyard"));
        Assert.False(seeds.ContainsKey("Character_Leaps"));
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
        var known = @"C:\Users\budcr\source\repos\gemini\PageToMovie";
        if (File.Exists(Path.Combine(known, "prompts", "fountain_to_cast.txt")))
            return known;
        return null;
    }

}
