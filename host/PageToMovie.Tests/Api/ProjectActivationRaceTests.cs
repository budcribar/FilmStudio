using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace PageToMovie.Tests.Api;

/// <summary>
/// Issue 10: background job execution must not mutate the process-global active-project
/// pointer. ActivateAsync (and the workspace.json it writes) is a UI-only preference set
/// by POST /api/projects/{id}/activate — a job running against project B (explicit
/// projectId) must not silently flip the globally active project away from whatever the
/// UI last selected (project A), which would otherwise race any other concurrent job or
/// UI read that falls back to the active-project default.
/// </summary>
public sealed class ProjectActivationRaceTests : IClassFixture<PageToMovieApiFactory>, IAsyncLifetime
{
    private readonly PageToMovieApiFactory _factory;
    private readonly HttpClient _client;
    private string _projectA = "";
    private string _projectB = "";

    public ProjectActivationRaceTests(PageToMovieApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateUserClient();
    }

    public async Task InitializeAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var slugA = "RaceA_" + suffix;
        var slugB = "RaceB_" + suffix;

        // CreateProjectAsync activates on create, so B ends up active first — then we
        // explicitly re-activate A so it is the UI's current selection going into the test.
        var createA = await _client.PostAsJsonAsync("/api/projects", new { name = slugA, title = "A" });
        Assert.True(createA.IsSuccessStatusCode, await createA.Content.ReadAsStringAsync());
        var jsonA = await createA.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        _projectA = jsonA.GetProperty("active").GetProperty("id").GetString()!;

        var createB = await _client.PostAsJsonAsync("/api/projects", new { name = slugB, title = "B" });
        Assert.True(createB.IsSuccessStatusCode, await createB.Content.ReadAsStringAsync());
        var jsonB = await createB.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        _projectB = jsonB.GetProperty("active").GetProperty("id").GetString()!;

        var act = await _client.PostAsync($"/api/projects/{Uri.EscapeDataString(_projectA)}/activate", null);
        Assert.True(act.IsSuccessStatusCode, await act.Content.ReadAsStringAsync());
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string?> ActiveProjectAsync()
    {
        var health = await _client.GetAsync("/health");
        var json = await health.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        return json.GetProperty("activeProject").GetString();
    }

    [Fact]
    public async Task Background_job_for_project_B_does_not_flip_active_project_from_A()
    {
        Assert.Equal(_projectA, await ActiveProjectAsync());

        var start = await _client.PostAsJsonAsync("/api/jobs/character-variants",
            new { projectId = _projectB, charKey = "Character_Race" });
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var startJson = await start.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var jobId = startJson.GetProperty("job").GetProperty("jobId").GetString()!;

        // Poll to completion (fakes run fast) instead of a fixed sleep.
        for (var i = 0; i < 50; i++)
        {
            var job = await _client.GetAsync($"/api/jobs/{jobId}");
            var jobJson = await job.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var status = jobJson.GetProperty("job").GetProperty("status").GetString();
            if (!string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "running", StringComparison.OrdinalIgnoreCase))
                break;
            await Task.Delay(50);
        }

        // The job ran against project B, but the UI's active-project selection (A) must
        // be untouched — job execution is not allowed to call ActivateAsync.
        Assert.Equal(_projectA, await ActiveProjectAsync());
    }
}
