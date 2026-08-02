using System.Net.Http.Headers;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Single apply-voice entry: resolves the project's voice model from catalog, then routes to
/// ElevenLabs (<see cref="IVoiceClient"/>) or Fal MiniMax (<see cref="IVoiceCloneClient"/>).
/// Writes the same seed fields either way so speak / film paths can read one id.
/// </summary>
public sealed class VoiceCloneApplyService
{
    private readonly ProjectStore _projects;
    private readonly IVoiceClient _eleven;
    private readonly IVoiceCloneClient _fal;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ProjectTelemetryService? _telemetry;
    private readonly ILogger<VoiceCloneApplyService> _log;

    public VoiceCloneApplyService(
        ProjectStore projects,
        IVoiceClient eleven,
        IVoiceCloneClient fal,
        IHttpClientFactory httpFactory,
        ILogger<VoiceCloneApplyService> log,
        ProjectTelemetryService? telemetry = null)
    {
        _projects = projects;
        _eleven = eleven;
        _fal = fal;
        _httpFactory = httpFactory;
        _log = log;
        _telemetry = telemetry;
    }

    public bool IsConfigured =>
        _eleven.IsConfigured || _fal.IsConfigured;

    /// <summary>
    /// Create a provider clone from the character's saved sample (or supplied bytes),
    /// store provider voice id on the seed, update voice_profile/label, optional TTS preview.
    /// </summary>
    public async Task<VoiceApplyResult> ApplyFromSampleAsync(
        string projectId,
        string charKey,
        byte[]? sampleOverride = null,
        string? sampleFileName = null,
        string? previewText = null,
        string? voiceLabel = null,
        string? modelOverride = null,
        CancellationToken ct = default)
    {
        await _projects.RequireProjectAsync(projectId, ct).ConfigureAwait(false);
        var display = charKey.Replace("Character_", "", StringComparison.OrdinalIgnoreCase).Replace('_', ' ');

        // Ensure sample on disk (shared path for both providers)
        string samplePath;
        if (sampleOverride is { Length: > 0 })
        {
            var fileName = string.IsNullOrWhiteSpace(sampleFileName) ? "voice_clone_sample.wav" : sampleFileName!;
            await using var ms = new MemoryStream(sampleOverride);
            samplePath = await _projects.SaveVoiceCloneSampleAsync(projectId, charKey, ms, fileName, ct)
                .ConfigureAwait(false);
        }
        else
        {
            samplePath = _projects.GetVoiceCloneSamplePath(projectId, charKey);
            if (!File.Exists(samplePath))
            {
                var seed = MockToneWav.Sine(2.2, 210);
                await using var ms = new MemoryStream(seed);
                samplePath = await _projects.SaveVoiceCloneSampleAsync(
                    projectId, charKey, ms, "voice_clone_sample.wav", ct).ConfigureAwait(false);
            }
        }

        var (cloneModel, speakModel) = await ResolveVoiceModelsAsync(projectId, modelOverride, ct)
            .ConfigureAwait(false);
        var route = ResolveRoute(cloneModel);

        return route switch
        {
            VoiceProviderRoute.Fal => await ApplyViaFalAsync(
                projectId, charKey, display, samplePath, cloneModel, speakModel,
                previewText, voiceLabel, ct).ConfigureAwait(false),
            _ => await ApplyViaElevenAsync(
                projectId, charKey, display, samplePath, cloneModel, speakModel,
                previewText, voiceLabel, ct).ConfigureAwait(false),
        };
    }

