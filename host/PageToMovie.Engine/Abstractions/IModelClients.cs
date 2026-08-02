namespace PageToMovie.Engine.Abstractions;

/// <summary>Video generate / poll / download. Grok, Gemini (Veo), or fake — see MultiProviderVideoClient.</summary>
public interface IVideoClient
{
    bool IsConfigured { get; }

    /// <param name="referenceImagePaths">
    /// Character/style refs for reference-to-video (<c>reference_images</c>).
    /// Prompt should use <c>&lt;IMAGE_1&gt;</c>… tags.
    /// Mutually exclusive with start-frame / video-continue modes.
    /// </param>
    /// <param name="startFrameImagePath">
    /// Optional still used as the first frame (image-to-video).
    /// Prefer <paramref name="continueFromVideoPath"/> for true clip-to-clip continue.
    /// </param>
    /// <param name="continueFromVideoPath">
    /// Local path to previous clip video. Uses Imagine <c>/videos/extensions</c>
    /// (continue from last frame with the new prompt). Result is prev+extension;
    /// caller should trim the new portion for a per-clip file.
    /// </param>
    Task<string> SubmitGenerationAsync(
        string prompt,
        int durationSeconds,
        string resolution,
        string model,
        CancellationToken ct,
        IReadOnlyList<string>? referenceImagePaths = null,
        string? startFrameImagePath = null,
        string? continueFromVideoPath = null);

    Task<string> PollForVideoUrlAsync(
        string requestId,
        Action<string>? onProgress,
        CancellationToken ct);

    Task DownloadToFileAsync(string url, string destPath, CancellationToken ct);
}

/// <summary>Image generate / edit. Grok, Gemini, or fake — see MultiProviderImageClient.</summary>
public interface IImageClient
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<byte[]>> GenerateVariantsAsync(
        string prompt,
        int n = 3,
        string aspectRatio = "1:1",
        string? model = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<byte[]>> EditVariantsAsync(
        string prompt,
        IReadOnlyList<string> referenceImagePaths,
        int n = 3,
        string aspectRatio = "1:1",
        string? model = null,
        int maxRefs = 0,
        string? costumeRefPath = null,
        bool illustratedMedium = true,
        Action<string>? onProgress = null,
        CancellationToken ct = default);
}

/// <summary>Chat completions. Grok, Anthropic, Gemini, or fake — see MultiProviderChatClient.</summary>
public interface IChatClient
{
    bool IsConfigured { get; }

    /// <param name="mode">
    /// Telemetry tag for <c>api_calls.jsonl</c> (<c>ApiCallTelemetry.Mode</c>), e.g.
    /// <c>book_to_fountain</c>, <c>cast_from_screenplay</c>, <c>cast_visual_literalize</c>.
    /// </param>
    /// <param name="reasoningEffort">
    /// Provider-neutral reasoning/thinking intensity hint: <c>"low"</c>, <c>"medium"</c>,
    /// <c>"high"</c>, or <c>"max"</c>. Null (default) leaves each provider's own default
    /// behavior untouched. Each concrete client translates this to its own API shape
    /// (OpenAI/xAI <c>reasoning_effort</c>, Anthropic <c>thinking</c>+<c>output_config.effort</c>,
    /// Gemini <c>thinkingConfig.thinkingLevel</c>) and self-heals by retrying without it if the
    /// requested model doesn't support the parameter at all, rather than requiring callers to
    /// know which models are reasoning-capable.
    /// </param>
    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        string model = "",
        double temperature = 0.2,
        CancellationToken ct = default,
        string? mode = null,
        string? reasoningEffort = null);
}

