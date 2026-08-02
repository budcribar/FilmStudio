using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Applies a voice-clone sample (or a catalog voice) to a character and optionally
/// synthesizes a short preview line via the configured voice provider.
/// </summary>
public sealed class VoiceCloneApplyService
{
    private readonly ProjectStore _projects;
    private readonly IVoiceClient _voices;
    private readonly ILogger<VoiceCloneApplyService> _log;

    public VoiceCloneApplyService(
        ProjectStore projects,
        IVoiceClient voices,
        ILogger<VoiceCloneApplyService> log)
    {
        _projects = projects;
        _voices = voices;
        _log = log;
    }

    public bool IsConfigured => _voices.IsConfigured;

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
        CancellationToken ct = default)
    {
        await _projects.RequireProjectAsync(projectId, ct).ConfigureAwait(false);
        var display = charKey.Replace("Character_", "", StringComparison.OrdinalIgnoreCase).Replace('_', ' ');

        byte[] sample;
        string fileName;
        if (sampleOverride is { Length: > 0 })
        {
            sample = sampleOverride;
            fileName = string.IsNullOrWhiteSpace(sampleFileName) ? "voice_clone_sample.wav" : sampleFileName!;
            await using var ms = new MemoryStream(sample);
            await _projects.SaveVoiceCloneSampleAsync(projectId, charKey, ms, fileName, ct).ConfigureAwait(false);
        }
        else
        {
            var path = _projects.GetVoiceCloneSamplePath(projectId, charKey);
            if (!File.Exists(path))
            {
                // Seed a short mock sample so demo path always has something
                var seed = MockToneWav.Sine(2.2, 210);
                await using var ms = new MemoryStream(seed);
                path = await _projects.SaveVoiceCloneSampleAsync(
                    projectId, charKey, ms, "voice_clone_sample.wav", ct).ConfigureAwait(false);
            }
            sample = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
            fileName = Path.GetFileName(path);
        }

        var clone = await _voices.CreateCloneAsync(display, sample, fileName, ct).ConfigureAwait(false);
        if (!clone.Ok || string.IsNullOrWhiteSpace(clone.ProviderVoiceId))
            return new VoiceApplyResult { Ok = false, Error = clone.Error ?? "Clone failed" };

        var label = string.IsNullOrWhiteSpace(voiceLabel)
            ? (clone.UsedMock ? "Personal clone (demo)" : "Personal clone")
            : voiceLabel.Trim();
        var profile =
            $"Provider voice ({_voices.ProviderId}:{clone.ProviderVoiceId}). " +
            (clone.UsedMock
                ? "Mock clone — set ELEVENLABS_API_KEY for live Instant Voice Cloning."
                : "ElevenLabs instant clone from operator sample.");

        _projects.UpdateCharacterSeedText(
            projectId,
            charKey,
            voiceProfile: profile,
            voiceLabel: label,
            voiceProvider: _voices.ProviderId,
            voiceProviderVoiceId: clone.ProviderVoiceId);

        string? previewRel = null;
        string? previewUrl = null;
        var ttsText = string.IsNullOrWhiteSpace(previewText)
            ? $"True! — nervous — very, very dreadfully nervous I had been and am; but why will you say that I am mad?"
            : previewText.Trim();

        var tts = await _voices.TextToSpeechAsync(clone.ProviderVoiceId, ttsText, ct: ct).ConfigureAwait(false);
        if (tts.Ok && tts.AudioBytes is { Length: > 0 })
        {
            var ext = tts.FileExtension ?? ".mp3";
            if (!ext.StartsWith('.')) ext = "." + ext;
            var dir = Path.Combine(
                _projects.GetProjectDir(projectId),
                "assets", "characters", Sanitize(charKey));
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, "voice_preview_tts" + ext);
            await File.WriteAllBytesAsync(dest, tts.AudioBytes, ct).ConfigureAwait(false);
            previewRel = Path.GetRelativePath(_projects.GetProjectDir(projectId), dest).Replace('\\', '/');
            previewUrl =
                $"/api/projects/{Uri.EscapeDataString(projectId)}/characters/{Uri.EscapeDataString(charKey)}/voice/tts-preview";
            _log.LogInformation("TTS preview written {Path} ({Bytes} bytes, mock={Mock})",
                dest, tts.AudioBytes.Length, tts.UsedMock);
        }

        return new VoiceApplyResult
        {
            Ok = true,
            ProviderId = _voices.ProviderId,
            ProviderVoiceId = clone.ProviderVoiceId,
            UsedMock = clone.UsedMock || (tts.UsedMock),
            PreviewRelativePath = previewRel,
            PreviewUrl = previewUrl,
            VoiceLabel = label,
            Message = clone.UsedMock
                ? "Mock clone applied (no ElevenLabs key). Sample + TTS preview saved under the character."
                : "ElevenLabs clone applied. Narrator voice id stored on the character seed.",
        };
    }

    /// <summary>Attach a catalog/premade voice id without cloning from a sample.</summary>
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
        var profile = $"Provider voice ({_voices.ProviderId}:{providerVoiceId.Trim()}). Catalog/premade selection.";
        _projects.UpdateCharacterSeedText(
            projectId,
            charKey,
            voiceProfile: profile,
            voiceLabel: label,
            voiceProvider: _voices.ProviderId,
            voiceProviderVoiceId: providerVoiceId.Trim());

        string? previewRel = null;
        string? previewUrl = null;
        var ttsText = string.IsNullOrWhiteSpace(previewText)
            ? "True! nervous — very, very dreadfully nervous I had been and am."
            : previewText.Trim();
        var tts = await _voices.TextToSpeechAsync(providerVoiceId.Trim(), ttsText, ct: ct).ConfigureAwait(false);
        if (tts.Ok && tts.AudioBytes is { Length: > 0 })
        {
            var ext = tts.FileExtension ?? ".mp3";
            if (!ext.StartsWith('.')) ext = "." + ext;
            var dir = Path.Combine(_projects.GetProjectDir(projectId), "assets", "characters", Sanitize(charKey));
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, "voice_preview_tts" + ext);
            await File.WriteAllBytesAsync(dest, tts.AudioBytes, ct).ConfigureAwait(false);
            previewRel = Path.GetRelativePath(_projects.GetProjectDir(projectId), dest).Replace('\\', '/');
            previewUrl =
                $"/api/projects/{Uri.EscapeDataString(projectId)}/characters/{Uri.EscapeDataString(charKey)}/voice/tts-preview";
        }

        return new VoiceApplyResult
        {
            Ok = true,
            ProviderId = _voices.ProviderId,
            ProviderVoiceId = providerVoiceId.Trim(),
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
    public bool UsedMock { get; init; }
    public string? PreviewRelativePath { get; init; }
    public string? PreviewUrl { get; init; }
    public string? VoiceLabel { get; init; }
}
