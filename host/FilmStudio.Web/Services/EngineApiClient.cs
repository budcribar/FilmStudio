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

    public async Task StartBatchGenAsync(
        string projectId,
        IReadOnlyList<int> scenes,
        bool onlyMissing = true,
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            "/api/jobs/gen-batch",
            new StartBatchGenRequest
            {
                ProjectId = projectId,
                Scenes = scenes.ToList(),
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

    public async Task<ScenesListDto?> GetScenesAsync(string projectId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<ScenesListDto>(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/scenes",
            JsonOpts,
            ct);

    public async Task<SceneDetailDto?> GetSceneDetailAsync(
        string projectId,
        int sceneNumber,
        CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<SceneDetailDto>(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/scenes/{sceneNumber}",
            JsonOpts,
            ct);

    public string ClipVideoUrl(string projectId, int sceneNumber, int clipNumber)
    {
        var baseUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? "";
        return $"{baseUrl}/api/projects/{Uri.EscapeDataString(projectId)}/scenes/{sceneNumber}/clips/{clipNumber}/video";
    }

    public string CompositeVideoUrl(string projectId, int sceneNumber)
    {
        var baseUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? "";
        return $"{baseUrl}/api/projects/{Uri.EscapeDataString(projectId)}/scenes/{sceneNumber}/composite";
    }

    public async Task<AdaptationDto?> GetAdaptationAsync(string projectId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<AdaptationDto>(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/adaptation",
            JsonOpts,
            ct);

    public async Task StartStage1Async(StartStage1Request req, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("/api/jobs/stage1", req, JsonOpts, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
    }

    public async Task StartStage2Async(StartStage2Request req, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("/api/jobs/stage2", req, JsonOpts, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
    }

    public async Task StartRemuxAsync(
        string projectId,
        int? scene = null,
        bool rebuildWip = true,
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            "/api/jobs/remux",
            new StartRemuxRequest
            {
                ProjectId = projectId,
                Scene = scene,
                RebuildWip = rebuildWip,
            },
            JsonOpts,
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
    }

    public async Task ReviewClipAsync(
        string projectId,
        int scene,
        int clip,
        string status,
        string note = "",
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/clips/review",
            new ClipReviewRequest
            {
                ProjectId = projectId,
                Scene = scene,
                Clip = clip,
                Status = status,
                Note = note,
            },
            JsonOpts,
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
    }

    public async Task ApproveSceneAsync(
        string projectId,
        int scene,
        string note = "",
        bool remux = false,
        bool rebuildWip = false,
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/scenes/{scene}/approve",
            new SceneApproveRequest
            {
                ProjectId = projectId,
                Scene = scene,
                Note = note,
                Remux = remux,
                RebuildWip = rebuildWip,
            },
            JsonOpts,
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
    }

    public async Task<EditLogDto?> GetEditLogAsync(string projectId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<EditLogDto>(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/edit-log",
            JsonOpts,
            ct);

    public async Task<ClipReviewsDto?> GetClipReviewsAsync(string projectId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<ClipReviewsDto>(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/clip-reviews",
            JsonOpts,
            ct);

    public async Task UploadBookAsync(
        string projectId,
        string fileName,
        Stream content,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        form.Add(streamContent, "file", fileName);
        using var resp = await _http.PostAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/adaptation/upload",
            form,
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
    }

    public async Task<CostDto?> GetCostAsync(
        string projectId,
        string? draftResolution = null,
        string? heroResolution = null,
        double? assumeAvgRetries = null,
        CancellationToken ct = default)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(draftResolution))
            q.Add($"draftResolution={Uri.EscapeDataString(draftResolution)}");
        if (!string.IsNullOrWhiteSpace(heroResolution))
            q.Add($"heroResolution={Uri.EscapeDataString(heroResolution)}");
        if (assumeAvgRetries is double r)
            q.Add($"assumeAvgRetries={r.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        var qs = q.Count > 0 ? "?" + string.Join("&", q) : "";
        return await _http.GetFromJsonAsync<CostDto>(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/cost{qs}",
            JsonOpts,
            ct);
    }

    public async Task<CostBackfillDto?> BackfillCostAsync(string projectId, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/cost/backfill",
            new { },
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
        return await resp.Content.ReadFromJsonAsync<CostBackfillDto>(JsonOpts, ct);
    }

    public async Task<ConfigDto?> GetConfigAsync(string projectId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<ConfigDto>(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/config",
            JsonOpts,
            ct);

    public async Task<ConfigDto?> SaveConfigAsync(
        string projectId,
        Dictionary<string, object?> updates,
        CancellationToken ct = default)
    {
        using var resp = await _http.PutAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/config",
            updates,
            JsonOpts,
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
        return await resp.Content.ReadFromJsonAsync<ConfigDto>(JsonOpts, ct);
    }

    public async Task<CharactersDto?> GetCharactersAsync(string projectId, CancellationToken ct = default) =>
        await _http.GetFromJsonAsync<CharactersDto>(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/characters",
            JsonOpts,
            ct);

    public string CharacterRefUrl(string projectId, string charKey)
    {
        var baseUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? "";
        return $"{baseUrl}/api/projects/{Uri.EscapeDataString(projectId)}/characters/{Uri.EscapeDataString(charKey)}/ref";
    }

    public string CharacterVariantUrl(string projectId, string charKey, int index)
    {
        var baseUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? "";
        return $"{baseUrl}/api/projects/{Uri.EscapeDataString(projectId)}/characters/{Uri.EscapeDataString(charKey)}/variants/{index}";
    }

    public string CharacterBookRefUrl(string projectId, string charKey, int index)
    {
        var baseUrl = _http.BaseAddress?.ToString().TrimEnd('/') ?? "";
        return $"{baseUrl}/api/projects/{Uri.EscapeDataString(projectId)}/characters/{Uri.EscapeDataString(charKey)}/bookrefs/{index}";
    }

    public async Task StartCharacterVariantsAsync(
        string projectId,
        string charKey,
        CancellationToken ct = default) =>
        await StartCharacterVariantsAsync(new StartCharacterVariantsRequest
        {
            ProjectId = projectId,
            CharKey = charKey,
            SeedMode = "auto",
        }, ct);

    public async Task StartCharacterVariantsAsync(
        StartCharacterVariantsRequest req,
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            "/api/jobs/character-variants",
            req,
            JsonOpts,
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
    }

    public async Task<AttachCharacterPlatesResult?> AttachBookPlatesAsync(
        string projectId,
        bool force = true,
        string? charKey = null,
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/characters/attach-book-plates",
            new AttachCharacterPlatesRequest
            {
                ProjectId = projectId,
                Force = force,
                CopyIntoAssets = true,
                CharKey = charKey,
            },
            JsonOpts,
            ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(TryError(body) ?? resp.ReasonPhrase);
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("attach", out var att))
                return JsonSerializer.Deserialize<AttachCharacterPlatesResult>(att.GetRawText(), JsonOpts);
        }
        catch { /* ignore */ }
        return null;
    }

    public async Task StartBookPrepareAsync(
        string projectId,
        bool forceExtract = true,
        bool forceVision = false,
        bool autoVision = true,
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            "/api/jobs/book-prepare",
            new StartBookPrepareRequest
            {
                ProjectId = projectId,
                ForceExtract = forceExtract,
                ForceVision = forceVision,
                AutoVision = autoVision,
            },
            JsonOpts,
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
    }

    public async Task LockCharacterVariantAsync(
        string projectId,
        string charKey,
        int index,
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/characters/{Uri.EscapeDataString(charKey)}/lock-variant",
            new { index },
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
    }

    public async Task LockCharacterBookRefAsync(
        string projectId,
        string charKey,
        int index,
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/characters/{Uri.EscapeDataString(charKey)}/lock-bookref",
            new { index },
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
    }

    public async Task UnlockCharacterAsync(
        string projectId,
        string charKey,
        CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/characters/{Uri.EscapeDataString(charKey)}/unlock",
            new { },
            ct);
        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(TryError(err) ?? resp.ReasonPhrase);
        }
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

public sealed class ConfigDto
{
    public bool Ok { get; set; }
    public string? ProjectId { get; set; }
    public Dictionary<string, JsonElement>? Config { get; set; }
}

public sealed class CharactersDto
{
    public bool Ok { get; set; }
    public string? ProjectId { get; set; }
    public List<CharacterSummary> Characters { get; set; } = new();
}

public sealed class EditLogDto
{
    public bool Ok { get; set; }
    public string? ProjectId { get; set; }
    public EditLogDocument? EditLog { get; set; }
}

public sealed class ClipReviewsDto
{
    public bool Ok { get; set; }
    public string? ProjectId { get; set; }
    public Dictionary<string, string>? Reviews { get; set; }
}

public sealed class ScenesListDto
{
    public bool Ok { get; set; }
    public string? ProjectId { get; set; }
    public int SceneCount { get; set; }
    public int ClipCount { get; set; }
    public int ClipsOnDisk { get; set; }
    public List<SceneSummary> Scenes { get; set; } = new();
}

public sealed class SceneDetailDto
{
    public bool Ok { get; set; }
    public string? ProjectId { get; set; }
    public SceneDetail? Scene { get; set; }
}

public sealed class AdaptationDto
{
    public bool Ok { get; set; }
    public string? ProjectId { get; set; }
    public AdaptationStatus? Adaptation { get; set; }
}

public sealed class CostDto
{
    public bool Ok { get; set; }
    public string? ProjectId { get; set; }
    public CostReport? Cost { get; set; }
}

public sealed class CostBackfillDto
{
    public bool Ok { get; set; }
    public string? ProjectId { get; set; }
    public CostBackfillResult? Backfill { get; set; }
}