    /// <summary>Attach a catalog/premade provider voice id (ElevenLabs-oriented; Fal has no premade list in-app).</summary>
    public async Task<VoiceApplyResult> ApplyCatalogVoiceAsync(
        string projectId,
        string charKey,
        string providerVoiceId,
        string? displayName = null,
        string? previewText = null,
        CancellationToken ct = default)
    {
        await _projects.RequireProjectAsync(projectId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(providerVoiceId))
            return new VoiceApplyResult { Ok = false, Error = "providerVoiceId required" };

        var label = string.IsNullOrWhiteSpace(displayName) ? "Catalog voice" : displayName.Trim();
        var profile = $"Provider voice (elevenlabs:{providerVoiceId.Trim()}). Catalog/premade selection.";
        _projects.UpdateCharacterSeedText(
            projectId,
            charKey,
            voiceProfile: profile,
            voiceLabel: label,
            voiceProvider: "elevenlabs",
            voiceProviderVoiceId: providerVoiceId.Trim(),
            voiceCloneProviderId: providerVoiceId.Trim());

        string? previewRel = null;
        string? previewUrl = null;
        var ttsText = string.IsNullOrWhiteSpace(previewText)
            ? "True! nervous — very, very dreadfully nervous I had been and am."
            : previewText.Trim();
        var tts = await _eleven.TextToSpeechAsync(providerVoiceId.Trim(), ttsText, ct: ct).ConfigureAwait(false);
        if (tts.Ok && tts.AudioBytes is { Length: > 0 })
            (previewRel, previewUrl) = await WritePreviewAsync(
                projectId, charKey, tts.AudioBytes, tts.FileExtension ?? ".mp3", ct).ConfigureAwait(false);

        return new VoiceApplyResult
        {
            Ok = true,
            ProviderId = "elevenlabs",
            ProviderVoiceId = providerVoiceId.Trim(),
            ModelId = "eleven_multilingual_v2",
            UsedMock = tts.UsedMock || providerVoiceId.StartsWith("mock_", StringComparison.OrdinalIgnoreCase),
            PreviewRelativePath = previewRel,
            PreviewUrl = previewUrl,
            VoiceLabel = label,
            Message = "Catalog voice assigned to character.",
        };
    }

    public string? GetTtsPreviewPath(string projectId, string charKey)
    {
        var dir = Path.Combine(_projects.GetProjectDir(projectId), "assets", "characters", Sanitize(charKey));
        if (!Directory.Exists(dir)) return null;
        foreach (var name in new[] { "voice_preview_tts.mp3", "voice_preview_tts.wav", "voice_preview_tts.m4a" })
        {
            var p = Path.Combine(dir, name);
            if (File.Exists(p)) return p;
        }
        return Directory.EnumerateFiles(dir, "voice_preview_tts.*").FirstOrDefault();
    }

    // ── routing ──────────────────────────────────────────────────────────

    private enum VoiceProviderRoute { ElevenLabs, Fal }

    private static VoiceProviderRoute ResolveRoute(SupportedModelEntry? cloneModel)
    {
        if (cloneModel is null) return VoiceProviderRoute.ElevenLabs;
        if (cloneModel.Provider == ModelProviderFamily.Fal ||
            cloneModel.ProviderId.Equals("fal", StringComparison.OrdinalIgnoreCase) ||
            cloneModel.Id.StartsWith("fal-ai/", StringComparison.OrdinalIgnoreCase))
            return VoiceProviderRoute.Fal;
        return VoiceProviderRoute.ElevenLabs;
    }