/// <summary>Canonical <see cref="IChatClient.CompleteAsync"/> mode tags for telemetry.</summary>
public static class ChatCallModes
{
    public const string PromptImprovementReview = "prompt_improvement_review";
    public const string SidecarPlanning = "sidecar_planning";
    public const string BookToFountain = "book_to_fountain";
    public const string BookToFountainRetry = "book_to_fountain_retry";
    public const string BookToFountainCoverage = "book_to_fountain_coverage";
    public const string BookToFountainChunk = "book_to_fountain_chunk";
    public const string BookToFountainChunkRetry = "book_to_fountain_chunk_retry";
    public const string BookToFountainMerge = "book_to_fountain_merge";
    public const string BookToFountainLocationsRetry = "book_to_fountain_locations_retry";
    public const string BookToFountainSpeakersRetry = "book_to_fountain_speakers_retry";
    public const string CastFromScreenplay = "cast_from_screenplay";
    public const string VisionMetaAdaptation = "vision_meta_adaptation";
    public const string CastVisualLiteralize = "cast_visual_literalize";
    public const string LearningPropose = "learning_propose";
    public const string SilentBeatClassify = "silent_beat_classify";
    public const string AmbientSfxClassify = "ambient_sfx_classify";
    public const string OnScreenCastClassify = "onscreen_cast_classify";
    public const string ExtendCutClassify = "extend_cut_classify";
    public const string SpeciesKindClassify = "species_kind_classify";
    public const string PlateRankClassify = "plate_rank_classify";
    public const string ShotPlanRefineClassify = "shot_plan_refine_classify";
    public const string BeatPacingClassify = "beat_pacing_classify";
    public const string CinematicLightingClassify = "cinematic_lighting_classify";
    public const string CameraDirectorClassify = "camera_director_classify";
    public const string NegativePromptClassify = "negative_prompt_classify";
    public const string WardrobeContinuityClassify = "wardrobe_continuity_classify";
    public const string CharacterEmotionArcClassify = "character_emotion_arc_classify";
    public const string SoundDesignComposerClassify = "sound_design_composer_classify";
    public const string DepthOfFieldClassify = "depth_of_field_classify";
    public const string ColorPaletteGradingClassify = "color_palette_grading_classify";
}

/// <summary>
/// Vision (transcribe / classify / multi-image completion). Grok, or fake; Anthropic and Gemini
/// only implement <see cref="CompleteWithImagesAsync"/> — see MultiProviderVisionClient.
/// </summary>
public interface IVisionClient
{
    bool IsConfigured { get; }

    Task<string> TranscribePageAsync(
        string imagePath,
        int page,
        string model = "",
        CancellationToken ct = default);

    Task<CharacterPageClassification> ClassifyCharactersOnImageAsync(
        string imagePath,
        int page,
        IReadOnlyList<CharacterClassifyHint> cast,
        string model = "",
        CancellationToken ct = default);

    /// <summary>
    /// Multi-image vision completion (clip auto-review: prev tail + current frames).
    /// Returns model text (JSON expected by caller).
    /// </summary>
    Task<string> CompleteWithImagesAsync(
        string prompt,
        IReadOnlyList<string> imagePaths,
        string model = "",
        string detail = "low",
        CancellationToken ct = default);
}

/// <summary>
/// Native multimodal video+audio analysis — Gemini specifically can inline a raw video file
/// (with its audio track) for the model to watch/listen to, unlike other vision providers which
/// only accept still images (see ClipDialogueVerificationService, the only caller: it needs
/// Gemini's real video capability specifically, not whatever <see cref="IVisionClient"/>'s
/// routing config currently points at). Kept as its own interface rather than reusing
/// <see cref="IVisionClient"/> so a caller that specifically needs Gemini can depend on a
/// distinct DI seam (GeminiChatClient implements both) — and so fakes mode has something to
/// fake instead of this silently resolving to null.
/// </summary>
public interface IGeminiVideoAnalysisClient
{
    bool IsConfigured { get; }

    Task<string> CompleteWithImagesAsync(
        string prompt,
        IReadOnlyList<string> imagePaths,
        string model = "",
        string detail = "low",
        CancellationToken ct = default);
}

/// <summary>Audio &amp; music generate / download (Fal.ai, Suno resellers, or fake).</summary>
public interface IAudioClient
{
    bool IsConfigured { get; }

    /// <summary>
    /// Returns the provider's audio URL, not downloaded bytes — mirrors
    /// IVideoClient.PollForVideoUrlAsync so the caller can hand it straight to
    /// MediaProxyTicketStore/ClientMediaUrl for client-side download, the same as video. The key
    /// never leaves the API host; only the short-lived provider URL does. Duration limits live on
    /// SupportedModelEntry.MaxAudioDurationSeconds (per-model, resolved by the caller via
    /// SupportedModelCatalog) rather than on this interface — a fixed per-client value doesn't
    /// generalize once <paramref name="model"/> selects the provider per call.
    /// </summary>
    /// <param name="onProgress">
    /// Optional status callback for slow providers (Suno-family generation runs 1-3+ minutes via
    /// internal polling) so the caller can surface progress in a job log. Fast providers (Fal.ai)
    /// may ignore it.
    /// </param>
    /// <param name="isVocal">
    /// True to request sung vocals instead of an instrumental track. Only the Suno-family providers
    /// (SunoClient, AiMusicApiClient) can honor this — Fal.ai Stable Audio has no vocal capability and
    /// always generates instrumental regardless of this flag.
    /// </param>
    /// <param name="lyrics">Lyrics text to sing, required by Suno-family providers when <paramref name="isVocal"/> is true; ignored otherwise.</param>
    Task<string?> GenerateMusicTrackAsync(
        string prompt,
        int durationSeconds,
        string? model = null,
        CancellationToken ct = default,
        Action<string>? onProgress = null,
        bool isVocal = false,
        string? lyrics = null);
}

