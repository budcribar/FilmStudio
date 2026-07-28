using System.IO;
using System.Threading.Tasks;
using Xunit;
using PageToMovie.Core.Models;
using PageToMovie.Engine;

namespace PageToMovie.Tests;

public class ClipDialogueVerificationTests
{
    [Fact]
    public void LoadVerification_returns_null_when_file_does_not_exist()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var projDir = Path.Combine(tempDir, "projects", "test_proj");
        Directory.CreateDirectory(projDir);
        try
        {
            var opts = Microsoft.Extensions.Options.Options.Create(new PageToMovie.Core.Options.PageToMovieOptions { WorkspaceRoot = tempDir });
            var store = new ProjectStore(opts);
            var service = new ClipDialogueVerificationService(store, new MockVisionClient(), null);
            var result = service.LoadVerification("test_proj", 1, 1);
            Assert.Null(result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void SaveVerification_persists_json_report()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var projDir = Path.Combine(tempDir, "projects", "test_proj");
        Directory.CreateDirectory(projDir);
        try
        {
            var opts = Microsoft.Extensions.Options.Options.Create(new PageToMovie.Core.Options.PageToMovieOptions { WorkspaceRoot = tempDir });
            var store = new ProjectStore(opts);
            var service = new ClipDialogueVerificationService(store, new MockVisionClient(), null);

            var vo = new ClipDialogueVerificationResult
            {
                SceneNumber = 1,
                ClipNumber = 1,
                ExpectedSpeaker = "Buster",
                ExpectedDialogue = "Hello world!",
                DetectedSpeaker = "Buster",
                TranscribedDialogue = "Hello world!",
                DialogueAccuracyScore = 1.0,
                SpeakerMatch = true,
                Status = "verified",
            };

            service.SaveVerification("test_proj", vo);

            var loaded = service.LoadVerification("test_proj", 1, 1);
            Assert.NotNull(loaded);
            Assert.Equal("Buster", loaded!.ExpectedSpeaker);
            Assert.Equal("verified", loaded.Status);
            Assert.Equal(1.0, loaded.DialogueAccuracyScore);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    private class MockVisionClient : PageToMovie.Engine.Abstractions.IVisionClient
    {
        public bool IsConfigured => true;

        public Task<string> TranscribePageAsync(string imagePath, int page, string model = "grok-4.5", System.Threading.CancellationToken ct = default) =>
            Task.FromResult("");

        public Task<CharacterPageClassification> ClassifyCharactersOnImageAsync(string imagePath, int page, System.Collections.Generic.IReadOnlyList<CharacterClassifyHint> cast, string model = "grok-4.5", System.Threading.CancellationToken ct = default) =>
            Task.FromResult(new CharacterPageClassification());

        public Task<string> CompleteWithImagesAsync(string prompt, System.Collections.Generic.IReadOnlyList<string> imagePaths, string model = "grok-4.5", string detail = "low", System.Threading.CancellationToken ct = default) =>
            Task.FromResult(@"{ ""detectedSpeaker"": ""Buster"", ""transcribedDialogue"": ""Hello world!"", ""dialogueAccuracyScore"": 1.0, ""speakerMatch"": true, ""status"": ""verified"" }");
    }
}
