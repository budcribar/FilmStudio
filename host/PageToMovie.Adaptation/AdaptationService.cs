using PageToMovie.Adaptation.Contracts;
using PageToMovie.Core.Abstractions;

namespace PageToMovie.Adaptation;

/// <summary>
/// Public Stage‑1 façade: pure book text in, fountain / estimates / analysis out.
/// No ProjectStore, paths, or HTTP. Callers inject <see cref="IChatClient"/>.
/// </summary>
public sealed class AdaptationService
{
    public const int MinRuntimeMinutes = 2;
    public const int MaxRuntimeMinutes = 180;

    /// <summary>
    /// Kind, words, quality notes, natural minutes via <see cref="BookTextAnalyzer"/>.
    /// </summary>
    public BookAnalysisResult AnalyzeBook(string bookText)
    {
        var a = BookTextAnalyzer.Analyze(bookText ?? "");
        return new BookAnalysisResult
        {
            Pages = a.Pages,
            TextChars = a.TextChars,
            TextWords = a.TextWords,
            LetterRatio = a.LetterRatio,
            EmptyPageRatio = a.EmptyPageRatio,
            SparsePageRatio = a.SparsePageRatio,
            AvgCharsPerPage = a.AvgCharsPerPage,
            GarbageScore = a.GarbageScore,
            TextQuality = a.TextQuality,
            TextDensity = a.TextDensity,
            BookKind = a.BookKind,
            ReadyForStage1 = a.ReadyForStage1,
            SuggestedTotalMinutes = a.SuggestedTotalMinutes,
            SuggestedChunkPages = a.SuggestedChunkPages,
            Notes = a.Notes?.ToArray() ?? Array.Empty<string>(),
            TextEngine = a.TextEngine,
        };
    }

    /// <summary>
    /// Density-only natural runtime estimate via <see cref="AdaptationDensity"/>.
    /// </summary>
    public NaturalRuntimeEstimate EstimateNaturalRuntime(string bookText)
    {
        var e = AdaptationDensity.EstimateNatural(bookText);
        return ToNaturalRuntimeEstimate(e, targetMinutes: e.NaturalFilmMinutes, mode: "natural");
    }

    /// <summary>
    /// Natural + optional override clamp (2–180). Pure — no store.
    /// </summary>
    public NaturalRuntimeEstimate ResolveTargetMinutes(string bookText, int? overrideMinutes = null)
    {
        var e = AdaptationDensity.EstimateNatural(bookText);
        int target;
        string mode;
        if (overrideMinutes is > 0)
        {
            target = Math.Clamp(overrideMinutes.Value, MinRuntimeMinutes, MaxRuntimeMinutes);
            mode = target == e.NaturalFilmMinutes
                ? "natural"
                : target < e.NaturalFilmMinutes ? "reduced" : "custom";
        }
        else
        {
            target = Math.Clamp(e.NaturalFilmMinutes, MinRuntimeMinutes, MaxRuntimeMinutes);
            mode = "natural";
        }

        return ToNaturalRuntimeEstimate(e, target, mode);
    }

    /// <summary>
    /// Full Stage‑1 convert. Phase 2 implements via moved <c>BookToFountainConverter</c>.
    /// </summary>
    public Task<AdaptationResult> ConvertAsync(
        AdaptationRequest request,
        IChatClient chat,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(chat);
        _ = progress;
        _ = ct;
        throw new NotImplementedException(
            "Phase 2: move BookToFountainConverter into PageToMovie.Adaptation.Conversion " +
            "and implement ConvertAsync (see adaptation-module-implementation-plan.md A2.x).");
    }

    private static NaturalRuntimeEstimate ToNaturalRuntimeEstimate(
        AdaptationDensity.Estimate e,
        int targetMinutes,
        string mode) =>
        new()
        {
            NaturalMinutes = e.NaturalFilmMinutes,
            TargetMinutes = targetMinutes,
            Mode = mode,
            Method = e.Method,
            SourceWords = e.SourceWords,
            SourceSyllables = e.SourceSyllables,
            BookKind = e.BookKind,
            MinutesPerThousandWords = e.MinutesPerThousandWords,
            TemporalCompressionRatio = e.TemporalCompressionRatio,
            QuotedDialogueFraction = e.QuotedDialogueFraction,
            Notes = e.Notes,
        };
}
