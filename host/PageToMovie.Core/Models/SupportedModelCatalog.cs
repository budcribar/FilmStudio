namespace PageToMovie.Core.Models;

/// <summary>
/// What the model is used for in Film Studio (drives Configuration dropdowns).
/// </summary>
public enum ModelCapability
{
    Video,
    Image,
    Chat,
    Vision,
    Audio,
    /// <summary>Voice clone / TTS (e.g. ElevenLabs, Fal.ai MiniMax) — dialogue personalization, not BGM. Covers both the clone step (see <see cref="SupportedModelEntry.IsVoiceCloneStep"/>) and the text-to-speech step.</summary>
    Voice,
    /// <summary>Video lip-sync: resync a finished clip's mouth movement to a separate audio track (Fal.ai / Sync Labs). Input is video+audio, not a text prompt — kept distinct from <see cref="Video"/>.</summary>
    LipSync,
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
    /// <summary>Suno via sunoapi.org (<c>SUNO_API_KEY</c>) — unofficial third-party Suno reseller.</summary>
    Suno = 4,
    /// <summary>Suno via aimusicapi.ai (<c>AIMUSICAPI_API_KEY</c>) — a different unofficial Suno reseller.</summary>
    AiMusicApi = 5,
    /// <summary>ElevenLabs (<c>ElevenLabs_API_KEY</c>) — voice clone + TTS for personal dialogue.</summary>
    ElevenLabs = 6,
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

    /// <summary>
    /// Maximum output tokens the model will generate in a single synchronous Messages API call
    /// (Chat / Vision only), i.e. the real per-model ceiling for the <c>max_tokens</c> request
    /// field. Null when not applicable or not yet confirmed against provider docs — callers must
    /// fall back to their own hardcoded default rather than guess. Sourced from provider docs;
    /// providers do raise these over time, so re-check before trusting an old number for a
    /// cost/quality-sensitive decision.
    /// </summary>
    public int? MaxOutputTokens { get; init; }

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

    /// <summary>
    /// USD charged once per video regardless of length, by resolution (Video only) — same key
    /// convention as <see cref="VideoCostPerSecondByResolution"/>. Total cost is
    /// <c>base + perSecond * durationSeconds</c>, so a provider that bills a flat fee per clip
    /// (e.g. Fal's Hunyuan/Wan, which price per generation, not per second, since they're
    /// frame-count-based) sets this and leaves the per-second rate at 0 — a genuinely
    /// duration-priced provider (Grok, Veo) leaves this null/0 and only sets the per-second rate,
    /// so existing behavior is unchanged for them. Modeling flat fees as a fake per-second rate
    /// (dividing by an assumed "typical" duration) silently produces the wrong number the moment a
    /// clip's actual duration differs from that assumption — this field avoids the whole problem
    /// by representing the real billing shape directly.
    /// </summary>
    public IReadOnlyDictionary<string, double>? VideoBaseCostByResolution { get; init; }

    /// <summary>USD per generated image (Image only). Null when not applicable.</summary>
    public double? ImageCostPerImage { get; init; }

    /// <summary>
    /// USD per reference/character image attached to a video generation call (Video only), when
    /// the vendor publishes this as a distinct line item separate from the flat per-second output
    /// rate. Null when not published/verified — <c>PageToMovie.Engine.CostReportService</c> then
    /// applies a small estimated fallback constant instead. As of 2026-08 no enabled video provider
    /// (including xAI's grok-imagine-video, checked against docs.x.ai/developers/pricing) itemizes
    /// reference images separately, so this is null for every catalog entry today.
    /// </summary>
    public double? VideoReferenceImageCost { get; init; }

