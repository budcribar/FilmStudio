using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using PageToMovie.Core.Options;
using PageToMovie.Engine;

namespace PageToMovie.UiTests;

/// <summary>
/// Domain-reuse: instead of re-deriving expected values in the test, drive the *real* Engine
/// (CostReportService, over the same workspace the host uses) to decide what the UI should show,
/// then assert the browser matches. This is the payoff of a C# UI suite.
/// </summary>
[Collection("ui")]
public class DomainReuseTests
{
    private readonly AppFixture _fx;
    public DomainReuseTests(AppFixture fx) => _fx = fx;

    [Fact]
    public async Task Cost_page_estimate_presence_matches_CostReportService_for_active_project()
    {
        var repo = AppFixture.FindRepoRoot();
        var activeId = ReadActiveProjectId(repo);
        Assert.False(string.IsNullOrWhiteSpace(activeId));

        var costs = new CostReportService(new ProjectStore(Options.Create(
            new PageToMovieOptions { WorkspaceRoot = repo, EnableReadCaches = false })));

        // The real Engine decides whether an estimate is even possible for this project (fail-fast
        // with no model selected → no estimate). The UI must reflect the same conclusion.
        double? engineDraftTotal = null;
        try
        {
            var report = await costs.GetReportAsync(activeId!, "480p", "720p");
            engineDraftTotal = report.Summary.FullFilmAllDraftUsd;
        }
        catch (InvalidOperationException) { /* no model set → no estimate */ }

        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await Ui.GotoAppAsync(page, _fx.BaseUrl, "/cost");
            var estimate = page.GetByTestId("cost-estimate");
            if (engineDraftTotal is null)
            {
                // Engine can't price it → the UI must not show a cost figure.
                await Assertions.Expect(estimate).ToHaveCountAsync(0);
            }
            else
            {
                // Engine can price it → the UI shows a number, and it's a sane positive value.
                await Assertions.Expect(estimate).ToBeVisibleAsync();
                var text = await estimate.InnerTextAsync();
                var shown = double.Parse(text.Replace("$", "").Replace(",", "").Trim());
                Assert.True(shown > 0, $"expected a positive estimate, got '{text}'");
            }
        }
        finally { await ctx.CloseAsync(); }
    }

    private static string? ReadActiveProjectId(string repo)
    {
        var wsPath = Path.Combine(repo, "projects", "workspace.json");
        if (!File.Exists(wsPath)) return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(wsPath));
        foreach (var name in new[] { "ActiveProject", "activeProject" })
            if (doc.RootElement.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
                return el.GetString();
        return null;
    }
}
