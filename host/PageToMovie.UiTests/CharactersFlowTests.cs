using Microsoft.Playwright;

namespace PageToMovie.UiTests;

/// <summary>
/// A-3: drives the Characters operator flow through the UI (select a character → generate looks →
/// see the pick grid; voice section for speakers). These are the operator components that will be
/// extracted from Characters.razor (~3k lines), so behaviour must be pinned before the refactor.
/// </summary>
[Collection("ui-pipeline")]
public class CharactersFlowTests
{
    private readonly PipelineFixture _fx;
    public CharactersFlowTests(PipelineFixture fx) => _fx = fx;

    [Fact]
    public async Task Generate_looks_from_description_shows_the_pick_grid()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await PipelineFlow.RunToCharactersAsync(page, _fx.BaseUrl, "CharUI_" + Guid.NewGuid().ToString("N")[..6], "tell_tale_heart.fountain");

            // Select the first character; its detail panel opens on the "choose a look route" state.
            await Assertions.Expect(page.GetByTestId("char-list-item").First).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await page.GetByTestId("char-list-item").First.ClickAsync();

            // Choose the "generate from description" route → description form appears.
            await page.GetByTestId("char-route-generate").ClickAsync(new() { Timeout = 30_000 });
            var desc = page.GetByPlaceholder("How they look");
            await desc.WaitForAsync(new() { Timeout = 30_000 });
            if (string.IsNullOrWhiteSpace(await desc.InputValueAsync()))
                await desc.FillAsync("A pale, thin adult with dark hair and a dark wool coat, photoreal.");

            // Generate looks (fake image) → the variant pick grid appears with options.
            await page.GetByTestId("char-generate-looks").ClickAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("char-pick-grid")).ToBeVisibleAsync(new() { Timeout = 90_000 });
            await Assertions.Expect(page.GetByTestId("char-pick-card").First).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        finally { await ctx.CloseAsync(); }
    }

    [Fact]
    public async Task Speaking_character_shows_a_voice_section()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await PipelineFlow.RunToCharactersAsync(page, _fx.BaseUrl, "CharVoice_" + Guid.NewGuid().ToString("N")[..6], "tell_tale_heart.fountain");

            // Narrator speaks, so its detail panel offers a voice section (not the silent/animal hint).
            await Assertions.Expect(page.GetByTestId("char-list-item").First).ToBeVisibleAsync(new() { Timeout = 60_000 });
            await page.EvaluateAsync(
                "() => [...document.querySelectorAll('[data-testid=char-list-item]')].find(b => /narrator/i.test(b.textContent))?.click()");
            await Assertions.Expect(page.GetByTestId("char-voice-section")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        finally { await ctx.CloseAsync(); }
    }
}
