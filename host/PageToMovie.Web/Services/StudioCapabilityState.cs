using System.Text.Json;
using PageToMovie.Core.Models;

namespace PageToMovie.Web.Services;

/// <summary>
/// Whether optional studio features (music, voice clone, video review) have a selected model + key.
/// Used to gray out UI and deep-link to Settings focused on that capability.
/// </summary>
public sealed class StudioCapabilityState
{
    public event Action? Changed;

    public bool Loaded { get; private set; }
    public string? ProjectId { get; private set; }

    public bool MusicReady { get; private set; }
    public string MusicBlockedReason { get; private set; } = "Choose a music model in Settings.";
    public string MusicSettingsHref => "/configuration?focus=music#api-keys";

    public bool VoiceCloneReady { get; private set; }
    public string VoiceCloneBlockedReason { get; private set; } = "Add a voice clone key in Settings.";
    public string VoiceCloneSettingsHref => "/configuration?focus=voice#api-keys";

    public bool VideoReviewReady { get; private set; }
    public string VideoReviewBlockedReason { get; private set; } = "Choose a video review model in Settings.";
    public string VideoReviewSettingsHref => "/configuration?focus=review#api-keys";

    public async Task RefreshAsync(
        EngineApiClient engine,
        string? projectId,
        CancellationToken ct = default)
    {
        ProjectId = string.IsNullOrWhiteSpace(projectId) ? null : projectId.Trim();
        var musicReady = false;
        var musicReason = "Choose a background music model in Settings.";
        var voiceReady = false;
        var voiceReason = "Add a voice cloning key in Settings.";
        var reviewReady = false;
        var reviewReason = "Choose a video review model in Settings.";

        try
        {
            UserSettingsDto? settings = null;
            try { settings = await engine.GetUserSettingsAsync(ct).ConfigureAwait(false); }
            catch { /* offline */ }

            var keyByProvider = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (settings?.Providers is { Count: > 0 } providers)
            {
                foreach (var p in providers)
                {
                    if (!string.IsNullOrWhiteSpace(p.ProviderId))
                        keyByProvider[SupportedModelCatalog.NormalizeProviderId(p.ProviderId)] = p.IsConfigured;
                }
            }

            string audioModel = "none";
            string voiceModel = "none";
            string qualityModel = SupportedModelCatalog.DefaultModelIdForCapability("video-review")
                ?? SupportedModelCatalog.DefaultModelIdForCapability("chat")
                ?? "";
            if (!string.IsNullOrWhiteSpace(ProjectId))
            {
                try
                {
                    var cfgDto = await engine.GetConfigAsync(ProjectId, ct).ConfigureAwait(false);
                    var map = cfgDto?.Config;
                    if (map is not null)
                    {
                        audioModel = GetStr(map, "audio_model_name", "none");
                        voiceModel = GetStr(map, "voice_model_name", "none");
                        qualityModel = GetStr(map, "quality_model_name",
                            GetStr(map, "video_review_model_name", qualityModel));
                    }
                }
                catch { /* keep defaults */ }
            }

            // —— Music ——
            if (IsOff(audioModel))
            {
                musicReady = false;
                musicReason = "No music model selected. Open Settings to pick one for this project.";
            }
            else
            {
                var pid = ProviderFor(audioModel, ModelCapability.Audio);
                if (!string.IsNullOrEmpty(pid) && keyByProvider.TryGetValue(pid, out var has) && has)
                {
                    musicReady = true;
                    musicReason = "";
                }
                else
                {
                    musicReady = false;
                    musicReason = string.IsNullOrEmpty(pid)
                        ? "Music model needs a provider key. Open Settings to add it."
                        : $"Music needs a {DisplayProvider(pid)} key. Open Settings to add it.";
                }
            }

            // —— Voice clone ——
            // Ready when Voice cloning key is present. Model "none" means feature off until key + model.
            var hasEleven = keyByProvider.TryGetValue("elevenlabs", out var ek) && ek;
            if (IsOff(voiceModel))
            {
                // Treat missing model as blocked even if key exists — user asked for model selection.
                if (hasEleven)
                {
                    voiceReady = false;
                    voiceReason = "Voice clone key is set, but no voice model is selected. Open Settings → Voice clone clone.";
                }
                else
                {
                    voiceReady = false;
                    voiceReason = "Voice clone needs a model and key. Open Settings → Voice clone clone.";
                }
            }
            else
            {
                var vpid = ProviderFor(voiceModel, ModelCapability.Voice);
                if (string.IsNullOrEmpty(vpid))
                {
                    // Prefer a provider the user already has a key for.
                    if (hasEleven) vpid = "elevenlabs";
                    else if (keyByProvider.TryGetValue("fal", out var hasFal) && hasFal) vpid = "fal";
                    else vpid = "elevenlabs";
                }
                var hasKey = keyByProvider.TryGetValue(vpid, out var vk) && vk;
                if (hasKey)
                {
                    voiceReady = true;
                    voiceReason = "";
                }
                else
                {
                    voiceReady = false;
                    voiceReason = $"Voice clone needs a {DisplayProvider(vpid)} key. Open Settings to add it.";
                }
            }

            // —— Video review ——
            if (IsOff(qualityModel))
            {
                reviewReady = false;
                reviewReason = "No video review model selected. Open Settings to pick one.";
            }
            else
            {
                var rpid = ProviderFor(qualityModel, ModelCapability.Chat);
                if (string.IsNullOrEmpty(rpid))
                    rpid = ProviderFor(qualityModel, ModelCapability.Vision);
                if (string.IsNullOrEmpty(rpid)) rpid = "gemini";
                if (keyByProvider.TryGetValue(rpid, out var rh) && rh)
                {
                    reviewReady = true;
                    reviewReason = "";
                }
                else
                {
                    reviewReady = false;
                    reviewReason = $"Video review needs a {DisplayProvider(rpid)} key. Open Settings to add it.";
                }
            }
        }
        catch
        {
            // leave defaults
        }

        var changed =
            MusicReady != musicReady ||
            VoiceCloneReady != voiceReady ||
            VideoReviewReady != reviewReady ||
            !string.Equals(MusicBlockedReason, musicReason, StringComparison.Ordinal) ||
            !string.Equals(VoiceCloneBlockedReason, voiceReason, StringComparison.Ordinal) ||
            !string.Equals(VideoReviewBlockedReason, reviewReason, StringComparison.Ordinal) ||
            !Loaded;

        MusicReady = musicReady;
        MusicBlockedReason = musicReason;
        VoiceCloneReady = voiceReady;
        VoiceCloneBlockedReason = voiceReason;
        VideoReviewReady = reviewReady;
        VideoReviewBlockedReason = reviewReason;
        Loaded = true;

        if (changed)
            Changed?.Invoke();
    }

    private static bool IsOff(string? model) =>
        string.IsNullOrWhiteSpace(model)
        || model.Equals("none", StringComparison.OrdinalIgnoreCase)
        || model.Equals("disabled", StringComparison.OrdinalIgnoreCase);

    private static string ProviderFor(string modelId, ModelCapability cap)
    {
        try
        {
            return SupportedModelCatalog.NormalizeProviderId(
                SupportedModelCatalog.ProviderIdFor(modelId, cap));
        }
        catch { return ""; }
    }

    private static string DisplayProvider(string providerId) => providerId.ToLowerInvariant() switch
    {
        "grok" => "xAI / Grok",
        "gemini" => "Google Gemini",
        "fal" => "Fal.ai",
        "openai" => "OpenAI",
        "anthropic" => "Anthropic",
        "elevenlabs" => "Voice cloning",
        "suno" => "Suno",
        "aimusicapi" => "AI Music API",
        _ => providerId,
    };

    private static string GetStr(Dictionary<string, JsonElement> map, string key, string fallback)
    {
        if (!map.TryGetValue(key, out var el)) return fallback;
        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString();
            return string.IsNullOrWhiteSpace(s) ? fallback : s.Trim();
        }
        if (el.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
            return el.ToString();
        return fallback;
    }
}
