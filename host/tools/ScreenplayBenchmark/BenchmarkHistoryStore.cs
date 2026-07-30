using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ScreenplayBenchmark;

public sealed class HistoricalBenchmarkRun
{
    public string RunId { get; set; } = Guid.NewGuid().ToString("N");
    public string Timestamp { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
    public string BookSlug { get; set; } = "default";
    public string BookTitle { get; set; } = "";
    public string BookPath { get; set; } = "";
    public bool IsMockRun { get; set; }
    public List<ModelScoreSummary> ModelScores { get; set; } = new();
    public Dictionary<string, Dictionary<string, double>> JudgeMatrix { get; set; } = new();
    public List<string> SelfBiasNotes { get; set; } = new();
}

public sealed class HistoricalStoreContainer
{
    public List<HistoricalBenchmarkRun> Runs { get; set; } = new();
}

public sealed class CompositeModelSummary
{
    public string ModelId { get; set; } = "";
    public double MultiBookCompositeScore { get; set; }
    public double AvgSyntaxScore { get; set; }
    public double AvgFormatCompliance { get; set; }
    public double AvgSceneBudget { get; set; }
    public double AvgDialoguePacing { get; set; }
    public double AvgCharDisambiguationSyntax { get; set; }
    public double AvgMusicSpec { get; set; }
    public double AvgQualitativeScore { get; set; }
    public double AvgFidelity { get; set; }
    public double AvgCharSplit { get; set; }
    public double AvgVideoDirect { get; set; }
    public double AvgPacing { get; set; }
    public double AvgDialogue { get; set; }
    public double AvgMusic { get; set; }
    public int TotalBooksEvaluated { get; set; }
    public List<string> EvaluatedBookTitles { get; set; } = new();
    public int FirstPlaceWins { get; set; }
}

public static class BenchmarkHistoryStore
{
    public static HistoricalStoreContainer LoadHistory(string historyFilePath)
    {
        if (!File.Exists(historyFilePath))
            return new HistoricalStoreContainer();

        try
        {
            var json = File.ReadAllText(historyFilePath);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<HistoricalStoreContainer>(json, opts) ?? new HistoricalStoreContainer();
        }
        catch
        {
            return new HistoricalStoreContainer();
        }
    }

    public static void SaveHistory(HistoricalStoreContainer container, string historyFilePath)
    {
        var dir = Path.GetDirectoryName(historyFilePath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(historyFilePath, JsonSerializer.Serialize(container, opts));
    }

    public static void AppendRun(HistoricalBenchmarkRun newRun, string historyFilePath)
    {
        var container = LoadHistory(historyFilePath);
        container.Runs.Add(newRun);
        SaveHistory(container, historyFilePath);
    }

    public static bool IsLiveRun(HistoricalBenchmarkRun run)
    {
        if (run.IsMockRun) return false;
        if (run.ModelScores == null || run.ModelScores.Count == 0) return false;

        // Check if all composite scores are identical mock ties or all negative
        // (fallback-drafted models are excluded — their "score" reflects a shared, model-agnostic
        // heuristic draft, not that model's real generation, so it can't anchor liveness either)
        var validScores = run.ModelScores.Where(m => !m.IsGenerationFallback).Select(m => m.CompositeScore).Where(s => s >= 0).ToList();
        if (validScores.Count == 0 || (validScores.Distinct().Count() <= 1 && validScores.Count > 1))
            return false;

        // Check if judge matrix has at least one real non-mock rating (> 0)
        if (run.JudgeMatrix == null || run.JudgeMatrix.Count == 0) return false;
        var hasRealJudgeRating = run.JudgeMatrix.Values.Any(dict => dict.Values.Any(v => v > 0));
        return hasRealJudgeRating;
    }

    public static List<CompositeModelSummary> ComputeGlobalCompositeLeaderboard(HistoricalStoreContainer container)
    {
        var liveRuns = container.Runs.Where(IsLiveRun).ToList();
        if (liveRuns.Count == 0)
            return new List<CompositeModelSummary>();

        // Group by model across live runs only
        var allModelIds = liveRuns.SelectMany(r => r.ModelScores.Select(m => m.ModelId)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<CompositeModelSummary>();

        foreach (var modelId in allModelIds)
        {
            var modelRuns = liveRuns
                .Where(r => r.ModelScores.Any(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (modelRuns.Count == 0) continue;

            var modelScoresList = modelRuns
                .Select(r => r.ModelScores.First(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase)))
                .Where(s => s.CompositeScore >= 0 && !s.IsGenerationFallback)
                .ToList();

            if (modelScoresList.Count == 0) continue;

            int wins = 0;
            foreach (var run in liveRuns)
            {
                var validScores = run.ModelScores.Where(m => m.CompositeScore >= 0 && !m.IsGenerationFallback).OrderByDescending(m => m.CompositeScore).ToList();
                if (validScores.Count > 0)
                {
                    var topScore = validScores[0].CompositeScore;
                    var topTies = validScores.Where(m => Math.Abs(m.CompositeScore - topScore) < 0.01).ToList();
                    // Award a win only if topScore > 0 and it's not a universal tie across all candidates
                    if (topScore > 0 && topTies.Count < validScores.Count && topTies.Any(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase)))
                    {
                        wins++;
                    }
                }
            }

            result.Add(new CompositeModelSummary
            {
                ModelId = modelId,
                MultiBookCompositeScore = Math.Round(modelScoresList.Average(s => s.CompositeScore), 1),
                AvgSyntaxScore = Math.Round(modelScoresList.Average(s => s.SyntaxAudit.OverallSyntaxScore), 1),
                AvgFormatCompliance = Math.Round(modelScoresList.Average(s => s.SyntaxAudit.FormatComplianceScore), 1),
                AvgSceneBudget = Math.Round(modelScoresList.Average(s => s.SyntaxAudit.SceneBudgetScore), 1),
                AvgDialoguePacing = Math.Round(modelScoresList.Average(s => s.SyntaxAudit.DialoguePacingScore), 1),
                AvgCharDisambiguationSyntax = Math.Round(modelScoresList.Average(s => s.SyntaxAudit.CharacterDisambiguationScore), 1),
                AvgMusicSpec = Math.Round(modelScoresList.Average(s => s.SyntaxAudit.MusicSpecScore), 1),
                AvgQualitativeScore = Math.Round(modelScoresList.Average(s => s.AvgOverallQualitative * 10.0), 1),
                AvgFidelity = Math.Round(modelScoresList.Average(s => s.AvgAdaptationFidelity), 1),
                AvgCharSplit = Math.Round(modelScoresList.Average(s => s.AvgCharacterDisambiguation), 1),
                AvgVideoDirect = Math.Round(modelScoresList.Average(s => s.AvgAiVideoDirectibility), 1),
                AvgPacing = Math.Round(modelScoresList.Average(s => s.AvgDramaticPacing), 1),
                AvgDialogue = Math.Round(modelScoresList.Average(s => s.AvgDialogueAuthenticity), 1),
                AvgMusic = Math.Round(modelScoresList.Average(s => s.AvgSoundDesignMusic), 1),
                TotalBooksEvaluated = modelRuns.Select(r => r.BookSlug).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                EvaluatedBookTitles = modelRuns
                    .Select(r => !string.IsNullOrWhiteSpace(r.BookTitle) ? r.BookTitle : r.BookSlug)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                FirstPlaceWins = wins,
            });
        }

        return result.OrderByDescending(c => c.MultiBookCompositeScore).ToList();
    }
}
