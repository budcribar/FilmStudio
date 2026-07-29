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
/// Fal.ai serverless audio / music client (fal-ai/stable-audio, fal-ai/minimax-music, etc.).
/// Endpoint: https://fal.run/fal-ai/stable-audio
/// </summary>
public sealed class FalMusicClient : IMusicClient
{
    public const string ApiBase = "https://fal.run/";

    private readonly HttpClient _http;
    private readonly PageToMovieOptions _opts;
    private readonly ILogger<FalMusicClient> _log;

    public FalMusicClient(
        HttpClient http,
        IOptions<PageToMovieOptions> opts,
        ILogger<FalMusicClient> log)
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

    public async Task<byte[]?> GenerateMusicTrackAsync(
        string prompt,
        double durationSeconds,
        string? model = null,
        CancellationToken ct = default)
    {
        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.LogWarning("Fal.ai API key is missing — skipping background music generation.");
            return null;
        }

        model = string.IsNullOrWhiteSpace(model) ? "fal-ai/stable-audio" : model;
        var dur = Math.Clamp((int)Math.Round(durationSeconds), 5, 47);

        var payload = new Dictionary<string, object?>
        {
            ["prompt"] = prompt,
            ["seconds_total"] = dur,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, model.TrimStart('/'));
        req.Headers.Authorization = new AuthenticationHeaderValue("Key", apiKey);
        req.Content = JsonContent.Create(payload);

        var sw = Stopwatch.StartNew();
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            _log.LogError("Fal.ai music gen failed HTTP {Status} ({Elapsed}ms): {Body}", resp.StatusCode, sw.ElapsedMilliseconds, body);
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("audio_file", out var audioEl) && audioEl.ValueKind == JsonValueKind.Object)
        {
            if (audioEl.TryGetProperty("url", out var urlEl) && urlEl.GetString() is { Length: > 0 } url)
            {
                return await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
            }
        }

        return null;
    }
}
