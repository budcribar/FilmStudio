using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

/// <summary>
/// Shared provider API-key resolution used by the concrete provider clients. Each client's
/// <c>ResolveApiKey()</c> was byte-identical per provider family; centralizing here keeps the
/// scope (per-request override) → env-var precedence and quote/whitespace trimming in one place.
/// </summary>
public static class ProviderApiKey
{
    /// <summary>
    /// Sanitizes an API key or token string by removing surrounding whitespace and quote characters.
    /// </summary>
    public static string? Clean(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : raw.Trim(' ', '"', '\'', '\r', '\n', '\t');

    /// <summary>
    /// Fal.ai key: request-scoped override, then the canonical env var, then the fallback env var.
    /// Trims surrounding whitespace and stray quote characters. Returns null when unset.
    /// </summary>
    public static string? ResolveFal()
    {
        var key = ApiKeyScope.CurrentFal
            ?? Environment.GetEnvironmentVariable(SupportedModelCatalog.FalApiKeyEnv)
            ?? Environment.GetEnvironmentVariable(SupportedModelCatalog.FalApiKeyFallbackEnv);
        return Clean(key);
    }

    /// <summary>
    /// ElevenLabs key: request-scoped override, canonical <c>ElevenLabs_API_KEY</c>, then the
    /// all-caps alias for older shells/deploy scripts. Trims whitespace and unwraps a single pair
    /// of surrounding double quotes. Returns null when unset.
    /// </summary>
    public static string? ResolveElevenLabs()
    {
        var key = ApiKeyScope.CurrentElevenLabs
                  ?? Environment.GetEnvironmentVariable(SupportedModelCatalog.ElevenLabsApiKeyEnv)
                  ?? Environment.GetEnvironmentVariable("ELEVENLABS_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) return null;
        key = key.Trim();
        if (key.Length >= 2 && key[0] == '"' && key[^1] == '"')
            key = key[1..^1].Trim();
        return key;
    }
}
