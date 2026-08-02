using PageToMovie.Core.Models;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Benchmark-driven model router that dynamically selects the optimal model for each task
/// based on task_rankings in models_catalog.json and active user API keys.
/// </summary>
public sealed class SmartClassifierModelRouter
{
    private readonly ILogger<SmartClassifierModelRouter>? _log;

    public SmartClassifierModelRouter(ILogger<SmartClassifierModelRouter>? log = null)
    {
        _log = log;
    }

    public string ResolveOptimalModelForTask(
        string taskKey,
        string? userConfiguredModel = null,
        Action<string>? onLog = null)
    {
        // 1. If user explicitly specified a non-auto model, honor their manual lock
        if (!string.IsNullOrWhiteSpace(userConfiguredModel) &&
            !string.Equals(userConfiguredModel, "auto", StringComparison.OrdinalIgnoreCase))
        {
            var userChoice = userConfiguredModel.Trim();
            onLog?.Invoke($"[SmartRouter] Task '{taskKey}' -> Using user override model '{userChoice}'.");
            return userChoice;
        }

        // 2. Resolve ranked benchmark models for taskKey (catalog taskRankings only).
        if (!SupportedModelCatalog.TaskRankings.TryGetValue(taskKey, out var rankedModels) || rankedModels.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(userConfiguredModel)
                && !string.Equals(userConfiguredModel, "auto", StringComparison.OrdinalIgnoreCase))
                return userConfiguredModel.Trim();
            throw new InvalidOperationException(
                $"Classifier task '{taskKey}': no model selected and no taskRankings in models_catalog.json. " +
                "Open Settings and choose a Script & planning model.");
        }

        // 3. Find highest-ranked model whose required API key is active
        foreach (var candidateId in rankedModels)
        {
            var entry = SupportedModelCatalog.Find(candidateId);
            if (entry is null || !entry.Enabled) continue;

            bool keysPresent = true;
            foreach (var reqKey in entry.RequiredEnvKeys)
            {
                var val = Environment.GetEnvironmentVariable(reqKey);
                if (string.IsNullOrWhiteSpace(val))
                {
                    keysPresent = false;
                    break;
                }
            }

            if (keysPresent)
            {
                var msg = $"[SmartRouter] Task '{taskKey}' -> Assigned '{candidateId}' (Rank #{rankedModels.IndexOf(candidateId) + 1} for provider {entry.ProviderId}).";
                _log?.LogInformation("{Message}", msg);
                onLog?.Invoke(msg);
                return candidateId;
            }
        }

        // 4. No ranked candidate has a key — do not invent another model.
        throw new InvalidOperationException(
            $"Classifier task '{taskKey}': no ranked catalog model has an available API key. " +
            "Add a key in Settings for one of: " + string.Join(", ", rankedModels) + ".");
    }
}