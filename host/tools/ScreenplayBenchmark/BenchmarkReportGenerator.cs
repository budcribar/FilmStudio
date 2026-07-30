using System.Text;
using System.Text.Json;

namespace ScreenplayBenchmark;

public sealed class ModelScoreSummary
{
    public string ModelId { get; set; } = "";
    public string AnonymousLabel { get; set; } = "";
    public double CompositeScore { get; set; } // 0 - 100

    /// <summary>
    /// True when the "screenplay" scored here is not this model's real output — generation
    /// silently fell back to <c>BookToFountainConverter.ConvertHeuristic</c> (a deterministic,
    /// model-agnostic narrator-per-paragraph draft) after the live API call failed. Every model
    /// that hits this path for the same book produces byte-identical text, so their scores must
    /// never be compared to each other or averaged into multi-book history as if they were real.
    /// </summary>
    public bool IsGenerationFallback { get; set; }
    public string? GenerationFallbackReason { get; set; }
    public int BordaPoints { get; set; }
    public double AvgJudgeRank { get; set; }
    public DeterministicSyntaxResult SyntaxAudit { get; set; } = new();
    public double AvgAdaptationFidelity { get; set; }
    public double AvgCharacterDisambiguation { get; set; }
    public double AvgAiVideoDirectibility { get; set; }
    public double AvgDramaticPacing { get; set; }
    public double AvgDialogueAuthenticity { get; set; }
    public double AvgSoundDesignMusic { get; set; }
    public double AvgOverallQualitative { get; set; }

    /// <summary>"{judgeId}: {issue}" entries from any judge that marked this candidate not production-ready.</summary>
    public List<string> DisqualifyingFlags { get; set; } = new();
}

public sealed class BenchmarkRunData
{
    public string BookPath { get; set; } = "";
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
    public bool IsMockRun { get; set; }
    public List<ModelScoreSummary> Leaderboard { get; set; } = new();
    public Dictionary<string, Dictionary<string, double>> JudgeMatrix { get; set; } = new(); // JudgeModel -> (ScreenplayModel -> Score)
    public Dictionary<string, Dictionary<string, int>> JudgeRankMatrix { get; set; } = new(); // JudgeModel -> (ScreenplayModel -> Rank)
    public List<string> SelfBiasNotes { get; set; } = new();

    /// <summary>JudgeModel -> (ScreenplayModel -> that judge's written rationale for that candidate).</summary>
    public Dictionary<string, Dictionary<string, string>> JudgeRationale { get; set; } = new();

    /// <summary>JudgeModel -> that judge's free-text overall comparison summary.</summary>
    public Dictionary<string, string> JudgeSummaries { get; set; } = new();
}

public static class BenchmarkReportGenerator
{
    public static string GenerateMarkdownReport(BenchmarkRunData data)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# 🏆 Screenplay Benchmark & Peer-Evaluation Report");
        sb.AppendLine($"*Generated at: {data.Timestamp}*  ");
        sb.AppendLine($"*Source Story File: `{Path.GetFileName(data.BookPath)}`*");
        if (data.IsMockRun)
        {
            sb.AppendLine();
            sb.AppendLine("> ⚠️ **NOTE:** This report was generated in **DRY-RUN / MOCK** mode. Scores are for testing purposes and excluded from global leaderboards.");
        }
        sb.AppendLine();

        var fallbackModels = data.Leaderboard.Where(m => m.IsGenerationFallback).ToList();
        if (fallbackModels.Count > 0)
        {
            sb.AppendLine("> ⚠️ **GENERATION FALLBACK DETECTED:** The following models' live API generation failed, and the tool " +
                "silently substituted a non-AI, book-text-only draft (identical for every failing model). Their rows below do NOT " +
                "reflect that model's real output and are excluded from multi-book history:");
            foreach (var m in fallbackModels)
                sb.AppendLine($"> - **{m.ModelId}**: {m.GenerationFallbackReason}");
            sb.AppendLine();
        }

        sb.AppendLine("## 📊 Overall Model Leaderboard");
        sb.AppendLine();
        sb.AppendLine("| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |");
        sb.AppendLine("| :---: | :--- | :---: | :---: | :---: | :---: | :---: |");

        for (int i = 0; i < data.Leaderboard.Count; i++)
        {
            var m = data.Leaderboard[i];
            var medal = i switch { 0 => "🥇 ", 1 => "🥈 ", 2 => "🥉 ", _ => $"{i + 1}. " };
            var modelLabel = m.IsGenerationFallback ? $"{m.ModelId} ⚠️ *(fallback draft, not real output)*" : m.ModelId;
            sb.AppendLine($"| {medal} | **{modelLabel}** | **{m.CompositeScore:F1}** | {m.SyntaxAudit.OverallSyntaxScore:F1}% | {m.AvgOverallQualitative * 10:F1}% | {m.BordaPoints} pts | {m.AvgJudgeRank:F1} |");
        }
        sb.AppendLine();

