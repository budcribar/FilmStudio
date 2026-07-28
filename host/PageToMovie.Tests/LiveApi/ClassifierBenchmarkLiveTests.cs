using System.Text.Json;
using ClassifierBenchmarks;
using PageToMovie.Core.Models;
using Xunit;

namespace PageToMovie.Tests.LiveApi;

/// <summary>
/// PAID: live classifier benchmark evaluations over gold datasets across all supported providers
/// (Grok xAI, Claude Anthropic, Gemini Google).
///
/// Runs under <c>dotnet test --filter Category=LiveApi</c> when <c>PAGETOMOVIE_LIVE_API_TESTS=1</c>.
///
/// Ensures:
/// 1. Classifier AI accuracy scores are as good or better than historical bests for each model.
/// 2. Every run appends to history index and updates the HTML/Markdown benchmark reports:
///    - <c>host/evals/classifier_benchmarks/reports/history.html</c>
///    - <c>host/evals/classifier_benchmarks/reports/LATEST.md</c>
/// </summary>
[Trait("Category", LiveApiGate.Category)]
public class ClassifierBenchmarkLiveTests
{
    /// <summary>
    /// Matrix of classifier benchmark tasks, models, and required API key env vars.
    /// </summary>
    public static IEnumerable<object[]> ClassifierMatrix()
    {
        // Grok xAI
        yield return new object[] { "ambient_sfx",         "grok-4.5",        SupportedModelCatalog.XaiApiKeyEnv };
        yield return new object[] { "species_kind",        "grok-4.5",        SupportedModelCatalog.XaiApiKeyEnv };
        yield return new object[] { "onscreen_cast",       "grok-4.5",        SupportedModelCatalog.XaiApiKeyEnv };
        yield return new object[] { "silent_beat_action", "grok-4.5",        SupportedModelCatalog.XaiApiKeyEnv };
        yield return new object[] { "extend_cut",          "grok-4.5",        SupportedModelCatalog.XaiApiKeyEnv };
        yield return new object[] { "plate_rank",          "grok-4.5",        SupportedModelCatalog.XaiApiKeyEnv };

        // Claude Anthropic
        yield return new object[] { "ambient_sfx",         "claude-sonnet-5", SupportedModelCatalog.AnthropicApiKeyEnv };
        yield return new object[] { "species_kind",        "claude-sonnet-5", SupportedModelCatalog.AnthropicApiKeyEnv };
        yield return new object[] { "onscreen_cast",       "claude-sonnet-5", SupportedModelCatalog.AnthropicApiKeyEnv };

        // Gemini Google
        yield return new object[] { "ambient_sfx",         "gemini-2.5-pro",    SupportedModelCatalog.GoogleApiKeyEnv };
        yield return new object[] { "species_kind",        "gemini-2.5-pro",    SupportedModelCatalog.GoogleApiKeyEnv };
        yield return new object[] { "onscreen_cast",       "gemini-2.5-pro",    SupportedModelCatalog.GoogleApiKeyEnv };
    }

    [LiveApiTheory]
    [MemberData(nameof(ClassifierMatrix))]
    public async Task LiveClassifierBenchmark_evaluates_task_against_gold_and_updates_reports(
        string task, string model, string envKey)
    {
        var apiKey = Environment.GetEnvironmentVariable(envKey);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            // Provider API key not present — skip gracefully
            return;
        }

        var paths = new BenchPaths(BenchPaths.FindRepoRoot());
        var xaiKey = Environment.GetEnvironmentVariable(SupportedModelCatalog.XaiApiKeyEnv);
        var claudeKey = Environment.GetEnvironmentVariable(SupportedModelCatalog.AnthropicApiKeyEnv);
        var geminiKey = Environment.GetEnvironmentVariable(SupportedModelCatalog.GoogleApiKeyEnv);

        using var chat = new ChatRunner(xaiKey, claudeKey, geminiKey);

        var promptId = TaskRunners.DefaultPromptId(task);
        var prompt = PromptStore.Load(paths, task, promptId);

        const string projectId = "The_Jungle_Book";
        const double temperature = 0.0;