    private async Task<(SupportedModelEntry? Clone, SupportedModelEntry? Speak)> ResolveVoiceModelsAsync(
        string projectId,
        string? modelOverride,
        CancellationToken ct)
    {
        string? configured = modelOverride;
        if (string.IsNullOrWhiteSpace(configured))
        {
            var cfg = await _projects.GetConfigAsync(projectId, ct).ConfigureAwait(false);
            if (cfg.TryGetValue("voice_model_name", out var el) && el.ValueKind == JsonValueKind.String)
                configured = el.GetString();
        }

        SupportedModelEntry? selected = null;
        if (!string.IsNullOrWhiteSpace(configured) &&
            !configured.Equals("none", StringComparison.OrdinalIgnoreCase) &&
            !configured.Equals("disabled", StringComparison.OrdinalIgnoreCase))
        {
            selected = SupportedModelCatalog.Find(configured, ModelCapability.Voice)
                       ?? SupportedModelCatalog.Find(configured);
        }

        // Map TTS-only selection to its clone sibling on the same provider.
        SupportedModelEntry? clone = selected is { IsVoiceCloneStep: true }
            ? selected
            : selected is not null
                ? SupportedModelCatalog.ForCapability(ModelCapability.Voice)
                    .FirstOrDefault(m => m.IsVoiceCloneStep && m.Provider == selected.Provider)
                : null;

        if (clone is null)
        {
            // Prefer a provider that is actually configured.
            if (_fal.IsConfigured)
                clone = SupportedModelCatalog.ForCapability(ModelCapability.Voice)
                    .FirstOrDefault(m => m.IsVoiceCloneStep && m.Provider == ModelProviderFamily.Fal);
            if (clone is null)
                clone = SupportedModelCatalog.ForCapability(ModelCapability.Voice)
                    .FirstOrDefault(m => m.IsVoiceCloneStep && m.Provider == ModelProviderFamily.ElevenLabs)
                    ?? SupportedModelCatalog.Find("eleven_voice_clone", ModelCapability.Voice);
        }

        SupportedModelEntry? speak = null;
        if (selected is { IsVoiceCloneStep: false })
            speak = selected;
        else if (clone is not null)
        {
            speak = SupportedModelCatalog.ForCapability(ModelCapability.Voice)
                .FirstOrDefault(m => !m.IsVoiceCloneStep && m.Provider == clone.Provider);
        }

        return (clone, speak);
    }

    private async Task<VoiceApplyResult> ApplyViaElevenAsync(
        string projectId,
        string charKey,
        string display,
        string samplePath,
        SupportedModelEntry? cloneModel,
        SupportedModelEntry? speakModel,
        string? previewText,
        string? voiceLabel,
        CancellationToken ct)
    {
        var sample = await File.ReadAllBytesAsync(samplePath, ct).ConfigureAwait(false);
        var fileName = Path.GetFileName(samplePath);
        var clone = await _eleven.CreateCloneAsync(display, sample, fileName, ct).ConfigureAwait(false);
        if (!clone.Ok || string.IsNullOrWhiteSpace(clone.ProviderVoiceId))
            return new VoiceApplyResult { Ok = false, Error = clone.Error ?? "ElevenLabs clone failed" };

        if (_telemetry is not null)
        {
            await _telemetry.LogApiCallAsync(new ApiCallTelemetry
            {
                ProjectId = projectId,
                Kind = "voice_clone",
                Mode = "voice_clone",
                Model = cloneModel?.Id ?? "eleven_voice_clone",
                Provider = "elevenlabs",
                CharKey = charKey,
                EstimatedUsd = cloneModel?.CostPerCloneUsd,
                Ok = true,
            }, ct).ConfigureAwait(false);
        }

        var label = string.IsNullOrWhiteSpace(voiceLabel)
            ? (clone.UsedMock ? "Personal clone (demo)" : "Personal clone")
            : voiceLabel.Trim();
        var profile =
            $"Provider voice (elevenlabs:{clone.ProviderVoiceId}). " +
            (clone.UsedMock
                ? "Mock clone — set ElevenLabs_API_KEY for live Instant Voice Cloning."
                : "ElevenLabs instant clone from operator sample.");

        _projects.UpdateCharacterSeedText(
            projectId,
            charKey,
            voiceProfile: profile,
            voiceLabel: label,
            voiceProvider: "elevenlabs",
            voiceProviderVoiceId: clone.ProviderVoiceId,
            voiceCloneProviderId: clone.ProviderVoiceId);

        string? previewRel = null;
        string? previewUrl = null;
        var ttsText = DefaultPreviewText(previewText);
        var ttsModel = speakModel?.Id ?? "eleven_multilingual_v2";
        var tts = await _eleven.TextToSpeechAsync(clone.ProviderVoiceId, ttsText, ttsModel, ct)
            .ConfigureAwait(false);
        if (tts.Ok && tts.AudioBytes is { Length: > 0 })
        {
            (previewRel, previewUrl) = await WritePreviewAsync(
                projectId, charKey, tts.AudioBytes, tts.FileExtension ?? ".mp3", ct).ConfigureAwait(false);
            if (_telemetry is not null && !tts.UsedMock)
            {
                await _telemetry.LogApiCallAsync(new ApiCallTelemetry
                {
                    ProjectId = projectId,
                    Kind = "tts",
                    Mode = "dialogue_tts",
                    Model = ttsModel,
                    Provider = "elevenlabs",
                    CharKey = charKey,
                    PromptChars = ttsText.Length,
                    Ok = true,
                }, ct).ConfigureAwait(false);
            }
        }

        return new VoiceApplyResult
        {
            Ok = true,
            ProviderId = "elevenlabs",
            ProviderVoiceId = clone.ProviderVoiceId,
            ModelId = cloneModel?.Id ?? "eleven_voice_clone",
            UsedMock = clone.UsedMock || tts.UsedMock,
            PreviewRelativePath = previewRel,
            PreviewUrl = previewUrl,
            VoiceLabel = label,
            Message = clone.UsedMock
                ? "Demo voice applied (no ElevenLabs key). Sample + TTS preview saved."
                : "Voice applied via ElevenLabs. Id stored on this character.",
        };
    }

