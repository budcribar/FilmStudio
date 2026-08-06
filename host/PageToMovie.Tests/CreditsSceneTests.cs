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
