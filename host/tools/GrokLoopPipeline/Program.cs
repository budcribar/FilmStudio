// GrokLoopPipeline — experimental continuity loop (branch: experiment/grok-loop-pipeline)
// See DE_DUP.md for real overlap/de-dup strategies.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FFMpegCore;
using FFMpegCore.Enums;
using NAudio.Wave;

static class GrokLoopPipeline
{
    static async Task<int> Main(string[] args)
    {
        var workDir = Directory.GetCurrentDirectory();
        var dialogFile = args.Length > 0 ? args[0] : Path.Combine(workDir, "dialog.txt");
        if (!File.Exists(dialogFile))
        {
            Console.Error.WriteLine($"Missing dialog file: {dialogFile}");
            return 1;
        }

        GlobalFFOptions.Configure(new FFOptions
        {
            BinaryFolder = "",
            TemporaryFilesFolder = Path.Combine(workDir, ".fftmp")
        });
        Directory.CreateDirectory(Path.Combine(workDir, ".fftmp"));
        Directory.CreateDirectory(Path.Combine(workDir, "out"));

        var dialogText = await File.ReadAllTextAsync(dialogFile);
        var grokPart1 = Path.Combine(workDir, "out", "grok_part1.mp4");
        var grokPart2 = Path.Combine(workDir, "out", "grok_part2.mp4");
        var grokCombined = Path.Combine(workDir, "out", "grok_full.mp4");
        var rawAudio = Path.Combine(workDir, "out", "grok_audio.wav");
        var filteredAudio = Path.Combine(workDir, "out", "grok_audio_filtered.wav");
        var finalVideo = Path.Combine(workDir, "out", "final_dialog_video.mp4");

        Console.WriteLine("1) Grok video part 1…");
        await GenerateGrokVideoAsync(dialogText, grokPart1, durationSeconds: 10);

        Console.WriteLine("2) Extract audio (FFMpegCore)…");
        await ExtractAudioAsync(grokPart1, rawAudio);

        Console.WriteLine("3) Whisper transcription…");
        var transcript = await TranscribeWithWhisperAsync(rawAudio);
        Console.WriteLine($"   transcript ({transcript.Length} chars): {Truncate(transcript, 120)}");

        Console.WriteLine("4) Grok video part 2 (continuation; anti-echo prompt)…");
        var continuationPrompt =
            "Continue this scene seamlessly. Match prior look, characters, and pacing.\n" +
            "Do NOT repeat dialog or story beats already covered below.\n" +
            "Advance only to the next moment after what was heard.\n\n" +
            "Original planned dialog:\n" + dialogText + "\n\n" +
            "ALREADY COVERED (transcript of part 1 — do not re-speak):\n" + transcript;
        await GenerateGrokVideoAsync(continuationPrompt, grokPart2, durationSeconds: 10);

        Console.WriteLine("5) Concatenate part 1 + part 2…");
        await ConcatenateVideosAsync(grokPart1, grokPart2, grokCombined);

        Console.WriteLine("6) Soft-limit peaks only (see DE_DUP.md for real de-dup)…");
        SoftLimitPeaks(rawAudio, filteredAudio);

        Console.WriteLine("7) Mux combined video + filtered audio…");
        await MergeVideoAndAudioAsync(grokCombined, filteredAudio, finalVideo);

        Console.WriteLine("Pipeline complete: " + finalVideo);
        return 0;
    }

    static async Task GenerateGrokVideoAsync(string prompt, string outputFile, int durationSeconds)
    {
        var apiKey = Environment.GetEnvironmentVariable("XAI_API_KEY")
            ?? throw new InvalidOperationException("Set XAI_API_KEY.");
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var payload = new Dictionary<string, object?>
        {
            ["model"] = "grok-imagine-video",
            ["prompt"] = prompt,
            ["duration"] = durationSeconds,
            ["aspect_ratio"] = "16:9",
            ["resolution"] = "720p"
        };
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("https://api.x.ai/v1/videos/generations", content);
        var result = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Grok video HTTP {(int)response.StatusCode}: {Truncate(result, 500)}");
        var url = TryExtractVideoUrl(result)
            ?? throw new InvalidOperationException("No video URL in response: " + Truncate(result, 400));
        var videoBytes = await client.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(outputFile, videoBytes);
        Console.WriteLine($"   wrote {outputFile} ({videoBytes.Length:N0} bytes)");
    }

