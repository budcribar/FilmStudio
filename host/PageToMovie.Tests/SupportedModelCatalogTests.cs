using PageToMovie.Core.Models;
using Xunit;

namespace PageToMovie.Tests;

public class SupportedModelCatalogTests
{
    public SupportedModelCatalogTests()
    {
        SupportedModelCatalog.ReloadCatalog();
    }

    [Theory]
    [InlineData("""{"models":[{"id":"x","capability":"Chat"}]}""")]
    [InlineData("""[{"id":"x","capability":"Chat"}]""")]
    public void IsUsableCatalogJson_accepts_object_or_array_with_entries(string json)
    {
        Assert.True(SupportedModelCatalog.IsUsableCatalogJson(json));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"models":[]}""")]
    [InlineData("[]")]
    [InlineData("""{"models":"not an array"}""")]
    [InlineData("null")]
    [InlineData("not json at all")]
    [InlineData("")]
    public void IsUsableCatalogJson_rejects_empty_or_structurally_invalid(string json)
    {
        Assert.False(SupportedModelCatalog.IsUsableCatalogJson(json));
    }

    [Theory]
    [InlineData("grok-imagine-video", 7)]   // multi-plate identity conditioning
    [InlineData("hunyuan-video", 1)]        // single init/reference image only (true i2v)
    [InlineData("fal-ai/wan-2.1", 1)]       // single init/reference image only (true i2v)
    [InlineData("veo-3.1", 0)]              // not implemented — fail loud, not silently ignored
    public void MaxReferenceImages_MatchesRealPerModelCapability(string modelId, int expectedMax)
    {
        var entry = SupportedModelCatalog.ResolveOrDefault(modelId, ModelCapability.Video);
        Assert.Equal(expectedMax, entry.MaxReferenceImages);
    }

    [Theory]
    [InlineData("grok-imagine-image-quality", 3)]  // Grok Imagine multi-image edit hard cap
    [InlineData("grok-imagine-image", 3)]          // Grok Imagine multi-image edit hard cap
    [InlineData("gemini-2.5-pro-image", 14)]       // documented soft max for Gemini 3 image family
    public void MaxReferenceImages_MatchesRealPerModelCapability_ForImageModels(string modelId, int expectedMax)
    {
        var entry = SupportedModelCatalog.ResolveOrDefault(modelId, ModelCapability.Image);
        Assert.Equal(expectedMax, entry.MaxReferenceImages);
    }

    [Theory]
    [InlineData("hunyuan-video", 30)]
    [InlineData("fal-ai/wan-2.1", 30)]
    public void NumInferenceSteps_MatchesRealPerModelFalDefault(string modelId, int expectedSteps)
    {
        var entry = SupportedModelCatalog.ResolveOrDefault(modelId, ModelCapability.Video);
        Assert.Equal(expectedSteps, entry.NumInferenceSteps);
    }

    [Fact]
    public void HunyuanVideo_HasRealFrameCountLadder()
    {
        // HunyuanVideo's real fal.ai num_frames parameter is a discrete enum: 85 or 129 (default 129).
        var entry = SupportedModelCatalog.ResolveOrDefault("hunyuan-video", ModelCapability.Video);
        Assert.Equal(85, entry.ShortClipFrameCount);
        Assert.Equal(129, entry.LongClipFrameCount);
    }

    [Fact]
    public void Wan21_IsDurationNative_NotFrameCountNative()
    {
        // Wan-2.1 stays duration-native (min/max/absMaxClipDurationSeconds) — it does not get a
        // short/long frame-count ladder like Hunyuan since FalVideoClient doesn't send an explicit
        // num_frames override for it today.
        var entry = SupportedModelCatalog.ResolveOrDefault("fal-ai/wan-2.1", ModelCapability.Video);
        Assert.Null(entry.ShortClipFrameCount);
        Assert.Null(entry.LongClipFrameCount);
        Assert.Equal(5, entry.MinClipDurationSeconds);
        Assert.Equal(6, entry.MaxClipDurationSeconds);
    }

    [Fact]
    public void NumInferenceSteps_And_FrameCount_FallBackToNull_ForUnknownModel()
    {
        // A model with no catalog entry (or one that predates these fields) must resolve to null
        // so callers (FalVideoClient) know to fall back to their own hardcoded defaults.
        var entry = SupportedModelCatalog.ResolveOrDefault("veo-3.1", ModelCapability.Video);
        Assert.Null(entry.NumInferenceSteps);
        Assert.Null(entry.ShortClipFrameCount);
        Assert.Null(entry.LongClipFrameCount);
    }

    [Fact]
    public void SaveCatalogJson_throws_and_does_not_touch_disk_on_invalid_structure()
    {
        var before = SupportedModelCatalog.Entries.Count;
        Assert.Throws<ArgumentException>(() => SupportedModelCatalog.SaveCatalogJson("""{"models":[]}"""));
        // Reload from whatever's still on disk — must be exactly what was there before the failed save.
        SupportedModelCatalog.ReloadCatalog();
        Assert.Equal(before, SupportedModelCatalog.Entries.Count);
    }

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
        // Capability flags gate multi-clip / cast-locked gen before API spend
        Assert.False(m.SupportsVideoContinue);
        Assert.False(m.SupportsReferenceImages);
    }

    [Fact]
    public void Grok_video_supports_continue_and_reference_images()
    {
        var m = SupportedModelCatalog.Find("grok-imagine-video", ModelCapability.Video);
        Assert.NotNull(m);
        Assert.True(m!.SupportsVideoContinue);
        Assert.True(m.SupportsReferenceImages);
    }

    [Fact]
    public void Gemini_image_is_selectable_as_a_portrait_model()
    {
        var m = SupportedModelCatalog.Find("gemini-2.5-pro-image", ModelCapability.Image);
        Assert.NotNull(m);
        Assert.True(m!.Enabled);
        Assert.Equal("gemini", m.ProviderId);
    }

    [Fact]
    public void Claude_and_gemini_are_selectable_as_chat_models()
    {
        var claude = SupportedModelCatalog.Find("claude-sonnet-5", ModelCapability.Chat);
        var gemini = SupportedModelCatalog.Find("gemini-2.5-flash", ModelCapability.Chat);
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
            ("gemini-2.5-pro-image", ModelCapability.Image),
            ("claude-sonnet-5", ModelCapability.Chat),
            ("gemini-2.5-flash", ModelCapability.Chat),
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
        foreach (var id in new[] { "claude-sonnet-5", "gemini-2.5-flash" })
        {
            var e = SupportedModelCatalog.Find(id, ModelCapability.Vision);
            Assert.NotNull(e);
            var notes = (e!.Notes ?? "").ToLowerInvariant();
            Assert.Contains("wired", notes);
            Assert.Contains("ocr", notes);
        }
    }

    [Theory]
    [InlineData("grok-4.5", ModelCapability.Chat, 500_000)]
    [InlineData("grok-4", ModelCapability.Chat, 256_000)]
    [InlineData("claude-sonnet-5", ModelCapability.Chat, 1_000_000)]
    [InlineData("gemini-2.5-flash", ModelCapability.Chat, 1_000_000)]
    public void Chat_models_carry_real_context_window(string id, ModelCapability cap, int expectedMaxInputTokens)
    {
        // Provider-documented context windows (2026-07) — BookToFountainConverter.
        // ResolvePromptBudget reads this instead of assuming every model is 128k.
        var e = SupportedModelCatalog.Find(id, cap);
        Assert.NotNull(e);
        Assert.Equal(expectedMaxInputTokens, e!.MaxInputTokens);
    }

    [Fact]
    public void Video_and_image_models_leave_context_window_unset()
    {
        // Not a meaningful concept for these capabilities — should stay null, not a guessed value.
        foreach (var cap in new[] { ModelCapability.Video, ModelCapability.Image })
        {
            foreach (var e in SupportedModelCatalog.ForCapability(cap, enabledOnly: false))
                Assert.Null(e.MaxInputTokens);
        }
    }

    [Theory]
    [InlineData("grok-4.5", ModelCapability.Chat, 2.00, 6.00)]
    [InlineData("grok-4", ModelCapability.Chat, 3.00, 15.00)]
    [InlineData("claude-sonnet-5", ModelCapability.Chat, 2.00, 10.00)]
    [InlineData("gemini-2.5-flash", ModelCapability.Chat, 2.00, 12.00)]
    public void Chat_models_carry_real_token_pricing(
        string id, ModelCapability cap, double expectedInput, double expectedOutput)
    {
        // Provider-documented base-tier USD/1M-token pricing (2026-07).
        var e = SupportedModelCatalog.Find(id, cap);
        Assert.NotNull(e);
        Assert.Equal(expectedInput, e!.InputCostPerMillionTokens);
        Assert.Equal(expectedOutput, e.OutputCostPerMillionTokens);
    }

    [Fact]
    public void Video_models_carry_per_resolution_pricing()
    {
        // Grok's numbers also match Configuration.razor's default $/sec fields (480p/720p/1080p)
        // exactly — same underlying xAI pricing, sourced independently; should never drift apart
        // silently.
        var grok = SupportedModelCatalog.Find("grok-imagine-video", ModelCapability.Video);
        Assert.NotNull(grok?.VideoCostPerSecondByResolution);
        Assert.Equal(0.05, grok!.VideoCostPerSecondByResolution!["480p"]);
        Assert.Equal(0.07, grok.VideoCostPerSecondByResolution["720p"]);
        Assert.Equal(0.25, grok.VideoCostPerSecondByResolution["1080p"]);

        var veo = SupportedModelCatalog.Find("veo-3.1", ModelCapability.Video);
        Assert.NotNull(veo?.VideoCostPerSecondByResolution);
        Assert.Equal(0.40, veo!.VideoCostPerSecondByResolution!["720p"]);
        Assert.Equal(0.40, veo.VideoCostPerSecondByResolution["1080p"]);
    }

    [Fact]
    public void Image_models_carry_per_image_pricing()
    {
        Assert.Equal(0.05, SupportedModelCatalog.Find("grok-imagine-image-quality", ModelCapability.Image)?.ImageCostPerImage);
        Assert.Equal(0.02, SupportedModelCatalog.Find("grok-imagine-image", ModelCapability.Image)?.ImageCostPerImage);
        Assert.Equal(0.134, SupportedModelCatalog.Find("gemini-2.5-pro-image", ModelCapability.Image)?.ImageCostPerImage);
    }

    [Fact]
    public void Chat_models_leave_video_and_image_pricing_unset()
    {
        foreach (var e in SupportedModelCatalog.ForCapability(ModelCapability.Chat, enabledOnly: false))
        {
            Assert.Null(e.VideoCostPerSecondByResolution);
            Assert.Null(e.ImageCostPerImage);
        }
    }

    [Fact]
    public void Video_and_image_models_leave_token_pricing_unset()
    {
        foreach (var cap in new[] { ModelCapability.Video, ModelCapability.Image })
        {
            foreach (var e in SupportedModelCatalog.ForCapability(cap, enabledOnly: false))
            {
                Assert.Null(e.InputCostPerMillionTokens);
                Assert.Null(e.OutputCostPerMillionTokens);
            }
        }
    }

    [Theory]
    [InlineData("claude-sonnet-5", ModelCapability.Chat, 128_000)]
    [InlineData("claude-opus-5", ModelCapability.Chat, 128_000)]
    public void Claude_chat_models_carry_real_max_output_tokens(string id, ModelCapability cap, int expectedMaxOutputTokens)
    {
        // Provider-documented (platform.claude.com/docs/en/about-claude/models/overview, 2026-08):
        // Claude Sonnet 5 and Claude Opus 5 both cap at 128k output tokens on the synchronous
        // Messages API. AnthropicChatClient.ResolveMaxTokens reads this instead of always sending
        // the DefaultMaxTokens fallback.
        var e = SupportedModelCatalog.Find(id, cap);
        Assert.NotNull(e);
        Assert.Equal(expectedMaxOutputTokens, e!.MaxOutputTokens);
    }

    [Fact]
    public void MaxOutputTokens_round_trips_through_ToDto_and_FromDto()
    {
        var entry = new SupportedModelEntry
        {
            Id = "test-max-output-tokens",
            DisplayName = "Test Model",
            Capability = ModelCapability.Chat,
            Provider = ModelProviderFamily.Anthropic,
            ApiBase = SupportedModelCatalog.AnthropicApiBase,
            EndpointPath = "messages",
            RequiredEnvKeys = new List<string> { SupportedModelCatalog.AnthropicApiKeyEnv },
            MaxOutputTokens = 128_000,
        };

        var dto = SupportedModelCatalog.ToDto(entry);
        Assert.Equal(128_000, dto.MaxOutputTokens);

        var roundTripped = SupportedModelCatalog.FromDto(dto);
        Assert.Equal(128_000, roundTripped.MaxOutputTokens);
    }

    [Fact]
    public void MaxOutputTokens_defaults_to_null_when_unset()
    {
        var entry = new SupportedModelEntry
        {
            Id = "test-max-output-tokens-unset",
            DisplayName = "Test Model",
            Capability = ModelCapability.Chat,
            Provider = ModelProviderFamily.Xai,
            ApiBase = SupportedModelCatalog.XaiApiBase,
            EndpointPath = "chat/completions",
            RequiredEnvKeys = new List<string> { SupportedModelCatalog.XaiApiKeyEnv },
        };

        var dto = SupportedModelCatalog.ToDto(entry);
        Assert.Null(dto.MaxOutputTokens);

        var roundTripped = SupportedModelCatalog.FromDto(dto);
        Assert.Null(roundTripped.MaxOutputTokens);
    }
}