    /// <summary>
    /// USD per second billed for a video-extend/continuation call (Video only; only meaningful when
    /// <see cref="SupportsVideoContinue"/> is true), when the vendor publishes an extend rate
    /// distinct from its base per-second generation rate. Null when not published/verified —
    /// <c>PageToMovie.Engine.CostReportService</c> then applies a small estimated fallback constant
    /// instead. As of 2026-08 xAI (the only enabled provider with <see cref="SupportsVideoContinue"/>
    /// true) has no published extend-specific rate on docs.x.ai/developers/pricing — grok-imagine-video
    /// is a flat $0.050/sec with no separate extend line item — so this is null for every catalog
    /// entry today.
    /// </summary>
    public double? VideoExtendCostPerSecond { get; init; }

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
    /// Real max reference images this model's API accepts (Video only) — a plain
    /// <see cref="SupportsReferenceImages"/> boolean isn't precise enough: Fal's Wan/HunyuanVideo
    /// accept exactly one init/reference image (true single-image i2v, not Grok-style multi-plate
    /// identity conditioning), so treating "supports" as "accepts up to N" would silently attach
    /// more images than the model can actually use. Null falls back to whatever the caller's own
    /// historical default was (today 7 for Grok in <c>GrokVideoClient</c>, 5 at the
    /// <c>FilmJobService</c> call site) until every model has a confirmed real number here.
    /// </summary>
    public int? MaxReferenceImages { get; init; }

    /// <summary>
    /// When true, accepts native MP4 video & audio files directly for clip/dialogue review (Google Gemini).
    /// </summary>
    public bool SupportsVideoReview { get; init; } = false;

    /// <summary>
    /// Shortest clip this model should be asked to generate (Video only). Null falls back to
    /// <see cref="PageToMovie.Engine.ClipDurationEstimator.MinSeconds"/> in <c>ClipDurationEstimator</c>.
    /// </summary>
    public int? MinClipDurationSeconds { get; init; }

    /// <summary>
    /// Soft cap for a single clip (Video only) — the duration/dialogue budget planner should split
    /// rather than exceed this. Null falls back to <c>ClipDurationEstimator.MaxSeconds</c>.
    /// </summary>
    public int? MaxClipDurationSeconds { get; init; }

    /// <summary>
    /// Absolute ceiling for a single clip even for big-action beats (Video only). Null falls back to
    /// <c>ClipDurationEstimator.AbsMaxSeconds</c>. Values below are today's known-safe defaults, not
    /// necessarily each provider's real published limit — confirm against provider docs before relying
    /// on a per-model number for a cost/quality-sensitive decision.
    /// </summary>
    public int? AbsMaxClipDurationSeconds { get; init; }

    /// <summary>
    /// Discrete set of durations this model accepts (Video only) — e.g. Veo 3.1 documents exactly
    /// 4, 6, or 8 seconds, not an arbitrary continuous range. When set, generation-time duration
    /// resolution must snap to the nearest value here rather than a plain min/max clamp (a clamped
    /// "7" is still not an accepted Veo duration). Null means the model accepts any value in
    /// [MinClipDurationSeconds, MaxClipDurationSeconds] (or the global defaults).
    /// </summary>
    public IReadOnlyList<int>? AllowedDurationsSeconds { get; init; }

    /// <summary>
    /// Tighter max duration specifically for image-to-video / reference-conditioned / video-extend
    /// generation (Video only) — some providers (Grok) allow a longer fresh text-to-video clip than
    /// they do for the "new portion" of a reference/continue call. Null means image/ref/continue
    /// modes use the same <see cref="MaxClipDurationSeconds"/> as fresh generation.
    /// </summary>
    public int? MaxExtensionSeconds { get; init; }

    /// <summary>
    /// Longest single-call duration this audio model will accept, in seconds (Audio only) — the
    /// generation-side counterpart to <see cref="MaxClipDurationSeconds"/> for video. Callers
    /// (FilmJobService's music job) generate this many seconds per segment and concatenate
    /// client-side for anything longer. Null when the provider doesn't document/enforce a duration
    /// control at all (the caller then treats one call as "whatever length comes back").
    /// </summary>
    public int? MaxAudioDurationSeconds { get; init; }

    /// <summary>
    /// Maximum character length for visual prompts passed to the API (Video/Image models).
    /// Null defaults to 4096 (Grok's budget). Models with tighter limits (e.g. Fal.ai / HunyuanVideo max 1000)
    /// specify their limit here so prompt builders automatically trim to fit without API 400 errors.
    /// </summary>
    public int? MaxPromptLength { get; init; }

