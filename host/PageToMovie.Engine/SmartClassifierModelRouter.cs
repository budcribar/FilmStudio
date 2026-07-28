using PageToMovie.Core.Models;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Benchmark-driven model router that dynamically selects the optimal model for each task
/// based on task_rankings in models_catalog.json and active user API keys.
/// </summary>
public sealed class SmartClassifierModelRouter
{
    private readonly ILogger<SmartClassifierModelRouter> _log;

    public SmartClassifierModelRouter(ILogger<SmartClassifierModelRouter> log)
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

        // 2. Resolve ranked benchmark models for taskKey
        if (!SupportedModelCatalog.TaskRankings.TryGetValue(taskKey, out var rankedModels) || rankedModels.Count == 0)
        {
            var fallback = "grok-4.5";
            onLog?.Invoke($"[SmartRouter] Task '{taskKey}' -> No task rankings found. Using fallback '{fallback}'.");
            return fallback;
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
                _log.LogInformation("{Message}", msg);
                onLog?.Invoke(msg);
                return candidateId;
            }
        }

        // 4. Fallback if no specific candidate keys match
        var defaultFallback = rankedModels[0];
        onLog?.Invoke($"[SmartRouter] Task '{taskKey}' -> Using default fallback model '{defaultFallback}'.");
        return defaultFallback;
    }
}