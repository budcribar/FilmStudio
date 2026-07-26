namespace PageToMovie.Core.Models;

public sealed class ContributionDiffDto
{
    public string ProjectId { get; set; } = "";
    public string ParentProjectId { get; set; } = "";
    public bool HasConflicts { get; set; }
    public List<ContributionDiffItemDto> FileDiffs { get; set; } = new();
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
