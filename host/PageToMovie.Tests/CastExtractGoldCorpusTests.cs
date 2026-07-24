using System.Text.Json;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// Release-gate gold corpus: every book case must keep required Character_* keys
/// even when the model only emits dialogue speakers (Buster-class failure).
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
    /// Core release gate: speaker-only model + production backfill must cover required keys.
    /// This is the Buster regression (silent title hero dropped).
    /// </summary>
    [Theory]
    [MemberData(nameof(AllGoldCases))]
    public void Gold_case_backfill_covers_required_keys_when_model_is_speaker_only(string caseId)
    {
        var c = LoadCase(caseId);
        var speakerSeeds = BuildSpeakerOnlySeeds(c.Fountain);
        var before = speakerSeeds.Keys.OrderBy(k => k).ToList();

        var hints = CastFromScreenplayService.CollectCastNameHints(c.Fountain, c.Book);
        var added = CastFromScreenplayService.EnsureSeedsForNameHints(
            speakerSeeds, hints, c.Book, c.Fountain);

        var have = speakerSeeds.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        var missing = c.RequiredKeys
            .Where(req => !SeedSetCoversRequiredKey(speakerSeeds, req))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"[{caseId}] missing required cast after backfill (added={added}).\n" +
            $"  Missing: {string.Join(", ", missing)}\n" +
            $"  Speakers-only before: {string.Join(", ", before)}\n" +
            $"  After backfill: {string.Join(", ", have)}\n" +
            $"  Name hints ({hints.Count}): {string.Join(", ", hints.Take(24))}");

        foreach (var bad in c.ForbiddenKeySubstrings)
        {
            var hit = have.FirstOrDefault(k =>
                k.Contains(bad, StringComparison.OrdinalIgnoreCase));
            Assert.True(
                hit is null,
                $"[{caseId}] forbidden key fragment '{bad}' present as '{hit}' in {string.Join(", ", have)}");
        }
    }

    /// <summary>Each case is also listed as its own named fact surface for IDE discoverability.</summary>
    [Theory]
    [MemberData(nameof(AllGoldCases))]
    public void Gold_case_name_hints_mention_required_heroes(string caseId)
    {
        var c = LoadCase(caseId);
        var hints = CastFromScreenplayService.CollectCastNameHints(c.Fountain, c.Book);
        var hintBlob = string.Join(" | ", hints);

        foreach (var req in c.RequiredKeys)
        {
            var core = req.Replace("Character_", "", StringComparison.OrdinalIgnoreCase)
                .Replace('_', ' ');
            // At least one token of the required key should appear in hints (Buster, Mowgli, …)
            var tokens = core.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 3)
                .Where(t => !t.Equals("the", StringComparison.OrdinalIgnoreCase)
                            && !t.Equals("of", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (tokens.Count == 0) continue;
            var hit = tokens.Any(t =>
                hints.Any(h => h.Contains(t, StringComparison.OrdinalIgnoreCase)));
            Assert.True(
                hit,
                $"[{caseId}] name hints missing token for {req}. " +
                $"Wanted one of [{string.Join(", ", tokens)}]. Hints: {hintBlob}");
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

    /// <summary>
    /// Simulates a dialogue-biased model: only Fountain character cues become seeds.
    /// </summary>
    private static Dictionary<string, object?> BuildSpeakerOnlySeeds(string fountain)
    {
        var seeds = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var parsed = FountainParser.Parse(fountain);
            foreach (var el in parsed.Elements)
            {
                if (el.Type != FountainParser.ElementType.Character) continue;
                var raw = (el.Text ?? "").Trim();
                raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\s*\([^)]*\)\s*$", "").Trim();
                raw = raw.TrimStart('@', '^', '*').Trim();
                if (raw.Length < 2) continue;
                var key = CastFromScreenplayService.NameToCharacterKey(raw);
                if (seeds.ContainsKey(key)) continue;
                var display = raw.Length <= 40 ? raw : raw[..40];
                seeds[key] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["canonical_given_name"] = display,
                    ["description"] = $"{display} (speaker cue only).",
                    ["display_name_policy"] = "ok_anytime",
                    ["voice_label"] = display.Replace(' ', '_'),
                };
            }
        }
        catch
        {
            // empty speaker set — backfill must still recover from book
        }
        return seeds;
    }

    /// <summary>
    /// Flexible coverage: Character_Marley matches Character_Marley_Ghost;
    /// Character_Mom matches Character_Momma; Character_Van_Helsing matches Character_Professor_Van_Helsing.
    /// </summary>
    private static bool SeedSetCoversRequiredKey(
        Dictionary<string, object?> seeds,
        string requiredKey)
    {
        if (seeds.ContainsKey(requiredKey))
            return true;

        var want = NormalizeKeyCore(requiredKey);
        if (want.Length < 2) return false;

        foreach (var (key, val) in seeds)
        {
            var have = NormalizeKeyCore(key);
            if (have == want) return true;
            if (have.Contains(want, StringComparison.Ordinal) || want.Contains(have, StringComparison.Ordinal))
            {
                if (Math.Min(have.Length, want.Length) >= 4)
                    return true;
            }
            // token overlap: bobcratchit vs bob + cratchit
            if (CoreTokens(want).All(t => have.Contains(t, StringComparison.Ordinal)))
                return true;

            if (val is Dictionary<string, object?> seed &&
                seed.TryGetValue("canonical_given_name", out var gn) &&
                gn?.ToString() is { Length: > 0 } given)
            {
                var g = NormalizeKeyCore(given);
                if (g == want || g.Contains(want, StringComparison.Ordinal) || want.Contains(g, StringComparison.Ordinal))
                    return true;
            }
        }
        return false;
    }

    private static string NormalizeKeyCore(string key)
    {
        var s = (key ?? "").Trim();
        if (s.StartsWith("Character_", StringComparison.OrdinalIgnoreCase))
            s = s["Character_".Length..];
        s = s.ToLowerInvariant();
        s = System.Text.RegularExpressions.Regex.Replace(s, @"[^a-z0-9]+", "");
        return s switch
        {
            "momma" or "mommy" or "mama" or "mother" => "mom",
            "daddy" or "dad" or "papa" or "father" => "dad",
            "marleysghost" or "ghostofmarley" => "marley",
            _ => s,
        };
    }

    private static IEnumerable<string> CoreTokens(string normalized) =>
        // split camel/underscore remnants already stripped — use 4+ char chunks from original if needed
        System.Text.RegularExpressions.Regex.Split(normalized, @"(?<=[a-z])(?=[A-Z])|_+")
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length >= 4);
}
