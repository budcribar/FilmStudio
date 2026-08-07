using Microsoft.Playwright;

namespace PageToMovie.UiTests;

/// <summary>
/// Drives varied screenplay fixtures through the fresh-project pipeline and asserts the Characters
/// display renders correctly for each — the bug-prone "cast gate" area (speaking-part counts, animal
/// species, ensemble/group roles) we want locked down before the Blazor component refactor.
/// </summary>
[Collection("ui-pipeline")]
public class CastDisplayVariationTests
{
    private readonly PipelineFixture _fx;
    public CastDisplayVariationTests(PipelineFixture fx) => _fx = fx;

    [Fact]
    public async Task Solo_screenplay_shows_single_cast_member()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await PipelineFlow.RunToCharactersAsync(page, _fx.BaseUrl, "Solo_" + Rand(), "solo.fountain");
            await Assertions.Expect(page.GetByTestId("char-list-item")).ToHaveCountAsync(1, new() { Timeout = 90_000 });
            await Assertions.Expect(page.GetByText("Narrator", new() { Exact = false }).First).ToBeVisibleAsync(Slow);
        }
        finally { await ctx.CloseAsync(); }
    }

    [Fact]
    public async Task Large_screenplay_shows_all_speaking_parts()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await PipelineFlow.RunToCharactersAsync(page, _fx.BaseUrl, "Large_" + Rand(), "large_cast.fountain");
            // Eight distinct speakers — the roster must list every one, none collapsed or dropped.
            await Assertions.Expect(page.GetByTestId("char-list-item")).ToHaveCountAsync(8, new() { Timeout = 90_000 });
            foreach (var name in new[] { "Alice", "Boris", "Cora", "Dmitri", "Elena", "Felix", "Greta", "Hakim" })
                await Assertions.Expect(page.GetByText(name, new() { Exact = false }).First).ToBeVisibleAsync(Slow);
        }
        finally { await ctx.CloseAsync(); }
    }

    [Fact]
    public async Task Animal_screenplay_shows_talking_and_silent_animals()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await PipelineFlow.RunToCharactersAsync(page, _fx.BaseUrl, "Jungle_" + Rand(), "jungle_animals.fountain");
            // Wolf + Owl (talking animals), Ranger (human), Lamb (silent animal) — 4 individual roles.
            await Assertions.Expect(page.GetByTestId("char-list-item")).ToHaveCountAsync(4, new() { Timeout = 90_000 });
            foreach (var name in new[] { "Wolf", "Owl", "Ranger", "Lamb" })
                await Assertions.Expect(page.GetByText(name, new() { Exact = false }).First).ToBeVisibleAsync(Slow);

            // The silent animal (Lamb) must not demand a voice — selecting it shows the optional hint,
            // not a required-voice section.
            await page.EvaluateAsync(
                "() => [...document.querySelectorAll('[data-testid=char-list-item]')].find(b => /lamb/i.test(b.textContent))?.click()");
            await Assertions.Expect(page.GetByTestId("char-voice-hidden-hint")).ToBeVisibleAsync(Slow);
            await Assertions.Expect(page.GetByTestId("char-voice-section")).ToHaveCountAsync(0);
        }
        finally { await ctx.CloseAsync(); }
    }

    [Fact]
    public async Task All_ensemble_screenplay_acknowledges_cast_not_no_cast_yet()
    {
        // Regression for the "all-group cast" gate bug: when every speaking role is an ensemble
        // (crowd / children / villagers), the operator roster is empty by design — but the page used
        // to show "No cast yet. Approve the screenplay, then build the cast", implying extraction
        // failed. It must instead acknowledge the extracted ensemble cast.
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await PipelineFlow.RunToCharactersAsync(page, _fx.BaseUrl, "Crowd_" + Rand(), "crowd_only.fountain");

            // Cast WAS extracted, so the "no cast yet" empty-state must NOT show.
            await Assertions.Expect(page.GetByTestId("characters-empty")).ToHaveCountAsync(0);
            // Instead, the all-ensembles acknowledgement is shown, naming the groups.
            var ack = page.GetByTestId("characters-all-ensembles");
            await Assertions.Expect(ack).ToBeVisibleAsync(Slow);
            await Assertions.Expect(ack).ToContainTextAsync("Crowd");
            await Assertions.Expect(ack).ToContainTextAsync("Children");
        }
        finally { await ctx.CloseAsync(); }
    }

    private static readonly LocatorAssertionsToBeVisibleOptions Slow = new() { Timeout = 90_000 };
    private static string Rand() => Guid.NewGuid().ToString("N")[..6];
}
