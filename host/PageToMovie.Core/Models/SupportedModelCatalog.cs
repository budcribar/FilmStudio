namespace PageToMovie.Core.Models;

public sealed record SceneMusicResult(
    string MusicFilePath,
    string RemuxedScenePath,
    string MusicPrompt,
    double DurationSeconds);

/// <summary>
/// What the model is used for in Film Studio (drives Configuration dropdowns).
/// </summary>
public enum ModelCapability
{
    Video,
    Image,
    Chat,
    Vision,
}

/// <summary>
/// Backend family — maps to API base URL + required env keys.
/// User never picks this; it is derived from the model id via the catalog.
/// Clients are selected by <see cref="SupportedModelEntry.Id"/> through multi-provider
/// facades (chat / image / video / vision) in PageToMovie.Engine.
/// </summary>
public enum ModelProviderFamily
{
    /// <summary>xAI (api.x.ai) — <c>XAI_API_KEY</c>. Full product path (chat, image, video, vision/OCR).</summary>
    Xai = 0,
    /// <summary>
    /// Google Gemini / Veo (<c>GEMINI_API_KEY</c>) — wired via GeminiChatClient, GeminiImageClient,
    /// GeminiVideoClient (text/image-to-video only), MultiProviderVisionClient for frame review.
    /// Book-page OCR and cast-on-image classify stay Grok-only; Veo has no clip-extend / multi-ref plates.
    /// </summary>
    Google = 1,
    /// <summary>
    /// Anthropic Claude (<c>ANTHROPIC_API_KEY</c>) — wired via AnthropicChatClient and
    /// MultiProviderVisionClient for frame review. No image generation API; OCR/cast classify stay Grok-only.
    /// </summary>
    Anthropic = 2,
    /// <summary>
    /// Fal.ai (<c>FAL_KEY</c>) — serverless open-source video/image models (HunyuanVideo).
    /// </summary>
    Fal = 3,
}

/// <summary>
/// One supported model. Only entries with <see cref="Enabled"/> true appear in user pickers.
/// Wishlist / not-yet-wired models stay off the list and can be tracked as GitHub feature requests.
/// </summary>
public sealed class SupportedModelEntry
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required ModelCapability Capability { get; init; }
    public required ModelProviderFamily Provider { get; init; }

    /// <summary>API origin, e.g. <c>https://api.x.ai/v1</c>.</summary>
    public required string ApiBase { get; init; }

    /// <summary>
    /// Primary relative path under <see cref="ApiBase"/> (e.g. <c>videos/generations</c>).
    /// Extensions / alternate routes stay in the client; this is the capability home.
    /// </summary>
    public required string EndpointPath { get; init; }

    /// <summary>Env var names that must be set (e.g. <c>XAI_API_KEY</c>).</summary>
    public required IReadOnlyList<string> RequiredEnvKeys { get; init; }

    /// <summary>When false, hidden from Configuration pickers.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Context window (max input tokens), for callers that need to budget large prompts against
    /// the actual model — e.g. book-to-screenplay chunking. Null for models where this isn't a
    /// meaningful concept (video/image) or isn't verified yet. Sourced from provider docs as of
    /// 2026-07; providers do increase these over time, so re-check before trusting an old number
    /// for a cost/quality-sensitive decision.
    /// </summary>
    public int? MaxInputTokens { get; init; }

    /// <summary>USD per 1,000,000 input tokens (Chat / Vision only). Null when not applicable.</summary>
    public double? InputCostPerMillionTokens { get; init; }

    /// <summary>USD per 1,000,000 output tokens (Chat / Vision only). Null when not applicable.</summary>
    public double? OutputCostPerMillionTokens { get; init; }

    /// <summary>
    /// USD per second of generated output, by resolution (Video only) — same key convention
    /// ("480p" / "720p" / "1080p") as the project-level <c>cost_estimates.video_output_per_sec</c>
    /// table in Configuration. That table is an operator-editable planning estimate for whichever
    /// video model is active; this is the catalog's own reference price per model, and a given
    /// model may not price every resolution (only confirmed keys are present). Null when no
    /// per-resolution pricing applies (non-video capabilities).
    /// </summary>
    public IReadOnlyDictionary<string, double>? VideoCostPerSecondByResolution { get; init; }

    /// <summary>USD per generated image (Image only). Null when not applicable.</summary>
    public double? ImageCostPerImage { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Optional link to a GitHub issue / feature request for models we plan to support.
    /// Prefer leaving unsupported models out of the enabled list and tracking them on GitHub.
    /// </summary>
    public string? FeatureRequestUrl { get; init; }

    /// <summary>
    /// When true (default for Grok Imagine Video), clip 2+ can continue via video-extend.
    /// False for providers that only support text/image-to-video (e.g. Veo today).
    /// </summary>
    public bool SupportsVideoContinue { get; init; } = true;

    /// <summary>
    /// When true, locked character reference plates can be attached on fresh gen.
    /// False for backends that reject multi-image / reference conditioning.
    /// </summary>
    public bool SupportsReferenceImages { get; init; } = true;

    /// <summary>
    /// When true, accepts native MP4 video & audio files directly for clip/dialogue review (Google Gemini).
    /// </summary>
    public bool SupportsVideoReview { get; init; } = false;

    /// <summary>Raw provider string from models_catalog.json (e.g. OpenAI, DeepSeek, Grok, Gemini).</summary>
    public string ProviderName { get; init; } = "";

    /// <summary>Provider id for config / cost reports (<c>grok</c>, <c>gemini</c>, <c>anthropic</c>, <c>openai</c>).</summary>
    public string ProviderId => !string.IsNullOrWhiteSpace(ProviderName)
        ? ProviderName.ToLowerInvariant()
        : Provider switch
        {
            ModelProviderFamily.Google => "gemini",
            ModelProviderFamily.Anthropic => "anthropic",
            ModelProviderFamily.Fal => "fal",
            _ => "grok",
        };
}

