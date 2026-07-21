using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClassifierBenchmarks;

public sealed class ChatRunner : IDisposable
{
    private readonly HttpClient _http;

    public ChatRunner(string apiKey)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.x.ai/v1/"),
            Timeout = TimeSpan.FromMinutes(5),
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> CompleteAsync(string model, double temperature, string system, string user, CancellationToken ct = default)
    {
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
        using var resp = await _http.PostAsync("chat/completions",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"chat {(int)resp.StatusCode}: {Trim(text, 400)}");
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
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
