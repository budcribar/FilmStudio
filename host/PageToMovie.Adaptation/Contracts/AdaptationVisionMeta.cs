namespace PageToMovie.Adaptation.Contracts;

/// <summary>
/// Adaptation-owned vision medium document (no disk I/O). Engine maps to
/// <c>ProjectVisionMeta.Document</c> / project files at the orchestration boundary.
/// </summary>
public sealed class AdaptationVisionMeta
{
    public string SchemaVersion { get; set; } = "vision_meta.v1";

    /// <summary>photoreal_live_action | illustrated_picture_book | stylized_3d_animated | other</summary>
    public string VisualMedium { get; set; } = "photoreal_live_action";

    public string? RenderStyleLock { get; set; }
    public string? PerformanceLock { get; set; }

    /// <summary>adaptation | cast_extract | user</summary>
    public string DecidedBy { get; set; } = "adaptation";

    public string? DecidedAt { get; set; }
    public string? Notes { get; set; }
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
