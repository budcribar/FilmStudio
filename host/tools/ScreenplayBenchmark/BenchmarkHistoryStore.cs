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
    public double AvgQualitativeScore { get; set; }
    public double AvgFidelity { get; set; }
    public double AvgCharSplit { get; set; }
    public double AvgVideoDirect { get; set; }
    public double AvgPacing { get; set; }
    public double AvgDialogue { get; set; }
    public double AvgMusic { get; set; }
    public int TotalBooksEvaluated { get; set; }
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

    public static void AppendRun(HistoricalBenchmarkRun newRun, string historyFilePath)
    {
        var container = LoadHistory(historyFilePath);
        container.Runs.Add(newRun);

        var dir = Path.GetDirectoryName(historyFilePath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        var opts = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(historyFilePath, JsonSerializer.Serialize(container, opts));
    }

    public static List<CompositeModelSummary> ComputeGlobalCompositeLeaderboard(HistoricalStoreContainer container)
    {
        if (container.Runs.Count == 0)
            return new List<CompositeModelSummary>();

        // Group by model across all runs
        var allModelIds = container.Runs.SelectMany(r => r.ModelScores.Select(m => m.ModelId)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var result = new List<CompositeModelSummary>();

        foreach (var modelId in allModelIds)
        {
            var modelRuns = container.Runs
                .Where(r => r.ModelScores.Any(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (modelRuns.Count == 0) continue;

            var modelScoresList = modelRuns
                .Select(r => r.ModelScores.First(m => string.Equals(m.ModelId, modelId, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            int wins = 0;
            foreach (var run in container.Runs)
            {
                var topModel = run.ModelScores.OrderByDescending(m => m.CompositeScore).FirstOrDefault();
                if (topModel != null && string.Equals(topModel.ModelId, modelId, StringComparison.OrdinalIgnoreCase))
                    wins++;
            }

            result.Add(new CompositeModelSummary
            {
                ModelId = modelId,
                MultiBookCompositeScore = Math.Round(modelScoresList.Average(s => s.CompositeScore), 1),
                AvgSyntaxScore = Math.Round(modelScoresList.Average(s => s.SyntaxAudit.OverallSyntaxScore), 1),
                AvgQualitativeScore = Math.Round(modelScoresList.Average(s => s.AvgOverallQualitative * 10.0), 1),
                AvgFidelity = Math.Round(modelScoresList.Average(s => s.AvgAdaptationFidelity), 1),
                AvgCharSplit = Math.Round(modelScoresList.Average(s => s.AvgCharacterDisambiguation), 1),
                AvgVideoDirect = Math.Round(modelScoresList.Average(s => s.AvgAiVideoDirectibility), 1),
                AvgPacing = Math.Round(modelScoresList.Average(s => s.AvgDramaticPacing), 1),
                AvgDialogue = Math.Round(modelScoresList.Average(s => s.AvgDialogueAuthenticity), 1),
                AvgMusic = Math.Round(modelScoresList.Average(s => s.AvgSoundDesignMusic), 1),
                TotalBooksEvaluated = modelRuns.Select(r => r.BookSlug).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                FirstPlaceWins = wins,
            });
        }

        return result.OrderByDescending(c => c.MultiBookCompositeScore).ToList();
    }
}