/// <summary>
/// Master list of models Film Studio knows how to call.
/// User picks <see cref="SupportedModelEntry.Id"/> only; app resolves endpoint + keys.
/// </summary>
public static class SupportedModelCatalog
{
    public const string XaiApiBase = "https://api.x.ai/v1";
    public const string XaiApiKeyEnv = "XAI_API_KEY";
    public const string GoogleApiBase = "https://generativelanguage.googleapis.com/v1beta";
    public const string GoogleApiKeyEnv = "GEMINI_API_KEY";
    public const string AnthropicApiBase = "https://api.anthropic.com/v1";
    public const string AnthropicApiKeyEnv = "ANTHROPIC_API_KEY";
    public const string FalApiBase = "https://queue.fal.run";
    public const string FalApiKeyEnv = "FAL_API_KEY";
    public const string FalApiKeyFallbackEnv = "FAL_KEY";

    private static readonly SupportedModelEntry[] BuiltInDefaults =
    [
        new()
        {
            Id = "grok-imagine-video",
            DisplayName = "Grok Imagine Video",
            Capability = ModelCapability.Video,
            Provider = ModelProviderFamily.Xai,
            ApiBase = XaiApiBase,
            EndpointPath = "videos/generations",
            RequiredEnvKeys = [XaiApiKeyEnv],
            VideoCostPerSecondByResolution = new Dictionary<string, double> { ["480p"] = 0.05, ["720p"] = 0.07, ["1080p"] = 0.25 },
            SupportsVideoContinue = true,
            SupportsReferenceImages = true,
            Notes = "Also uses videos/extensions for clip continue.",
        },
        new()
        {
            Id = "hunyuan-video",
            DisplayName = "HunyuanVideo (Fal.ai)",
            Capability = ModelCapability.Video,
            Provider = ModelProviderFamily.Fal,
            ApiBase = FalApiBase,
            EndpointPath = "fal-ai/hunyuan-video",
            RequiredEnvKeys = [FalApiKeyEnv],
            VideoCostPerSecondByResolution = new Dictionary<string, double> { ["720p"] = 0.005, ["1080p"] = 0.005 },
            SupportsVideoContinue = true,
            SupportsReferenceImages = true,
            Notes = "Open-weights 13B DiT video generation model hosted on Fal.ai serverless GPUs (~$0.025 per 5s clip).",
        },
        new()
        {
            Id = "veo-3.1",
            DisplayName = "Google Veo 3.1",
            Capability = ModelCapability.Video,
            Provider = ModelProviderFamily.Google,
            ApiBase = GoogleApiBase,
            EndpointPath = "models/veo-3.1:predictLongRunning",
            RequiredEnvKeys = [GoogleApiKeyEnv],
            VideoCostPerSecondByResolution = new Dictionary<string, double> { ["720p"] = 0.40, ["1080p"] = 0.40 },
            SupportsVideoContinue = false,
            SupportsReferenceImages = false,
            Notes = "Wired via GeminiVideoClient (text/image-to-video only).",
        },
        new()
        {
            Id = "grok-imagine-image-quality",
            DisplayName = "Grok Imagine Image (quality)",
            Capability = ModelCapability.Image,
            Provider = ModelProviderFamily.Xai,
            ApiBase = XaiApiBase,
            EndpointPath = "images/generations",
            RequiredEnvKeys = [XaiApiKeyEnv],
            ImageCostPerImage = 0.05,
        },
        new()
        {
            Id = "grok-imagine-image",
            DisplayName = "Grok Imagine Image",
            Capability = ModelCapability.Image,
            Provider = ModelProviderFamily.Xai,
            ApiBase = XaiApiBase,
            EndpointPath = "images/generations",
            RequiredEnvKeys = [XaiApiKeyEnv],
            ImageCostPerImage = 0.02,
        },
        new()
        {
            Id = "gemini-2.5-pro-image",
            DisplayName = "Gemini 2.5 Pro Image",
            Capability = ModelCapability.Image,
            Provider = ModelProviderFamily.Google,
            ApiBase = GoogleApiBase,
            EndpointPath = "models/gemini-2.5-pro:generateContent",
            RequiredEnvKeys = [GoogleApiKeyEnv],
            ImageCostPerImage = 0.134,
            Notes = "Wired via GeminiImageClient. Supports up to 14 reference images.",
        },
        new()
        {
            Id = "fal-ai/flux/dev",
            DisplayName = "Fal.ai Flux.1 Dev",
            Capability = ModelCapability.Image,
            Provider = ModelProviderFamily.Fal,
            ApiBase = FalApiBase,
            EndpointPath = "fal-ai/flux/dev",
            RequiredEnvKeys = [FalApiKeyEnv],
            ImageCostPerImage = 0.025,
            Notes = "Open-source Flux.1 Dev model via Fal.ai serverless GPU (~$0.025/image).",
        },
        new()
        {
            Id = "fal-ai/flux/schnell",
            DisplayName = "Fal.ai Flux.1 Schnell (Fast)",
            Capability = ModelCapability.Image,
            Provider = ModelProviderFamily.Fal,
            ApiBase = FalApiBase,
            EndpointPath = "fal-ai/flux/schnell",
            RequiredEnvKeys = [FalApiKeyEnv],
            ImageCostPerImage = 0.003,
            Notes = "Ultra-fast open-source Flux.1 Schnell model via Fal.ai (~$0.003/image).",
        },
        new()
        {
            Id = "grok-4.5",
            DisplayName = "Grok 4.5",
            Capability = ModelCapability.Chat,
            Provider = ModelProviderFamily.Xai,
            ApiBase = XaiApiBase,
            EndpointPath = "chat/completions",
            RequiredEnvKeys = [XaiApiKeyEnv],
            MaxInputTokens = 500_000,
            InputCostPerMillionTokens = 2.00,
            OutputCostPerMillionTokens = 6.00,
            Notes = "Stage planning, cast scrub, screenplay helpers.",
        },
        new()
        {
            Id = "grok-4",
            DisplayName = "Grok 4",
            Capability = ModelCapability.Chat,
            Provider = ModelProviderFamily.Xai,
            ApiBase = XaiApiBase,
            EndpointPath = "chat/completions",
            RequiredEnvKeys = [XaiApiKeyEnv],
            MaxInputTokens = 256_000,
            InputCostPerMillionTokens = 3.00,
            OutputCostPerMillionTokens = 15.00,
        },
        new()
        {
            Id = "claude-sonnet-5",
            DisplayName = "Claude Sonnet 5",
            Capability = ModelCapability.Chat,
            Provider = ModelProviderFamily.Anthropic,
            ApiBase = AnthropicApiBase,
            EndpointPath = "messages",
            RequiredEnvKeys = [AnthropicApiKeyEnv],
            MaxInputTokens = 1_000_000,
            InputCostPerMillionTokens = 2.00,
            OutputCostPerMillionTokens = 10.00,
            Notes = "Wired via AnthropicChatClient.",
        },
        new()
        {
            Id = "gemini-2.5-pro",
            DisplayName = "Gemini 2.5 Pro",
            Capability = ModelCapability.Chat,
            Provider = ModelProviderFamily.Google,
            ApiBase = GoogleApiBase,
            EndpointPath = "models/gemini-2.5-pro:generateContent",
            RequiredEnvKeys = [GoogleApiKeyEnv],
            MaxInputTokens = 1_000_000,
            InputCostPerMillionTokens = 2.00,
            OutputCostPerMillionTokens = 12.00,
            SupportsVideoReview = true,
            Notes = "Wired via GeminiChatClient. Supports Native Multimodal MP4 Video Review.",
        },
        new()
        {
            Id = "grok-4.5",
            DisplayName = "Grok 4.5 (vision)",
            Capability = ModelCapability.Vision,
            Provider = ModelProviderFamily.Xai,
            ApiBase = XaiApiBase,
            EndpointPath = "chat/completions",
            RequiredEnvKeys = [XaiApiKeyEnv],
            MaxInputTokens = 500_000,
            InputCostPerMillionTokens = 2.00,
            OutputCostPerMillionTokens = 6.00,
            Notes = "GrokVisionClient: book-page OCR, cast-on-image classify, and multi-image frame review.",
        },
        new()
        {
            Id = "claude-sonnet-5",
            DisplayName = "Claude Sonnet 5 (vision)",
            Capability = ModelCapability.Vision,
            Provider = ModelProviderFamily.Anthropic,
            ApiBase = AnthropicApiBase,
            EndpointPath = "messages",
            RequiredEnvKeys = [AnthropicApiKeyEnv],
            MaxInputTokens = 1_000_000,
            InputCostPerMillionTokens = 2.00,
            OutputCostPerMillionTokens = 10.00,
            Notes = "Wired for clip/frame review via MultiProviderVisionClient (OCR/cast-classify stay Grok-only).",
        },
        new()
        {
            Id = "gemini-2.5-pro",
            DisplayName = "Gemini 2.5 Pro (vision)",
            Capability = ModelCapability.Vision,
            Provider = ModelProviderFamily.Google,
            ApiBase = GoogleApiBase,
            EndpointPath = "models/gemini-2.5-pro:generateContent",
            RequiredEnvKeys = [GoogleApiKeyEnv],
            MaxInputTokens = 1_000_000,
            InputCostPerMillionTokens = 2.00,
            OutputCostPerMillionTokens = 12.00,
            SupportsVideoReview = true,
            Notes = "Wired for clip/frame review (CompleteWithImagesAsync) via MultiProviderVisionClient (OCR/cast-classify stay Grok-only).",
        },
    ];

