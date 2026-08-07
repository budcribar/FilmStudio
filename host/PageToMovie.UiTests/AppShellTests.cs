using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace PageToMovie.UiTests;

[Collection("ui")]
public class AppShellTests
{
    private readonly AppFixture _fx;
    public AppShellTests(AppFixture fx) => _fx = fx;

    [Fact]
    public async Task Home_renders_tagline_projects_and_steps()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await Ui.GotoAppAsync(page, _fx.BaseUrl, "/");
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Drop a book" })).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByText("Cast & voice").First).ToBeVisibleAsync();
        }
        finally { await ctx.CloseAsync(); }
    }

    [Fact]
    public async Task Left_nav_navigates_between_core_pages()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await Ui.GotoAppAsync(page, _fx.BaseUrl, "/");

            await page.Locator("a[href='/scenes']").First.ClickAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/scenes"));
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Scenes & clips" })).ToBeVisibleAsync();

            await page.Locator("a[href='/characters']").First.ClickAsync();
            await Assertions.Expect(page).ToHaveURLAsync(new Regex("/characters"));
        }
        finally { await ctx.CloseAsync(); }
    }

    [Fact]
    public async Task Home_has_no_unexpected_console_errors()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            var errs = Ui.CollectConsoleErrors(page);
            await Ui.GotoAppAsync(page, _fx.BaseUrl, "/");
            await page.WaitForTimeoutAsync(1500);
            Assert.True(errs.Unexpected.Count == 0, "Unexpected console errors:\n" + string.Join("\n", errs.Unexpected));
        }
        finally { await ctx.CloseAsync(); }
    }
}
