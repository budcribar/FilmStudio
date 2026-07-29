using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PageToMovie.Engine;

/// <summary>
/// Fal.ai serverless GPU audio &amp; background music generation client (Stable Audio / MusicGen).
/// Direct endpoint: https://fal.run/fal-ai/stable-audio
/// </summary>
public sealed class FalAudioClient : IAudioClient
{
    public const string ApiBase = "https://fal.run/";

    /// <summary>fal-ai/stable-audio rejects seconds_total outside roughly this range — callers
    /// generate multiple segments and concatenate client-side for anything longer.</summary>
    public const int MaxSegmentDurationSecondsConst = 47;

    public int MaxSegmentDurationSeconds => MaxSegmentDurationSecondsConst;

    private readonly HttpClient _http;
    private readonly PageToMovieOptions _opts;
    private readonly ILogger<FalAudioClient> _log;

    public FalAudioClient(
        HttpClient http,
        IOptions<PageToMovieOptions> opts,
        ILogger<FalAudioClient> log)
    {
        _http = http;
        _opts = opts.Value;
        _log = log;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(ApiBase);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveApiKey());

    private static string? ResolveApiKey()
    {
        var key = ApiKeyScope.CurrentFal
            ?? Environment.GetEnvironmentVariable(SupportedModelCatalog.FalApiKeyEnv)
            ?? Environment.GetEnvironmentVariable(SupportedModelCatalog.FalApiKeyFallbackEnv);
        if (!string.IsNullOrWhiteSpace(key)) return key.Trim(' ', '"', '\'', '\r', '\n', '\t');
        return null;
    }

    public async Task<string?> GenerateMusicTrackAsync(
        string prompt,
        int durationSeconds,
        string? model = null,
        CancellationToken ct = default)
    {
        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.LogWarning("Fal.ai API key is missing — skipping audio generation.");
            return null;
        }

        model = string.IsNullOrWhiteSpace(model) ? "fal-ai/stable-audio" : model;
        // Real fal-ai/stable-audio hard limit (not an arbitrary choice — see
        // https://github.com/Stability-AI/stable-audio-tools/issues/154). Callers that need
        // longer coverage generate MaxSegmentDurationSeconds-sized segments and concatenate
        // client-side, same as video's per-clip duration limits.
        durationSeconds = Math.Clamp(durationSeconds, 2, MaxSegmentDurationSecondsConst);

        var payload = new Dictionary<string, object?>
        {
            ["prompt"] = prompt,
            ["seconds_total"] = durationSeconds,
            ["seconds_start"] = 0,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, model.TrimStart('/'));
        req.Headers.Authorization = new AuthenticationHeaderValue("Key", apiKey);
        req.Content = JsonContent.Create(payload);

        var sw = Stopwatch.StartNew();
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            _log.LogError("Fal.ai audio gen failed HTTP {Status} ({Elapsed}ms): {Body}", resp.StatusCode, sw.ElapsedMilliseconds, body);
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        string? audioUrl = null;

        // Parse standard Fal audio response shapes: audio_file.url, audio.url, or a bare url.
        if (doc.RootElement.TryGetProperty("audio_file", out var audioFileEl) && audioFileEl.TryGetProperty("url", out var urlEl1))
        {
            audioUrl = urlEl1.GetString();
        }
        else if (doc.RootElement.TryGetProperty("audio", out var audioEl) && audioEl.TryGetProperty("url", out var urlEl2))
        {
            audioUrl = urlEl2.GetString();
        }
        else if (doc.RootElement.TryGetProperty("url", out var urlEl3))
        {
            audioUrl = urlEl3.GetString();
        }

        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            _log.LogError("Fal.ai returned no audio URL: {Body}", body);
            return null;
        }

        _log.LogInformation("Fal.ai audio generated successfully ({Elapsed}ms): {Url}", sw.ElapsedMilliseconds, audioUrl);
        // Return the provider URL directly — the caller proxies it to the client (same as
        // video); the server never downloads generated media bytes into its own memory/disk.
        return audioUrl;
    }
}