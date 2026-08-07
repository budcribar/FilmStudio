using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace PageToMovie.UiTests;

/// <summary>
/// True end-to-end functional test of the user journey on a FRESH project (isolated temp workspace,
/// fully-faked pipeline with the fake test-vendor catalog): create → pick fake models → import
/// screenplay → sign off → cast displayed. Proves the whole path works starting from nothing.
/// </summary>
[Collection("ui-pipeline")]
public class PipelineE2ETests
{
    private readonly PipelineFixture _fx;
    public PipelineE2ETests(PipelineFixture fx) => _fx = fx;

    [Fact]
    public async Task Fresh_project_configure_import_and_cast_display()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await PipelineFlow.RunToCharactersAsync(page, _fx.BaseUrl,
                "E2E_" + Guid.NewGuid().ToString("N")[..6], "tell_tale_heart.fountain");

            // The Tell-Tale Heart adaptation has 3 speaking humans: Narrator, Old Man, Officer.
            var roster = page.GetByTestId("char-list-item");
            await Assertions.Expect(roster).ToHaveCountAsync(3, new() { Timeout = 90_000 });
            foreach (var name in new[] { "Narrator", "Old Man", "Officer" })
                await Assertions.Expect(page.GetByText(name, new() { Exact = false }).First)
                    .ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        finally { await ctx.CloseAsync(); }
    }
}