    static string? TryExtractVideoUrl(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("url", out var u) && u.ValueKind == JsonValueKind.String)
                return u.GetString();
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                    if (item.TryGetProperty("url", out var u2) && u2.ValueKind == JsonValueKind.String)
                        return u2.GetString();
            }
            if (doc.RootElement.TryGetProperty("video", out var video) &&
                video.TryGetProperty("url", out var u3) && u3.ValueKind == JsonValueKind.String)
                return u3.GetString();
        }
        catch (JsonException) { }
        var i = json.IndexOf("https://", StringComparison.Ordinal);
        if (i < 0) return null;
        var j = i;
        while (j < json.Length && json[j] is not ('"' or '\'' or ' ' or '\n' or '\r' or '}')) j++;
        return json[i..j];
    }

    static async Task ExtractAudioAsync(string videoFile, string audioFile) =>
        await FFMpegArguments.FromFileInput(videoFile)
            .OutputToFile(audioFile, overwrite: true, o => o
                .DisableChannel(Channel.Video).WithAudioCodec("pcm_s16le").ForceFormat("wav"))
            .ProcessAsynchronously();

    static async Task ConcatenateVideosAsync(string part1, string part2, string output)
    {
        var listFile = Path.Combine(Path.GetDirectoryName(output)!, "concat_list.txt");
        await File.WriteAllTextAsync(listFile,
            $"file '{Path.GetFullPath(part1).Replace("'", "'\\''")}'\nfile '{Path.GetFullPath(part2).Replace("'", "'\\''")}'\n");
        await FFMpegArguments
            .FromFileInput(listFile, verifyExists: true, o => o.WithCustomArgument("-f concat").WithCustomArgument("-safe 0"))
            .OutputToFile(output, overwrite: true, o => o.WithCustomArgument("-c copy"))
            .ProcessAsynchronously();
    }

    static async Task MergeVideoAndAudioAsync(string video, string audio, string output) =>
        await FFMpegArguments.FromFileInput(video).AddFileInput(audio)
            .OutputToFile(output, overwrite: true, o => o
                .WithCustomArgument("-c:v copy").WithAudioCodec(AudioCodec.Aac).WithCustomArgument("-shortest"))
            .ProcessAsynchronously();

    static async Task<string> TranscribeWithWhisperAsync(string audioFile)
    {
        var key = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? Environment.GetEnvironmentVariable("WHISPER_API_KEY")
            ?? throw new InvalidOperationException("Set OPENAI_API_KEY or WHISPER_API_KEY.");
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        using var form = new MultipartFormDataContent();
        var bytes = await File.ReadAllBytesAsync(audioFile);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", Path.GetFileName(audioFile));
        form.Add(new StringContent("whisper-1"), "model");
        using var response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", form);
        var json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Whisper HTTP {(int)response.StatusCode}: {Truncate(json, 400)}");
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
            return text.GetString() ?? "";
        throw new InvalidOperationException("Whisper response missing text: " + Truncate(json, 300));
    }

    static void SoftLimitPeaks(string inputFile, string outputFile, float threshold = 0.95f)
    {
        using var reader = new AudioFileReader(inputFile);
        var format = reader.WaveFormat;
        var sampleProvider = reader.ToSampleProvider();
        var buffer = new float[format.SampleRate * format.Channels];
        using var writer = new WaveFileWriter(outputFile, format);
        int read;
        while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
                if (Math.Abs(buffer[i]) > threshold)
                    buffer[i] = threshold * Math.Sign(buffer[i]);
            writer.WriteSamples(buffer, 0, read);
        }
    }

    static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
}
