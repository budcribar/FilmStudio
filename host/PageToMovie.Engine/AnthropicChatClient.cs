using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using PageToMovie.Engine.Abstractions;

namespace PageToMovie.Engine;

/// <summary>
/// Anthropic Messages API client — chat completion (planning / cast scrub / QA reasoning)
/// and multimodal completion (clip / frame review). Mirrors <see cref="GrokChatClient"/>'s
/// telemetry and auth conventions so callers and the api_calls.jsonl log see the same shape
/// regardless of provider.
/// </summary>
public sealed class AnthropicChatClient : IChatClient, IVisionClient
{
    public const string ApiBase = SupportedModelCatalog.AnthropicApiBase;
    public const string ApiVersion = "2023-06-01";
    // 4096 silently truncated full-book single-shot screenplay adaptations mid-scene (Anthropic
    // returns 200 OK with stop_reason "max_tokens", not an error, so nothing caught it). Other
    // models' full adaptations of the same book ran 3.6K-9.7K output tokens already close to or
    // past the old cap; 16K leaves real headroom without guessing per-model.
    public const int DefaultMaxTokens = 16_000;

    /// <summary>
    /// Resolves the <c>max_tokens</c> request field from the model catalog's confirmed
    /// per-model ceiling (<see cref="SupportedModelEntry.MaxOutputTokens"/>), falling back to
    /// <see cref="DefaultMaxTokens"/> when the catalog has no entry or hasn't confirmed a real
    /// number for this model. Never guesses a per-model max — an unconfirmed catalog entry means
    /// "use the safe default", not "make one up".
    /// </summary>
    private static int ResolveMaxTokens(string model) =>
        SupportedModelCatalog.Find(model, ModelCapability.Chat)?.MaxOutputTokens ?? DefaultMaxTokens;

    private readonly HttpClient _http;
    private readonly ProjectTelemetryService _telemetry;
    private readonly ILogger<AnthropicChatClient> _log;

    public AnthropicChatClient(
        HttpClient http,
        IOptions<PageToMovieOptions> opts,
        ProjectTelemetryService telemetry,
        ILogger<AnthropicChatClient> log)
    {
        _ = opts; // reserved — no Anthropic-specific options today
        _http = http;
        _telemetry = telemetry;
        _log = log;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(ApiBase + "/");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveApiKey());