    /// <summary>
    /// Maximum bounding dimension (in pixels) for reference images sent to the API.
    /// Null defaults to 1280px (optimal for HunyuanVideo / Veo 720p latent dimensions).
    /// </summary>
    public int? MaxReferenceImageDimension { get; init; }

    /// <summary>
    /// Diffusion sampling steps (Video/Image only) — the quality/speed knob Fal-hosted diffusion
    /// models expose as <c>num_inference_steps</c>. Null falls back to the calling client's own
    /// hardcoded default (30 in <c>FalVideoClient</c>).
    /// </summary>
    public int? NumInferenceSteps { get; init; }

    /// <summary>
    /// For Fal video models whose real generation API takes a discrete <c>num_frames</c> value
    /// rather than a continuous seconds-based duration (see the frame-count-native note on the
    /// <c>hunyuan-video</c> catalog entry) — the frame count <c>FalVideoClient</c> requests for
    /// clips at or under its short/long duration split (4s). Null falls back to the client's
    /// hardcoded default (85). Duration-native Fal models (e.g. Wan-2.1, which instead populate
    /// <see cref="MinClipDurationSeconds"/>/<see cref="MaxClipDurationSeconds"/>) leave this null.
    /// </summary>
    public int? ShortClipFrameCount { get; init; }

    /// <summary>
    /// Frame count <c>FalVideoClient</c> requests for clips over its short/long duration split
    /// (4s) — the counterpart to <see cref="ShortClipFrameCount"/>. Null falls back to the
    /// client's hardcoded default (129).
    /// </summary>
    public int? LongClipFrameCount { get; init; }

    /// <summary>
    /// Discrete set of aspect-ratio strings (e.g. <c>"16:9"</c>, <c>"9:16"</c>) this model's API
    /// actually accepts for generation (Video only), sourced from provider docs — mirrors
    /// <see cref="MaxReferenceImageDimension"/>-style per-model capability data rather than a
    /// continuous range, since providers document aspect ratio as a fixed enum. Null when not yet
    /// confirmed for this model; callers should keep sending their historical fixed value in that
    /// case rather than guessing.
    /// </summary>
    public IReadOnlyList<string>? SupportedAspectRatios { get; init; }

    /// <summary>
    /// Aspect ratio to request when the caller doesn't have a more specific one in mind (Video
    /// only). Should be a member of <see cref="SupportedAspectRatios"/> when both are set. Null
    /// falls back to the client's historical hardcoded default (<c>"16:9"</c>).
    /// </summary>
    public string? DefaultAspectRatio { get; init; }

    /// <summary>
    /// True for a clone-shaped Voice model (takes a reference audio sample, returns a provider voice
    /// id — e.g. Fal.ai <c>fal-ai/minimax/voice-clone</c>, ElevenLabs <c>eleven_voice_clone</c>).
    /// False (default) means a speak-shaped Voice model (takes text + a voice id/name, returns
    /// synthesized speech audio — e.g. <c>fal-ai/minimax/speech-02-hd</c>, <c>eleven_multilingual_v2</c>).
    /// Both shapes share <see cref="ModelCapability.Voice"/> since a caller resolving "the voice
    /// model" for a narration flow needs one clone-shaped and one speak-shaped entry, not two
    /// separate capability buckets — this flag disambiguates which is which. Only meaningful when
    /// <see cref="Capability"/> is <see cref="ModelCapability.Voice"/>.
    /// </summary>
    public bool IsVoiceCloneStep { get; init; }

    /// <summary>USD per voice-clone call (Voice, clone-shaped models only — see <see cref="IsVoiceCloneStep"/>). Null when not applicable/unconfirmed.</summary>
    public double? CostPerCloneUsd { get; init; }

    /// <summary>USD per 1,000 characters of synthesized speech (Voice, speak-shaped models only). Null when not applicable/unconfirmed.</summary>
    public double? CostPerThousandCharsUsd { get; init; }

    /// <summary>USD per minute of output video (LipSync only, flat rate — Sync Labs-style lip-sync models aren't priced per resolution like <see cref="VideoCostPerSecondByResolution"/>). Null when not applicable/unconfirmed.</summary>
    public double? CostPerMinuteUsd { get; init; }

