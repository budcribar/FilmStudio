using System.Text.RegularExpressions;

namespace PageToMovie.Engine;

/// <summary>
/// Adaptation density: how much finished film a source yields when adapted naturally
/// (no pad-to-target, no forced crush below the story spine).
/// </summary>
/// <remarks>
/// <para><b>Definition</b></para>
/// <para>
/// <c>δ = natural_film_minutes / (source_words / 1000)</c>
/// — expected minutes of finished film per 1,000 source words under a natural adaptation.
/// </para>
/// <para>
/// Companion ratio (audiobook / full-prose speech baseline):
/// <c>τ = natural_film_minutes / audiobook_minutes</c>
/// where audiobook_minutes uses ~150 wpm on all source words (every word spoken).
/// τ ≪ 1 for novels (most prose is not spoken on screen); τ ≈ 1–1.5 for short verse
/// filmed at read-aloud pace with light staging.
/// </para>
/// <para><b>What density is not</b></para>
/// <list type="bullet">
/// <item>Not “read every word as dialogue” (that is audiobook length).</item>
/// <item>Not a user budget (budget = cut from natural when the user chooses).</item>
/// <item>Not post-Stage-2 clip ledger time (that refines after screenplay exists).</item>
/// </list>
/// <para><b>How natural film minutes are estimated pre-screenplay</b></para>
/// <list type="number">
/// <item>Very short sources (<500 words): slow speech (syllables + words) × staging multiplier.</item>
/// <item>Longer sources: baseline δ by book kind, adjusted by quoted-dialogue fraction, then
///     natural = δ × words/1000, clamped to sane feature/miniseries bands.</item>
/// </list>
/// </remarks>
public static class AdaptationDensity
{
    /// <summary>Full-prose narration reference rate (audiobook-ish), words per minute.</summary>
    public const double AudiobookWordsPerMinute = 150.0;

    /// <summary>Slow storybook read for short verse (matches ~2 min Mary), words per second.</summary>
    public const double StorybookWordsPerSecond = 1.15;

    /// <summary>Slow syllable rate for short verse performance read.</summary>
    public const double StorybookSyllablesPerSecond = 3.2;

    /// <summary>
    /// Staging multiplier on pure speech for short sources (establish, pans, silent business).
    /// ~40–50% overhead — see ActionCameraOverheadLedger discussion for clip-level costs.
    /// </summary>
    public const double ShortSourceStagingMultiplier = 1.45;

    // Baseline δ (film minutes per 1k source words) when dialogue mix is average.
    public const double DeltaPictureBook = 12.0;
    public const double DeltaShort = 5.0;
    public const double DeltaNovel = 2.0;

    private static readonly Regex QuotedSpan = new(
        "[\"“]([^\"”]{2,})[\"”]",
        RegexOptions.Compiled);

    public sealed class Estimate
    {
        public int SourceWords { get; init; }
        public int SourceSyllables { get; init; }
        public string BookKind { get; init; } = "";
        /// <summary>Fraction of characters inside quote marks (0–1), rough spoken-dialogue prior.</summary>
        public double QuotedDialogueFraction { get; init; }
        /// <summary>All source words at <see cref="AudiobookWordsPerMinute"/>.</summary>
        public double AudiobookMinutes { get; init; }
        /// <summary>δ — finished film minutes per 1,000 source words.</summary>
        public double MinutesPerThousandWords { get; init; }
        /// <summary>τ — natural film / audiobook; compression of temporal mass.</summary>
        public double TemporalCompressionRatio { get; init; }
        /// <summary>Natural finished-film minutes (starting point before any user cut).</summary>
        public int NaturalFilmMinutes { get; init; }
        /// <summary>How the estimate was derived (for logs / benchmark manifests).</summary>
        public string Method { get; init; } = "";
        public string Notes { get; init; } = "";
    }

