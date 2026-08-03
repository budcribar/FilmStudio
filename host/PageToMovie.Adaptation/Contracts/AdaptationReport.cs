namespace PageToMovie.Adaptation.Contracts;

/// <summary>
/// Diagnostic sidecar from Stage‑1 (<c>---ADAPTATION_REPORT---</c>).
/// Operator-facing only — never part of the Fountain body or audience film.
/// </summary>
public sealed class AdaptationReport
{
    /// <summary>yes | no | uncertain — whether the source read as a complete book.</summary>
    public string SourceComplete { get; set; } = "";

    public AdaptationReportMetrics Metrics { get; set; } = new();

    public List<AdaptationReportIssue> Issues { get; set; } = new();

    /// <summary>Feedback about the prompt/spec, not the book.</summary>
    public List<string> SpecFeedback { get; set; } = new();

    /// <summary>Raw JSON body as returned by the model (trimmed).</summary>
    public string? RawJson { get; set; }
}

public sealed class AdaptationReportMetrics
{
    public int Scenes { get; set; }
    public int SpeakingCast { get; set; }
    public int BodyWords { get; set; }
    public double EstRuntimeMin { get; set; }
}

public sealed class AdaptationReportIssue
{
    public string Type { get; set; } = "";
    public string Severity { get; set; } = "";
    public string Where { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Resolution { get; set; } = "";
}

public enum AdaptationReportStatus
{
    /// <summary>Sidecar absent (older prompts or heuristic path).</summary>
    Missing = 0,
    /// <summary>Parsed and accepted.</summary>
    Present = 1,
    /// <summary>Markers present but JSON invalid.</summary>
    Malformed = 2,
}
