using System;
using System.Collections.Generic;

namespace PageToMovie.Core.Models;

/// <summary>
/// Scene-specific Git commit history item with change diff details.
/// </summary>
public sealed class SceneCommitHistoryItem
{
    public string CommitHash { get; set; } = "";
    public string ShortHash => CommitHash.Length >= 8 ? CommitHash[..8] : CommitHash;
    public string Author { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime CommittedAt { get; set; }
    public List<string> Changes { get; set; } = new();
}
