using AdaptationCrossCheck = PageToMovie.Adaptation.Validation.CastPackageCrossCheck;

namespace PageToMovie.Engine;

/// <summary>
/// Thin Engine façade over <see cref="AdaptationCrossCheck"/> for backward-compatible call sites.
/// Prefer <see cref="PageToMovie.Adaptation.AdaptationService.CrossCheckCast"/> or
/// <see cref="AdaptationCrossCheck"/> directly.
/// </summary>
public static class CastPackageCrossCheck
{
    public sealed class Report
    {
        public bool Ok => Failures.Count == 0;
        public double Score { get; set; }
        public List<string> Speakers { get; set; } = new();
        public List<string> RequiredSpeakers { get; set; } = new();
        public List<string> CastKeys { get; set; } = new();
        public List<string> MatchedKeys { get; set; } = new();
        public List<string> Failures { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, CharacterQuality> Quality { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public List<string> SpeakersMissingFromBook { get; set; } = new();
        public List<string> SpeakersMissingFromCast { get; set; } = new();
        public List<string> GroupCastKeys { get; set; } = new();
        public double MembershipScore { get; set; }
        public double DescriptionScore { get; set; }
        public bool NoInventedNames => SpeakersMissingFromBook.Count == 0;
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
        => Map(AdaptationCrossCheck.Evaluate(fountainText, castSeedsJson, bookText));

    public static IReadOnlyList<string> ExtractSpeakers(string? fountainText)
        => AdaptationCrossCheck.ExtractSpeakers(fountainText);

    public static IReadOnlyList<string> FindSpeakersMissingFromBook(
        IEnumerable<string> speakers,
        string? bookText)
        => AdaptationCrossCheck.FindSpeakersMissingFromBook(speakers, bookText);

    private static Report Map(AdaptationCrossCheck.Report r)
    {
        var report = new Report
        {
            Score = r.Score,
            Speakers = r.Speakers.ToList(),
            RequiredSpeakers = r.RequiredSpeakers.ToList(),
            CastKeys = r.CastKeys.ToList(),
            MatchedKeys = r.MatchedKeys.ToList(),
            Failures = r.Failures.ToList(),
            Warnings = r.Warnings.ToList(),
            SpeakersMissingFromBook = r.SpeakersMissingFromBook.ToList(),
            SpeakersMissingFromCast = r.SpeakersMissingFromCast.ToList(),
            GroupCastKeys = r.GroupCastKeys.ToList(),
            MembershipScore = r.MembershipScore,
            DescriptionScore = r.DescriptionScore,
        };
        foreach (var (key, q) in r.Quality)
        {
            report.Quality[key] = new CharacterQuality
            {
                CastKey = q.CastKey,
                Speaker = q.Speaker,
                HasDescription = q.HasDescription,
                HasVisualLock = q.HasVisualLock,
                HasWardrobe = q.HasWardrobe,
                HasSpecies = q.HasSpecies,
                DescriptionChars = q.DescriptionChars,
                Notes = q.Notes.ToList(),
            };
        }
        return report;
    }
}
