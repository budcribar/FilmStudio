using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace PageToMovie.UiTests;

/// <summary>
/// Product rule #5: user-facing workflow pages must not leak provider names or internal
/// identifiers. Checked in "view as user" mode. Allowlist: general/outcome language is fine and
/// "AI" is explicitly permitted — this only flags the clear violations below. (Configuration and
/// Cost are intentionally excluded: provider/model names are allowed there.)
/// </summary>
[Collection("ui")]
public class UserModeJargonTests
{
    private static readonly (string Label, Regex Rx)[] Banned =
    {
        ("Grok", new Regex(@"\bGrok\b", RegexOptions.IgnoreCase)),
        ("Gemini", new Regex(@"\bGemini\b", RegexOptions.IgnoreCase)),
        ("xAI", new Regex(@"\bxAI\b")),
        ("Veo", new Regex(@"\bVeo\b")),
        ("Anthropic", new Regex(@"\bAnthropic\b", RegexOptions.IgnoreCase)),
        ("Suno", new Regex(@"\bSuno\b", RegexOptions.IgnoreCase)),
        ("ElevenLabs", new Regex(@"ElevenLabs", RegexOptions.IgnoreCase)),
        ("blueprint.clips", new Regex(@"blueprint\.clips", RegexOptions.IgnoreCase)),
        ("scenes.json", new Regex(@"scenes\.json", RegexOptions.IgnoreCase)),
    };

    private readonly AppFixture _fx;
    public UserModeJargonTests(AppFixture fx) => _fx = fx;

    [Theory]
    [InlineData("/")]
    [InlineData("/adaptation")]
    [InlineData("/characters")]
    [InlineData("/scenes")]
    [InlineData("/review")]
    public async Task Workflow_page_has_no_provider_jargon_in_user_mode(string route)
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await Ui.GotoAppAsync(page, _fx.BaseUrl, "/");
            await Ui.EnterUserModeAsync(page);

            if (route != "/")
            {
                await page.Locator($"a[href='{route}']").First.ClickAsync();
                await Assertions.Expect(page).ToHaveURLAsync(new Regex(Regex.Escape(route)));
            }
            await page.WaitForTimeoutAsync(1000);

            var body = await page.EvalOnSelectorAsync<string>("body", "el => el.innerText");
            var hits = Banned.Where(b => b.Rx.IsMatch(body)).Select(b => b.Label).ToList();
            Assert.True(hits.Count == 0, $"{route} leaks provider jargon in user mode: {string.Join(", ", hits)}");
        }
        finally { await ctx.CloseAsync(); }
    }
}
