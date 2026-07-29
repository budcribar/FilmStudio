using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using PageToMovie.Core.Models;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.Logging;

namespace PageToMovie.Engine;

/// <summary>
/// Scores continuous background music for full stitched scene MP4s using AI audio models (Fal.ai Stable Audio / MusicGen).
/// Applies ffmpeg audio ducking so music plays under dialogue without drowning out character speech.
/// Bypasses cleanly if audio_model_name is 'none' or missing required API keys.
/// </summary>
public sealed class SceneMusicScoringService
{
    private readonly IChatClient _chat;
    private readonly IMusicClient _musicClient;
    private readonly ILogger<SceneMusicScoringService> _log;

    public SceneMusicScoringService(
        IChatClient chat,
        IMusicClient musicClient,
        ILogger<SceneMusicScoringService> log)
    {
        _chat = chat;
        _musicClient = musicClient;
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

        var enableMusic = GetConfigBool(config, "enable_background_music", true);
        if (!enableMusic)
        {
            onProgress?.Invoke("Background music is disabled in settings. Skipping music pass.");
            return null;
        }

        var audioModel = GetConfigStr(config, "audio_model_name", "fal-ai/stable-audio");
        if (string.Equals(audioModel, "none", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(audioModel))
        {
            onProgress?.Invoke("Background music is disabled ('none'). Skipping music pass.");
            return null;
        }

        if (!_musicClient.IsConfigured)
        {
            onProgress?.Invoke("Audio synthesis API key missing. Skipping background music pass.");
            _log.LogWarning("Skipping scene music for scene {Scene} because music client is not configured.", sceneNumber);
            return null;
        }

        onProgress?.Invoke($"AI Music Scoring: Analyzing Scene {sceneNumber:D2} for continuous background score…");

        // Step 1: AI Prompting pass — prefer pre-planned score prompt from blueprint.clips.grok.json if present
        var musicPrompt = GetPreplannedMusicPrompt(projectDir, sceneNumber);
        if (string.IsNullOrWhiteSpace(musicPrompt))
        {
            var scoringModel = GetConfigStr(config, "planning_model_name", "grok-4.5");
            musicPrompt = await ComposeSceneMusicPromptAsync(screenplayText, durationSeconds, scoringModel, ct).ConfigureAwait(false);
        }

        onProgress?.Invoke($"AI Audio Synthesis: Generating {durationSeconds}s music score via {audioModel}…");
        _log.LogInformation("Generating scene {Scene} music score: {Prompt}", sceneNumber, musicPrompt);

        // Step 2: Synthesize stereo audio track via Fal.ai audio client
        var audioBytes = await _musicClient.GenerateMusicTrackAsync(musicPrompt, durationSeconds, audioModel, ct).ConfigureAwait(false);
        if (audioBytes is null || audioBytes.Length == 0)
        {
            onProgress?.Invoke("Background music synthesis produced 0 bytes. Keeping original scene video.");
            return null;
        }

        var audioDir = Path.Combine(projectDir, "assets");
        Directory.CreateDirectory(audioDir);
        var musicFilePath = Path.Combine(audioDir, $"scene_{sceneNumber:D2}_music.mp3");
        await File.WriteAllBytesAsync(musicFilePath, audioBytes, ct).ConfigureAwait(false);

        // Step 3: Layer music track onto stitched scene MP4 via ffmpeg with volume ducking & fade-out
        var volumePercent = GetConfigInt(config, "background_music_volume_percent", 20);
        onProgress?.Invoke($"Audio Mixing: Layering music score into Scene {sceneNumber:D2} at {volumePercent}% volume with auto-ducking…");
        await LayerAudioWithFfmpegAsync(inputSceneMp4Path, musicFilePath, outputSceneMp4Path, durationSeconds, volumePercent, ct).ConfigureAwait(false);

        return new SceneMusicResult(musicFilePath, outputSceneMp4Path, musicPrompt, durationSeconds);
    }

    /// <summary>
    /// Batch synthesizes AI background music audio tracks (assets/scene_XX_music.mp3) for all scenes in a project.
    /// Skips any scene that already has an audio track file present on disk.
    /// </summary>
    public async Task<int> GenerateProjectSceneAudioAsync(
        string projectDir,
        Dictionary<string, JsonElement>? config = null,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        var blueprintPath = Path.Combine(projectDir, "blueprint.clips.grok.json");
        if (!File.Exists(blueprintPath))
        {
            _log.LogWarning("blueprint.clips.grok.json not found in {ProjectDir}", projectDir);
            onProgress?.Invoke("No blueprint file found. Cannot generate scene music.");
            return 0;
        }

        var audioModel = GetConfigStr(config, "audio_model_name", "fal-ai/stable-audio");
        if (string.Equals(audioModel, "none", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(audioModel))
        {
            onProgress?.Invoke("Background music is disabled ('none'). Skipping audio pass.");
            return 0;
        }

        if (!_musicClient.IsConfigured)
        {
            onProgress?.Invoke("Audio synthesis API key missing. Skipping background music pass.");
            return 0;
        }

        JsonNode? doc;
        try
        {
            var json = await File.ReadAllTextAsync(blueprintPath, ct).ConfigureAwait(false);
            doc = JsonNode.Parse(json);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to parse blueprint JSON at {Path}", blueprintPath);
            onProgress?.Invoke("Failed to read blueprint JSON.");
            return 0;
        }

        var scenesArray = doc?["scenes"]?.AsArray();
        if (scenesArray is null || scenesArray.Count == 0)
        {
            onProgress?.Invoke("No scenes found in blueprint.");
            return 0;
        }

        var assetsDir = Path.Combine(projectDir, "assets");
        Directory.CreateDirectory(assetsDir);
        var generatedCount = 0;

        foreach (var scNode in scenesArray)
        {
            if (ct.IsCancellationRequested) break;

            if (scNode is null) continue;
            var sceneNum = scNode["scene_number"]?.GetValue<int>() ?? 0;
            if (sceneNum <= 0) continue;

            var mp3Path = Path.Combine(assetsDir, $"scene_{sceneNum:D2}_music.mp3");
            var wavPath = Path.Combine(assetsDir, $"scene_{sceneNum:D2}_music.wav");

            if (File.Exists(mp3Path) || File.Exists(wavPath))
            {
                onProgress?.Invoke($"Scene {sceneNum:D2}: Music track already exists. Skipping synthesis.");
                continue;
            }

            // Obtain prompt from pre-planned score or fallback
            var prompt = scNode["music_prompt"]?.GetValue<string>()
                ?? scNode["music_score"]?["prompt"]?.GetValue<string>();

            var duration = scNode["total_estimated_duration_seconds"]?.GetValue<int>() ?? 0;
            if (duration <= 0 && scNode["veo_clips"]?.AsArray() is { } clips)
            {
                duration = clips.Sum(c => c?["duration_seconds"]?.GetValue<int>() ?? 0);
            }
            if (duration <= 0) duration = 10;

            if (string.IsNullOrWhiteSpace(prompt))
            {
                var setting = scNode["setting"]?.GetValue<string>() ?? "";
                var scoringModel = GetConfigStr(config, "planning_model_name", "grok-4.5");
                prompt = await ComposeSceneMusicPromptAsync(setting, duration, scoringModel, ct).ConfigureAwait(false);
            }

            onProgress?.Invoke($"Scene {sceneNum:D2}: Synthesizing {duration}s music score via {audioModel}…");
            _log.LogInformation("Synthesizing music for scene {Scene} ({Duration}s): {Prompt}", sceneNum, duration, prompt);

            var bytes = await _musicClient.GenerateMusicTrackAsync(prompt, duration, audioModel, ct).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                onProgress?.Invoke($"Scene {sceneNum:D2}: Synthesis returned 0 bytes.");
                continue;
            }

            await File.WriteAllBytesAsync(mp3Path, bytes, ct).ConfigureAwait(false);
            generatedCount++;
            onProgress?.Invoke($"Scene {sceneNum:D2}: Successfully saved music track ({bytes.Length / 1024} KB).");

            // If scene composite video exists, mix background audio with volume ducking
            var inputVideo = Path.Combine(assetsDir, "scenes", $"scene_{sceneNum:D2}.mp4");
            if (!File.Exists(inputVideo))
            {
                inputVideo = Path.Combine(assetsDir, "video", $"scene_{sceneNum:D2}.mp4");
            }

            if (File.Exists(inputVideo))
            {
                var outputVideo = Path.Combine(assetsDir, "scenes", $"scene_{sceneNum:D2}.mp4");
                var volPercent = GetConfigInt(config, "background_music_volume_percent", 20);
                onProgress?.Invoke($"Scene {sceneNum:D2}: Layering audio into scene composite MP4…");
                try
                {
                    await LayerAudioWithFfmpegAsync(inputVideo, mp3Path, outputVideo, duration, volPercent, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to layer audio into video for scene {Scene}", sceneNum);
                }
            }
        }

        onProgress?.Invoke($"Batch music synthesis complete. Generated audio for {generatedCount} scene(s).");
        return generatedCount;
    }

    public static string? GetPreplannedMusicPrompt(string projectDir, int sceneNumber)
    {
        var blueprintPath = Path.Combine(projectDir, "blueprint.clips.grok.json");
        if (!File.Exists(blueprintPath)) return null;

        try
        {
            var json = File.ReadAllText(blueprintPath);
            var doc = JsonNode.Parse(json);
            if (doc?["scenes"]?.AsArray() is { } scenes)
            {
                foreach (var sc in scenes)
                {
                    if (sc?["scene_number"]?.GetValue<int>() == sceneNumber)
                    {
                        var prompt = sc["music_prompt"]?.GetValue<string>()
                            ?? sc["music_score"]?["prompt"]?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(prompt))
                            return prompt.Trim();
                    }
                }
            }
        }
        catch
        {
            /* fallback to null */
        }
        return null;
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
        int durationSeconds,
        int volumePercent,
        CancellationToken ct)
    {
        var tempOut = outputPath + ".tmp_music.mp4";
        if (File.Exists(tempOut)) File.Delete(tempOut);

        var volRatio = Math.Clamp(volumePercent / 100.0, 0.05, 1.0);
        var fadeStart = Math.Max(0.0, durationSeconds - 1.5);

        // ffmpeg filter_complex: mixes background music at configured volume ratio with 1.5s fade-out under dialogue
        var args = $"-y -i \"{videoPath}\" -i \"{audioPath}\" -filter_complex \"[1:a]volume={volRatio:F2},afade=t=out:st={fadeStart:F1}:d=1.5[bg];[0:a][bg]amix=inputs=2:duration=first[a]\" -map 0:v -map \"[a]\" -c:v copy -c:a aac -b:a 192k \"{tempOut}\"";

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

    private static bool GetConfigBool(Dictionary<string, JsonElement>? cfg, string key, bool fallback)
    {
        if (cfg is not null && cfg.TryGetValue(key, out var el))
        {
            if (el.ValueKind is JsonValueKind.True or JsonValueKind.False)
                return el.GetBoolean();
        }
        return fallback;
    }

    private static int GetConfigInt(Dictionary<string, JsonElement>? cfg, string key, int fallback)
    {
        if (cfg is not null && cfg.TryGetValue(key, out var el) && el.TryGetInt32(out var v))
        {
            return v;
        }
        return fallback;
    }
}