using System.Security.Cryptography;
using System.Text;
using PageToMovie.Adaptation.Contracts;
using PageToMovie.Adaptation.Conversion;
using PageToMovie.Adaptation.Validation;
using PageToMovie.Core.Abstractions;

namespace PageToMovie.Adaptation;

/// <summary>
/// Public Stage‑1 façade: pure book text in, fountain / estimates / analysis out.
/// No ProjectStore, paths, or HTTP. Callers inject <see cref="IChatClient"/>.
/// </summary>
public sealed class AdaptationService
{
    public const int MinRuntimeMinutes = NaturalRuntime.MinMinutes;
    public const int MaxRuntimeMinutes = NaturalRuntime.MaxMinutes;

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
    /// Density-only natural runtime estimate via <see cref="AdaptationDensity"/> /
    /// <see cref="NaturalRuntime"/>.
    /// </summary>
    public NaturalRuntimeEstimate EstimateNaturalRuntime(string bookText)
    {
        var e = AdaptationDensity.EstimateNatural(bookText ?? "");
        var natural = NaturalRuntime.ClampMinutes(e.NaturalFilmMinutes);
        return ToNaturalRuntimeEstimate(e, targetMinutes: natural, mode: natural > 0 ? "natural" : "none");
    }

    /// <summary>
    /// Natural + optional override clamp (2–180). Pure — no store.
    /// </summary>
    public NaturalRuntimeEstimate ResolveTargetMinutes(string bookText, int? overrideMinutes = null)
    {
        var e = AdaptationDensity.EstimateNatural(bookText ?? "");
        var (natural, target, mode) = NaturalRuntime.Resolve(bookText, overrideMinutes);
        // Prefer density estimate details; clamp natural to the resolved value.
        return ToNaturalRuntimeEstimate(
            e,
            targetMinutes: target,
            mode: mode,
            naturalOverride: natural);
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
    /// Deterministic cast package gate: speaking Fountain cues must resolve to cast_seeds
    /// with usable look fields. Optional <paramref name="bookText"/> flags invented names.
    /// </summary>
    public CastPackageCrossCheck.Report CrossCheckCast(
        string? fountainText,
        string? castSeedsJson,
        string? bookText = null) =>
        CastPackageCrossCheck.Evaluate(fountainText, castSeedsJson, bookText);

    /// <summary>
    /// Full Stage‑1 convert via <see cref="BookToFountainConverter"/>.
    /// Optional <paramref name="bookSession"/> enables provider file_id + multi-turn
    /// (retry/coverage/merge/repair without re-billing full book tokens).
    /// </summary>
    public async Task<AdaptationResult> ConvertAsync(
        AdaptationRequest request,
        IChatClient chat,
        IProgress<string>? progress = null,
        CancellationToken ct = default,
        Func<StructuralGateFailure, CancellationToken, Task>? onStructuralGateFailure = null,
        IBookFileSession? bookSession = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(chat);

        var analysis = AnalyzeBook(request.BookText);
        var runtime = ResolveTargetMinutes(request.BookText, request.TargetRuntimeMinutes);
        var minutes = NaturalRuntime.ClampMinutes(runtime.TargetMinutes);

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
            temperature: request.Temperature,
            bookSession: bookSession).ConfigureAwait(false);

        // Re-emit runtime with the clamped minutes actually used for generation.
        var runtimeUsed = new NaturalRuntimeEstimate
        {
            NaturalMinutes = runtime.NaturalMinutes,
            TargetMinutes = minutes,
            Mode = NaturalRuntime.ResolveMode(runtime.NaturalMinutes, minutes),
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
        string mode,
        int? naturalOverride = null) =>
        new()
        {
            NaturalMinutes = naturalOverride ?? NaturalRuntime.ClampMinutes(e.NaturalFilmMinutes),
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
