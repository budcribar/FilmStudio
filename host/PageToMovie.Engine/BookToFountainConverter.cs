using PageToMovie.Adaptation.Conversion;
using PageToMovie.Adaptation.Contracts;
using PageToMovie.Core.Abstractions;
using AdaptationConverter = PageToMovie.Adaptation.Conversion.BookToFountainConverter;
using AdaptationConversionResultCore = PageToMovie.Adaptation.Conversion.AdaptationConversionResult;

namespace PageToMovie.Engine;

public enum VisionMetaStatus
{
    PrimaryResponse,
    RepairResponse,
    Missing,
    Malformed,
    InvalidValue,
}

/// <summary>
/// Engine-facing conversion result (maps Adaptation vision DTOs → <see cref="ProjectVisionMeta.Document"/>).
/// </summary>
public sealed record AdaptationConversionResult
{
    public required string Fountain { get; init; }
    public ProjectVisionMeta.Document? VisionMeta { get; init; }
    public VisionMetaStatus VisionMetaStatus { get; init; }
    public string? VisionMetaError { get; init; }
}

/// <summary>
/// Thin Engine façade over <see cref="AdaptationConverter"/> for backward-compatible call sites.
/// Production Stage‑1 path prefers <see cref="PageToMovie.Adaptation.AdaptationService.ConvertAsync"/>.
/// </summary>
public static class BookToFountainConverter
{
    public const int SingleShotMaxChars = AdaptationConverter.SingleShotMaxChars;
    public const int ChunkSoftMaxChars = AdaptationConverter.ChunkSoftMaxChars;
    public const int MaxAdaptChunks = AdaptationConverter.MaxAdaptChunks;
    public const int AbsoluteMaxAdaptChunks = AdaptationConverter.AbsoluteMaxAdaptChunks;
    public const int DefaultSingleShotBookMaxChars = AdaptationConverter.DefaultSingleShotBookMaxChars;
    public const int DefaultChunkSoftMaxChars = AdaptationConverter.DefaultChunkSoftMaxChars;
    public const int MinBookCharsForChunkFallback = AdaptationConverter.MinBookCharsForChunkFallback;
    public const int AbsoluteSingleShotCeiling = AdaptationConverter.AbsoluteSingleShotCeiling;
    public const int ReservedOverheadChars = AdaptationConverter.ReservedOverheadChars;
    public const string FountainOutputOverride = AdaptationConverter.FountainOutputOverride;

    public enum AdaptPath
    {
        Single = 0,
        Multi = 1,
    }

    public sealed class PromptBudget
    {
        public required string ModelId { get; init; }
        public int SingleShotBookMaxChars { get; init; }
        public int ChunkSoftMaxChars { get; init; }
        public int MaxChunks { get; init; }
        public int ReservedOverheadChars { get; init; }

        internal AdaptationConverter.PromptBudget ToCore() => new()
        {
            ModelId = ModelId,
            SingleShotBookMaxChars = SingleShotBookMaxChars,
            ChunkSoftMaxChars = ChunkSoftMaxChars,
            MaxChunks = MaxChunks,
            ReservedOverheadChars = ReservedOverheadChars,
        };

        internal static PromptBudget FromCore(AdaptationConverter.PromptBudget b) => new()
        {
            ModelId = b.ModelId,
            SingleShotBookMaxChars = b.SingleShotBookMaxChars,
            ChunkSoftMaxChars = b.ChunkSoftMaxChars,
            MaxChunks = b.MaxChunks,
            ReservedOverheadChars = b.ReservedOverheadChars,
        };
    }

    public sealed class QualityResult
    {
        public bool Ok { get; init; }
        public string Reason { get; init; } = "";
        public int SceneCount { get; init; }
        public int FountainChars { get; init; }
        public IReadOnlyList<string> Failures { get; init; } = Array.Empty<string>();
        public bool HasHardFailure { get; init; }

        internal static QualityResult FromCore(AdaptationConverter.QualityResult q) => new()
        {
            Ok = q.Ok,
            Reason = q.Reason,
            SceneCount = q.SceneCount,
            FountainChars = q.FountainChars,
            Failures = q.Failures,
            HasHardFailure = q.HasHardFailure,
        };
    }

