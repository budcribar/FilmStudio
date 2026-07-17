using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FilmStudio.Core.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FilmStudio.Engine.Abstractions;

namespace FilmStudio.Engine;

/// <summary>xAI chat/completions client for Stage 1 scene bible generation.</summary>
public sealed class GrokChatClient : IGrokChatClient
{
    public const string ApiBase = "https://api.x.ai/v1";

    private readonly HttpClient _http;
    private readonly ILogger<GrokChatClient> _log;

    public GrokChatClient(
        HttpClient http,
        IOptions<FilmStudioOptions> opts,
        ILogger<GrokChatClient> log)
    {
        _http = http;
        _log = log;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri(ApiBase + "/");
    }

    public bool IsConfigured
    {
        get
        {
            EnsureAuth();
            return _http.DefaultRequestHeaders.Authorization is not null;
        }
    }

    public async Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        string model = "grok-4.5",
        double temperature = 0.2,
        CancellationToken ct = default)
    {
        EnsureAuth();
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["temperature"] = temperature,
            ["messages"] = new object[]
            {
                new Dictionary<string, object?> { ["role"] = "system", ["content"] = systemPrompt },
                new Dictionary<string, object?> { ["role"] = "user", ["content"] = userPrompt },
            },
        };

        using var resp = await _http.PostAsJsonAsync("chat/completions", payload, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Grok chat HTTP {(int)resp.StatusCode}: {Trim(body, 800)}");

        using var doc = JsonDocument.Parse(body);
        return ExtractMessageText(doc.RootElement);
    }

    public static Dictionary<string, object?> ParseJsonObject(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            text = Regex.Replace(text, @"^```(?:json)?\s*", "");
            text = Regex.Replace(text, @"\s*```$", "");
        }
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            throw new InvalidOperationException("No JSON object in model output");
        var blob = text[start..(end + 1)];
        using var doc = JsonDocument.Parse(blob);
        return JsonElementToDict(doc.RootElement);
    }

    private static Dictionary<string, object?> JsonElementToDict(JsonElement el)
    {
        var d = new Dictionary<string, object?>();
        foreach (var p in el.EnumerateObject())
            d[p.Name] = JsonElementToObject(p.Value);
        return d;
    }

    private static object? JsonElementToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object => JsonElementToDict(el),
        JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var i) ? i : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetRawText(),
    };

    private static string ExtractMessageText(JsonElement result)
    {
        if (result.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0)
        {
            var msg = choices[0].GetProperty("message");
            if (msg.TryGetProperty("content", out var content))
            {
                if (content.ValueKind == JsonValueKind.String)
                    return content.GetString() ?? "";
                if (content.ValueKind == JsonValueKind.Array)
                {
                    var parts = new List<string>();
                    foreach (var c in content.EnumerateArray())
                    {
                        if (c.ValueKind == JsonValueKind.String)
                            parts.Add(c.GetString() ?? "");
                        else if (c.TryGetProperty("text", out var t))
                            parts.Add(t.GetString() ?? "");
                    }
                    return string.Join("\n", parts);
                }
            }
        }
        if (result.TryGetProperty("output_text", out var ot) && ot.GetString() is { Length: > 0 } s)
            return s;
        return result.GetRawText()[..Math.Min(2000, result.GetRawText().Length)];
    }

    private void EnsureAuth()
    {
        if (_http.DefaultRequestHeaders.Authorization is not null)
            return;
        var key = Environment.GetEnvironmentVariable("XAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key))
            return;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", key.Trim());
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..n];
}
