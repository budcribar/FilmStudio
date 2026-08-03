using PageToMovie.Adaptation;
using PageToMovie.Adaptation.Contracts;
using PageToMovie.Adaptation.Conversion;
using PageToMovie.Core.Abstractions;
using AdaptationConverter = PageToMovie.Adaptation.Conversion.BookToFountainConverter;
using AdaptationConversionResultCore = PageToMovie.Adaptation.Conversion.AdaptationConversionResult;

namespace PageToMovie.Engine;

/// <summary>
/// Engine-side Stage‑1 vision mapping only.
/// Pure text helpers and LLM conversion live in <see cref="PageToMovie.Adaptation.Conversion.BookToFountainConverter"/>
/// and <see cref="AdaptationService"/> — do not re-add forwarders here.
/// </summary>
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
/// Maps Adaptation vision DTOs to Engine <see cref="ProjectVisionMeta"/>.
/// For pure Fountain helpers, call <see cref="AdaptationConverter"/> directly.
/// For Stage‑1 generation, call <see cref="AdaptationService.ConvertAsync"/>.
/// </summary>
public static class BookToFountainConverter
{
    public static AdaptationConversionResult MapResult(AdaptationConversionResultCore core) => new()
    {
        Fountain = core.Fountain,
        VisionMeta = MapVision(core.VisionMeta),
        VisionMetaStatus = MapStatus(core.VisionMetaStatus),
        VisionMetaError = core.VisionMetaError,
    };

    /// <summary>
    /// Split a model response that may include a VISION_META trailer, mapping vision to project document.
    /// </summary>
    public static (string Fountain, ProjectVisionMeta.Document? Vision) SplitVisionMetaTrailer(string? text)
    {
        var (fountain, vision) = AdaptationConverter.SplitVisionMetaTrailer(text);
        return (fountain, MapVision(vision));
    }

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