    public static async Task<AdaptationConversionResult> ConvertWithMetadataAsync(
        string workspaceRoot,
        string title,
        string bookText,
        string? author = null,
        int totalRuntimeMinutes = 10,
        IChatClient? chat = null,
        string? model = null,
        Action<string>? onProgress = null,
        CancellationToken ct = default,
        PromptBudget? budgetOverride = null,
        Action<string>? onHeuristicFallback = null,
        string? reasoningEffort = null,
        GenerationErrorLogger? errorLogger = null,
        string? jobId = null,
        string? projectId = null,
        double temperature = 0.2)
    {
        _ = workspaceRoot; // prompts are embedded in Adaptation; kept for call-site compatibility
        Func<StructuralGateFailure, CancellationToken, Task>? onFail = null;
        if (errorLogger is not null)
        {
            onFail = async (fail, token) =>
            {
                await errorLogger.LogAsync(new GenerationErrorRecord
                {
                    ProjectId = projectId,
                    JobId = jobId,
                    Stage = fail.Stage,
                    Model = fail.Model,
                    ErrorType = fail.ErrorType,
                    ErrorMessage = fail.ErrorMessage,
                    Resolved = false,
                    ResponseSummary = fail.ResponseSummary,
                }, token).ConfigureAwait(false);
            };
        }

        var core = await AdaptationConverter.ConvertWithMetadataAsync(
            title: title,
            bookText: bookText,
            author: author,
            totalRuntimeMinutes: totalRuntimeMinutes,
            chat: chat,
            model: model,
            onProgress: onProgress,
            ct: ct,
            budgetOverride: budgetOverride?.ToCore(),
            onHeuristicFallback: onHeuristicFallback,
            reasoningEffort: reasoningEffort,
            onStructuralGateFailure: onFail,
            temperature: temperature).ConfigureAwait(false);

        return MapResult(core);
    }

    public static (string Fountain, ProjectVisionMeta.Document? Vision) SplitVisionMetaTrailer(string? text)
    {
        var (fountain, vision) = AdaptationConverter.SplitVisionMetaTrailer(text);
        return (fountain, MapVision(vision));
    }

    public static IReadOnlyList<string> FindVagueLocationHeadings(string? fountain) =>
        AdaptationConverter.FindVagueLocationHeadings(fountain);

    public static bool HeadingContainsVagueLocationLanguage(string? heading) =>
        AdaptationConverter.HeadingContainsVagueLocationLanguage(heading);

    public static IReadOnlyList<string> FindGenericNumberedSpeakers(string? fountain) =>
        AdaptationConverter.FindGenericNumberedSpeakers(fountain);

    public static bool IsGenericNumberedSpeaker(string? characterName) =>
        AdaptationConverter.IsGenericNumberedSpeaker(characterName);

    public static int SoftMaxSceneHeadings(string? bookKind) =>
        AdaptationConverter.SoftMaxSceneHeadings(bookKind);

    public static string NormalizeSceneHeadingWording(string? fountain) =>
        AdaptationConverter.NormalizeSceneHeadingWording(fountain);

    public static bool IsLocationNameAlias(string longer, string shorter) =>
        AdaptationConverter.IsLocationNameAlias(longer, shorter);

    public static string FixDraftDate(string? fountain) =>
        AdaptationConverter.FixDraftDate(fountain);

    public static PromptBudget ResolvePromptBudget(string? modelId) =>
        PromptBudget.FromCore(AdaptationConverter.ResolvePromptBudget(modelId));

    public static int ResolveMaxChunks(string? bookText, PromptBudget budget) =>
        AdaptationConverter.ResolveMaxChunks(bookText, budget.ToCore());

    public static bool FitsSingleShot(string bookText, PromptBudget budget) =>
        AdaptationConverter.FitsSingleShot(bookText, budget.ToCore());

    public static bool ShouldChunkFallback(string bookText, PromptBudget budget) =>
        AdaptationConverter.ShouldChunkFallback(bookText, budget.ToCore());

