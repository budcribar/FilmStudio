using FilmStudio.Core.Models;
using Xunit;

namespace FilmStudio.Tests;

public class SupportedModelCatalogTests
{
    [Fact]
    public void Video_default_is_grok_imagine_video()
    {
        var m = SupportedModelCatalog.ResolveOrDefault(null, ModelCapability.Video);
        Assert.Equal("grok-imagine-video", m.Id);
        Assert.Equal(ModelProviderFamily.Xai, m.Provider);
        Assert.Contains("XAI_API_KEY", m.RequiredEnvKeys);
        Assert.Equal("videos/generations", m.EndpointPath);
    }

    [Fact]
    public void Model_id_implies_provider_without_service_dropdown()
    {
        Assert.Equal("grok", SupportedModelCatalog.ProviderIdFor(
            "grok-imagine-image-quality", ModelCapability.Image));
        Assert.Equal("grok", SupportedModelCatalog.ProviderIdFor(
            "grok-4.5", ModelCapability.Chat));
    }

    [Fact]
    public void Enabled_video_models_are_nonempty()
    {
        var list = SupportedModelCatalog.ForCapability(ModelCapability.Video);
        Assert.NotEmpty(list);
        Assert.All(list, e => Assert.True(e.Enabled));
    }

    [Fact]
    public void Find_is_case_insensitive()
    {
        var m = SupportedModelCatalog.Find("Grok-Imagine-Video", ModelCapability.Video);
        Assert.NotNull(m);
        Assert.Equal("grok-imagine-video", m!.Id);
    }

    [Fact]
    public void Gemini_veo_is_selectable_as_a_video_model()
    {
        var m = SupportedModelCatalog.Find("veo-3.1", ModelCapability.Video);
        Assert.NotNull(m);
        Assert.True(m!.Enabled, "should be selectable on the Configuration page");
        Assert.Equal(ModelProviderFamily.Google, m.Provider);
        Assert.Equal("gemini", m.ProviderId);
        Assert.Contains("GEMINI_API_KEY", m.RequiredEnvKeys);
    }

    [Fact]
    public void Gemini_image_is_selectable_as_a_portrait_model()
    {
        var m = SupportedModelCatalog.Find("gemini-3-pro-image", ModelCapability.Image);
        Assert.NotNull(m);
        Assert.True(m!.Enabled);
        Assert.Equal("gemini", m.ProviderId);
    }

    [Fact]
    public void Claude_and_gemini_are_selectable_as_chat_models()
    {
        var claude = SupportedModelCatalog.Find("claude-sonnet-5", ModelCapability.Chat);
        var gemini = SupportedModelCatalog.Find("gemini-3-pro", ModelCapability.Chat);
        Assert.NotNull(claude);
        Assert.NotNull(gemini);
        Assert.True(claude!.Enabled);
        Assert.True(gemini!.Enabled);
        Assert.Equal("anthropic", claude.ProviderId);
        Assert.Equal("gemini", gemini.ProviderId);
        Assert.Contains("ANTHROPIC_API_KEY", claude.RequiredEnvKeys);
    }

    [Fact]
    public void No_anthropic_image_model_exists()
    {
        // Anthropic has no image-generation API — there should never be a Claude entry
        // under the Image capability, regardless of what future entries get added.
        var images = SupportedModelCatalog.ForCapability(ModelCapability.Image, enabledOnly: false);
        Assert.DoesNotContain(images, e => e.Provider == ModelProviderFamily.Anthropic);
    }

    [Fact]
    public void Chat_image_and_video_non_grok_models_disclose_wiring_in_notes()
    {
        // These route through a real MultiProvider dispatcher (see Program.cs) — the
        // operator-facing hint text should say so, not describe them as unwired.
        foreach (var (id, cap) in new[]
        {
            ("veo-3.1", ModelCapability.Video),
            ("gemini-3-pro-image", ModelCapability.Image),
            ("claude-sonnet-5", ModelCapability.Chat),
            ("gemini-3-pro", ModelCapability.Chat),
        })
        {
            var e = SupportedModelCatalog.Find(id, cap);
            Assert.NotNull(e);
            Assert.Contains("wired", e!.Notes ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("not wired", (e.Notes ?? "").ToLowerInvariant());
            Assert.DoesNotContain("no anthropic client is wired", (e.Notes ?? "").ToLowerInvariant());
            Assert.DoesNotContain("no gemini", (e.Notes ?? "").ToLowerInvariant());
        }
    }

    [Fact]
    public void Vision_non_grok_models_disclose_partial_wiring_in_notes()
    {
        // CompleteWithImagesAsync (clip/frame review) is wired for these; the OCR / cast-classify
        // vision methods are not — notes must not claim full parity with Grok's vision client.
        foreach (var id in new[] { "claude-sonnet-5", "gemini-3-pro" })
        {
            var e = SupportedModelCatalog.Find(id, ModelCapability.Vision);
            Assert.NotNull(e);
            var notes = (e!.Notes ?? "").ToLowerInvariant();
            Assert.Contains("wired", notes);
            Assert.Contains("ocr", notes);
        }
    }
}
