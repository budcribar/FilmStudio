using System.Text;
using System.Text.Json;

namespace ScreenplayBenchmark;

public sealed class ModelScoreSummary
{
    public string ModelId { get; set; } = "";
    public string AnonymousLabel { get; set; } = "";
    public double CompositeScore { get; set; } // 0 - 100
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
}

public sealed class BenchmarkRunData
{
    public string BookPath { get; set; } = "";
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
    public List<ModelScoreSummary> Leaderboard { get; set; } = new();
    public Dictionary<string, Dictionary<string, double>> JudgeMatrix { get; set; } = new(); // JudgeModel -> (ScreenplayModel -> Score)
    public Dictionary<string, Dictionary<string, int>> JudgeRankMatrix { get; set; } = new(); // JudgeModel -> (ScreenplayModel -> Rank)
    public List<string> SelfBiasNotes { get; set; } = new();
}

public static class BenchmarkReportGenerator
{
    public static string GenerateMarkdownReport(BenchmarkRunData data)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# 🏆 Screenplay Benchmark & Peer-Evaluation Report");
        sb.AppendLine($"*Generated at: {data.Timestamp}*  ");
        sb.AppendLine($"*Source Story File: `{Path.GetFileName(data.BookPath)}`*");
        sb.AppendLine();

        sb.AppendLine("## 📊 Overall Model Leaderboard");
        sb.AppendLine();
        sb.AppendLine("| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |");
        sb.AppendLine("| :---: | :--- | :---: | :---: | :---: | :---: | :---: |");

        for (int i = 0; i < data.Leaderboard.Count; i++)
        {
            var m = data.Leaderboard[i];
            var medal = i switch { 0 => "🥇 ", 1 => "🥈 ", 2 => "🥉 ", _ => $"{i + 1}. " };
            sb.AppendLine($"| {medal} | **{m.ModelId}** | **{m.CompositeScore:F1}** | {m.SyntaxAudit.OverallSyntaxScore:F1}% | {m.AvgOverallQualitative * 10:F1}% | {m.BordaPoints} pts | {m.AvgJudgeRank:F1} |");
        }
        sb.AppendLine();

        sb.AppendLine("## 📐 8-Dimension Category Breakdown Matrix");
        sb.AppendLine();
        sb.AppendLine("| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Sound/Music Design |");
        sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

        foreach (var m in data.Leaderboard)
        {
            sb.AppendLine($"| **{m.ModelId}** | {m.SyntaxAudit.FormatComplianceScore:F0}% | {m.SyntaxAudit.SceneBudgetScore:F0}% | {m.SyntaxAudit.DialoguePacingScore:F0}% | {m.AvgAdaptationFidelity:F1}/10 | {m.AvgCharacterDisambiguation:F1}/10 | {m.AvgAiVideoDirectibility:F1}/10 | {m.AvgDramaticPacing:F1}/10 | {m.AvgSoundDesignMusic:F1}/10 |");
        }
        sb.AppendLine();

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
                        var tag = isSelf ? $" **{score:F1}** *(self)*" : $" {score:F1}";
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
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