    private async Task<VoiceApplyResult> ApplyViaFalAsync(
        string projectId,
        string charKey,
        string display,
        string samplePath,
        SupportedModelEntry? cloneModel,
        SupportedModelEntry? speakModel,
        string? previewText,
        string? voiceLabel,
        CancellationToken ct)
    {
        if (!_fal.IsConfigured)
        {
            // Fall back to ElevenLabs mock/live rather than hard-failing if Fal key missing.
            if (_eleven.IsConfigured)
            {
                _log.LogInformation("Fal voice model selected but FAL key missing — falling back to ElevenLabs.");
                return await ApplyViaElevenAsync(
                    projectId, charKey, display, samplePath,
                    SupportedModelCatalog.Find("eleven_voice_clone", ModelCapability.Voice),
                    SupportedModelCatalog.Find("eleven_multilingual_v2", ModelCapability.Voice),
                    previewText, voiceLabel, ct).ConfigureAwait(false);
            }
            return new VoiceApplyResult
            {
                Ok = false,
                Error = "Connect Fal (FAL_API_KEY) in Settings for MiniMax voice clone, or switch the voice model to ElevenLabs.",
            };
        }

        var cloneModelId = cloneModel?.Id ?? "fal-ai/minimax/voice-clone";
        var voiceId = await _fal.CloneVoiceAsync(samplePath, cloneModelId, ct).ConfigureAwait(false);
        if (_telemetry is not null)
        {
            await _telemetry.LogApiCallAsync(new ApiCallTelemetry
            {
                ProjectId = projectId,
                Kind = "voice_clone",
                Mode = "voice_clone",
                Model = cloneModelId,
                Provider = "fal",
                CharKey = charKey,
                EstimatedUsd = cloneModel?.CostPerCloneUsd,
                Ok = !string.IsNullOrWhiteSpace(voiceId),
                Error = string.IsNullOrWhiteSpace(voiceId) ? "Voice clone failed" : null,
            }, ct).ConfigureAwait(false);
        }
        if (string.IsNullOrWhiteSpace(voiceId))
            return new VoiceApplyResult { Ok = false, Error = "Fal MiniMax voice clone failed — see server logs." };

        var label = string.IsNullOrWhiteSpace(voiceLabel) ? "Personal clone" : voiceLabel.Trim();
        var profile = $"Provider voice (fal:{voiceId}). MiniMax clone from operator sample.";
        _projects.UpdateCharacterSeedText(
            projectId,
            charKey,
            voiceProfile: profile,
            voiceLabel: label,
            voiceProvider: "fal",
            voiceProviderVoiceId: voiceId,
            voiceCloneProviderId: voiceId);

        string? previewRel = null;
        string? previewUrl = null;
        var ttsText = DefaultPreviewText(previewText);
        var speakModelId = speakModel?.Id ?? "fal-ai/minimax/speech-02-hd";
        try
        {
            var audioUrl = await _fal.SynthesizeSpeechAsync(ttsText, voiceId, speakModelId, ct)
                .ConfigureAwait(false);
            var estimatedUsd = speakModel?.CostPerThousandCharsUsd is { } rate
                ? Math.Round(rate * ttsText.Length / 1000.0, 4)
                : (double?)null;
            if (_telemetry is not null)
            {
                await _telemetry.LogApiCallAsync(new ApiCallTelemetry
                {
                    ProjectId = projectId,
                    Kind = "tts",
                    Mode = "dialogue_tts",
                    Model = speakModelId,
                    Provider = "fal",
                    CharKey = charKey,
                    PromptChars = ttsText.Length,
                    EstimatedUsd = estimatedUsd,
                    Ok = !string.IsNullOrWhiteSpace(audioUrl),
                    Error = string.IsNullOrWhiteSpace(audioUrl) ? "Speech synthesis failed" : null,
                }, ct).ConfigureAwait(false);
            }
            if (!string.IsNullOrWhiteSpace(audioUrl))
            {
                var bytes = await DownloadBytesAsync(audioUrl, ct).ConfigureAwait(false);
                if (bytes is { Length: > 0 })
                    (previewRel, previewUrl) = await WritePreviewAsync(
                        projectId, charKey, bytes, ".mp3", ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Fal TTS preview failed after successful clone — seed still saved");
        }

        return new VoiceApplyResult
        {
            Ok = true,
            ProviderId = "fal",
            ProviderVoiceId = voiceId,
            ModelId = cloneModelId,
            UsedMock = false,
            PreviewRelativePath = previewRel,
            PreviewUrl = previewUrl,
            VoiceLabel = label,
            Message = "Voice applied via Fal MiniMax. Id stored on this character.",
            EstimatedCloneUsd = cloneModel?.CostPerCloneUsd,
        };
    }

    private async Task<(string Rel, string Url)> WritePreviewAsync(
        string projectId,
        string charKey,
        byte[] audioBytes,
        string ext,
        CancellationToken ct)
    {
        if (!ext.StartsWith('.')) ext = "." + ext;
        var dir = Path.Combine(_projects.GetProjectDir(projectId), "assets", "characters", Sanitize(charKey));
        Directory.CreateDirectory(dir);
        // Clear prior previews so GetTtsPreviewPath is unambiguous
        foreach (var old in Directory.EnumerateFiles(dir, "voice_preview_tts.*"))
        {
            try { File.Delete(old); } catch { /* ignore */ }
        }
        var dest = Path.Combine(dir, "voice_preview_tts" + ext);
        await File.WriteAllBytesAsync(dest, audioBytes, ct).ConfigureAwait(false);
        var rel = Path.GetRelativePath(_projects.GetProjectDir(projectId), dest).Replace('\\', '/');
        var url =
            $"/api/projects/{Uri.EscapeDataString(projectId)}/characters/{Uri.EscapeDataString(charKey)}/voice/tts-preview";
        _log.LogInformation("TTS preview written {Path} ({Bytes} bytes)", dest, audioBytes.Length);
        return (rel, url);
    }

    private async Task<byte[]?> DownloadBytesAsync(string url, CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient();
            using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to download TTS audio from {Url}", url);
            return null;
        }
    }

    private static string DefaultPreviewText(string? previewText) =>
        string.IsNullOrWhiteSpace(previewText)
            ? "True! — nervous — very, very dreadfully nervous I had been and am; but why will you say that I am mad?"
            : previewText.Trim();

    private static string Sanitize(string charKey)
    {
        var k = (charKey ?? "").Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            k = k.Replace(c, '_');
        return string.IsNullOrEmpty(k) ? "character" : k;
    }
}

public sealed class VoiceApplyResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public string? Message { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderVoiceId { get; init; }
    public string? ModelId { get; init; }
    public bool UsedMock { get; init; }
    public string? PreviewRelativePath { get; init; }
    public string? PreviewUrl { get; init; }
    public string? VoiceLabel { get; init; }
    public double? EstimatedCloneUsd { get; init; }
}