        sb.AppendLine("## 📐 Dimension Breakdown Matrix");
        sb.AppendLine();
        sb.AppendLine("| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

        foreach (var m in data.Leaderboard)
        {
            sb.AppendLine($"| **{m.ModelId}** | {m.SyntaxAudit.FormatComplianceScore:F0}% | {m.SyntaxAudit.SceneBudgetScore:F0}% | {m.SyntaxAudit.DialoguePacingScore:F0}% | {m.AvgAdaptationFidelity:F1}/10 | {m.AvgCharacterDisambiguation:F1}/10 | {m.AvgAiVideoDirectibility:F1}/10 | {m.AvgDramaticPacing:F1}/10 | {m.AvgDialogueAuthenticity:F1}/10 | {m.AvgSoundDesignMusic:F1}/10 |");
        }
        sb.AppendLine();

        var disqualified = data.Leaderboard.Where(m => m.DisqualifyingFlags.Count > 0).ToList();
        if (disqualified.Count > 0)
        {
            sb.AppendLine("## 🚫 Production-Readiness Flags");
            sb.AppendLine("Deal-breaker issues judges called out independent of the averaged scores above:");
            sb.AppendLine();
            foreach (var m in disqualified)
            {
                sb.AppendLine($"- **{m.ModelId}**:");
                foreach (var flag in m.DisqualifyingFlags)
                    sb.AppendLine($"  - {flag}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## ⚖️ Peer Judge Matrix (Heatmap)");
        sb.AppendLine("Shows how each judge model evaluated candidate screenplays (scored out of 10):");
        sb.AppendLine();

        if (data.JudgeMatrix.Count > 0)
        {
            var candidateModels = data.Leaderboard.Select(l => l.ModelId).ToList();
            sb.Append("| Judge Model \\ Author |");
            foreach (var cand in candidateModels) sb.Append($" {cand} |");
            sb.AppendLine();
            sb.Append("| :--- |");
            foreach (var _ in candidateModels) sb.Append(" :---: |");
            sb.AppendLine();

            foreach (var (judgeModel, ratings) in data.JudgeMatrix)
            {
                sb.Append($"| **{judgeModel}** |");
                foreach (var cand in candidateModels)
                {
                    if (ratings.TryGetValue(cand, out var score))
                    {
                        var isSelf = string.Equals(judgeModel, cand, StringComparison.OrdinalIgnoreCase);
                        string tag;
                        if (score < 0.0)
                        {
                            tag = " ⚠️ **-1.0** *(Mock/Failed)*";
                        }
                        else
                        {
                            tag = isSelf ? $" **{score:F1}** *(self)*" : $" {score:F1}";
                        }
                        sb.Append($"{tag} |");
                    }
                    else
                    {
                        sb.Append(" N/A |");
                    }
                }
                sb.AppendLine();
            }
        }
        sb.AppendLine();

        if (data.SelfBiasNotes.Count > 0)
        {
            sb.AppendLine("### 🧐 Self-Bias Analysis");
            foreach (var note in data.SelfBiasNotes)
            {
                sb.AppendLine($"- {note}");
            }
            sb.AppendLine();
        }

        if (data.JudgeSummaries.Count(kv => !string.IsNullOrWhiteSpace(kv.Value)) > 0)
        {
            sb.AppendLine("### 🗣️ Judge Summary Notes");
            foreach (var (judgeId, summary) in data.JudgeSummaries)
            {
                if (string.IsNullOrWhiteSpace(summary)) continue;
                sb.AppendLine($"- **{judgeId}:** {summary}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## 🔍 Character & Music Structural Diagnostics (C# Audit)");
        sb.AppendLine();
        foreach (var m in data.Leaderboard)
        {
            sb.AppendLine($"### 🎬 {m.ModelId}");
            sb.AppendLine($"- **Scene Headings Count:** {m.SyntaxAudit.TotalSceneHeadings}");
            sb.AppendLine($"- **Dialogue Blocks:** {m.SyntaxAudit.TotalDialogueBlocks} (Avg `{m.SyntaxAudit.AvgWordsPerDialogue}` words/turn, Max `{m.SyntaxAudit.MaxWordsInSingleDialogue}` words)");
            sb.AppendLine($"- **Generic Numbered Speakers:** `{m.SyntaxAudit.GenericNumberedSpeakerCount}` (e.g. MAN 1, OFFICER 2)");
            if (m.SyntaxAudit.AgeDisambiguatedCharacters.Count > 0)
                sb.AppendLine($"- **Age-Disambiguated Character Headers:** `{string.Join(", ", m.SyntaxAudit.AgeDisambiguatedCharacters)}`");
            if (m.SyntaxAudit.DiagnosticWarnings.Count > 0)
            {
                sb.AppendLine("- **Diagnostics & Warnings:**");
                foreach (var w in m.SyntaxAudit.DiagnosticWarnings)
                    sb.AppendLine($"  - ⚠️ {w}");
            }

            var rationaleForModel = data.JudgeRationale
                .Where(kv => kv.Value.TryGetValue(m.ModelId, out var text) && !string.IsNullOrWhiteSpace(text))
                .Select(kv => (JudgeId: kv.Key, Text: kv.Value[m.ModelId]))
                .ToList();
            if (rationaleForModel.Count > 0)
            {
                sb.AppendLine("- **Judge Rationale:**");
                foreach (var (judgeId, text) in rationaleForModel)
                {
                    var isSelf = string.Equals(judgeId, m.ModelId, StringComparison.OrdinalIgnoreCase);
                    sb.AppendLine($"  - *{judgeId}{(isSelf ? " (self)" : "")}:* {text}");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
