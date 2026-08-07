using System.Text.Json;
using PageToMovie.Web.Services;

namespace PageToMovie.Web.Components;

/// <summary>
/// Shared presentation + config plumbing for the three cost pages (Estimate, Project breakdown,
/// Account). Concentrates the USD formatting, vendor labeling, active-project resolution, saved
/// resolution/retries read, and draft-resolution persist that these pages previously repeated verbatim.
/// </summary>
internal static class CostFormatting
{
    public static string Usd(double v) => $"${v:0.00}";

    public static string ProviderLabel(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "Unknown";
        return id.Trim().ToLowerInvariant() switch
        {
            "xai" or "grok" => "xAI",
            "google" or "gemini" => "Google",
            "openai" => "OpenAI",
            "anthropic" => "Anthropic",
            "elevenlabs" => "ElevenLabs",
            "fal" => "Fal",
            "suno" => "Suno API",
            "aimusicapi" => "AI Music API",
            "perplexity" => "Perplexity",
            "unknown" => "Unknown",
            _ => id,
        };
    }

    /// <summary>Active project id, or the first non-empty project id, or "".</summary>
    public static async Task<string> ResolveActiveProjectIdAsync(EngineApiClient engine)
    {
        var projs = await engine.GetProjectsAsync();
        return projs?.Active?.Id
               ?? projs?.Projects.Select(p => p.Id ?? "").FirstOrDefault(s => s.Length > 0)
               ?? "";
    }

    /// <summary>
    /// Reads the saved draft resolution and average-retries from project config, returning the current
    /// values unchanged when the config is missing/unreadable.
    /// </summary>
    public static async Task<(string DraftRes, double Retries)> ReadResolutionAndRetriesAsync(
        EngineApiClient engine, string projectId, string draftRes, double retries)
    {
        try
        {
            var cfg = await engine.GetConfigAsync(projectId);
            if (cfg?.Config is not null &&
                cfg.Config.TryGetValue("resolution", out var res) &&
                res.ValueKind == JsonValueKind.String &&
                res.GetString() is { Length: > 0 } r)
                draftRes = r;
            if (cfg?.Config is not null &&
                cfg.Config.TryGetValue("cost_estimates", out var ce) &&
                ce.ValueKind == JsonValueKind.Object &&
                ce.TryGetProperty("assume_avg_retries", out var ar) &&
                ar.TryGetDouble(out var rt))
                retries = rt;
        }
        catch { /* ignore */ }
        return (draftRes, retries);
    }

    /// <summary>
    /// Normalizes and persists a newly-chosen draft resolution. Returns the normalized value when the
    /// caller should adopt it and reload, or null when the input is invalid or unchanged (no reload).
    /// </summary>
    public static async Task<string?> TrySetDraftResolutionAsync(
        EngineApiClient engine, string projectId, string? res, string currentRes, bool hasReport)
    {
        res = (res ?? "480p").Trim().ToLowerInvariant();
        if (res is not ("480p" or "720p" or "1080p"))
            return null;
        if (string.Equals(currentRes, res, StringComparison.OrdinalIgnoreCase) && hasReport)
            return null;

        // Persist so film generation and Home estimate use the same resolution.
        try
        {
            if (!string.IsNullOrWhiteSpace(projectId))
                await engine.SaveConfigAsync(projectId, new Dictionary<string, object?> { ["resolution"] = res });
        }
        catch { /* recompute anyway */ }
        return res;
    }
}
