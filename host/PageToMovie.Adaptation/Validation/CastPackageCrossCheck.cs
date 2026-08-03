using System.Text.Json;
using System.Text.RegularExpressions;
using PageToMovie.Adaptation.Conversion;

namespace PageToMovie.Adaptation.Validation;

/// <summary>
/// Deterministic Stage-1→cast package gate: every speaking Fountain character must resolve
/// to a stable <c>cast_seeds.json</c> entry with production-usable look fields.
/// Used by benchmarks and as a pre-Stage-2 readiness check. Does not invent cast members.
/// Pure text only — no ProjectStore, no Engine.
/// </summary>
public static class CastPackageCrossCheck
{
    private static readonly Regex ExtensionStrip = new(
        @"\s*(\(.*\)|V\.?O\.?|O\.?S\.?|O\.?C\.?|CONT'D|CONTINUED)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex WordToken = new(
        @"[A-Za-z][A-Za-z']+",
        RegexOptions.Compiled);

    /// <summary>Speakers that never need cast plates (optional packaging).</summary>
    private static readonly HashSet<string> OptionalSpeakers = new(StringComparer.OrdinalIgnoreCase)
    {
        "NARRATOR", "NARRATION", "TITLE", "CARD", "SUPER", "CHYRON",
    };

    public sealed class Report
    {
        public bool Ok => Failures.Count == 0;
        /// <summary>0–100. Membership + description quality for speaking cast.</summary>
        public double Score { get; set; }
        public List<string> Speakers { get; set; } = new();
        public List<string> RequiredSpeakers { get; set; } = new();
        public List<string> CastKeys { get; set; } = new();
        public List<string> MatchedKeys { get; set; } = new();
        public List<string> Failures { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, CharacterQuality> Quality { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Speakers that look like invented proper names (not present in book text).
        /// Advisory — does not fail the package by itself.
        /// </summary>
        public List<string> SpeakersMissingFromBook { get; set; } = new();
    }

    public sealed class CharacterQuality
    {
        public string CastKey { get; set; } = "";
        public string Speaker { get; set; } = "";
        public bool HasDescription { get; set; }
        public bool HasVisualLock { get; set; }
        public bool HasWardrobe { get; set; }
        public bool HasSpecies { get; set; }
        public int DescriptionChars { get; set; }
        public List<string> Notes { get; set; } = new();
    }

    public static Report Evaluate(string? fountainText, string? castSeedsJson, string? bookText = null)
    {
        var report = new Report();
        var speakers = ExtractSpeakers(fountainText);
        report.Speakers = speakers.ToList();
        report.RequiredSpeakers = speakers
            .Where(s => !OptionalSpeakers.Contains(NormalizeSpeaker(s)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(bookText))
        {
            report.SpeakersMissingFromBook = FindSpeakersMissingFromBook(
                    report.RequiredSpeakers, bookText)
                .ToList();
            foreach (var name in report.SpeakersMissingFromBook)
            {
                report.Warnings.Add(
                    $"Speaking character '{name}' does not appear in the book text " +
                    "(possible invented name; prefer group tokens like CHILDREN for unnamed groups).");
            }
        }

        if (string.IsNullOrWhiteSpace(castSeedsJson))
        {
            report.Failures.Add(
                "No cast_seeds.json — cast package missing. Stage 1 alone is not a complete adaptation package.");
            report.Score = 0;
            return report;
        }

        Dictionary<string, JsonElement> seeds;
        try
        {
            seeds = ParseSeeds(castSeedsJson);
        }
        catch (Exception ex)
        {
            report.Failures.Add($"cast_seeds.json did not parse: {ex.Message}");
            report.Score = 0;
            return report;
        }

        report.CastKeys = seeds.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        if (seeds.Count == 0)
        {
            report.Failures.Add("character_seed_tokens is empty.");
            report.Score = 0;
            return report;
        }

        var points = 0.0;
        var maxPoints = 0.0;

        foreach (var speaker in report.RequiredSpeakers)
        {
            maxPoints += 10;
            var match = ResolveCastKey(speaker, seeds);
            if (match is null)
            {
                report.Failures.Add(
                    $"Speaking character '{speaker}' has no cast_seeds entry " +
                    $"(expected Character_{SanitizeKey(speaker)} or matching display name).");
                continue;
            }

            report.MatchedKeys.Add(match);
            points += 5; // membership
            var seed = seeds[match];
            var q = ScoreSeed(match, speaker, seed);
            report.Quality[match] = q;
            // description + visual detail (5 pts)
            var detail = 0.0;
            if (q.HasDescription && q.DescriptionChars >= 24) detail += 2.5;
            else if (q.HasDescription) detail += 1.0;
            else report.Failures.Add($"Cast '{match}' missing usable description.");

            if (q.HasVisualLock) detail += 1.5;
            else report.Warnings.Add($"Cast '{match}' missing visual_lock — portraits may drift.");

            if (q.HasWardrobe) detail += 0.5;
            else report.Warnings.Add($"Cast '{match}' missing wardrobe lock.");

            if (q.HasSpecies) detail += 0.5;
            else report.Warnings.Add($"Cast '{match}' missing species_kind.");

            points += Math.Min(5.0, detail);
        }

        // Extra cast not required for dialogue is fine (animals, background).
        foreach (var key in report.CastKeys)
        {
            if (report.MatchedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                continue;
            var seed = seeds[key];
            var q = ScoreSeed(key, key, seed);
            report.Quality[key] = q;
            if (!q.HasDescription)
                report.Warnings.Add($"Non-speaking cast '{key}' has weak/empty description.");
        }

        report.Score = maxPoints <= 0
            ? (report.Failures.Count == 0 ? 100 : 0)
            : Math.Round(100.0 * points / maxPoints, 1);

        if (report.RequiredSpeakers.Count == 0 && seeds.Count > 0)
            report.Warnings.Add("No non-narrator dialogue cues found — cast membership could not be cross-checked.");

        return report;
    }

    /// <summary>
    /// Speakers that look like invented proper names (title-case single tokens not found in book).
    /// Group words (CHILDREN, PUPILS, CROWD) and roles (TEACHER, NARRATOR) are allowed without book match.
    /// </summary>
    public static IReadOnlyList<string> FindSpeakersMissingFromBook(
        IEnumerable<string> speakers,
        string? bookText)
    {
        if (string.IsNullOrWhiteSpace(bookText))
            return Array.Empty<string>();

        var bookWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in WordToken.Matches(bookText))
            bookWords.Add(m.Value);

        var missing = new List<string>();
        foreach (var raw in speakers)
        {
            var s = NormalizeSpeaker(raw);
            if (string.IsNullOrWhiteSpace(s)) continue;
            if (OptionalSpeakers.Contains(s)) continue;
            if (IsAllowedWithoutBookMention(s)) continue;

            // Multi-word speakers: all significant tokens should appear, or full phrase.
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var found = parts.All(p => bookWords.Contains(p) || IsAllowedWithoutBookMention(p));
            if (!found && !bookWords.Contains(s.Replace(" ", "")))
                missing.Add(s);
        }

        return missing
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<string> ExtractSpeakers(string? fountainText)
    {
        if (string.IsNullOrWhiteSpace(fountainText))
            return Array.Empty<string>();

        // Line-scan (same assembly as BookToFountainConverter) — no Engine FountainParser.
        var list = new List<string>();
        foreach (var raw in BookToFountainConverter.EnumerateCharacterCueNames(fountainText))
        {
            var name = NormalizeSpeaker(raw);
            if (string.IsNullOrWhiteSpace(name))
                continue;
            list.Add(name);
        }

        return list
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string NormalizeSpeaker(string raw)
    {
        var t = (raw ?? "").Trim();
        if (t.StartsWith('@')) t = t[1..].Trim();
        t = ExtensionStrip.Replace(t, "").Trim();
        // Collapse internal whitespace
        t = Regex.Replace(t, @"\s+", " ");
        return t.ToUpperInvariant();
    }

    private static bool IsAllowedWithoutBookMention(string speaker)
    {
        // Roles / group chorus tokens common in adaptations of unnamed ensembles.
        return speaker is "TEACHER" or "MOTHER" or "FATHER" or "PARENT" or "DOCTOR"
            or "NURSE" or "POLICE" or "OFFICER" or "GUARD" or "KING" or "QUEEN"
            or "PRINCE" or "PRINCESS" or "CHILDREN" or "CHILD" or "KIDS" or "PUPILS"
            or "STUDENTS" or "CROWD" or "CLASS" or "CLASSMATES" or "VILLAGERS"
            or "TOWNSFOLK" or "PEOPLE" or "GROUP" or "CHORUS" or "ENSEMBLE"
            or "BOY" or "GIRL" or "MAN" or "WOMAN" or "LAMB" or "DOG" or "CAT"
            or "ANIMAL" or "CREATURE";
    }

    private static string SanitizeKey(string speaker) =>
        Regex.Replace(speaker, @"[^A-Za-z0-9]+", "_").Trim('_');

    private static string? ResolveCastKey(string speaker, Dictionary<string, JsonElement> seeds)
    {
        var want = NormalizeSpeaker(speaker);
        var wantKey = "Character_" + SanitizeKey(want);

        foreach (var key in seeds.Keys)
        {
            if (key.Equals(wantKey, StringComparison.OrdinalIgnoreCase))
                return key;
            var suffix = key.StartsWith("Character_", StringComparison.OrdinalIgnoreCase)
                ? key["Character_".Length..]
                : key;
            if (NormalizeSpeaker(suffix).Equals(want, StringComparison.OrdinalIgnoreCase))
                return key;

            if (seeds[key].ValueKind != JsonValueKind.Object)
                continue;
            foreach (var prop in new[] { "canonical_given_name", "display_name", "name" })
            {
                if (seeds[key].TryGetProperty(prop, out var p) &&
                    p.ValueKind == JsonValueKind.String &&
                    NormalizeSpeaker(p.GetString() ?? "").Equals(want, StringComparison.OrdinalIgnoreCase))
                    return key;
            }
        }

        return null;
    }

    private static CharacterQuality ScoreSeed(string key, string speaker, JsonElement seed)
    {
        var q = new CharacterQuality { CastKey = key, Speaker = speaker };
        if (seed.ValueKind != JsonValueKind.Object)
        {
            q.Notes.Add("not an object");
            return q;
        }

        var desc = GetText(seed, "description");
        var vlock = GetText(seed, "visual_lock");
        var wardrobe = GetText(seed, "wardrobe_lock") ?? GetText(seed, "wardrobe_always");
        var species = GetText(seed, "species_kind") ?? GetText(seed, "species");

        q.DescriptionChars = desc?.Length ?? 0;
        q.HasDescription = !string.IsNullOrWhiteSpace(desc) && q.DescriptionChars >= 8;
        q.HasVisualLock = !string.IsNullOrWhiteSpace(vlock) && vlock!.Length >= 8;
        q.HasWardrobe = !string.IsNullOrWhiteSpace(wardrobe);
        q.HasSpecies = !string.IsNullOrWhiteSpace(species);
        if (q.HasDescription && q.DescriptionChars < 40)
            q.Notes.Add("description is short for portrait generation");
        return q;
    }

    private static string? GetText(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }

    private static Dictionary<string, JsonElement> ParseSeeds(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        JsonElement seedsEl = default;
        var found = false;

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("character_seed_tokens", out seedsEl) &&
                seedsEl.ValueKind == JsonValueKind.Object)
                found = true;
            else if (root.TryGetProperty("global_production_variables", out var gpv) &&
                     gpv.ValueKind == JsonValueKind.Object &&
                     gpv.TryGetProperty("character_seed_tokens", out seedsEl) &&
                     seedsEl.ValueKind == JsonValueKind.Object)
                found = true;
            else if (root.TryGetProperty("cast_seeds", out var cs) &&
                     cs.ValueKind == JsonValueKind.Object &&
                     cs.TryGetProperty("character_seed_tokens", out seedsEl) &&
                     seedsEl.ValueKind == JsonValueKind.Object)
                found = true;
            // Whole object is character map (Character_* keys)
            else if (root.EnumerateObject().Any(p =>
                         p.Name.StartsWith("Character_", StringComparison.OrdinalIgnoreCase)))
            {
                seedsEl = root;
                found = true;
            }
        }

        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (!found)
            return map;

        foreach (var prop in seedsEl.EnumerateObject())
            map[prop.Name] = prop.Value.Clone();
        return map;
    }
}
