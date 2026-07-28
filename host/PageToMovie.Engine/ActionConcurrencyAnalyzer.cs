using System.Text.RegularExpressions;

namespace PageToMovie.Engine;

public sealed record ActionConcurrencyResult(
    string Mode,
    double OverlapRatioGamma,
    string Reason);

/// <summary>
/// Analyzes Fountain beat action descriptions and parentheticals to determine whether
/// physical action occurs serially (before/after speech) or concurrently (during speech).
/// </summary>
public static class ActionConcurrencyAnalyzer
{
    private static readonly Regex ConcurrentKeywordsRegex = new(
        @"\b(while|as he|as she|as they|simultaneously|during|pacing|driving|riding|holding|sorting|walking|eating|drinking)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SerialKeywordsRegex = new(
        @"\b(pauses|stops|then|after|first|clicks open|drops|pulls out|launches|slaps|hits|stabs)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static ActionConcurrencyResult AnalyzeBeat(string? actionDescription, string? parenthetical)
    {
        var text = $"{actionDescription} {parenthetical}".Trim();
        if (string.IsNullOrWhiteSpace(text))
            return new ActionConcurrencyResult("serial", 0.0, "Empty action description; default to serial.");

        if (ConcurrentKeywordsRegex.IsMatch(text))
        {
            return new ActionConcurrencyResult(
                Mode: "concurrent",
                OverlapRatioGamma: 0.85,
                Reason: "Detected concurrent verb/action marker (e.g. 'while', 'as he', 'pacing').");
        }

        if (SerialKeywordsRegex.IsMatch(text))
        {
            return new ActionConcurrencyResult(
                Mode: "serial",
                OverlapRatioGamma: 0.0,
                Reason: "Detected serial action marker (e.g. 'pauses', 'then', 'clicks open').");
        }

        // Default heuristic: if action text is short (< 5 words) inside parentheticals, assume concurrent.
        if (!string.IsNullOrWhiteSpace(parenthetical) && parenthetical.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4)
        {
            return new ActionConcurrencyResult(
                Mode: "concurrent",
                OverlapRatioGamma: 0.80,
                Reason: "Short parenthetical beat; default to concurrent overlap.");
        }

        return new ActionConcurrencyResult(
            Mode: "serial",
            OverlapRatioGamma: 0.0,
            Reason: "Standard action beat; default to serial execution.");
    }
}
