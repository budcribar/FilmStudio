namespace PageToMovie.Adaptation.Contracts;

/// <summary>
/// Adaptation-owned vision medium document (no disk I/O). Engine maps to
/// <c>ProjectVisionMeta.Document</c> / project files at the orchestration boundary.
/// </summary>
public sealed class AdaptationVisionMeta
{
    public string SchemaVersion { get; init; } = "vision_meta.v1";

    /// <summary>photoreal_live_action | illustrated_picture_book | stylized_3d_animated | other</summary>
    public string VisualMedium { get; init; } = "photoreal_live_action";

    public string? RenderStyleLock { get; init; }
    public string? PerformanceLock { get; init; }

    /// <summary>adaptation | cast_extract | user</summary>
    public string DecidedBy { get; init; } = "adaptation";

    public string? DecidedAt { get; init; }
    public string? Notes { get; init; }
}

/// <summary>How vision meta was obtained from the model response (converter status).</summary>
public enum AdaptationVisionMetaStatus
{
    PrimaryResponse,
    RepairResponse,
    Missing,
    Malformed,
    InvalidValue,
}