    private static List<SupportedModelEntry>? _loadedEntries;

    private static IReadOnlyList<ModelCapabilityDefinition>? _loadedCapabilities;

    /// <summary>Dynamic list of capabilities registered in models_catalog.json (or defaults).</summary>
    public static IReadOnlyList<ModelCapabilityDefinition> RegisteredCapabilities
    {
        get
        {
            if (_loadedCapabilities is null)
            {
                EnsureLoaded();
            }
            return _loadedCapabilities ?? DefaultCapabilityDefinitions;
        }
    }

    public static readonly IReadOnlyList<ModelCapabilityDefinition> DefaultCapabilityDefinitions =
    [
        new() { Id = "video", DisplayName = "Video Generation", Description = "Generates MP4 video clips from prompts and character reference plates.", Order = 1 },
        new() { Id = "image", DisplayName = "Portrait / Image Generation", Description = "Creates character reference portraits and book plate graphics.", Order = 2 },
        new() { Id = "chat", DisplayName = "Script & Planning", Description = "Screenplay reasoning, shot planning, and cast analysis.", Order = 3 },
        new() { Id = "vision", DisplayName = "Image Vision & OCR", Description = "Book page OCR, cast-on-image classification, and frame inspection.", Order = 4 },
        new() { Id = "video-review", DisplayName = "Video & Clip Review (Multimodal)", Description = "Evaluates dialogue, lip sync, and scene rhythm (Google Gemini natively analyzes MP4 video files).", Order = 5 },
        new() { Id = "audio", DisplayName = "Audio & Music Generation", Description = "Generates beat-aligned background music scores and sound effects.", Order = 6 },
    ];

