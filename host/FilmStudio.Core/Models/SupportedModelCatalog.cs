namespace FilmStudio.Core.Models;

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
/// </summary>
public enum ModelProviderFamily
{
    /// <summary>xAI (api.x.ai) — <c>XAI_API_KEY</c>.</summary>
    Xai = 0,
    /// <summary>Google Gemini (reserved; not fully wired yet).</summary>
    Google = 1,
    /// <summary>Anthropic Claude (reserved; not fully wired yet).</summary>
    Anthropic = 2,
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

    public string? Notes { get; init; }

    /// <summary>
    /// Optional link to a GitHub issue / feature request for models we plan to support.
    /// Prefer leaving unsupported models out of the enabled list and tracking them on GitHub.
    /// </summary>
    public string? FeatureRequestUrl { get; init; }

    /// <summary>Provider id for config / cost reports (<c>grok</c>, <c>gemini</c>, <c>anthropic</c>).</summary>
    public string ProviderId => Provider switch
    {
        ModelProviderFamily.Google => "gemini",
        ModelProviderFamily.Anthropic => "anthropic",
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

    /// <summary>Google Gemini API. No client wired yet — see notes on entries below.</summary>
    public const string GoogleApiBase = "https://generativelanguage.googleapis.com/v1beta";
    public const string GoogleApiKeyEnv = "GEMINI_API_KEY";

    /// <summary>Anthropic Messages API. No client wired yet — see notes on entries below.</summary>
    public const string AnthropicApiBase = "https://api.anthropic.com/v1";
    public const string AnthropicApiKeyEnv = "ANTHROPIC_API_KEY";

    private static readonly SupportedModelEntry[] All =
    [
        // ── Video ──────────────────────────────────────────────────────────
        new()
        {
            Id = "grok-imagine-video",
            DisplayName = "Grok Imagine Video",
            Capability = ModelCapability.Video,
            Provider = ModelProviderFamily.Xai,
            ApiBase = XaiApiBase,
            EndpointPath = "videos/generations",
            RequiredEnvKeys = [XaiApiKeyEnv],
            Notes = "Also uses videos/extensions for clip continue.",
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
            Notes = "Wired via GeminiVideoClient (Veo long-running-operation flow). Not smoke-tested " +
                    "against a live account yet — see the CONFIDENCE NOTE on that class. " +
                    "Reference/extend-clip mechanics differ from Grok Imagine and are not mapped: " +
                    "only text-to-video and image-to-video (first frame) work today.",
            FeatureRequestUrl = "https://github.com/budcribar/FilmStudio/issues",
        },

        // ── Image / portraits ──────────────────────────────────────────────
        new()
        {
            Id = "grok-imagine-image-quality",
            DisplayName = "Grok Imagine Image (quality)",
            Capability = ModelCapability.Image,
            Provider = ModelProviderFamily.Xai,
            ApiBase = XaiApiBase,
            EndpointPath = "images/generations",
            RequiredEnvKeys = [XaiApiKeyEnv],
            Notes = "Edits use the multi-image edit path on the same family.",
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
        },
        new()
        {
            Id = "gemini-3-pro-image",
            DisplayName = "Gemini 3 Pro Image",
            Capability = ModelCapability.Image,
            Provider = ModelProviderFamily.Google,
            ApiBase = GoogleApiBase,
            EndpointPath = "models/gemini-3-pro-image:generateContent",
            RequiredEnvKeys = [GoogleApiKeyEnv],
            Notes = "Wired via GeminiImageClient. Supports up to 14 reference images (see " +
                    "ImageApiLimits.cs), vs. Grok's 3. Response-shape parsing is not smoke-tested " +
                    "against a live account yet.",
            FeatureRequestUrl = "https://github.com/budcribar/FilmStudio/issues",
        },
        // Note: no Claude/Anthropic entry here on purpose — Anthropic does not offer an image
        // generation API, so there is no real backend this could ever call. Claude is added
        // below under Chat instead, where it's a real, callable capability.

        // ── Chat / planning / scrub ────────────────────────────────────────
        new()
        {
            Id = "grok-4.5",
            DisplayName = "Grok 4.5",
            Capability = ModelCapability.Chat,
            Provider = ModelProviderFamily.Xai,
            ApiBase = XaiApiBase,
            EndpointPath = "chat/completions",
            RequiredEnvKeys = [XaiApiKeyEnv],
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
            Notes = "Wired via AnthropicChatClient, routed automatically through " +
                    "MultiProviderChatClient for planning/QA calls.",
            FeatureRequestUrl = "https://github.com/budcribar/FilmStudio/issues",
        },
        new()
        {
            Id = "gemini-3-pro",
            DisplayName = "Gemini 3 Pro",
            Capability = ModelCapability.Chat,
            Provider = ModelProviderFamily.Google,
            ApiBase = GoogleApiBase,
            EndpointPath = "models/gemini-3-pro:generateContent",
            RequiredEnvKeys = [GoogleApiKeyEnv],
            Notes = "Wired via GeminiChatClient, routed automatically through " +
                    "MultiProviderChatClient for planning/QA calls. Response-shape parsing is not " +
                    "smoke-tested against a live account yet.",
            FeatureRequestUrl = "https://github.com/budcribar/FilmStudio/issues",
        },

        // ── Vision (same chat models with image input; listed for QA config) ─
        new()
        {
            Id = "grok-4.5",
            DisplayName = "Grok 4.5 (vision)",
            Capability = ModelCapability.Vision,
            Provider = ModelProviderFamily.Xai,
            ApiBase = XaiApiBase,
            EndpointPath = "chat/completions",
            RequiredEnvKeys = [XaiApiKeyEnv],
            Notes = "Book plates / frame QA when wired.",
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
            Notes = "Wired for clip/frame review (CompleteWithImagesAsync) via " +
                    "MultiProviderVisionClient. Book-page OCR and cast classify still run on Grok " +
                    "only — those two methods are not implemented for Anthropic.",
            FeatureRequestUrl = "https://github.com/budcribar/FilmStudio/issues",
        },
        new()
        {
            Id = "gemini-3-pro",
            DisplayName = "Gemini 3 Pro (vision)",
            Capability = ModelCapability.Vision,
            Provider = ModelProviderFamily.Google,
            ApiBase = GoogleApiBase,
            EndpointPath = "models/gemini-3-pro:generateContent",
            RequiredEnvKeys = [GoogleApiKeyEnv],
            Notes = "Wired for clip/frame review (CompleteWithImagesAsync) via " +
                    "MultiProviderVisionClient. Book-page OCR and cast classify still run on Grok " +
                    "only — those two methods are not implemented for Gemini. Response-shape " +
                    "parsing is not smoke-tested against a live account yet.",
            FeatureRequestUrl = "https://github.com/budcribar/FilmStudio/issues",
        },

        // Film character voice samples use video (VOICE LOCK), not a separate TTS model.
    ];

    /// <summary>All catalog rows (enabled + disabled).</summary>
    public static IReadOnlyList<SupportedModelEntry> Entries => All;

    public static IReadOnlyList<SupportedModelEntry> ForCapability(
        ModelCapability capability,
        bool enabledOnly = true) =>
        All.Where(e => e.Capability == capability && (!enabledOnly || e.Enabled)).ToList();

    public static SupportedModelEntry? Find(string? modelId, ModelCapability? capability = null)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return null;
        var id = modelId.Trim();
        var exact = All.Where(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 0) return null;

        if (capability is not { } cap)
            return exact[0];

        var match = exact.FirstOrDefault(e => e.Capability == cap);
        if (match is not null) return match;

        // Only share Chat ↔ Vision for the same model id (e.g. grok-4.5).
        // Do not return a video model when the caller asked for chat/image/etc.
        if (cap is ModelCapability.Chat or ModelCapability.Vision)
        {
            return exact.FirstOrDefault(e =>
                e.Capability is ModelCapability.Chat or ModelCapability.Vision);
        }

        return null;
    }

