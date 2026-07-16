namespace FilmStudio.Core.Models;

public sealed class ProjectInfo
{
    public string Id { get; set; } = "";
    public string? Label { get; set; }
    public string? Title { get; set; }
    public string Path { get; set; } = "";
}

public sealed class WorkspaceState
{
    public string? ActiveProject { get; set; }
}

public sealed class JobSnapshot
{
    public string Status { get; set; } = "idle"; // idle|running|done|error|cancelled
    public string? Kind { get; set; }
    public string? Message { get; set; }
    public string? ProjectId { get; set; }
    public int? Scene { get; set; }
    public int? Clip { get; set; }
    public int Index { get; set; }
    public int Total { get; set; }
    public List<string> Log { get; set; } = new();
    public string? Error { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class StartSceneGenRequest
{
    public string ProjectId { get; set; } = "";
    public int Scene { get; set; }
    public bool OnlyMissing { get; set; } = true;
}

public sealed class StartBatchGenRequest
{
    public string ProjectId { get; set; } = "";
    public List<int> Scenes { get; set; } = new();
    public bool OnlyMissing { get; set; } = true;
}

public sealed class CharacterSummary
{
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string VisualLock { get; set; } = "";
    public string VoiceProfile { get; set; } = "";
    public string VoiceLabel { get; set; } = "";
    public bool VoiceOnly { get; set; }
    public bool Locked { get; set; }
    public string? RefFileName { get; set; }
    public string? RefUrl { get; set; }
    public List<string> WardrobeAlways { get; set; } = new();
    public List<string> DesignReferenceImages { get; set; } = new();
    public string? AgeBand { get; set; }
}

public sealed class SceneSummary
{
    public int SceneNumber { get; set; }
    public string Setting { get; set; } = "";
    public int ClipCount { get; set; }
    public int ClipsOnDisk { get; set; }
    public bool ClipsComplete { get; set; }
    public double? DurationSeconds { get; set; }
    public bool CompositeExists { get; set; }
    public List<string> CharactersOnScreen { get; set; } = new();
    public string Status { get; set; } = "empty"; // empty | partial | complete
}

public sealed class ClipSummary
{
    public int ClipNumber { get; set; }
    public string Timestamp { get; set; } = "";
    public int DurationSeconds { get; set; }
    public string Continuation { get; set; } = "none";
    public string PrimarySubject { get; set; } = "";
    public string VisualPrompt { get; set; } = "";
    public string NegativePrompt { get; set; } = "";
    public string Dialogue { get; set; } = "";
    public string? Speaker { get; set; }
    public string? Delivery { get; set; }
    public bool OnDisk { get; set; }
    public long SizeBytes { get; set; }
    public string? VideoUrl { get; set; }
    public string? FileName { get; set; }
}

public sealed class SceneDetail
{
    public int SceneNumber { get; set; }
    public string Setting { get; set; } = "";
    public double? DurationSeconds { get; set; }
    public int ClipCount { get; set; }
    public int ClipsOnDisk { get; set; }
    public bool CompositeExists { get; set; }
    public string? CompositeUrl { get; set; }
    public List<string> CharactersOnScreen { get; set; } = new();
    public List<string> LocationIds { get; set; } = new();
    public string? PrimaryLocationId { get; set; }
    public List<ClipSummary> Clips { get; set; } = new();
}

/// <summary>Book + Stage 1 + Stage 2 readiness for the Adaptation page.</summary>
public sealed class AdaptationStatus
{
    public string ProjectId { get; set; } = "";
    public BookSourceStatus Book { get; set; } = new();
    public Stage1Status Stage1 { get; set; } = new();
    public Stage2PlanStatus Stage2 { get; set; } = new();
    public bool XaiConfigured { get; set; }
    public string NextStep { get; set; } = "";
}

public sealed class BookSourceStatus
{
    public bool PdfExists { get; set; }
    public string? PdfName { get; set; }
    public bool BookTextExists { get; set; }
    public string? BookTextPath { get; set; }
    public long BookTextBytes { get; set; }
    public string? TextQuality { get; set; }
    public double GarbageScore { get; set; }
    public string? BookKind { get; set; }
    public string? TextEngine { get; set; }
    public int? TextWords { get; set; }
    public int? SuggestedTotalMinutes { get; set; }
    public int? SuggestedChunkPages { get; set; }
    public int PageImageCount { get; set; }
    public bool ReadyForStage1 { get; set; }
    public string? Preview { get; set; }
    public List<string> Notes { get; set; } = new();
}

public sealed class Stage1Status
{
    public bool Present { get; set; }
    public string? ScenesFile { get; set; }
    public string? MovieTitle { get; set; }
    public string? SourceBookTitle { get; set; }
    public int SceneCount { get; set; }
    public int BeatCount { get; set; }
    public int CharacterCount { get; set; }
    public int LocationCount { get; set; }
    public double? RuntimeSeconds { get; set; }
    public string? Mtime { get; set; }
    public List<string> CastNames { get; set; } = new();
    public List<Stage1SceneRow> Scenes { get; set; } = new();
}

public sealed class Stage1SceneRow
{
    public int SceneNumber { get; set; }
    public string Setting { get; set; } = "";
    public int BeatCount { get; set; }
    public double? DurationSeconds { get; set; }
}

public sealed class Stage2PlanStatus
{
    public bool Stage1Exists { get; set; }
    public int Stage1Scenes { get; set; }
    public bool BlueprintExists { get; set; }
    public string? BlueprintPath { get; set; }
    public string? BlueprintFileName { get; set; }
    public int Stage2Scenes { get; set; }
    public int Stage2Clips { get; set; }
    public bool Stage2Ready { get; set; }
    public bool Stage2Stale { get; set; }
    public string? LastCompletedAt { get; set; }
    public string? LastRunMessage { get; set; }
    public int ValidationIssueCount { get; set; }
}

public sealed class StartStage1Request
{
    public string ProjectId { get; set; } = "";
    public int ChunkPages { get; set; } = 10;
    public int? TotalMinutes { get; set; }
    public string Model { get; set; } = "grok-4.5";
    public bool Resume { get; set; }
    public int MaxChunks { get; set; }
}

public sealed class StartStage2Request
{
    public string ProjectId { get; set; } = "";
    public string Resolution { get; set; } = "720p";
    public string Scenes { get; set; } = "all";
}

/// <summary>SignalR event payloads.</summary>
public static class JobHubEvents
{
    public const string JobUpdated = "JobUpdated";
    public const string JobLog = "JobLog";
}
