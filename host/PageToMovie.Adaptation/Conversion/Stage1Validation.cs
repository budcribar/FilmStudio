namespace PageToMovie.Adaptation.Conversion;

/// <summary>Lightweight validation issue for Stage‑1 convert/repair loops (no Engine ModelExecution).</summary>
public sealed record Stage1ValidationIssue(
    string Code,
    string Message,
    string? Path = null);

public enum Stage1ResultSource
{
    PrimaryResponse,
    CorrectiveResponse,
    DeterministicFallback,
    Failed,
}
