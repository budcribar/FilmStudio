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

/// <summary>SignalR event payloads.</summary>
public static class JobHubEvents
{
    public const string JobUpdated = "JobUpdated";
    public const string JobLog = "JobLog";
}
