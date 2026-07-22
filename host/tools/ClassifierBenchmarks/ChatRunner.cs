using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClassifierBenchmarks;

public sealed class ChatRunner : IDisposable
{
    private readonly HttpClient _http;
    private readonly string? _xaiApiKey;
    private readonly string? _claudeApiKey;

    public ChatRunner(string? xaiApiKey, string? claudeApiKey)
    {
        _xaiApiKey = xaiApiKey;
        _claudeApiKey = claudeApiKey;
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
    }

    public static bool IsClaudeModel(string model) =>
        model.StartsWith("claude", StringComparison.OrdinalIgnoreCase);

    public async Task<string> CompleteAsync(string model, double temperature, string system, string user, CancellationToken ct = default) =>
        IsClaudeModel(model)
            ? await CompleteClaudeAsync(model, temperature, system, user, ct)
            : await CompleteXaiAsync(model, temperature, system, user, ct);

    private async Task<string> CompleteXaiAsync(string model, double temperature, string system, string user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_xaiApiKey))
            throw new InvalidOperationException($"XAI_API_KEY required for model '{model}'");

        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = temperature,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?> { ["role"] = "system", ["content"] = system },
                new Dictionary<string, object?> { ["role"] = "user", ["content"] = user },
            },
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.x.ai/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _xaiApiKey);

        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"xai chat {(int)resp.StatusCode}: {Trim(text, 400)}");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private async Task<string> CompleteClaudeAsync(string model, double temperature, string system, string user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_claudeApiKey))
            throw new InvalidOperationException($"CLAUDE_API_KEY required for model '{model}'");

        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            // Anthropic requires max_tokens; classifier replies are short JSON payloads.
            ["max_tokens"] = 4096,
            ["system"] = system,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?> { ["role"] = "user", ["content"] = user },
            },
        };
        // Newer Claude models (e.g. claude-sonnet-5) reject an explicit `temperature` field
        // ("temperature is deprecated for this model") — only send it when non-default.
        if (temperature > 0)
            body["temperature"] = Math.Clamp(temperature, 0, 1);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("x-api-key", _claudeApiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");

        using var resp = await _http.SendAsync(req, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"claude chat {(int)resp.StatusCode}: {Trim(text, 400)}");
        using var doc = JsonDocument.Parse(text);
        var sb = new StringBuilder();
        foreach (var block in doc.RootElement.GetProperty("content").EnumerateArray())
        {
            if (block.TryGetProperty("type", out var t) && t.GetString() == "text" &&
                block.TryGetProperty("text", out var txt))
                sb.Append(txt.GetString());
        }
        return sb.ToString();
    }

    public static string Sha256Short(string s)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(s ?? ""));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    public static string Trim(string s, int n) =>
        string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..n] + "…";

    public void Dispose() => _http.Dispose();
}
