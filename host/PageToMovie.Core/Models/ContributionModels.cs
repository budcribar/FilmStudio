namespace PageToMovie.Core.Models;

public sealed class ContributionDiffDto
{
    public string ProjectId { get; set; } = "";
    public string ParentProjectId { get; set; } = "";
    public bool HasConflicts { get; set; }
    public List<ContributionDiffItemDto> FileDiffs { get; set; } = new();
    public List<MediaClipContributionDto> MediaClips { get; set; } = new();
}

public sealed class MediaClipContributionDto
{
    public int SceneIndex { get; set; }
    public int ClipIndex { get; set; }
    public string RelativePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public string? ProviderCdnUrl { get; set; }
    public string Status { get; set; } = "Present"; // "Present" | "CdnAvailable" | "ProxyNeeded" | "Missing"
    public bool IsVerified { get; set; }
}

public sealed class MediaSyncResultDto
{
    public int SyncedCount { get; set; }
    public int CdnDownloadCount { get; set; }
    public int LocalCopyCount { get; set; }
    public int VerifiedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public sealed class ContributionDiffItemDto
{
    public string FilePath { get; set; } = "";
    public string Category { get; set; } = ""; // "Screenplay" | "Cast" | "Shot Plan" | "Rules"
    public string Status { get; set; } = ""; // "modified" | "added" | "deleted" | "identical"
    public string OursContent { get; set; } = "";
    public string TheirsContent { get; set; } = "";
    public List<DiffLineDto> Lines { get; set; } = new();
}

public sealed class DiffLineDto
{
    public string Kind { get; set; } = "unchanged"; // "unchanged" | "added" | "deleted" | "conflict"
    public int? LineNumberOurs { get; set; }
    public int? LineNumberTheirs { get; set; }
    public string Content { get; set; } = "";
}