    private static Dictionary<string, List<string>>? _loadedTaskRankings;

    public static IReadOnlyDictionary<string, List<string>> TaskRankings
    {
        get
        {
            EnsureLoaded();
            return _loadedTaskRankings ?? DefaultTaskRankings;
        }
    }

    public static readonly Dictionary<string, List<string>> DefaultTaskRankings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["script_import"] = new() { "claude-sonnet-5", "grok-4.5", "gemini-2.5-pro" },
        ["beat_pacing"] = new() { "grok-4.5", "gemini-2.0-flash", "claude-sonnet-5" },
        ["camera_director"] = new() { "grok-4.5", "claude-sonnet-5" },
        ["sound_design"] = new() { "grok-4.5", "gemini-2.5-pro" },
        ["cast_analysis"] = new() { "grok-4.5", "claude-sonnet-5", "gemini-2.5-pro" },
        ["video_review"] = new() { "gemini-2.5-pro", "grok-4.5" },
    };

    /// <summary>All catalog rows (loaded dynamically from models_catalog.json or built-in defaults).</summary>
    public static IReadOnlyList<SupportedModelEntry> Entries
    {
        get
        {
            EnsureLoaded();
            return _loadedEntries ?? (IReadOnlyList<SupportedModelEntry>)BuiltInDefaults;
        }
    }

    public static void ReloadCatalog(string? overrideJsonPath = null)
    {
        _loadedEntries = null;
        _loadedCapabilities = null;
        _loadedTaskRankings = null;
        EnsureLoaded(overrideJsonPath);
    }

    private static void EnsureLoaded(string? customPath = null)
    {
        if (_loadedEntries is not null && _loadedCapabilities is not null) return;

        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(customPath))
            candidates.Add(customPath);

        candidates.Add("/data/models_catalog.json");
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "config", "models_catalog.json"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "models_catalog.json"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "config", "models_catalog.json"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "host", "PageToMovie.Core", "config", "models_catalog.json"));

        foreach (var path in candidates.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)))
        {
            try
            {
                var json = File.ReadAllText(path);
                var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                
                // Parse object format or array format
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    var container = System.Text.Json.JsonSerializer.Deserialize<ModelCatalogContainerDto>(json, opts);
                    if (container?.Models is { Count: > 0 })
                    {
                        _loadedEntries = container.Models.Select(FromDto).ToList();
                        if (container.Capabilities is { Count: > 0 })
                        {
                            _loadedCapabilities = container.Capabilities.Select(c => new ModelCapabilityDefinition
                            {
                                Id = c.Id,
                                DisplayName = c.DisplayName,
                                Description = c.Description,
                                Order = c.Order,
                                DefaultModelId = c.DefaultModelId,
                            }).ToList();
                        }
                        else
                        {
                            _loadedCapabilities = DefaultCapabilityDefinitions;
                        }

                        _loadedTaskRankings = container.TaskRankings is { Count: > 0 }
                            ? new Dictionary<string, List<string>>(container.TaskRankings, StringComparer.OrdinalIgnoreCase)
                            : DefaultTaskRankings;

                        return;
                    }
                }
                else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var dtos = System.Text.Json.JsonSerializer.Deserialize<List<SupportedModelDto>>(json, opts);
                    if (dtos is { Count: > 0 })
                    {
                        _loadedEntries = dtos.Select(FromDto).ToList();
                        _loadedCapabilities = DefaultCapabilityDefinitions;
                        return;
                    }
                }
            }
            catch
            {
                // Ignore parse failures and try next candidate or fallback
            }
        }

        _loadedEntries = BuiltInDefaults.ToList();
        _loadedCapabilities = DefaultCapabilityDefinitions;
    }

    public static IReadOnlyList<SupportedModelEntry> ForCapability(
        ModelCapability capability,
        bool enabledOnly = true) =>
        Entries.Where(e => e.Capability == capability && (!enabledOnly || e.Enabled)).ToList();

    public static SupportedModelEntry? Find(string? modelId, ModelCapability? capability = null)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return null;
        var id = modelId.Trim();
        var exact = Entries.Where(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 0) return null;

        if (capability is not { } cap)
            return exact[0];

        var match = exact.FirstOrDefault(e => e.Capability == cap);
        if (match is not null) return match;

        if (cap is ModelCapability.Chat or ModelCapability.Vision)
        {
            return exact.FirstOrDefault(e =>
                e.Capability is ModelCapability.Chat or ModelCapability.Vision);
        }

        return null;
    }

    public static SupportedModelEntry ResolveOrDefault(
        string? modelId,
        ModelCapability capability,
        string? fallbackId = null)
    {
        var hit = Find(modelId, capability);
        if (hit is not null) return hit;

        var knownUnderAnyCap = !string.IsNullOrWhiteSpace(modelId) && Find(modelId) is not null;
        if (!string.IsNullOrWhiteSpace(modelId) && !knownUnderAnyCap)
        {
            var id = modelId.Trim();
            return MakeSynthetic(id, capability);
        }

        if (!string.IsNullOrWhiteSpace(fallbackId))
        {
            hit = Find(fallbackId, capability);
            if (hit is not null) return hit;
        }

        hit = ForCapability(capability).FirstOrDefault();
        if (hit is not null) return hit;

        return MakeSynthetic(
            string.IsNullOrWhiteSpace(modelId) ? "unknown" : modelId.Trim(),
            capability);
    }

    private static SupportedModelEntry MakeSynthetic(string id, ModelCapability capability) => new()
    {
        Id = id,
        DisplayName = id,
        Capability = capability,
        Provider = ModelProviderFamily.Xai,
        ApiBase = XaiApiBase,
        EndpointPath = capability switch
        {
            ModelCapability.Video => "videos/generations",
            ModelCapability.Image => "images/generations",
            _ => "chat/completions",
        },
        RequiredEnvKeys = [XaiApiKeyEnv],
        Enabled = false,
        Notes = "Not in master catalog — add to models_catalog.json or track as feature request.",
    };

    public static string ProviderIdFor(string? modelId, ModelCapability capability) =>
        ResolveOrDefault(modelId, capability).ProviderId;

    public static IReadOnlyList<string> MissingEnvKeys(SupportedModelEntry model)
    {
        var missing = new List<string>();
        foreach (var key in model.RequiredEnvKeys)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                missing.Add(key);
        }
        return missing;
    }

    public static IReadOnlyList<SupportedModelDto> ToDtoList(bool enabledOnly = true) =>
        Entries.Where(e => !enabledOnly || e.Enabled)
            .Select(ToDto)
            .ToList();

    public static SupportedModelDto ToDto(SupportedModelEntry e) => new()
    {
        Id = e.Id,
        DisplayName = e.DisplayName,
        Capability = e.Capability.ToString().ToLowerInvariant(),
        Provider = e.Provider.ToString().ToLowerInvariant(),
        ApiBase = e.ApiBase,
        EndpointPath = e.EndpointPath,
        RequiredEnvKeys = e.RequiredEnvKeys.ToList(),
        Enabled = e.Enabled,
        MaxInputTokens = e.MaxInputTokens,
        InputCostPerMillionTokens = e.InputCostPerMillionTokens,
        OutputCostPerMillionTokens = e.OutputCostPerMillionTokens,
        VideoCostPerSecondByResolution = e.VideoCostPerSecondByResolution is { } v
            ? new Dictionary<string, double>(v)
            : null,
        ImageCostPerImage = e.ImageCostPerImage,
        Notes = e.Notes,
        FeatureRequestUrl = e.FeatureRequestUrl,
        ProviderId = e.ProviderId,
        SupportsVideoContinue = e.SupportsVideoContinue,
        SupportsReferenceImages = e.SupportsReferenceImages,
        SupportsVideoReview = e.SupportsVideoReview,
    };

    public static SupportedModelEntry FromDto(SupportedModelDto d) => new()
    {
        Id = d.Id,
        DisplayName = d.DisplayName,
        Capability = Enum.TryParse<ModelCapability>(d.Capability, true, out var cap) ? cap : ModelCapability.Chat,
        ProviderName = d.Provider ?? "",
        Provider = Enum.TryParse<ModelProviderFamily>(d.Provider, true, out var prov) ? prov : ModelProviderFamily.Xai,
        ApiBase = string.IsNullOrWhiteSpace(d.ApiBase) ? XaiApiBase : d.ApiBase,
        EndpointPath = d.EndpointPath ?? "",
        RequiredEnvKeys = d.RequiredEnvKeys ?? new List<string>(),
        Enabled = d.Enabled,
        MaxInputTokens = d.MaxInputTokens,
        InputCostPerMillionTokens = d.InputCostPerMillionTokens,
        OutputCostPerMillionTokens = d.OutputCostPerMillionTokens,
        VideoCostPerSecondByResolution = d.VideoCostPerSecondByResolution,
        ImageCostPerImage = d.ImageCostPerImage,
        Notes = d.Notes,
        FeatureRequestUrl = d.FeatureRequestUrl,
        SupportsVideoContinue = d.SupportsVideoContinue,
        SupportsReferenceImages = d.SupportsReferenceImages,
        SupportsVideoReview = d.SupportsVideoReview,
    };
}

