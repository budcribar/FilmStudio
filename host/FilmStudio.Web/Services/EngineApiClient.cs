using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FilmStudio.Core.Models;

namespace FilmStudio.Web.Services;

/// <summary>HTTP client for FilmStudio.Api (C# backend).</summary>
public sealed class EngineApiClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;

    public EngineApiClient(HttpClient http) => _http = http;

    public async Task EnsureHealthyAsync(CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync("/health", ct);
        resp.EnsureSuccessStatusCode();
    }

    public async Task<ProjectsDto?> GetProjectsAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<ProjectsDto>("/api/projects", JsonOpts, ct);

    public async Task ActivateProjectAsync(string projectId, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/activate",
            new { },
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(err);
        }
    }

    public async Task<JobsDto?> GetJobAsync(CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<JobsDto>("/api/jobs", JsonOpts, ct);

    public async Task StartSceneGenAsync(
        string projectId,
        int scene,
        bool onlyMissing = true,
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            "/api/jobs/gen-scene",
            new StartSceneGenRequest
            {
                ProjectId = projectId,
                Scene = scene,
                OnlyMissing = onlyMissing,
            },
            JsonOpts,
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? $"{(int)resp.StatusCode}");
        }
    }

    public async Task CancelJobAsync(CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("/api/jobs/cancel", new { }, ct);
        resp.EnsureSuccessStatusCode();
    }

    private static string? TryError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var e))
                return e.GetString();
        }
        catch { /* ignore */ }
        return json.Length > 200 ? json[..200] : json;
    }
}

public sealed class ProjectsDto
{
    public bool Ok { get; set; }
    public ProjectInfo? Active { get; set; }
    public List<ProjectInfo> Projects { get; set; } = new();
}

public sealed class JobsDto
{
    public bool Ok { get; set; }
    public bool Running { get; set; }
    public JobSnapshot? Job { get; set; }
}
