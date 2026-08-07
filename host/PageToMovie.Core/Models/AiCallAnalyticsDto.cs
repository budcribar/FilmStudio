namespace PageToMovie.Core.Models;

/// <summary>
/// Aggregated view of the collected AI/model-call telemetry (projects/&lt;id&gt;/telemetry/api_calls.jsonl),
/// for the admin "AI Calls" analytics page. This is the read side of the feedback loop: until every call
/// site emits the unified AiCallRecord, outcomes are classified here from the existing fields
/// (Ok / HttpStatus / Attempt / Error).
/// </summary>
public sealed class AiCallAnalyticsDto
{
    public System.DateTimeOffset GeneratedAt { get; set; }
    public int ProjectsScanned { get; set; }
    public int TotalCalls { get; set; }
    public int OkCalls { get; set; }
    public int RetriedCalls { get; set; }
    public int FailedCalls { get; set; }
    public int FakeCalls { get; set; }
    public double TotalCostUsd { get; set; }
    public double AvgDurationMs { get; set; }
    public string WindowNote { get; set; } = "";

    /// <summary>Per-operation (telemetry "kind") rollup, most-called first.</summary>
    public List<AiOpStat> Ops { get; set; } = new();
    /// <summary>Per-model rollup, most-called first.</summary>
    public List<AiModelStat> Models { get; set; } = new();
    /// <summary>Plain-language key learnings surfaced from the numbers.</summary>
    public List<string> Learnings { get; set; } = new();
    /// <summary>A few recent failures for quick diagnosis.</summary>
    public List<AiFailureSample> RecentFailures { get; set; } = new();
    /// <summary>
    /// Why creators overrode a model verdict (<c>style_gate_override</c> calls), reason → count —
    /// every override capture asks why (ai_wrong/user_preference/other); this is where that data
    /// finally surfaces for the team to read.
    /// </summary>
    public List<AiOverrideReasonStat> OverrideReasons { get; set; } = new();

    public double SuccessRatePct => TotalCalls == 0 ? 0 : System.Math.Round(100.0 * (OkCalls + RetriedCalls) / TotalCalls, 1);
}

public sealed class AiOverrideReasonStat
{
    /// <summary>ai_wrong | user_preference | other | unspecified</summary>
    public string Reason { get; set; } = "";
    public int Count { get; set; }
}

public sealed class AiOpStat
{
    public string Op { get; set; } = "";
    public int Calls { get; set; }
    public int Retried { get; set; }
    public int Failed { get; set; }
    public double AvgDurationMs { get; set; }
    public double CostUsd { get; set; }
    public double FailPct => Calls == 0 ? 0 : System.Math.Round(100.0 * Failed / Calls, 1);
    public double RetryPct => Calls == 0 ? 0 : System.Math.Round(100.0 * Retried / Calls, 1);
}

public sealed class AiModelStat
{
    public string Model { get; set; } = "";
    public string Provider { get; set; } = "";
    public int Calls { get; set; }
    public int Failed { get; set; }
    public double CostUsd { get; set; }
    public double FailPct => Calls == 0 ? 0 : System.Math.Round(100.0 * Failed / Calls, 1);
}

public sealed class AiFailureSample
{
    public System.DateTimeOffset? Ts { get; set; }
    public string Op { get; set; } = "";
    public string Model { get; set; } = "";
    public int? HttpStatus { get; set; }
    public string FailureKind { get; set; } = "";
    public string Error { get; set; } = "";
    public string ProjectId { get; set; } = "";
}
