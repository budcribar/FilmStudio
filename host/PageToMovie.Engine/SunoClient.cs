using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Suno background-music generation via sunoapi.org — an unofficial third-party Suno reseller
/// (Suno itself has no public API as of 2026-07). Submits a generation task, then polls for
/// completion (no public webhook receiver exists here, so polling only). Unlike Fal.ai's
/// stable-audio, this provider documents a real duration control (10-360s on model V5_5 custom
/// mode) — see SupportedModelCatalog's suno-v5-5 entry — which is the whole reason to have it:
/// scenes over Fal's 47s cap don't need to be stitched from independently-generated segments.
/// </summary>
public sealed class SunoClient : IAudioClient
{
    public const string ApiBase = "https://api.sunoapi.org/api/v1/";
    private const string DefaultModel = "V5_5";
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan PollTimeout = TimeSpan.FromMinutes(6);

    private readonly HttpClient _http;
    private readonly ILogger<SunoClient> _log;

    public SunoClient(HttpClient http, ILogger<SunoClient> log)
    {
        _http = http;
        _log = log;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(ApiBase);
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveApiKey());

    private static string? ResolveApiKey()
    {
        var key = ApiKeyScope.CurrentSuno
            ?? Environment.GetEnvironmentVariable(SupportedModelCatalog.SunoApiKeyEnv);
        if (!string.IsNullOrWhiteSpace(key)) return key.Trim(' ', '"', '\'', '\r', '\n', '\t');
        return null;
    }

    public async Task<string?> GenerateMusicTrackAsync(
        string prompt,
        int durationSeconds,
        string? model = null,
        CancellationToken ct = default,
        Action<string>? onProgress = null,
        bool isVocal = false,
        string? lyrics = null)
    {
        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.LogWarning("Suno (sunoapi.org) API key is missing — skipping audio generation.");
            return null;
        }

        var clampedDuration = Math.Clamp(durationSeconds, 10, 360);
        var payload = new Dictionary<string, object?>
        {
            ["customMode"] = true,
            ["instrumental"] = !isVocal,
            // "style" carries genre/mood tags either way; when singing, "prompt" carries the actual
            // lyrics to sing — Suno's customMode API keeps these as two separate fields.
            ["style"] = prompt,
            ["prompt"] = isVocal ? (lyrics ?? "") : "",
            ["title"] = "Scene Score",
            ["model"] = DefaultModel,
            ["duration"] = clampedDuration,
            // No public webhook receiver here — we poll record-info instead. Their docs list
            // callBackUrl as required; empty string is accepted in practice by this class of API.
            ["callBackUrl"] = "",
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "generate");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        req.Content = JsonContent.Create(payload);

        onProgress?.Invoke("Submitting to Suno (sunoapi.org)…");
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            _log.LogError("Suno (sunoapi.org) submit failed HTTP {Status}: {Body}", resp.StatusCode, body);
            onProgress?.Invoke($"Suno submit failed: HTTP {(int)resp.StatusCode}");
            return null;
        }

        string? taskId;
        try
        {
            using var doc = JsonDocument.Parse(body);
            taskId = doc.RootElement.TryGetProperty("data", out var dataEl)
                     && dataEl.TryGetProperty("taskId", out var idEl)
                ? idEl.GetString()
                : null;
        }
        catch (JsonException ex)
        {
            _log.LogError(ex, "Suno (sunoapi.org) submit returned unparseable JSON: {Body}", body);
            return null;
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            _log.LogError("Suno (sunoapi.org) submit response had no taskId: {Body}", body);
            return null;
        }

        return await PollForAudioUrlAsync(taskId, apiKey, onProgress, ct).ConfigureAwait(false);
    }

    private async Task<string?> PollForAudioUrlAsync(
        string taskId, string apiKey, Action<string>? onProgress, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + PollTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(PollInterval, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            using var req = new HttpRequestMessage(HttpMethod.Get, $"generate/record-info?taskId={Uri.EscapeDataString(taskId)}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Suno (sunoapi.org) poll HTTP {Status}: {Body}", resp.StatusCode, body);
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("data", out var dataEl))
                    continue;

                var status = dataEl.TryGetProperty("status", out var statusEl) ? statusEl.GetString() : null;
                onProgress?.Invoke($"Suno status: {status ?? "unknown"}");

                if (string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                {
                    if (dataEl.TryGetProperty("response", out var responseEl) &&
                        responseEl.TryGetProperty("sunoData", out var sunoDataEl) &&
                        sunoDataEl.ValueKind == JsonValueKind.Array &&
                        sunoDataEl.GetArrayLength() > 0)
                    {
                        var first = sunoDataEl[0];
                        var audioUrl = first.TryGetProperty("audioUrl", out var urlEl) ? urlEl.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(audioUrl))
                        {
                            _log.LogInformation("Suno (sunoapi.org) audio ready: {Url}", audioUrl);
                            return audioUrl;
                        }
                    }
                    _log.LogError("Suno (sunoapi.org) status SUCCESS but no audioUrl found: {Body}", body);
                    return null;
                }

                var isFailure = status is "CREATE_TASK_FAILED" or "GENERATE_AUDIO_FAILED"
                    or "CALLBACK_EXCEPTION" or "SENSITIVE_WORD_ERROR";
                if (isFailure)
                {
                    var errorMessage = dataEl.TryGetProperty("errorMessage", out var errEl) ? errEl.GetString() : null;
                    _log.LogError("Suno (sunoapi.org) generation failed: {Status} {Error}", status, errorMessage);
                    return null;
                }
                // PENDING / TEXT_SUCCESS / FIRST_SUCCESS — keep polling.
            }
            catch (JsonException ex)
            {
                _log.LogWarning(ex, "Suno (sunoapi.org) poll returned unparseable JSON: {Body}", body);
            }
        }

        _log.LogError("Suno (sunoapi.org) generation timed out after {Timeout} for task {TaskId}", PollTimeout, taskId);
        onProgress?.Invoke("Suno generation timed out.");
        return null;
    }
}