        // Fetch previous best score from history prior to this run
        var previousBest = GetPreviousBestScore(paths, task, model);

        // Run classifier evaluation on gold fixture
        TaskResult result = task switch
        {
            "ambient_sfx" => await TaskRunners.RunAmbientAsync(
                paths, projectId, model, temperature, prompt, chat),
            "species_kind" => await TaskRunners.RunSpeciesAsync(
                paths, projectId, model, temperature, prompt, chat),
            "onscreen_cast" => await TaskRunners.RunOnScreenCastAsync(
                paths, projectId, model, temperature, prompt, chat),
            "silent_beat_action" => await TaskRunners.RunSilentBeatActionAsync(
                paths, projectId, model, temperature, prompt, chat),
            "extend_cut" => await TaskRunners.RunExtendCutAsync(
                paths, projectId, model, temperature, prompt, chat),
            "plate_rank" => await TaskRunners.RunPlateRankAsync(
                paths, projectId, model, temperature, prompt, chat),
            _ => throw new ArgumentOutOfRangeException(nameof(task)),
        };

        // Assert quality: AI score must be as good or better than previous best (with 0.05 sampling margin)
        // or exceed baseline score.
        if (previousBest.HasValue)
        {
            Assert.True(result.AiScore >= (previousBest.Value - 0.05),
                $"[{task} / {model}] AI score ({result.AiScore:F3}) regressed significantly below " +
                $"previous best ({previousBest.Value:F3}). Baseline: {result.BaselineScore:F3}");
        }
        else
        {
            Assert.True(result.AiScore >= (result.BaselineScore - 0.05),
                $"[{task} / {model}] First run AI score ({result.AiScore:F3}) is below baseline ({result.BaselineScore:F3}).");
        }

        // Record run & append to history
        var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'") + "_" + Guid.NewGuid().ToString("N")[..6];
        var run = new BenchmarkRun
        {
            RunId = runId,
            Utc = DateTimeOffset.UtcNow.ToString("u"),
            Config = new RunConfig
            {
                ProjectId = projectId,
                Tasks = new List<string> { task },
                Models = new List<string> { model },
                Prompts = new List<string> { promptId },
                Temperatures = new List<double> { temperature },
                Note = "LiveApi test suite benchmark execution",
            },
            RepoRoot = paths.RepoRoot,
            Results = new List<TaskResult> { result },
        };

        await ReportWriter.WriteRunArtifactsAsync(paths, run);
        await ReportWriter.AppendHistoryAsync(paths, run);
        await ReportWriter.WriteAggregateReportsAsync(paths);

        // Verify HTML report & LATEST.md files are written out and present
        var htmlReport = Path.Combine(paths.Reports, "history.html");
        var latestMd = Path.Combine(paths.Reports, "LATEST.md");

        Assert.True(File.Exists(htmlReport), $"history.html missing at {htmlReport}");
        Assert.True(File.Exists(latestMd), $"LATEST.md missing at {latestMd}");

        var htmlText = await File.ReadAllTextAsync(htmlReport);
        Assert.Contains("Classifier benchmarks", htmlText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(task, htmlText, StringComparison.OrdinalIgnoreCase);
    }

    private static double? GetPreviousBestScore(BenchPaths paths, string task, string model)
    {
        if (!File.Exists(paths.HistoryIndex)) return null;

        try
        {
            var json = File.ReadAllText(paths.HistoryIndex);
            var index = JsonSerializer.Deserialize<HistoryIndex>(json, JsonDefaults.Flexible);
            if (index?.Runs == null || index.Runs.Count == 0) return null;

            double? best = null;
            foreach (var run in index.Runs)
            {
                foreach (var s in run.Scores)
                {
                    if (string.Equals(s.Task, task, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(s.Model, model, StringComparison.OrdinalIgnoreCase) &&
                        s.Metric != "error")
                    {
                        if (!best.HasValue || s.AiScore > best.Value)
                            best = s.AiScore;
                    }
                }
            }
            return best;
        }
        catch
        {
            return null;
        }
    }
}