using System.Text.Json;
using FilmStudio.Api.Hubs;
using FilmStudio.Api.Services;
using FilmStudio.Core.Models;
using FilmStudio.Core.Options;
using FilmStudio.Engine;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<FilmStudioOptions>(
    builder.Configuration.GetSection(FilmStudioOptions.SectionName));

// Default workspace = repo root (two levels up from host/FilmStudio.Api)
var repoGuess = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));
builder.Services.PostConfigure<FilmStudioOptions>(o =>
{
    if (string.IsNullOrWhiteSpace(o.WorkspaceRoot) || !Directory.Exists(o.WorkspaceRoot))
        o.WorkspaceRoot = repoGuess;
});

builder.Services.AddSingleton<ProjectStore>();
builder.Services.AddSingleton<FilmJobService>();
builder.Services.AddSingleton<IJobProgressSink, SignalRJobProgressSink>();
builder.Services.AddHttpClient<GrokVideoClient>(c =>
{
    c.BaseAddress = new Uri(GrokVideoClient.ApiBase + "/");
    c.Timeout = TimeSpan.FromMinutes(15);
});

builder.Services.AddSignalR();
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p =>
        p.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true));
});

var app = builder.Build();

// Wire SignalR sink into job service
var jobs = app.Services.GetRequiredService<FilmJobService>();
jobs.SetProgressSink(app.Services.GetRequiredService<IJobProgressSink>());

app.UseCors();
app.MapHub<JobHub>("/hubs/jobs");

app.MapGet("/health", (ProjectStore store) =>
    Results.Ok(new
    {
        ok = true,
        service = "FilmStudio.Api",
        workspace = store.WorkspaceRoot,
        activeProject = store.ActiveProjectId,
        xaiConfigured = !string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("XAI_API_KEY")),
    }));

app.MapGet("/api/projects", (ProjectStore store) =>
{
    var list = store.ListProjects();
    var activeId = store.ActiveProjectId;
    var active = list.FirstOrDefault(p =>
        string.Equals(p.Id, activeId, StringComparison.OrdinalIgnoreCase));
    return Results.Ok(new { ok = true, active, projects = list });
});

app.MapPost("/api/projects/{id}/activate", (string id, ProjectStore store) =>
{
    try
    {
        var p = store.Activate(id);
        return Results.Ok(new { ok = true, active = p });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { ok = false, error = ex.Message });
    }
});

app.MapGet("/api/jobs", (FilmJobService jobService) =>
{
    var snap = jobService.GetSnapshot();
    return Results.Ok(new
    {
        ok = true,
        running = jobService.IsRunning,
        job = snap,
    });
});

app.MapPost("/api/jobs/gen-scene", async (StartSceneGenRequest body, FilmJobService jobService) =>
{
    try
    {
        if (body.Scene <= 0)
            return Results.BadRequest(new { ok = false, error = "scene required" });
        await jobService.StartSceneGenAsync(body);
        return Results.Accepted("/api/jobs", new
        {
            ok = true,
            message = $"Started scene {body.Scene}",
            job = jobService.GetSnapshot(),
        });
    }
    catch (Exception ex)
    {
        return Results.Conflict(new { ok = false, error = ex.Message, job = jobService.GetSnapshot() });
    }
});

app.MapPost("/api/jobs/cancel", async (FilmJobService jobService) =>
{
    await jobService.CancelAsync();
    return Results.Ok(new { ok = true, job = jobService.GetSnapshot() });
});

app.MapGet("/api/stage2-status", (ProjectStore store) =>
{
    var id = store.ActiveProjectId;
    if (string.IsNullOrEmpty(id))
        return Results.Ok(new { ok = true, stage2_ready = false });
    var bp = store.FindBlueprintPath(id);
    var ready = bp is not null && File.Exists(bp);
    var scenes = 0;
    var clips = 0;
    if (ready)
    {
        try
        {
            using var doc = store.LoadBlueprint(id);
            if (doc is not null &&
                doc.RootElement.TryGetProperty("scenes", out var sc) &&
                sc.ValueKind == JsonValueKind.Array)
            {
                scenes = sc.GetArrayLength();
                foreach (var s in sc.EnumerateArray())
                {
                    if (s.TryGetProperty("veo_clips", out var vc) &&
                        vc.ValueKind == JsonValueKind.Array)
                        clips += vc.GetArrayLength();
                }
            }
        }
        catch { /* ignore */ }
    }
    return Results.Ok(new
    {
        ok = true,
        stage2_ready = ready && clips > 0,
        stage2_scenes = scenes,
        stage2_clips = clips,
        blueprint_path = bp,
        project_id = id,
    });
});

app.Run();
