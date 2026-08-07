using Microsoft.Playwright;

namespace PageToMovie.UiTests;

/// <summary>
/// Extends the fresh-project pipeline through Shot plan (Stage 2) to the Scenes page — the biggest
/// refactor target (Scenes.razor is ~5.2k lines). Proves the shot plan builds from an extracted cast
/// and the Scenes list renders, so the upcoming component extraction can be verified behaviour-preserving.
/// </summary>
[Collection("ui-pipeline")]
public class ScenesPipelineTests
{
    private readonly PipelineFixture _fx;
    public ScenesPipelineTests(PipelineFixture fx) => _fx = fx;

    [Fact]
    public async Task Fresh_project_builds_shot_plan_and_renders_scenes()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await PipelineFlow.RunToScenesAsync(page, _fx.BaseUrl, "Scenes_" + Guid.NewGuid().ToString("N")[..6], "tell_tale_heart.fountain");

            // Shot plan produced scenes — the authoritative count marker must show a non-empty plan,
            // and the "No scenes in the shot plan yet" empty state must be gone.
            var status = page.GetByTestId("scenes-status");
            await status.WaitForAsync(new() { Timeout = 60_000 });
            var sceneCount = int.Parse(await status.GetAttributeAsync("data-scene-count") ?? "0");
            Assert.True(sceneCount >= 1, $"expected >=1 scene in the shot plan, got {sceneCount}");

            // The scene table renders rows (virtualized — assert at least the first is present) and the
            // batch-generate control is on the page.
            await Assertions.Expect(page.GetByTestId("scene-row").First).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("scenes-generate-batch")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        finally { await ctx.CloseAsync(); }
    }

    // Varied casts through to a built shot plan. With maxSpeakersPerClip=1 (Grok/fake-video), every
    // speaker gets its own single-speaker clip, so the plan must contain at least one clip per
    // distinct speaker — this catches ensemble/animal/large-cast shot-plan regressions and proves the
    // single-speaker policy holds end-to-end (not just in the unit test).
    [Theory]
    [InlineData("solo.fountain", 1)]           // 1 speaker
    [InlineData("crowd_only.fountain", 3)]     // 3 ensemble speakers (Crowd/Children/Villagers)
    [InlineData("jungle_animals.fountain", 3)] // 3 talking (Wolf/Owl/Ranger) + 1 silent animal
    [InlineData("large_cast.fountain", 8)]     // 8 distinct speakers
    public async Task Varied_cast_builds_shot_plan_with_one_clip_per_speaker(string fixture, int minSpeakers)
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await PipelineFlow.RunToScenesAsync(page, _fx.BaseUrl, "SV_" + Guid.NewGuid().ToString("N")[..6], fixture);

            var status = page.GetByTestId("scenes-status");
            await status.WaitForAsync(new() { Timeout = 60_000 });
            var scenes = int.Parse(await status.GetAttributeAsync("data-scene-count") ?? "0");
            var clips = int.Parse(await status.GetAttributeAsync("data-clip-count") ?? "0");

            Assert.True(scenes >= 1, $"{fixture}: expected >=1 scene, got {scenes}");
            // One clip per speaker minimum (single-speaker policy — no two-hander merging).
            Assert.True(clips >= minSpeakers, $"{fixture}: expected >={minSpeakers} clips (one per speaker), got {clips}");
            await Assertions.Expect(page.GetByTestId("scene-row").First).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        finally { await ctx.CloseAsync(); }
    }

    [Fact]
    public async Task Opening_a_scene_shows_its_clip_select_bar()
    {
        var (ctx, page) = await _fx.NewPageAsync();
        try
        {
            await PipelineFlow.RunToScenesAsync(page, _fx.BaseUrl, "SceneOpen_" + Guid.NewGuid().ToString("N")[..6], "tell_tale_heart.fountain");

            await Assertions.Expect(page.GetByTestId("scene-row").First).ToBeVisibleAsync(new() { Timeout = 60_000 });
            // Open the first scene (click its S## badge) → the per-scene clip select bar appears.
            await page.GetByTestId("scene-row").First.Locator("span.badge").First.ClickAsync();
            await Assertions.Expect(page.GetByTestId("clip-select-bar")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
        finally { await ctx.CloseAsync(); }
    }
}
