using System.Security.Cryptography;
using System.Text;
using PageToMovie.Adaptation.Contracts;
using PageToMovie.Adaptation.Conversion;
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
    /// System prompt for book → Fountain (embedded <c>book_to_fountain.txt</c>).
    /// </summary>
    public Task<string> BuildSystemPromptAsync(int totalRuntimeMinutes, CancellationToken ct = default) =>
        BookToFountainConverter.BuildSystemPromptAsync(totalRuntimeMinutes, ct);

    /// <summary>
    /// Offline / test heuristic path (no chat).
    /// </summary>
    public string ConvertHeuristic(string title, string bookText, string? author = null) =>
        BookToFountainConverter.ConvertHeuristic(title, bookText, author);

    /// <summary>
    /// Full Stage‑1 convert via <see cref="BookToFountainConverter"/>.
    /// </summary>
    public async Task<AdaptationResult> ConvertAsync(
        AdaptationRequest request,
        IChatClient chat,
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        Func<StructuralGateFailure, CancellationToken, Task>? onStructuralGateFailure = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(chat);

        var analysis = AnalyzeBook(request.BookText);
        var runtime = ResolveTargetMinutes(request.BookText, request.TargetRuntimeMinutes);
        var minutes = Math.Clamp(runtime.TargetMinutes, MinRuntimeMinutes, MaxRuntimeMinutes);

        Action<string>? onProgress = progress is null ? null : s => progress.Report(s);
        var usedHeuristic = false;

        string promptSha;
        try
        {
            var prompt = await BuildSystemPromptAsync(minutes, ct).ConfigureAwait(false);
            promptSha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(prompt))).ToLowerInvariant();
        }
        catch
        {
            promptSha = "";
        }

        var conversion = await BookToFountainConverter.ConvertWithMetadataAsync(
            title: string.IsNullOrWhiteSpace(request.Title) ? "Untitled" : request.Title!,
            bookText: request.BookText,
            author: request.Author,
            totalRuntimeMinutes: minutes,
            chat: chat,
            model: request.ModelId,
            onProgress: onProgress,
            ct: ct,
            onHeuristicFallback: _ => usedHeuristic = true,
            reasoningEffort: request.ReasoningEffort,
            onStructuralGateFailure: onStructuralGateFailure,
            temperature: request.Temperature).ConfigureAwait(false);

        // Re-emit runtime with the clamped minutes actually used for generation.
        var runtimeUsed = new NaturalRuntimeEstimate
        {
            NaturalMinutes = runtime.NaturalMinutes,
            TargetMinutes = minutes,
            Mode = runtime.Mode,
            Method = runtime.Method,
            SourceWords = runtime.SourceWords,
            SourceSyllables = runtime.SourceSyllables,
            BookKind = runtime.BookKind,
            MinutesPerThousandWords = runtime.MinutesPerThousandWords,
            TemporalCompressionRatio = runtime.TemporalCompressionRatio,
            QuotedDialogueFraction = runtime.QuotedDialogueFraction,
            Notes = runtime.Notes,
        };

        return new AdaptationResult
        {
            Fountain = conversion.Fountain,
            VisionMeta = conversion.VisionMeta,
            VisionMetaStatus = conversion.VisionMetaStatus,
            VisionMetaError = conversion.VisionMetaError,
            Runtime = runtimeUsed,
            Analysis = analysis,
            UsedHeuristicFallback = usedHeuristic,
            PromptContentSha256 = promptSha,
            Notes = conversion.VisionMetaError,
        };
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
