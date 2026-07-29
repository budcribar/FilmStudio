using System;
using System.Collections.Generic;

namespace PageToMovie.Core.Models;

/// <summary>
/// Model representing a clip version/take for side-by-side comparison and rollback.
/// </summary>
public sealed class ClipVersionItem
{
    public string VersionId { get; set; } = "";
    public int Scene { get; set; }
    public int Clip { get; set; }
    public int Take { get; set; }
    public bool IsCurrent { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string VisualPrompt { get; set; } = "";
    public string ScriptText { get; set; } = "";
    public string Model { get; set; } = "";
    public string Resolution { get; set; } = "";
    public double DurationSeconds { get; set; }
    public string Mp4FileName { get; set; } = "";
    public string Sha256 { get; set; } = "";
    /// <summary>True when this version's bytes live only on the client (synced + pruned server-side) —
    /// the UI must resolve video playback via the local media folder, not a server URL.</summary>
    public bool ClientOnly { get; set; }
    /// <summary>Project-relative path (e.g. "assets/video/scene_01_clip_02.mp4") for ClientOnly
    /// versions — the exact key the media registry has, so the client can look up its local
    /// blob without re-deriving the folder convention (active vs. history vs. take-named).</summary>
    public string? RelativePath { get; set; }
}

/// <summary>
/// Status of uncommitted changes across scenes and clips.
/// </summary>
public sealed class UncommittedStatusDto
{
    public bool HasUncommittedChanges { get; set; }
    public List<int> ModifiedScenes { get; set; } = new();
    public List<string> ModifiedClipKeys { get; set; } = new();
    public string Summary { get; set; } = "";
}
