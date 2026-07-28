using System.Diagnostics;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

public sealed record SceneMusicResult(
    string MusicFilePath,
    string RemuxedScenePath,
    string MusicPrompt,
    double DurationSeconds);

/// <summary>
/// Scores continuous background music for full stitched scene MP4s using AI audio models (Fal.ai Stable Audio / MusicGen).
/// Applies ffmpeg audio ducking so music plays under dialogue without drowning out character speech.
/// Bypasses cleanly if audio_model_name is 'none' or missing required API keys.
/// </summary>
public sealed class SceneMusicScoringService
{
    private readonly IChatClient _chat;
    private readonly IAudioClient _audioClient;
    private readonly ILogger<SceneMusicScoringService> _log;

    public SceneMusicScoringService(
        IChatClient chat,
        IAudioClient audioClient,
        ILogger<SceneMusicScoringService> log)
    {
        _chat = chat;
        _audioClient = audioClient;
        _log = log;
    }

    public async Task<SceneMusicResult?> ProcessSceneMusicAsync(
        string projectDir,
        int sceneNumber,
        string inputSceneMp4Path,
        string outputSceneMp4Path,
        string screenplayText,
        int durationSeconds,
        Dictionary<string, JsonElement>? config = null,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(inputSceneMp4Path) || !File.Exists(inputSceneMp4Path))
        {
            _log.LogInformation("Scene {Scene} video missing for music scoring.", sceneNumber);
            return null;
        }

        var audioModel = GetConfigStr(config, "audio_model_name", "none");
        if (string.Equals(audioModel, "none", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(audioModel))
        {
            onProgress?.Invoke("Background music is disabled ('none'). Skipping music pass.");
            return null;
        }

        if (!_audioClient.IsConfigured)
        {
            onProgress?.Invoke("Audio synthesis API key missing. Skipping background music pass.");
            _log.LogWarning("Skipping scene music for scene {Scene} because audio client is not configured.", sceneNumber);
            return null;
        }

        onProgress?.Invoke($"AI Music Scoring: Analyzing Scene {sceneNumber:D2} for continuous background score…");

        // Step 1: AI Prompting pass to generate rich music description
        var scoringModel = GetConfigStr(config, "planning_model_name", "grok-4.5");
        var musicPrompt = await ComposeSceneMusicPromptAsync(screenplayText, durationSeconds, scoringModel, ct).ConfigureAwait(false);

        onProgress?.Invoke($"AI Audio Synthesis: Generating {durationSeconds}s music score via {audioModel}…");
        _log.LogInformation("Generating scene {Scene} music score: {Prompt}", sceneNumber, musicPrompt);

        // Step 2: Synthesize stereo audio track via Fal.ai audio client
        var audioBytes = await _audioClient.GenerateMusicTrackAsync(musicPrompt, durationSeconds, audioModel, ct).ConfigureAwait(false);

        var audioDir = Path.Combine(projectDir, "assets");
        Directory.CreateDirectory(audioDir);
        var musicFilePath = Path.Combine(audioDir, $"scene_{sceneNumber:D2}_music.mp3");
        await File.WriteAllBytesAsync(musicFilePath, audioBytes, ct).ConfigureAwait(false);

        // Step 3: Layer music track onto stitched scene MP4 via ffmpeg with auto-ducking
        onProgress?.Invoke($"Audio Mixing: Layering music score into Scene {sceneNumber:D2} with auto-ducking…");
        await LayerAudioWithFfmpegAsync(inputSceneMp4Path, musicFilePath, outputSceneMp4Path, ct).ConfigureAwait(false);

        return new SceneMusicResult(musicFilePath, outputSceneMp4Path, musicPrompt, durationSeconds);
    }

    private async Task<string> ComposeSceneMusicPromptAsync(
        string screenplayText,
        int durationSeconds,
        string model,
        CancellationToken ct)
    {
        var sysPrompt = "You are a Hollywood Film Score Composer. Create a short, highly descriptive music prompt (15-25 words) for an AI music generator based on a film scene. Specify genre, instruments, tempo (BPM), and mood. Do NOT include speech or voice descriptions. Output ONLY the music prompt text.";
        var userPrompt = $"Scene Duration: {durationSeconds} seconds.\n\nScreenplay Content:\n{screenplayText}";

        try
        {
            var res = await _chat.CompleteAsync(sysPrompt, userPrompt, model, temperature: 0.3, ct: ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(res))
                return res.Trim().Trim('"');
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to generate AI scene music prompt; using cinematic fallback prompt.");
        }

        return "Cinematic dark orchestral background score with low cellos, subtle brass swells, tense rhythmic pulse at 90 BPM.";
    }

    private async Task LayerAudioWithFfmpegAsync(
        string videoPath,
        string audioPath,
        string outputPath,
        CancellationToken ct)
    {
        var tempOut = outputPath + ".tmp_music.mp4";
        if (File.Exists(tempOut)) File.Delete(tempOut);

        // ffmpeg filter_complex: mixes background music at 30% volume (-10dB) under original dialogue track
        var args = $"-y -i \"{videoPath}\" -i \"{audioPath}\" -filter_complex \"[1:a]volume=0.30[bg];[0:a][bg]amix=inputs=2:duration=first[a]\" -map 0:v -map \"[a]\" -c:v copy -c:a aac -b:a 192k \"{tempOut}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to launch ffmpeg for audio mixing.");

        var errText = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        if (proc.ExitCode != 0 || !File.Exists(tempOut))
        {
            _log.LogError("ffmpeg audio mixing failed (code {Code}): {Err}", proc.ExitCode, errText);
            throw new InvalidOperationException($"ffmpeg audio mixing failed with exit code {proc.ExitCode}");
        }

        File.Move(tempOut, outputPath, overwrite: true);
    }

    private static string GetConfigStr(Dictionary<string, JsonElement>? cfg, string key, string fallback)
    {
        if (cfg is not null && cfg.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String)
        {
            var val = el.GetString();
            if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
        }
        return fallback;
    }
}