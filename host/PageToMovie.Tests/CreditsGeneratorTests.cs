using PageToMovie.Core.Options;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace PageToMovie.Tests;

public class CreditsGeneratorTests
{
    private static CreditsGeneratorService MakeService(string workspace)
    {
        var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = workspace });
        var store = new ProjectStore(opts);
        var video = new StubVideoClient();
        var proxy = new MediaProxyTicketStore();
        return new CreditsGeneratorService(
            store, opts, video, proxy, NullLogger<CreditsGeneratorService>.Instance);
    }

    [Fact]
    public void FormatCreditsText_includes_story_software_nick_repo_and_fair_use()
    {
        var service = MakeService(Path.GetTempPath());
        var formatted = service.FormatCreditsText("The Tell-Tale Heart", "Edgar Allan Poe");

        Assert.Contains("THE TELL-TALE HEART", formatted);
        Assert.Contains("Written by Edgar Allan Poe", formatted);
        Assert.Contains("Filmmaking Software: PageToMovie", formatted);
        Assert.Contains("Software Author: Bud Cribar", formatted);
        Assert.Contains("https://github.com/budcribar/PageToMovie", formatted);
        Assert.Contains("Fair Use", formatted);
    }

    [Fact]
    public void BuildCreditsVideoPrompt_includes_title_card_guidance()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "fs-credits-prompt-" + Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(tmp, "projects", "TestProject", "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "screenplay.fountain"),
            "Title: The Tell-Tale Heart\nAuthor: Edgar Allan Poe\n\nFADE IN:\n");

        try
        {
            var service = MakeService(tmp);
            var prompt = service.BuildCreditsVideoPrompt("TestProject");
            Assert.Contains("end-credits", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("TELL-TALE HEART", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Edgar Allan Poe", prompt);
            Assert.Contains("PageToMovie", prompt);
            Assert.DoesNotContain("ffmpeg", prompt, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { /* */ }
        }
    }

    [Fact]
    public void ExtractStoryTitleAndAuthor_parses_fountain_headers()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "fs-credits-test-" + Guid.NewGuid().ToString("N"));
        var sourceDir = Path.Combine(tmp, "projects", "TestProject", "source");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "screenplay.fountain"), """
            Title: The Tell-Tale Heart
            Author: Edgar Allan Poe
            Credit: Written by

            FADE IN:
            """);

        try
        {
            var service = MakeService(tmp);
            var (title, author) = service.ExtractStoryTitleAndAuthor("TestProject");
            Assert.Equal("The Tell-Tale Heart", title);
            Assert.Equal("Edgar Allan Poe", author);
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { /* */ }
        }
    }

    [Fact]
    public void AreAllScenesComplete_returns_true_when_all_blueprint_scenes_have_videos()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "fs-credits-scenes-" + Guid.NewGuid().ToString("N"));
        var projDir = Path.Combine(tmp, "projects", "TestProject");
        var videoDir = Path.Combine(projDir, "assets", "video");
        Directory.CreateDirectory(videoDir);

        File.WriteAllText(Path.Combine(projDir, "blueprint.clips.grok.json"), """
            {
              "scenes": [
                { "scene_number": 1 },
                { "scene_number": 2 }
              ]
            }
            """);
        File.WriteAllBytes(Path.Combine(videoDir, "scene_01.mp4"), new byte[2048]);
        File.WriteAllBytes(Path.Combine(videoDir, "scene_02.mp4"), new byte[2048]);

        try
        {
            var service = MakeService(tmp);
            Assert.True(service.AreAllScenesComplete("TestProject"));
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { /* */ }
        }
    }

    private sealed class StubVideoClient : IVideoClient
    {
        public bool IsConfigured => true;

        public Task<string> SubmitGenerationAsync(
            string prompt, int durationSeconds, string resolution, string model,
            CancellationToken ct, IReadOnlyList<string>? referenceImagePaths = null,
            string? startFrameImagePath = null, string? continueFromVideoPath = null) =>
            Task.FromResult("req-credits");

        public Task<string> PollForVideoUrlAsync(
            string requestId, Action<string>? onProgress, CancellationToken ct) =>
            Task.FromResult("https://example.com/credits.mp4");

        public Task DownloadToFileAsync(string url, string destPath, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