    /// <summary>Raw provider string from models_catalog.json (e.g. OpenAI, DeepSeek, Grok, Gemini).</summary>
    public string ProviderName { get; init; } = "";

    /// <summary>Provider id for config / cost reports (<c>grok</c>, <c>gemini</c>, <c>anthropic</c>, <c>openai</c>).
    /// Prefers <see cref="ProviderName"/> only when it doesn't match a known <see cref="ModelProviderFamily"/>
    /// member (a genuinely custom/unrecognized provider, e.g. one hand-added via the catalog admin UI before a
    /// matching enum value exists) — models_catalog.json's "provider" field is normally just the literal enum
    /// name (e.g. "Xai", "Google"), which must still resolve to the friendly id below ("grok", "gemini") rather
    /// than being echoed back verbatim.</summary>
    public string ProviderId => !string.IsNullOrWhiteSpace(ProviderName) &&
        !Enum.TryParse<ModelProviderFamily>(ProviderName, true, out _)
        ? ProviderName.ToLowerInvariant()
        : Provider switch
        {
            ModelProviderFamily.Google => "gemini",
            ModelProviderFamily.Anthropic => "anthropic",
            ModelProviderFamily.Fal => "fal",
            ModelProviderFamily.Suno => "suno",
            ModelProviderFamily.AiMusicApi => "aimusicapi",
            ModelProviderFamily.ElevenLabs => "elevenlabs",
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
    /// <summary>Unofficial Suno reseller — no official public Suno API exists as of 2026-07.</summary>
    public const string SunoApiBase = "https://api.sunoapi.org/api/v1";
    public const string SunoApiKeyEnv = "SUNO_API_KEY";
    /// <summary>A different unofficial Suno reseller (formerly reached via the sunoapi.com redirect).</summary>
    public const string AiMusicApiBase = "https://api.aimusicapi.ai/api/v1";
    public const string AiMusicApiKeyEnv = "AIMUSICAPI_API_KEY";
    public const string ElevenLabsApiKeyEnv = "ElevenLabs_API_KEY";
    public const string ElevenLabsApiBase = "https://api.elevenlabs.io/v1";

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
        new() { Id = "voice", DisplayName = "Voice Clone / TTS", Description = "Personal voice clone from a short sample, then spoken dialogue or narration for the film.", Order = 70 },
        new() { Id = "lipsync", DisplayName = "Video Lip-Sync", Description = "Resyncs a generated clip's mouth movement to a separate dialogue or narration audio track.", Order = 80 },
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
        ["script_import"] = new() { "claude-sonnet-5", "grok-4.5", "gemini-2.5-flash" },
        ["beat_pacing"] = new() { "grok-4.5", "gemini-2.0-flash", "claude-sonnet-5" },
        ["camera_director"] = new() { "grok-4.5", "claude-sonnet-5" },
        ["sound_design"] = new() { "grok-4.5", "gemini-2.5-flash" },
        ["cast_analysis"] = new() { "grok-4.5", "claude-sonnet-5", "gemini-2.5-flash" },
        ["video_review"] = new() { "gemini-2.5-flash", "grok-4.5" },
    };

    /// <summary>All catalog rows, loaded from models_catalog.json (shipped copy or /data override —
    /// see GetCandidateCatalogPaths). Throws via EnsureLoaded if no usable catalog file exists.</summary>
    public static IReadOnlyList<SupportedModelEntry> Entries
    {
        get
        {
            EnsureLoaded();
            return _loadedEntries!;
        }
    }

