using System.Text.Json;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// Offline fixtures: required heroes must appear in book/fountain text the model will read.
/// Cast membership itself is decided by the model (LiveApi tests) — not regex name guessing.
/// </summary>
public class CastExtractGoldCorpusTests
{
    private static string GoldRoot
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CastExtractGold");
            if (Directory.Exists(dir)) return dir;
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "CastExtractGold"));
        }
    }

    private static string PackageFountainDir
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "BookToFountainPackage", "fountain_adaptations");
            if (Directory.Exists(dir)) return dir;
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "BookToFountainPackage", "fountain_adaptations"));
        }
    }

    public static IEnumerable<object[]> AllGoldCases()
    {
        if (!Directory.Exists(GoldRoot))
            yield break;
        foreach (var dir in Directory.GetDirectories(GoldRoot)
                     .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var id = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(id) || id.StartsWith('.')) continue;
            if (!File.Exists(Path.Combine(dir, "expected_keys.json"))) continue;
            yield return new object[] { id };
        }
    }

    [Fact]
    public void Gold_corpus_has_expected_case_count()
    {
        Assert.True(Directory.Exists(GoldRoot), $"Missing gold root: {GoldRoot}");
        var n = AllGoldCases().Count();
        Assert.True(n >= 10, $"Expected at least 10 gold cases (got {n}). Root={GoldRoot}");
    }

    /// <summary>
    /// Source material must mention required heroes so the cast model can see them
    /// (no offline regex inventing Character_* keys).
    /// </summary>
    [Theory]
    [MemberData(nameof(AllGoldCases))]
    public void Gold_case_sources_mention_required_heroes(string caseId)
    {
        var c = LoadCase(caseId);
        var blob = c.Book + "\n" + c.Fountain;

        foreach (var req in c.RequiredKeys)
        {
            var core = req.Replace("Character_", "", StringComparison.OrdinalIgnoreCase)
                .Replace('_', ' ');
            var tokens = core.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 3)
                .Where(t => !t.Equals("the", StringComparison.OrdinalIgnoreCase)
                            && !t.Equals("of", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (tokens.Count == 0) continue;
            var hit = tokens.Any(t => blob.Contains(t, StringComparison.OrdinalIgnoreCase));
            Assert.True(
                hit,
                $"[{caseId}] book/fountain never mention token for {req}. " +
                $"Wanted one of [{string.Join(", ", tokens)}]. Model cannot cast what it never reads.");
        }
    }

    /// <summary>
    /// Short books go into the cast prompt in full (title heroes visible end-to-end).
    /// Long books use spine samples only — not a guessed name list.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllGoldCases))]
    public void Gold_case_book_prompt_has_no_name_list_guessing(string caseId)
    {
        var c = LoadCase(caseId);
        var selected = CastFromScreenplayService.SelectBookTextForCastPrompt(
            c.Book, CastFromScreenplayService.BookPromptChars, nameHints: null);

        Assert.DoesNotContain("LOOK EXCERPTS", selected, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("REQUIRED CAST NAMES", selected, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HIGH-CONFIDENCE CAST NAMES", selected, StringComparison.OrdinalIgnoreCase);

        if (c.Book.Length <= CastFromScreenplayService.BookPromptChars)
        {
            // Full book present for the model (normalize newlines like SelectBookText does)
            var sample = c.Book.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (sample.Length > 60)
                sample = sample[..60];
            Assert.Contains(sample, selected, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("spine", selected, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Look enrichment only fills stubs for keys the model already returned —
    /// never invents new cast members from book words.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllGoldCases))]
    public void Gold_case_enrich_looks_does_not_invent_cast(string caseId)
    {
        var c = LoadCase(caseId);
        // Simulate model that only emitted first required key (or a placeholder).
        var seeds = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var first = c.RequiredKeys[0];
        seeds[first] = new Dictionary<string, object?>
        {
            ["canonical_given_name"] = first.Replace("Character_", "").Replace('_', ' '),
            ["description"] = "as described in the screenplay",
            ["visual_lock"] = "",
            ["display_name_policy"] = "ok_anytime",
        };

        var before = seeds.Keys.ToList();
        CastFromScreenplayService.EnrichStubLooksFromSources(seeds, c.Book, c.Fountain);
        Assert.Equal(before.Count, seeds.Count);
        Assert.All(before, k => Assert.True(seeds.ContainsKey(k)));

        foreach (var bad in c.ForbiddenKeySubstrings)
        {
            var hit = seeds.Keys.FirstOrDefault(k =>
                k.Contains(bad, StringComparison.OrdinalIgnoreCase));
            Assert.True(hit is null, $"[{caseId}] enrich invented forbidden fragment '{bad}' as '{hit}'");
        }
    }

    private sealed class GoldCase
    {
        public string Id { get; init; } = "";
        public string Fountain { get; init; } = "";
        public string Book { get; init; } = "";
        public List<string> RequiredKeys { get; init; } = new();
        public List<string> ForbiddenKeySubstrings { get; init; } = new();
    }

    private static GoldCase LoadCase(string caseId)
    {
        var dir = Path.Combine(GoldRoot, caseId);
        Assert.True(Directory.Exists(dir), dir);

        var metaPath = Path.Combine(dir, "expected_keys.json");
        Assert.True(File.Exists(metaPath), metaPath);
        using var doc = JsonDocument.Parse(File.ReadAllText(metaPath));
        var root = doc.RootElement;

        var required = root.GetProperty("required_keys").EnumerateArray()
            .Select(e => e.GetString() ?? "")
            .Where(s => s.Length > 0)
            .ToList();
        Assert.NotEmpty(required);

        var forbidden = new List<string>();
        if (root.TryGetProperty("forbidden_key_substrings", out var forb) &&
            forb.ValueKind == JsonValueKind.Array)
        {
            forbidden = forb.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => s.Length > 0)
                .ToList();
        }

        string fountain;
        if (root.TryGetProperty("fountain_from_package", out var pkg) &&
            pkg.GetString() is { Length: > 0 } pkgName)
        {
            var pkgPath = Path.Combine(PackageFountainDir, pkgName);
            Assert.True(File.Exists(pkgPath), $"Package fountain missing: {pkgPath}");
            fountain = File.ReadAllText(pkgPath);
        }
        else
        {
            var local = Path.Combine(dir, "screenplay.fountain");
            Assert.True(File.Exists(local), local);
            fountain = File.ReadAllText(local);
        }

        var bookPath = Path.Combine(dir, "book.txt");
        Assert.True(File.Exists(bookPath), bookPath);
        var book = File.ReadAllText(bookPath);

        return new GoldCase
        {
            Id = caseId,
            Fountain = fountain,
            Book = book,
            RequiredKeys = required,
            ForbiddenKeySubstrings = forbidden,
        };
    }
}