    /// <summary>Maps the provider-neutral <c>reasoningEffort</c> scale to Anthropic's
    /// <c>output_config.effort</c> values (confirmed live: low/medium/high/xhigh/max).</summary>
    private static string? MapReasoningEffort(string? effort) => effort?.Trim().ToLowerInvariant() switch
    {
        null or "" => null,
        "max" or "xhigh" => "max",
        var other => other,
    };

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        string model = "claude-sonnet-5",
        double temperature = 0.2,
        CancellationToken ct = default,
        string? mode = null,
        string? reasoningEffort = null)
    {
        var mappedEffort = MapReasoningEffort(reasoningEffort);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["max_tokens"] = ResolveMaxTokens(model),
            ["system"] = systemPrompt,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?> { ["role"] = "user", ["content"] = userPrompt },
            },
        };
        if (mappedEffort is not null)
        {
            // Extended/adaptive thinking requires temperature to stay at its implicit default
            // (confirmed live: "`temperature` may only be set to 1 when thinking is enabled or
            // in adaptive mode") — omit it up front rather than round-tripping a 400 first.
            payload["thinking"] = new Dictionary<string, object?> { ["type"] = "adaptive" };
            payload["output_config"] = new Dictionary<string, object?> { ["effort"] = mappedEffort };
        }
        else
        {
            payload["temperature"] = temperature;
        }
        return await SendAsync(
            payload, model, "chat", "messages", mode,
            systemPrompt, userPrompt,
            (systemPrompt?.Length ?? 0) + (userPrompt?.Length ?? 0),
            ct).ConfigureAwait(false);
    }

    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    /// <summary>Multi-image completion for clip auto-review (prev tail + current frames).</summary>
    public async Task<string> CompleteWithImagesAsync(
        string prompt,
        IReadOnlyList<string> imagePaths,
        string model = "claude-sonnet-5",
        string detail = "low",
        CancellationToken ct = default)
    {
        var content = new List<object?>();
        foreach (var path in imagePaths.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p) && AllowedImageExtensions.Contains(Path.GetExtension(p))))
        {
            var (mime, b64) = await FileToBase64Async(path, ct).ConfigureAwait(false);
            content.Add(new Dictionary<string, object?>
            {
                ["type"] = "image",
                ["source"] = new Dictionary<string, object?>
                {
                    ["type"] = "base64",
                    ["media_type"] = mime,
                    ["data"] = b64,
                },
            });
        }
        content.Add(new Dictionary<string, object?> { ["type"] = "text", ["text"] = prompt });

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["max_tokens"] = ResolveMaxTokens(model),
            ["messages"] = new object[]
            {
                new Dictionary<string, object?> { ["role"] = "user", ["content"] = content },
            },
        };
        return await SendAsync(
            payload, model, "vision", "messages", "clip_auto_review",
            prompt, string.Join(", ", imagePaths.Select(Path.GetFileName)),
            prompt.Length, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Not implemented for Anthropic — book-page OCR / cast classification stay on
    /// <see cref="GrokVisionClient"/> for now. Fails loudly rather than silently
    /// returning a wrong answer if ever routed here.
    /// </summary>
    public Task<string> TranscribePageAsync(
        string imagePath, int page, string model = "claude-sonnet-5", CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Book-page transcription is not implemented for Anthropic yet — route this call to Grok.");

    /// <inheritdoc cref="TranscribePageAsync"/>
    public Task<CharacterPageClassification> ClassifyCharactersOnImageAsync(
        string imagePath, int page, IReadOnlyList<CharacterClassifyHint> cast,
        string model = "claude-sonnet-5", CancellationToken ct = default) =>
        throw new NotSupportedException(
            "Character-page classification is not implemented for Anthropic yet — route this call to Grok.");

    private async Task<string> SendAsync(
        Dictionary<string, object?> payload,
        string model,
        string kind,
        string endpoint,
        string? mode,
        string? promptForLog,
        string? userPromptForLog,
        int promptChars,
        CancellationToken ct)
    {
        var key = ResolveApiKey();
        var modeTag = string.IsNullOrWhiteSpace(mode) ? null : mode.Trim();
        var sw = Stopwatch.StartNew();
        try
        {
            // Auth on a per-request message, not _http.DefaultRequestHeaders: this client is a
            // singleton shared by every concurrent classifier call, and mutating shared headers
            // per-call is a race (one call's key can leak into or clobber another's in flight).
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(payload),
            };
            if (!string.IsNullOrWhiteSpace(key))
            {
                req.Headers.Add("x-api-key", key.Trim());
                req.Headers.Add("anthropic-version", ApiVersion);
            }
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                // Some Anthropic models (e.g. claude-sonnet-5) have deprecated `temperature`
                // entirely and 400 if it's present at all. Retry once without it rather than
                // hardcoding a model-id list that would drift as Anthropic adds/retires models —
                // the API itself is telling us definitively when this applies.
                if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest
                    && payload.ContainsKey("temperature")
                    && body.Contains("temperature", StringComparison.OrdinalIgnoreCase)
                    && body.Contains("deprecated", StringComparison.OrdinalIgnoreCase))
                {
                    var retryPayload = new Dictionary<string, object?>(payload);
                    retryPayload.Remove("temperature");
                    return await SendAsync(
                        retryPayload, model, kind, endpoint, mode,
                        promptForLog, userPromptForLog, promptChars, ct).ConfigureAwait(false);
                }

                // Older/smaller Claude models don't support adaptive thinking or output_config.effort
                // at all — self-heal the same way rather than maintaining a model-capability list.
                if (resp.StatusCode == System.Net.HttpStatusCode.BadRequest
                    && (payload.ContainsKey("thinking") || payload.ContainsKey("output_config"))
                    && (body.Contains("thinking", StringComparison.OrdinalIgnoreCase)
                        || body.Contains("output_config", StringComparison.OrdinalIgnoreCase)))
                {
                    var retryPayload = new Dictionary<string, object?>(payload);
                    retryPayload.Remove("thinking");
                    retryPayload.Remove("output_config");
                    retryPayload["temperature"] = 0.2;
                    return await SendAsync(
                        retryPayload, model, kind, endpoint, mode,
                        promptForLog, userPromptForLog, promptChars, ct).ConfigureAwait(false);
                }

                await _telemetry.LogApiCallAsync(new ApiCallTelemetry
                {
                    Kind = kind,
                    Mode = modeTag,
                    Endpoint = endpoint,
                    Model = model,
                    HttpStatus = (int)resp.StatusCode,
                    DurationMs = sw.ElapsedMilliseconds,
                    SystemPrompt = promptForLog,
                    UserPrompt = userPromptForLog,
                    PromptChars = promptChars,
                    Error = Trim(body, 800),
                    Ok = false,
                });
                throw new InvalidOperationException(
                    $"Anthropic {endpoint} HTTP {(int)resp.StatusCode}: {Trim(body, 800)}");
            }

            using var doc = JsonDocument.Parse(body);
            var text = ExtractMessageText(doc.RootElement);
            await _telemetry.LogApiCallAsync(new ApiCallTelemetry
            {
                Kind = kind,
                Mode = modeTag,
                Endpoint = endpoint,
                Model = model,
                HttpStatus = (int)resp.StatusCode,
                DurationMs = sw.ElapsedMilliseconds,
                SystemPrompt = promptForLog,
                UserPrompt = userPromptForLog,
                PromptChars = promptChars,
                ResponsePreview = text.Length > 2000 ? text[..2000] : text,
                ResponseChars = text.Length,
                Ok = true,
            });
            return text;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            await _telemetry.LogApiCallAsync(new ApiCallTelemetry
            {
                Kind = kind,
                Mode = modeTag,
                Endpoint = endpoint,
                Model = model,
                DurationMs = sw.ElapsedMilliseconds,
                SystemPrompt = promptForLog,
                UserPrompt = userPromptForLog,
                Error = ex.Message,
                Ok = false,
            });
            throw;
        }
    }

    /// <summary>Test helper for extracting assistant text from a Messages API response.</summary>
    public static string ExtractMessageTextForTests(JsonElement result) => ExtractMessageText(result);

    /// <summary>
    /// Anthropic Messages API response shape: <c>{ content: [{ type: "text", text: "..." }, ...] }</c>.
    /// Concatenates all text blocks (tool_use / other block types are skipped).
    /// </summary>
    private static string? ResolveApiKey()
    {
        var raw = Abstractions.ApiKeyScope.CurrentAnthropic
            ?? Environment.GetEnvironmentVariable(SupportedModelCatalog.AnthropicApiKeyEnv);
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim(' ', '"', '\'', '\r', '\n', '\t');
    }

    private static string ExtractMessageText(JsonElement result)
    {
        if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object &&
                    block.TryGetProperty("type", out var t) &&
                    string.Equals(t.GetString(), "text", StringComparison.Ordinal) &&
                    block.TryGetProperty("text", out var txt))
                {
                    parts.Add(txt.GetString() ?? "");
                }
            }
            if (parts.Count > 0)
                return string.Join("\n", parts);
        }
        var raw = result.GetRawText();
        return raw.Length <= 2000 ? raw : raw[..2000];
    }

    private static async Task<(string Mime, string Base64)> FileToBase64Async(string path, CancellationToken ct)
    {
        var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var mime = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "image/jpeg",
        };
        return (mime, Convert.ToBase64String(bytes));
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..n];
}
