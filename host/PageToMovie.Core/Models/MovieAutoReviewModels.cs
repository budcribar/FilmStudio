using System;
using System.Collections.Generic;

namespace PageToMovie.Core.Models;

public sealed class MovieAutoReviewReport
{
    public string ProjectId { get; set; } = "";
    public int OverallScore { get; set; } = 8; // 1 to 10
    public string Verdict { get; set; } = "Pass"; // "Pass", "Needs Polish", "Continuity Fixes"
    public string SummaryNotes { get; set; } = "";
    public string ExecutiveSummary { get; set; } = "";
    public Dictionary<string, int> CategoryScores { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<MovieSceneGroupFeedback> GroupFeedback { get; set; } = new();
    public List<int> FlaggedScenes { get; set; } = new();
    public string ProviderUsed { get; set; } = "grok";
    public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");
}

public sealed class MovieSceneGroupFeedback
{
    public string SceneRange { get; set; } = ""; // e.g. "Scenes 1-4"
    public int Score { get; set; } = 8;
    public int ContinuityScore { get; set; } = 8;
    public int CharacterScore { get; set; } = 8;
    public int LightingScore { get; set; } = 8;
    public int PacingScore { get; set; } = 8;
    public int DialogueScore { get; set; } = 8;
    public string ContinuityNotes { get; set; } = "";
    public string VisualConsistencyNotes { get; set; } = "";
    public string LightingNotes { get; set; } = "";
    public string DialogueNotes { get; set; } = "";
    public string AudioNotes { get; set; } = "";
    public List<int> SceneNumbers { get; set; } = new();
}

public sealed class MovieAutoReviewKeyframe
{
    public int SceneNumber { get; set; }
    public string Label { get; set; } = "";
    public string Base64 { get; set; } = "";
    public string Mime { get; set; } = "image/jpeg";
}
