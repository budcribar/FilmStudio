using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Microsoft.Extensions.Options;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// Regression guard for the Scenes page "Add credits" button, which hides when any scene reports
/// <see cref="SceneSummary.IsCredits"/> = true. The credits scene must be detected however it was
/// actually stored across blueprint generations — including the older auto-inserted shape (a CREDITS
/// <c>setting</c>, no scene-level <c>is_credits</c> flag) and a shape whose flag survives only on the
/// title-card clip. Shapes below mirror real project blueprints (e.g. TellTaleHeartV7's sc18_end_credits).
/// </summary>
public class CreditsSceneDetectionTests : IDisposable
{
    private readonly string _root;
    private readonly ProjectStore _store;
    private const string ProjectId = "CreditsDetect";

    public CreditsSceneDetectionTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fs-credits-detect-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "projects", ProjectId));
        var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = _root });
        _store = new ProjectStore(opts);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    private void WriteBlueprint(string scenesJson) =>
        File.WriteAllText(
            Path.Combine(_root, "projects", ProjectId, "blueprint.clips.grok.json"),
            "{ \"movie_title\": \"The Story\", \"scenes\": [" + scenesJson + "] }");

    // Ordinary story scene (control) — must NOT be flagged as credits.
    private const string OrdinaryScene = """
        {
          "scene_number": 1,
          "setting": "INT. BARE ROOM - NIGHT",
          "veo_clips": [ { "clip_number": 1, "visual_prompt": "A room." } ]
        }
        """;

    // Exact shape of TellTaleHeartV7's real auto-inserted credits scene: a CREDITS *setting*
    // (not scene_heading), NO scene-level is_credits flag, and a clip that also lacks the flag.
    private const string CreditsSceneSettingBased = """
        {
          "scene_number": 2,
          "setting": "END CREDITS - THE TELL-TALE HEART",
          "primary_location_id": "Loc_Credits",
          "scene_filename": "sc02_end_credits",
          "veo_clips": [
            { "clip_number": 1, "stage1_beat_id": "b_credits", "visual_prompt": "Cinematic end-credits title card." }
          ]
        }
        """;

    // Degenerate shape whose only surviving credits marker is on the title-card clip
    // (both the Stage 2 auto-insert and the manual "Add credits" write clip-level is_credits).
    private const string CreditsSceneClipFlagOnly = """
        {
          "scene_number": 3,
          "setting": "FADE OUT",
          "veo_clips": [
            { "clip_number": 1, "is_credits": true, "visual_prompt": "Elegant end-credits title card." }
          ]
        }
        """;

    [Fact]
    public async Task IsCredits_true_for_setting_based_credits_scene_with_no_flag()
    {
        WriteBlueprint(OrdinaryScene + "," + CreditsSceneSettingBased);

        var scenes = await _store.ListScenesAsync(ProjectId, probeDurations: false);

        Assert.False(scenes.Single(s => s.SceneNumber == 1).IsCredits);
        Assert.True(scenes.Single(s => s.SceneNumber == 2).IsCredits);
        Assert.Contains(scenes, s => s.IsCredits); // the guard the "Add credits" button reads
    }

    [Fact]
    public async Task IsCredits_true_when_flag_survives_only_on_the_clip()
    {
        WriteBlueprint(OrdinaryScene + "," + CreditsSceneClipFlagOnly);

        var scenes = await _store.ListScenesAsync(ProjectId, probeDurations: false);

        Assert.False(scenes.Single(s => s.SceneNumber == 1).IsCredits);
        Assert.True(scenes.Single(s => s.SceneNumber == 3).IsCredits);
    }
}
