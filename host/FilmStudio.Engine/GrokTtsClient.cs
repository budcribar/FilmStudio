using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FilmStudio.Core.Options;
using FilmStudio.Engine.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FilmStudio.Engine;

/// <summary>
/// xAI Text-to-Speech (<c>POST /v1/tts</c>) for character voice previews.
/// </summary>
public sealed class GrokTtsClient
{
    public const string ApiBase = "https://api.x.ai/v1";

    /// <summary>Built-in voices (case-insensitive).</summary>
    public static readonly string[] BuiltInVoices = ["ara", "eve", "leo", "rex", "sal"];

    private readonly HttpClient _http;
    private readonly ILogger<GrokTtsClient> _log;

    public GrokTtsClient(
        HttpClient http,
        IOptions<FilmStudioOptions> opts,
        ILogger<GrokTtsClient> log)
    {
        _http = http;
        _log = log;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(ApiBase + "/");
        _ = opts; // reserved for future options
    }

    public bool IsConfigured
    {
        get
        {
            EnsureAuthHeader();
            return _http.DefaultRequestHeaders.Authorization is not null;
        }
    }

    /// <summary>
    /// Map a free-form voice_label to a built-in <c>voice_id</c>, or default <c>eve</c>.
    /// </summary>
    public static string ResolveVoiceId(string? voiceLabel)
    {
        var raw = (voiceLabel ?? "").Trim();
        if (raw.Length == 0) return "eve";
        foreach (var v in BuiltInVoices)
        {
            if (raw.Equals(v, StringComparison.OrdinalIgnoreCase) ||
                raw.Contains(v, StringComparison.OrdinalIgnoreCase))
                return v;
        }
        // Common aliases from cast seeds
        var lower = raw.ToLowerInvariant();
        if (lower.Contains("warm") || lower.Contains("soft") || lower.Contains("narrat") || lower.Contains("gentle"))
            return "eve";
        if (lower.Contains("deep") || lower.Contains("male") || lower.Contains("low"))
            return "rex";
        if (lower.Contains("bright") || lower.Contains("young") || lower.Contains("child"))
            return "ara";
        if (lower.Contains("firm") || lower.Contains("bold"))
            return "leo";
        return "eve";
    }

    /// <summary>
    /// Build a short sample line that exercises the voice profile description.
    /// </summary>
    public static string BuildSampleText(string? displayName, string? voiceProfile)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? "this character" : displayName.Trim();
        var profile = (voiceProfile ?? "").Trim();
        // Keep short — TTS is for preview, not a monologue
        if (profile.Length > 180)
            profile = profile[..177] + "…";
        if (profile.Length > 0)
            return $"Hello — I am {name}. {profile}";
        return $"Hello — I am {name}. This is a sample of my speaking voice for the film.";
    }

    /// <summary>Synthesize MP3 bytes via xAI TTS.</summary>
    public async Task<byte[]> SynthesizeAsync(
        string text,
        string voiceId = "eve",
        string language = "en",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("text required", nameof(text));
        EnsureAuthHeader();
        if (_http.DefaultRequestHeaders.Authorization is null)
            throw new InvalidOperationException("XAI_API_KEY is not set.");

        var payload = new Dictionary<string, object?>
        {
            ["text"] = text.Length > 1500 ? text[..1500] : text,
            ["voice_id"] = ResolveVoiceId(voiceId),
            ["language"] = string.IsNullOrWhiteSpace(language) ? "en" : language,
            ["output_format"] = new Dictionary<string, object?>
            {
                ["codec"] = "mp3",
                ["sample_rate"] = 24000,
                ["bit_rate"] = 128000,
            },
        };

        using var resp = await _http.PostAsJsonAsync("tts", payload, ct);
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = System.Text.Encoding.UTF8.GetString(bytes);
            _log.LogWarning("TTS HTTP {Code}: {Body}", (int)resp.StatusCode, err.Length > 300 ? err[..300] : err);
            throw new InvalidOperationException(
                $"TTS failed ({(int)resp.StatusCode}): {(err.Length > 200 ? err[..200] : err)}");
        }
        if (bytes.Length < 64)
            throw new InvalidOperationException("TTS returned empty audio.");
        return bytes;
    }

    private void EnsureAuthHeader()
    {
        var key = ApiKeyScope.Current
                  ?? Environment.GetEnvironmentVariable("XAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
        {
            _http.DefaultRequestHeaders.Authorization = null;
            return;
        }
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", key.Trim());
    }
}
