using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace PageToMovie.Tests;

public class SceneMusicScoringTests
{
    private class FakeChatClient : IChatClient
    {
        public bool IsConfigured => true;
        public Task<string> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            string model,
            double temperature = 0.2,
            CancellationToken ct = default,
            string? mode = null, string? reasoningEffort = null)
        {
            return Task.FromResult("Dramatic orchestral music with low cellos at 90 BPM");
        }
    }

    [Fact]
    public async Task GetOrComposeMusicPromptAsync_UsesPreplannedBlueprintPrompt_WhenPresent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SceneMusicPrompt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var blueprintPath = Path.Combine(tempDir, "blueprint.clips.grok.json");
            var json = """
            {
              "scenes": [
                {
                  "scene_number": 1,
                  "music_prompt": "Fragile solo piano leitmotif in a minor key"
                }
              ]
            }
            """;
            await File.WriteAllTextAsync(blueprintPath, json);

            var service = new SceneMusicScoringService(new FakeChatClient(), NullLogger<SceneMusicScoringService>.Instance);
            var prompt = await service.GetOrComposeMusicPromptAsync(tempDir, 1, "INT. CASTLE - NIGHT", 10, "grok-4.5");

            Assert.Equal("Fragile solo piano leitmotif in a minor key", prompt);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GetOrComposeMusicPromptAsync_FallsBackToAiComposedPrompt_WhenNoBlueprintPrompt()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SceneMusicPrompt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            var service = new SceneMusicScoringService(new FakeChatClient(), NullLogger<SceneMusicScoringService>.Instance);
            var prompt = await service.GetOrComposeMusicPromptAsync(tempDir, 1, "INT. CASTLE - NIGHT", 10, "grok-4.5");

            Assert.Equal("Dramatic orchestral music with low cellos at 90 BPM", prompt);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetPreplannedMusicPrompt_ReturnsNull_WhenBlueprintMissing()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SceneMusicPrompt_" + Guid.NewGuid().ToString("N"));
        Assert.Null(SceneMusicScoringService.GetPreplannedMusicPrompt(tempDir, 1));
    }
}