    public static QualityResult EvaluateQuality(
        string fountain,
        string bookText,
        int totalRuntimeMinutes,
        AdaptPath path) =>
        QualityResult.FromCore(AdaptationConverter.EvaluateQuality(
            fountain, bookText, totalRuntimeMinutes,
            path == AdaptPath.Multi ? AdaptationConverter.AdaptPath.Multi : AdaptationConverter.AdaptPath.Single));

    public static string StripBookPageTags(string? fountain) =>
        AdaptationConverter.StripBookPageTags(fountain);

    public static string StripFountainPageBreaks(string? fountain) =>
        AdaptationConverter.StripFountainPageBreaks(fountain);

    public static Task<string> BuildSystemPromptAsync(
        string workspaceRoot,
        int totalRuntimeMinutes,
        CancellationToken ct = default)
    {
        _ = workspaceRoot;
        return AdaptationConverter.BuildSystemPromptAsync(totalRuntimeMinutes, ct);
    }

    public static IReadOnlyList<string> ChunkBookForAdaptation(
        string bookText,
        int maxChunks = MaxAdaptChunks,
        int softMaxChars = ChunkSoftMaxChars) =>
        AdaptationConverter.ChunkBookForAdaptation(bookText, maxChunks, softMaxChars);

    public static string StitchFountainParts(IReadOnlyList<string>? parts) =>
        AdaptationConverter.StitchFountainParts(parts);

    public static string ConvertHeuristic(string title, string bookText, string? author = null) =>
        AdaptationConverter.ConvertHeuristic(title, bookText, author);

    public static bool LooksLikeGoodFountain(string text, bool requirePageTags = false) =>
        AdaptationConverter.LooksLikeGoodFountain(text, requirePageTags);

    public static string NormalizeBookText(string bookText) =>
        AdaptationConverter.NormalizeBookText(bookText);

    public static string EnsureFadeIn(string text) =>
        AdaptationConverter.EnsureFadeIn(text);

    public static string StripFences(string text) =>
        AdaptationConverter.StripFences(text);

    public static AdaptationConversionResult MapResult(AdaptationConversionResultCore core) => new()
    {
        Fountain = core.Fountain,
        VisionMeta = MapVision(core.VisionMeta),
        VisionMetaStatus = MapStatus(core.VisionMetaStatus),
        VisionMetaError = core.VisionMetaError,
    };

    public static ProjectVisionMeta.Document? MapVision(AdaptationVisionMeta? v)
    {
        if (v is null) return null;
        return new ProjectVisionMeta.Document
        {
            SchemaVersion = string.IsNullOrWhiteSpace(v.SchemaVersion)
                ? ProjectVisionMeta.SchemaVersion
                : v.SchemaVersion,
            VisualMedium = ProjectVisionMeta.NormalizeMedium(v.VisualMedium),
            RenderStyleLock = v.RenderStyleLock,
            PerformanceLock = v.PerformanceLock,
            DecidedBy = string.IsNullOrWhiteSpace(v.DecidedBy) ? "adaptation" : v.DecidedBy,
            DecidedAt = v.DecidedAt,
            Notes = v.Notes,
        };
    }

    internal static AdaptationVisionMeta? MapVisionToAdaptation(ProjectVisionMeta.Document? d)
    {
        if (d is null) return null;
        return new AdaptationVisionMeta
        {
            SchemaVersion = d.SchemaVersion,
            VisualMedium = d.VisualMedium,
            RenderStyleLock = d.RenderStyleLock,
            PerformanceLock = d.PerformanceLock,
            DecidedBy = d.DecidedBy,
            DecidedAt = d.DecidedAt,
            Notes = d.Notes,
        };
    }

    private static VisionMetaStatus MapStatus(AdaptationVisionMetaStatus s) => s switch
    {
        AdaptationVisionMetaStatus.PrimaryResponse => VisionMetaStatus.PrimaryResponse,
        AdaptationVisionMetaStatus.RepairResponse => VisionMetaStatus.RepairResponse,
        AdaptationVisionMetaStatus.Missing => VisionMetaStatus.Missing,
        AdaptationVisionMetaStatus.Malformed => VisionMetaStatus.Malformed,
        AdaptationVisionMetaStatus.InvalidValue => VisionMetaStatus.InvalidValue,
        _ => VisionMetaStatus.Missing,
    };
}