public sealed class SupportedModelDto
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Capability { get; set; } = "";
    public string Provider { get; set; } = "";
    public string ApiBase { get; set; } = "";
    public string EndpointPath { get; set; } = "";
    public List<string> RequiredEnvKeys { get; set; } = new();
    public bool Enabled { get; set; } = true;
    public int? MaxInputTokens { get; set; }
    public double? InputCostPerMillionTokens { get; set; }
    public double? OutputCostPerMillionTokens { get; set; }
    public Dictionary<string, double>? VideoCostPerSecondByResolution { get; set; }
    public double? ImageCostPerImage { get; set; }
    public string? Notes { get; set; }
    public string? FeatureRequestUrl { get; set; }
    public string? ProviderId { get; set; }
    public bool SupportsVideoContinue { get; set; } = true;
    public bool SupportsReferenceImages { get; set; } = true;
    public bool SupportsVideoReview { get; set; }
}

public sealed class ModelCapabilityDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public string Description { get; init; } = "";
    public int Order { get; init; }
    public string? DefaultModelId { get; init; }
}

public sealed class ModelCapabilityDto
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public int Order { get; set; }
    public string? DefaultModelId { get; set; }
}

public sealed class ModelCatalogContainerDto
{
    public List<ModelCapabilityDto>? Capabilities { get; set; }
    public Dictionary<string, List<string>>? TaskRankings { get; set; }
    public List<SupportedModelDto>? Models { get; set; }
}