/// <summary>
/// Video lip-sync: given a finished clip and a separate dialogue/narration audio track, produce a
/// new video whose mouth movement matches that audio (Fal.ai / Sync Labs). A genuinely different
/// shape from <see cref="IVideoClient"/> (video+audio in, video out — not a text prompt), so it gets
/// its own interface rather than overloading video generation. Same request/response contract as
/// <see cref="IAudioClient.GenerateMusicTrackAsync"/> (single awaitable call, provider URL out, no
/// bytes downloaded server-side) since the underlying provider call — while queue-based — completes
/// in the time span of a single HTTP request for a short clip, unlike multi-minute video generation.
/// Explicit, human-triggered only (per-clip action) — must never be wired into any automatic job or
/// pipeline stage; every call spends real provider money.
/// </summary>
public interface ILipSyncClient
{
    bool IsConfigured { get; }

    /// <param name="videoPath">Local path to the source clip (already on the API host — callers upload it first, same on-demand transfer pattern as scene-continuation video).</param>
    /// <param name="audioPath">Local path to the dialogue/narration audio track to sync to.</param>
    /// <param name="model">Catalog model id (<see cref="PageToMovie.Core.Models.ModelCapability.LipSync"/>). Null resolves the catalog default.</param>
    /// <param name="syncMode">How to reconcile a video/audio duration mismatch: <c>cut_off</c> (default), <c>loop</c>, <c>bounce</c>, <c>silence</c>, or <c>remap</c> — passed straight through to the provider.</param>
    /// <returns>Provider URL for the resulting lip-synced video, or null if the call failed.</returns>
    Task<string?> GenerateLipSyncAsync(
        string videoPath,
        string audioPath,
        string? model = null,
        string syncMode = "cut_off",
        Action<string>? onProgress = null,
        CancellationToken ct = default);
}

/// <summary>
/// Voice-clone narration/dialogue: clone a voice from a short reference sample, then synthesize
/// speech audio for arbitrary text in that voice (Fal.ai / MiniMax). Two explicit steps mirroring
/// the underlying provider's own API shape (clone once → reuse the returned voice id across many
/// speak calls) rather than one combined call, so a caller only pays the (larger, one-time) clone
/// cost once per sample. Deliberately its own interface, not folded into <see cref="IAudioClient"/>:
/// that interface is shaped for one text-prompt-to-music-track call, while this is a stateful
/// two-step "identity, then arbitrary text" flow closer to <see cref="IVideoClient"/>'s
/// reference-image pattern than to music generation. Explicit, human-triggered only — never runs
/// automatically as part of any job/pipeline; every call spends real provider money.
/// </summary>
public interface IVoiceCloneClient
{
    bool IsConfigured { get; }

    /// <param name="sampleAudioPath">Local path to the reference voice sample (provider requires >=10s of clean speech).</param>
    /// <param name="model">Catalog model id for the clone step. Null resolves the catalog default clone-shaped model.</param>
    /// <returns>Provider voice id (e.g. MiniMax <c>custom_voice_id</c>) to pass to <see cref="SynthesizeSpeechAsync"/>, or null if the call failed.</returns>
    Task<string?> CloneVoiceAsync(
        string sampleAudioPath,
        string? model = null,
        CancellationToken ct = default);

    /// <param name="text">Narration/dialogue text to speak. Callers should check the resolved model's <c>MaxPromptLength</c> (catalog) before calling — long-form narration (e.g. a whole book chapter) is the caller's responsibility to split into multiple calls; this client does not chunk automatically.</param>
    /// <param name="voiceId">Provider voice id previously returned by <see cref="CloneVoiceAsync"/>.</param>
    /// <param name="model">Catalog model id for the speak step. Null resolves the catalog default speak-shaped model.</param>
    /// <returns>Provider URL for the synthesized speech audio, or null if the call failed.</returns>
    Task<string?> SynthesizeSpeechAsync(
        string text,
        string voiceId,
        string? model = null,
        CancellationToken ct = default);
}