    /// <summary>
    /// Where a catalog JSON file could live, checked in priority order — a persistent-volume
    /// runtime override (Railway's <c>/data</c> mount) first, then the copy shipped alongside the
    /// app's own binaries (see PageToMovie.Core.csproj's CopyToOutputDirectory item), then a couple
    /// of dev-time working-directory fallbacks. Shared by <see cref="GetCatalogFilePath"/> and
    /// <see cref="EnsureLoaded"/> so there's exactly one list to keep correct, not two.
    /// </summary>
    private static IEnumerable<string> GetCandidateCatalogPaths(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
            yield return customPath;
        yield return "/data/models_catalog.json";
        yield return Path.Combine(AppContext.BaseDirectory, "config", "models_catalog.json");
        yield return Path.Combine(AppContext.BaseDirectory, "models_catalog.json");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "config", "models_catalog.json");
        yield return Path.Combine(Directory.GetCurrentDirectory(), "host", "PageToMovie.Core", "config", "models_catalog.json");
    }

    public static string GetCatalogFilePath()
    {
        var candidates = GetCandidateCatalogPaths().ToList();
        return candidates.FirstOrDefault(File.Exists) ?? candidates[^1];
    }

    /// <summary>
    /// True when <paramref name="rawJson"/> is structurally usable by <see cref="EnsureLoaded"/> —
    /// either an object with a non-empty "models" array, or a bare non-empty array of model entries.
    /// Pulled out of <see cref="SaveCatalogJson"/> so it's testable without touching the filesystem.
    /// </summary>
    public static bool IsUsableCatalogJson(string rawJson)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            return root.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Object => root.TryGetProperty("models", out var modelsEl) &&
                    modelsEl.ValueKind == System.Text.Json.JsonValueKind.Array && modelsEl.GetArrayLength() > 0,
                System.Text.Json.JsonValueKind.Array => root.GetArrayLength() > 0,
                _ => false,
            };
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    public static void SaveCatalogJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            throw new ArgumentException("Catalog JSON payload cannot be empty", nameof(rawJson));

        // Reject anything EnsureLoaded wouldn't actually accept — without this, a structurally
        // invalid save (e.g. a top-level object with no "models" array) still overwrote the
        // previous good catalog file, and the next reload silently fell through every candidate
        if (!IsUsableCatalogJson(rawJson))
            throw new ArgumentException("Catalog JSON payload must be non-empty valid JSON with a non-empty 'models' array.", nameof(rawJson));

        var targetPath = GetCatalogFilePath();
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

        File.WriteAllText(targetPath, rawJson);
        ReloadCatalog(targetPath);
    }

    public static void ReloadCatalog(string? overrideJsonPath = null)
    {
        _loadedEntries = null;
        _loadedCapabilities = null;
        _loadedTaskRankings = null;
        EnsureLoaded(overrideJsonPath);
    }

    /// <summary>Parse catalog JSON into static fields. Returns true on success.</summary>
    public static bool TryLoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
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
                    return true;
                }
            }
            else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var dtos = System.Text.Json.JsonSerializer.Deserialize<List<SupportedModelDto>>(json, opts);
                if (dtos is { Count: > 0 })
                {
                    _loadedEntries = dtos.Select(FromDto).ToList();
                    _loadedCapabilities = DefaultCapabilityDefinitions;
                    _loadedTaskRankings = DefaultTaskRankings;
                    return true;
                }
            }
        }
        catch
        {
            // leave unloaded
        }
        return false;
    }

    /// <summary>True when the browser (or any host) has successfully loaded a catalog into memory.</summary>
    public static bool IsLoaded => _loadedEntries is { Count: > 0 };

    private static void EnsureLoaded(string? customPath = null)
    {
        if (_loadedEntries is not null && _loadedCapabilities is not null) return;

        var candidates = GetCandidateCatalogPaths(customPath).ToList();

        foreach (var path in candidates.Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)))
        {
            try
            {
                if (TryLoadFromJson(File.ReadAllText(path)))
                    return;
            }
            catch
            {
                // Ignore IO/parse failures and try the next candidate.
            }
        }

        // Blazor WebAssembly (and any host without the file next to the binary): load the
        // catalog embedded in PageToMovie.Core so Configuration / rate UI can render.
        try
        {
            var asm = typeof(SupportedModelCatalog).Assembly;
            foreach (var resourceName in asm.GetManifestResourceNames()
                         .Where(n => n.EndsWith("models_catalog.json", StringComparison.OrdinalIgnoreCase)))
            {
                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream is null) continue;
                using var reader = new StreamReader(stream);
                if (TryLoadFromJson(reader.ReadToEnd()))
                    return;
            }
        }
        catch
        {
            // fall through
        }

        // Browser has no /data or AppContext catalog path. Soft-load an empty shell so
        // Configuration can render; LoadCatalogAsync then hydrates via /api/models/catalog-json.
        if (OperatingSystem.IsBrowser())
        {
            _loadedEntries = new List<SupportedModelEntry>();
            _loadedCapabilities = DefaultCapabilityDefinitions;
            _loadedTaskRankings = DefaultTaskRankings;
            return;
        }

        throw new InvalidOperationException(
            "No usable models catalog found. Checked: " + string.Join(", ", candidates) +
            ", embedded:PageToMovie.Core.config.models_catalog.json" +
            ". Expected an object with a non-empty \"models\" array, or a non-empty array of model entries.");
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

        var capDef = RegisteredCapabilities.FirstOrDefault(c => string.Equals(c.Id, capability.ToString(), StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(capDef?.DefaultModelId))
        {
            hit = Find(capDef.DefaultModelId, capability);
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

    /// <summary>
    /// Build Configuration "API keys" rows from the catalog (enabled models only).
    /// Personal/server key flags are left false — server fills those from SQLite/env.
    /// </summary>
    public static List<ProviderKeyStatusDto> BuildProviderKeyRows()
    {
        var groups = Entries
            .Where(e => e.Enabled && e.RequiredEnvKeys is { Count: > 0 })
            .GroupBy(e => NormalizeProviderId(e.ProviderId), StringComparer.OrdinalIgnoreCase);

        var rows = new List<ProviderKeyStatusDto>();
        foreach (var group in groups.OrderBy(g => DisplayOrder(g.Key)))
        {
            var pId = group.Key;
            if (string.IsNullOrWhiteSpace(pId) || pId is "none") continue;
            var sample = group.First();
            var required = group.SelectMany(m => m.RequiredEnvKeys).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (required.Count == 0) continue;

            var supportsVideoGen = group.Any(m => m.Capability == ModelCapability.Video);
            var supportsVideoReview = group.Any(m => m.SupportsVideoReview);
            var supportsImageGen = group.Any(m => m.Capability == ModelCapability.Image);
            var supportsScriptPlanning = group.Any(m => m.Capability == ModelCapability.Chat);
            var supportsImageVision = group.Any(m => m.Capability == ModelCapability.Vision);
            var supportsAudio = group.Any(m => m.Capability == ModelCapability.Audio);
            var supportsVoice = group.Any(m => m.Capability == ModelCapability.Voice);
            var supportsLipSync = group.Any(m => m.Capability == ModelCapability.LipSync);

            var caps = new List<string>();
            if (supportsVideoGen) caps.Add("Video Gen");
            if (supportsVideoReview) caps.Add("Video Review");
            if (supportsImageGen) caps.Add("Image Gen");
            if (supportsScriptPlanning) caps.Add("Script & Planning");
            if (supportsImageVision) caps.Add("Image Vision / OCR");
            if (supportsAudio) caps.Add("Audio / Music");
            if (supportsVoice) caps.Add("Voice clone / TTS");
            if (supportsLipSync) caps.Add("Lip-sync");

            rows.Add(new ProviderKeyStatusDto
            {
                ProviderId = pId,
                DisplayName = DisplayNameForProvider(pId, sample),
                Family = string.IsNullOrWhiteSpace(sample.ProviderName) ? sample.Provider.ToString() : sample.ProviderName,
                ActiveSource = "none",
                CapabilitiesSummary = caps.Count > 0 ? string.Join(", ", caps) : "—",
                SupportsVideo = supportsVideoGen || supportsVideoReview,
                SupportsImage = supportsImageGen,
                SupportsChat = supportsScriptPlanning,
                SupportsVision = supportsImageVision,
                SupportsVideoGen = supportsVideoGen,
                SupportsVideoReview = supportsVideoReview,
                SupportsImageGen = supportsImageGen,
                SupportsScriptPlanning = supportsScriptPlanning,
                SupportsImageVision = supportsImageVision,
                RequiredEnvKeys = required,
                // Provider cards are for API keys — never dump per-model engineering notes here.
                Notes = ShortProviderBlurb(pId),
            });
        }
        return rows;
    }

    public static string NormalizeProviderId(string? providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId)) return "";
        var p = providerId.Trim().ToLowerInvariant();
        return p switch
        {
            "xai" or "grok" => "grok",
            "google" or "gemini" => "gemini",
            "claude" or "anthropic" => "anthropic",
            "fal" or "fal.ai" => "fal",
            "openai" or "oai" => "openai",
            "suno" => "suno",
            "aimusicapi" or "ai-music-api" => "aimusicapi",
            "elevenlabs" or "eleven" => "elevenlabs",
            _ => p,
        };
    }

    private static string DisplayNameForProvider(string pId, SupportedModelEntry sample) => pId switch
    {
        "grok" => "xAI / Grok",
        "gemini" => "Google Gemini",
        "anthropic" => "Anthropic Claude",
        "fal" => "Fal.ai",
        "openai" => "OpenAI",
        "suno" => "Suno",
        "aimusicapi" => "AI Music API",
        "elevenlabs" => "Voice cloning",
        _ => !string.IsNullOrWhiteSpace(sample.ProviderName)
            ? sample.ProviderName
            : char.ToUpperInvariant(pId[0]) + pId[1..],
    };

    private static string? ShortProviderBlurb(string pId) => pId switch
    {
        "grok" => "Video, image, script, and vision for the main studio pipeline.",
        "gemini" => "Video review (MP4), Veo video gen, image, and planning.",
        "openai" => "Script & planning (chat). Not for video or image generation.",
        "anthropic" => "Script & planning and image vision. Not for video or image gen.",
        "fal" => "Open-source video/image (and some audio) via Fal serverless.",
        "suno" or "aimusicapi" => "Background music generation.",
        "elevenlabs" => "Record a short sample to personalize character dialogue.",
        _ => null,
    };

    private static int DisplayOrder(string pId) => pId switch
    {
        "grok" => 0,
        "gemini" => 1,
        "openai" => 2,
        "anthropic" => 3,
        "fal" => 4,
        "suno" => 5,
        "aimusicapi" => 6,
        "elevenlabs" => 7,
        _ => 50,
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
        MaxOutputTokens = e.MaxOutputTokens,
        InputCostPerMillionTokens = e.InputCostPerMillionTokens,
        OutputCostPerMillionTokens = e.OutputCostPerMillionTokens,
        VideoCostPerSecondByResolution = e.VideoCostPerSecondByResolution is { } v
            ? new Dictionary<string, double>(v)
            : null,
        VideoBaseCostByResolution = e.VideoBaseCostByResolution is { } vb
            ? new Dictionary<string, double>(vb)
            : null,
        ImageCostPerImage = e.ImageCostPerImage,
        VideoReferenceImageCost = e.VideoReferenceImageCost,
        VideoExtendCostPerSecond = e.VideoExtendCostPerSecond,
        Notes = e.Notes,
        FeatureRequestUrl = e.FeatureRequestUrl,
        ProviderId = e.ProviderId,
        SupportsVideoContinue = e.SupportsVideoContinue,
        SupportsReferenceImages = e.SupportsReferenceImages,
        MaxReferenceImages = e.MaxReferenceImages,
        SupportsVideoReview = e.SupportsVideoReview,
        MinClipDurationSeconds = e.MinClipDurationSeconds,
        MaxClipDurationSeconds = e.MaxClipDurationSeconds,
        AbsMaxClipDurationSeconds = e.AbsMaxClipDurationSeconds,
        AllowedDurationsSeconds = e.AllowedDurationsSeconds is { } ad ? new List<int>(ad) : null,
        MaxExtensionSeconds = e.MaxExtensionSeconds,
        MaxAudioDurationSeconds = e.MaxAudioDurationSeconds,
        NumInferenceSteps = e.NumInferenceSteps,
        ShortClipFrameCount = e.ShortClipFrameCount,
        LongClipFrameCount = e.LongClipFrameCount,
        SupportedAspectRatios = e.SupportedAspectRatios is { } sar ? sar.ToList() : null,
        DefaultAspectRatio = e.DefaultAspectRatio,
        MaxPromptLength = e.MaxPromptLength,
        IsVoiceCloneStep = e.IsVoiceCloneStep,
        CostPerCloneUsd = e.CostPerCloneUsd,
        CostPerThousandCharsUsd = e.CostPerThousandCharsUsd,
        CostPerMinuteUsd = e.CostPerMinuteUsd,
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
        MaxOutputTokens = d.MaxOutputTokens,
        InputCostPerMillionTokens = d.InputCostPerMillionTokens,
        OutputCostPerMillionTokens = d.OutputCostPerMillionTokens,
        VideoCostPerSecondByResolution = d.VideoCostPerSecondByResolution,
        VideoBaseCostByResolution = d.VideoBaseCostByResolution,
        ImageCostPerImage = d.ImageCostPerImage,
        VideoReferenceImageCost = d.VideoReferenceImageCost,
        VideoExtendCostPerSecond = d.VideoExtendCostPerSecond,
        Notes = d.Notes,
        FeatureRequestUrl = d.FeatureRequestUrl,
        SupportsVideoContinue = d.SupportsVideoContinue,
        SupportsReferenceImages = d.SupportsReferenceImages,
        MaxReferenceImages = d.MaxReferenceImages,
        SupportsVideoReview = d.SupportsVideoReview,
        MinClipDurationSeconds = d.MinClipDurationSeconds,
        MaxClipDurationSeconds = d.MaxClipDurationSeconds,
        AbsMaxClipDurationSeconds = d.AbsMaxClipDurationSeconds,
        AllowedDurationsSeconds = d.AllowedDurationsSeconds,
        MaxExtensionSeconds = d.MaxExtensionSeconds,
        MaxAudioDurationSeconds = d.MaxAudioDurationSeconds,
        NumInferenceSteps = d.NumInferenceSteps,
        ShortClipFrameCount = d.ShortClipFrameCount,
        LongClipFrameCount = d.LongClipFrameCount,
        SupportedAspectRatios = d.SupportedAspectRatios,
        DefaultAspectRatio = d.DefaultAspectRatio,
        MaxPromptLength = d.MaxPromptLength,
        IsVoiceCloneStep = d.IsVoiceCloneStep,
        CostPerCloneUsd = d.CostPerCloneUsd,
        CostPerThousandCharsUsd = d.CostPerThousandCharsUsd,
        CostPerMinuteUsd = d.CostPerMinuteUsd,
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
    public int? MaxOutputTokens { get; set; }
    public double? InputCostPerMillionTokens { get; set; }
    public double? OutputCostPerMillionTokens { get; set; }
    public Dictionary<string, double>? VideoCostPerSecondByResolution { get; set; }
    public Dictionary<string, double>? VideoBaseCostByResolution { get; set; }
    public double? ImageCostPerImage { get; set; }
    public double? VideoReferenceImageCost { get; set; }
    public double? VideoExtendCostPerSecond { get; set; }
    public string? Notes { get; set; }
    public string? FeatureRequestUrl { get; set; }
    public string? ProviderId { get; set; }
    public bool SupportsVideoContinue { get; set; } = true;
    public bool SupportsReferenceImages { get; set; } = true;
    public int? MaxReferenceImages { get; set; }
    public bool SupportsVideoReview { get; set; }
    public int? MinClipDurationSeconds { get; set; }
    public int? MaxClipDurationSeconds { get; set; }
    public int? AbsMaxClipDurationSeconds { get; set; }
    public List<int>? AllowedDurationsSeconds { get; set; }
    public int? MaxExtensionSeconds { get; set; }
    public int? MaxAudioDurationSeconds { get; set; }
    public int? NumInferenceSteps { get; set; }
    public int? ShortClipFrameCount { get; set; }
    public int? LongClipFrameCount { get; set; }
    public List<string>? SupportedAspectRatios { get; set; }
    public string? DefaultAspectRatio { get; set; }
    public int? MaxPromptLength { get; set; }
    public bool IsVoiceCloneStep { get; set; }
    public double? CostPerCloneUsd { get; set; }
    public double? CostPerThousandCharsUsd { get; set; }
    public double? CostPerMinuteUsd { get; set; }
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
