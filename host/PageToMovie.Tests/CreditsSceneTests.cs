using System.Text.Json;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Microsoft.Extensions.Options;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// The editable end-credits scene (Scenes → "Add credits") builds a card prompt that includes the
/// config-wide creator site and, when the screenplay declares one, a "Based on the story by …" credit.
/// Kept short so the video-generated card stays legible.
/// </summary>
public class CreditsSceneTests : IDisposable
{
    private readonly string _root;
    private readonly ProjectStore _store;
    private const string ProjectId = "CreditsScene";

    public CreditsSceneTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fs-credits-scene-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "projects", ProjectId));
        var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = _root });
        _store = new ProjectStore(opts);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    private string ProjectDir => Path.Combine(_root, "projects", ProjectId);

    private void WriteBlueprint(string movieTitle) =>
        File.WriteAllText(Path.Combine(ProjectDir, "blueprint.clips.grok.json"),
            $"{{ \"movie_title\": {JsonSerializer.Serialize(movieTitle)}, \"scenes\": [] }}");

    private void WriteFountainAuthor(string author)
    {
        var srcDir = Path.Combine(ProjectDir, "source");
        Directory.CreateDirectory(srcDir);
        File.WriteAllText(Path.Combine(srcDir, "screenplay.fountain"),
            $"Title: The Story\nAuthor: {author}\n\nFADE IN:\n");
    }

    private string CreditsVisualPrompt()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDir, "blueprint.clips.grok.json")));
        var credits = doc.RootElement.GetProperty("scenes").EnumerateArray().Last();
        return credits.GetProperty("veo_clips")[0].GetProperty("visual_prompt").GetString() ?? "";
    }

    [Fact]
    public void Credits_card_includes_config_site_and_title()
    {
        WriteBlueprint("Mary Had a Little Lamb");
        _store.AddCreditsScene(ProjectId);

        var prompt = CreditsVisualPrompt();
        Assert.Contains("pagetomovie.com", prompt);
        Assert.Contains("Mary Had a Little Lamb", prompt);
        Assert.Contains("Made with PageToMovie", prompt);
    }

    [Fact]
    public void Credits_card_credits_a_named_author()
    {
        WriteBlueprint("The Story");
        WriteFountainAuthor("Sarah Josepha Hale");
        _store.AddCreditsScene(ProjectId);

        Assert.Contains("Based on the story by Sarah Josepha Hale", CreditsVisualPrompt());
    }

    [Fact]
    public void Credits_card_omits_public_domain_placeholder_author()
    {
        WriteBlueprint("The Story");
        WriteFountainAuthor("Public Domain / Source Material");
        _store.AddCreditsScene(ProjectId);

        var prompt = CreditsVisualPrompt();
        Assert.DoesNotContain("Based on the story by", prompt);
        Assert.Contains("pagetomovie.com", prompt); // creator site still shown
    }

    [Fact]
    public void BuildCreditsContent_carries_title_author_and_site()
    {
        WriteBlueprint("Mary Had a Little Lamb");

        var c = _store.BuildCreditsContent(ProjectId);

        Assert.Equal("Mary Had a Little Lamb", c.Title);
        Assert.Equal("", c.Author); // no fountain author line
        Assert.Equal("PageToMovie", c.SoftwareName);
        Assert.Equal("pagetomovie.com", c.SiteUrl);
    }

    [Fact]
    public void BuildCreditsContent_is_the_single_source_for_the_prompt()
    {
        // The client renders these exact strings; the prompt-based path must use the same content so a
        // deterministic card and any prompt can never drift.
        WriteBlueprint("Mary Had a Little Lamb");
        WriteFountainAuthor("Sarah Josepha Hale");

        var c = _store.BuildCreditsContent(ProjectId);
        var prompt = _store.BuildCreditsVisualPrompt(ProjectId);

        Assert.Contains(c.Title, prompt);
        Assert.Contains(c.Author, prompt);
        Assert.Contains(c.SoftwareName, prompt);
        Assert.Contains(c.SiteUrl, prompt);
    }

    [Fact]
    public void AddScene_without_credits_appends_at_end()
    {
        WriteBlueprint("The Story");

        var first = _store.AddScene(ProjectId);
        var second = _store.AddScene(ProjectId);

        Assert.Equal(1, first);
        Assert.Equal(2, second);
    }

    [Fact]
    public void AddScene_inserts_before_existing_credits_scene_instead_of_after()
    {
        WriteBlueprint("The Story");
        var creditsScene = _store.AddCreditsScene(ProjectId);

        var newScene = _store.AddScene(ProjectId);

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(ProjectDir, "blueprint.clips.grok.json")));
        var scenes = doc.RootElement.GetProperty("scenes").EnumerateArray().ToList();

        // The new blank scene took the credits scene's old number, and credits got bumped up by one —
        // credits must remain the last scene, not have a stray blank scene appended after it.
        Assert.Equal(creditsScene, newScene);
        Assert.Equal(2, scenes.Count);
        Assert.Equal(newScene, scenes[0].GetProperty("scene_number").GetInt32());
        Assert.False(scenes[0].TryGetProperty("is_credits", out _) && scenes[0].GetProperty("is_credits").GetBoolean());
        Assert.Equal(creditsScene + 1, scenes[1].GetProperty("scene_number").GetInt32());
        Assert.True(scenes[1].GetProperty("is_credits").GetBoolean());
    }

    [Fact]
    public void Site_url_is_config_wide()
    {
        var opts = Options.Create(new PageToMovieOptions
        {
            WorkspaceRoot = _root,
            Credits = new CreditsOptions { SiteUrl = "example.studio" },
        });
        var store = new ProjectStore(opts);
        WriteBlueprint("The Story");
        store.AddCreditsScene(ProjectId);

        Assert.Contains("example.studio", CreditsVisualPrompt());
    }
}