    /// <summary>
    /// Resolve a configured model id for a capability, or a safe default.
    /// Unknown ids: keep the string (forward-compatible) but provider metadata falls back to Xai.
    /// </summary>
    public static SupportedModelEntry ResolveOrDefault(
        string? modelId,
        ModelCapability capability,
        string? fallbackId = null)
    {
        var hit = Find(modelId, capability);
        if (hit is not null) return hit;

        // Known id under a different capability (e.g. video id for chat) → do not keep that id.
        // Truly unknown id → keep the string (forward-compatible) with Xai defaults.
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
        Notes = "Not in master catalog — add via PR or track as GitHub feature request.",
    };

    /// <summary>Provider string for project config / cost UI.</summary>
    public static string ProviderIdFor(string? modelId, ModelCapability capability) =>
        ResolveOrDefault(modelId, capability).ProviderId;

    /// <summary>Missing env keys for this model (empty if ready).</summary>
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

    /// <summary>DTO list for API / Configuration UI.</summary>
    public static IReadOnlyList<SupportedModelDto> ToDtoList(bool enabledOnly = true) =>
        All.Where(e => !enabledOnly || e.Enabled)
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
        Notes = e.Notes,
        FeatureRequestUrl = e.FeatureRequestUrl,
        ProviderId = e.ProviderId,
    };
}

/// <summary>JSON-friendly model catalog row for the API and Web UI.</summary>
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
    public string? Notes { get; set; }
    public string? FeatureRequestUrl { get; set; }
    public string? ProviderId { get; set; }
}
