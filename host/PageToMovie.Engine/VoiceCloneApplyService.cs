using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;
using PageToMovie.Engine.VoiceApply;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Orchestrates voice apply: ensure sample on disk, resolve catalog models, pick an
/// <see cref="IVoiceApplyStrategy"/>, execute. Strategies own provider HTTP + seed writes.
/// </summary>
public sealed class VoiceCloneApplyService
{
    private readonly ProjectStore _projects;
    private readonly VoicePreviewStore _previews;
    private readonly IReadOnlyList<IVoiceApplyStrategy> _strategies;
    private readonly IVoiceClient _eleven; // catalog/premade only
    private readonly ILogger<VoiceCloneApplyService> _log;

    public VoiceCloneApplyService(
        ProjectStore projects,
        VoicePreviewStore previews,
        IEnumerable<IVoiceApplyStrategy> strategies,
        IVoiceClient eleven,
        ILogger<VoiceCloneApplyService> log)
    {
        _projects = projects;
        _previews = previews;
        _strategies = strategies.ToList();
        _eleven = eleven;
        _log = log;
    }

    public bool IsConfigured => _strategies.Any(s => s.IsConfigured);

    /// <summary>
    /// Create a provider clone from the character's saved sample (or supplied bytes),
    /// store provider voice id on the seed, optional TTS preview.
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

        var samplePath = await EnsureSampleAsync(projectId, charKey, sampleOverride, sampleFileName, ct)
            .ConfigureAwait(false);

        var (cloneModel, speakModel) = await ResolveVoiceModelsAsync(projectId, modelOverride, ct)
            .ConfigureAwait(false);

        var strategy = SelectStrategy(cloneModel);
        if (strategy is null)
            return new VoiceApplyResult { Ok = false, Error = "No voice apply strategy available." };

        // If selected strategy isn't configured, try a configured fallback (e.g. Fal → ElevenLabs mock).
        if (!strategy.IsConfigured)
        {
            var fallback = _strategies.FirstOrDefault(s => s.IsConfigured);
            if (fallback is not null && !ReferenceEquals(fallback, strategy))
            {
                _log.LogInformation(
                    "Voice strategy {Wanted} not configured — falling back to {Fallback}",
                    strategy.ProviderId, fallback.ProviderId);
                strategy = fallback;
                // Remap models to fallback provider defaults when falling back.
                if (string.Equals(fallback.ProviderId, "elevenlabs", StringComparison.OrdinalIgnoreCase))
                {
                    cloneModel = SupportedModelCatalog.Find("eleven_voice_clone", ModelCapability.Voice);
                    speakModel = SupportedModelCatalog.Find("eleven_multilingual_v2", ModelCapability.Voice);
                }
            }
        }

        var ctx = new VoiceApplyContext
        {
            ProjectId = projectId,
            CharKey = charKey,
            DisplayName = display,
            SamplePath = samplePath,
            CloneModel = cloneModel,
            SpeakModel = speakModel,
            PreviewText = previewText,
            VoiceLabel = voiceLabel,
        };

        _log.LogInformation(
            "Apply voice {Project}/{Char} via {Provider} model={Model}",
            projectId, charKey, strategy.ProviderId, cloneModel?.Id ?? "(default)");

        return await strategy.ApplyAsync(ctx, ct).ConfigureAwait(false);
    }

    /// <summary>Attach a catalog/premade ElevenLabs voice id (no sample clone).</summary>
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
        _previews.PersistSeed(
            projectId, charKey, "elevenlabs", providerVoiceId.Trim(), label, profile);

        string? previewRel = null;
        string? previewUrl = null;
        var ttsText = VoicePreviewStore.DefaultPreviewText(previewText);
        var tts = await _eleven.TextToSpeechAsync(providerVoiceId.Trim(), ttsText, ct: ct).ConfigureAwait(false);
        if (tts.Ok && tts.AudioBytes is { Length: > 0 })
        {
            (previewRel, previewUrl) = await _previews.WriteAsync(
                projectId, charKey, tts.AudioBytes, tts.FileExtension ?? ".mp3", ct).ConfigureAwait(false);
        }

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

    public string? GetTtsPreviewPath(string projectId, string charKey) =>
        _previews.GetTtsPreviewPath(projectId, charKey);

    private IVoiceApplyStrategy? SelectStrategy(SupportedModelEntry? cloneModel)
    {
        // Explicit CanHandle match first (Fal when fal model, Eleven when eleven model).
        var match = _strategies.FirstOrDefault(s => s.CanHandle(cloneModel));
        if (match is not null) return match;
        // Last resort: any configured strategy.
        return _strategies.FirstOrDefault(s => s.IsConfigured) ?? _strategies.FirstOrDefault();
    }

    private async Task<string> EnsureSampleAsync(
        string projectId,
        string charKey,
        byte[]? sampleOverride,
        string? sampleFileName,
        CancellationToken ct)
    {
        if (sampleOverride is { Length: > 0 })
        {
            var fileName = string.IsNullOrWhiteSpace(sampleFileName) ? "voice_clone_sample.wav" : sampleFileName!;
            await using var ms = new MemoryStream(sampleOverride);
            return await _projects.SaveVoiceCloneSampleAsync(projectId, charKey, ms, fileName, ct)
                .ConfigureAwait(false);
        }

        var path = _projects.GetVoiceCloneSamplePath(projectId, charKey);
        if (File.Exists(path)) return path;

        var seed = MockToneWav.Sine(2.2, 210);
        await using var seedMs = new MemoryStream(seed);
        return await _projects.SaveVoiceCloneSampleAsync(
            projectId, charKey, seedMs, "voice_clone_sample.wav", ct).ConfigureAwait(false);
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

        SupportedModelEntry? clone = selected is { IsVoiceCloneStep: true }
            ? selected
            : selected is not null
                ? SupportedModelCatalog.ForCapability(ModelCapability.Voice)
                    .FirstOrDefault(m => m.IsVoiceCloneStep && m.Provider == selected.Provider)
                : null;

        if (clone is null)
        {
            // Prefer a strategy that is actually configured.
            foreach (var s in _strategies.Where(s => s.IsConfigured))
            {
                clone = SupportedModelCatalog.ForCapability(ModelCapability.Voice)
                    .FirstOrDefault(m => m.IsVoiceCloneStep &&
                        (m.ProviderId.Equals(s.ProviderId, StringComparison.OrdinalIgnoreCase) ||
                         (s.ProviderId == "fal" && m.Provider == ModelProviderFamily.Fal) ||
                         (s.ProviderId == "elevenlabs" && m.Provider == ModelProviderFamily.ElevenLabs)));
                if (clone is not null) break;
            }
            clone ??= SupportedModelCatalog.Find("eleven_voice_clone", ModelCapability.Voice)
                      ?? SupportedModelCatalog.ForCapability(ModelCapability.Voice)
                          .FirstOrDefault(m => m.IsVoiceCloneStep);
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
}