    /// <summary>
    /// Pre-screenplay natural film estimate and density metrics for a prepared book.
    /// </summary>
    public static Estimate EstimateNatural(string? bookText, string? bookKind = null)
    {
        var text = bookText ?? "";
        var analysis = BookTextAnalyzer.Analyze(text);
        var kind = string.IsNullOrWhiteSpace(bookKind) ? analysis.BookKind : bookKind.Trim();
        var words = Math.Max(analysis.TextWords, ClipDurationEstimator.CountWords(text));
        var syllables = ClipDurationEstimator.CountSyllables(text);
        var quoteFrac = EstimateQuotedDialogueFraction(text);
        var audiobookMin = words <= 0 ? 0 : words / AudiobookWordsPerMinute;

        int natural;
        double delta;
        string method;
        string notes;

        if (words > 0 && words < 500)
        {
            // Speech-first: max(word path, syllable path) at storybook rates, then staging.
            var speechSec = Math.Max(
                words / StorybookWordsPerSecond,
                syllables / StorybookSyllablesPerSecond);
            var filmSec = speechSec * ShortSourceStagingMultiplier;
            natural = Math.Clamp((int)Math.Round(filmSec / 60.0), 2, 15);
            delta = words > 0 ? natural / (words / 1000.0) : DeltaPictureBook;
            method = "short_speech_x_staging";
            notes =
                $"Slow read-aloud speech × {ShortSourceStagingMultiplier:F2} staging; " +
                "no novel compression (film ≈ performance length).";
        }
        else
        {
            var baseDelta = kind switch
            {
                "picture_book" => DeltaPictureBook,
                "short" => DeltaShort,
                _ => DeltaNovel,
            };

            // More quoted dialogue → slightly denser film (talky); sparse quotes → more montage / lower δ.
            // quoteFrac typical novel ~0.15–0.35; scale ±25% around baseline.
            var dialogueFactor = 0.85 + 0.5 * Math.Clamp(quoteFrac / 0.30, 0.0, 1.0);
            delta = baseDelta * dialogueFactor;

            var raw = delta * (words / 1000.0);
            natural = kind switch
            {
                "picture_book" => Math.Clamp((int)Math.Round(raw), 2, 40),
                "short" => Math.Clamp((int)Math.Round(raw), 5, 60),
                // Feature → limited series band; uncapped audiobook is not the film.
                _ => Math.Clamp((int)Math.Round(raw), 40, 180),
            };
            // Recompute effective δ after clamp so reported density matches the minute number.
            delta = words > 0 ? natural / (words / 1000.0) : baseDelta;
            method = "kind_delta_x_dialogue_mix";
            notes =
                $"Baseline δ by kind ({kind}) adjusted by quoted-dialogue fraction {quoteFrac:P0}; " +
                "natural film is adaptation length, not full-prose speech.";
        }

        var tau = audiobookMin > 0.01 ? natural / audiobookMin : 0;

        return new Estimate
        {
            SourceWords = words,
            SourceSyllables = syllables,
            BookKind = kind,
            QuotedDialogueFraction = Math.Round(quoteFrac, 3),
            AudiobookMinutes = Math.Round(audiobookMin, 1),
            MinutesPerThousandWords = Math.Round(delta, 2),
            TemporalCompressionRatio = Math.Round(tau, 3),
            NaturalFilmMinutes = natural,
            Method = method,
            Notes = notes,
        };
    }

    /// <summary>
    /// Suggested reduced budget for dual benchmarks: half of natural, floored for longform.
    /// Returns null when the book is short enough that reduce mode should be skipped.
    /// </summary>
    public static int? SuggestReducedBenchmarkMinutes(Estimate natural, int longThresholdMinutes = 45)
    {
        if (natural.NaturalFilmMinutes < longThresholdMinutes)
            return null;
        // Half natural, but keep a usable featurette floor for long books.
        var half = (int)Math.Round(natural.NaturalFilmMinutes * 0.5);
        return Math.Clamp(half, 20, natural.NaturalFilmMinutes - 5);
    }

    /// <summary>Rough prior: character mass inside ASCII/curly quotes over total letters.</summary>
    public static double EstimateQuotedDialogueFraction(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var letters = text.Count(char.IsLetter);
        if (letters < 20) return 0;

        var quotedLetters = 0;
        foreach (Match m in QuotedSpan.Matches(text))
            quotedLetters += m.Groups[1].Value.Count(char.IsLetter);

        return Math.Clamp(quotedLetters / (double)letters, 0, 1);
    }
}
