using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Engine;

namespace PageToMovie.Tests;

internal static class OfflineTestModelConfig
{
    public static string Required(string capability) =>
        SupportedModelCatalog.DefaultModelIdForCapability(capability)
        ?? throw new InvalidOperationException($"The test model catalog has no enabled default for '{capability}'.");

    public static Task ApplyAsync(ProjectStore store, string projectId) =>
        store.SaveConfigAsync(projectId, JsonSerializer.SerializeToElement(new
        {
            model_name = Required("video"),
            image_model_name = Required("image"),
            planning_model_name = Required("chat"),
            chat_model_name = Required("chat"),
            vision_model_name = Required("vision"),
            quality_model_name = Required("video-review"),
            audio_model_name = Required("audio"),
            voice_model_name = Required("voice")
        }));
}
