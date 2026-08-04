// GrokLoopPipeline — experimental continuity loop (branch: experiment/grok-loop-pipeline)
// xAI video is async: POST generations/extensions → poll GET /v1/videos/{request_id}.
// See DE_DUP.md for real overlap/de-dup strategies.

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FFMpegCore;
using FFMpegCore.Enums;
using NAudio.Wave;

static class GrokLoopPipeline
{
    static readonly HttpClient Http = CreateClient();

    static async Task<int> Main(string[] args)
    {
        var workDir = Directory.GetCurrentDirectory();
        var dialogFile = args.FirstOrDefault(a => !a.StartsWith("-") && a.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            ?? Path.Combine(workDir, "dialog.txt");
        if (!File.Exists(dialogFile))
        {
            Console.Error.WriteLine($"Missing dialog file: {dialogFile}");
            return 1;
        }

        var duration = 5;
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] is "--duration" or "-d" && int.TryParse(args[i + 1], out var d))
                duration = Math.Clamp(d, 1, 15);

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

        Console.WriteLine($"1) Grok video part 1 (duration={duration}s)…");
        var part1Url = await GenerateGrokVideoAsync(
            "Illustrated picture-book style. Soft daylight. " +
            "Mary (schoolgirl) and a small white lamb at the schoolyard gate. Gentle motion, no text on screen.\n\n" +
            "Dialog context:\n" + dialogText,
            grokPart1,
            duration);

        Console.WriteLine("2) Extract audio (FFMpegCore)…");
        await ExtractAudioAsync(grokPart1, rawAudio);

        Console.WriteLine("3) Whisper transcription…");
        var transcript = await TranscribeWithWhisperAsync(rawAudio);
        Console.WriteLine($"   transcript ({transcript.Length} chars): {Truncate(transcript, 160)}");

        Console.WriteLine("4) Grok video part 2 via /videos/extensions (true continuation)…");
        try
        {
            await ExtendGrokVideoAsync(
                part1Url,
                "Continue seamlessly. Same picture-book look. Lamb settles by the classroom window; " +
                "Mary whispers; teacher softens. Do not restart the scene. Advance the story only.\n" +
                "Already heard (do not re-speak):\n" + transcript,
                grokPart2,
                duration);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   extension failed ({ex.Message}); falling back to text-only part 2…");
            await GenerateGrokVideoAsync(
                "Continue this scene seamlessly. Match prior look. Do NOT repeat covered dialog.\n\n" +
                "Original dialog:\n" + dialogText + "\n\nAlready covered:\n" + transcript,
                grokPart2,
                duration);
        }

        Console.WriteLine("5) Concatenate part 1 + part 2…");
        await ConcatenateVideosAsync(grokPart1, grokPart2, grokCombined);

        Console.WriteLine("6) Soft-limit peaks only (see DE_DUP.md)…");
        SoftLimitPeaks(rawAudio, filteredAudio);

        Console.WriteLine("7) Mux combined video + filtered audio…");
        await MergeVideoAndAudioAsync(grokCombined, filteredAudio, finalVideo);

        Console.WriteLine("Pipeline complete: " + finalVideo);
        return 0;
    }

    static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        var key = Environment.GetEnvironmentVariable("XAI_API_KEY")
            ?? throw new InvalidOperationException("Set XAI_API_KEY.");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return c;
    }

    static async Task<string> GenerateGrokVideoAsync(string prompt, string outputFile, int durationSeconds)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = "grok-imagine-video",
            ["prompt"] = prompt,
            ["duration"] = durationSeconds,
            ["aspect_ratio"] = "16:9",
            ["resolution"] = "720p"
        };
        return await StartPollDownloadAsync("https://api.x.ai/v1/videos/generations", payload, outputFile);
    }

    static async Task ExtendGrokVideoAsync(string sourceVideoUrl, string prompt, string outputFile, int durationSeconds)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = "grok-imagine-video",
            ["prompt"] = prompt,
            ["duration"] = durationSeconds,
            ["video"] = new Dictionary<string, string> { ["url"] = sourceVideoUrl }
        };
        await StartPollDownloadAsync("https://api.x.ai/v1/videos/extensions", payload, outputFile);
    }

    static async Task<string> StartPollDownloadAsync(string startUrl, object payload, string outputFile)
    {
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync(startUrl, content);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Start HTTP {(int)response.StatusCode}: {Truncate(body, 500)}");

        using var startDoc = JsonDocument.Parse(body);
        if (!startDoc.RootElement.TryGetProperty("request_id", out var ridEl))
            throw new InvalidOperationException("No request_id in start response: " + Truncate(body, 400));
        var requestId = ridEl.GetString() ?? throw new InvalidOperationException("Empty request_id");
        Console.WriteLine($"   request_id={requestId}");

        var url = await PollUntilVideoUrlAsync(requestId);
        Console.WriteLine("   downloading…");
        var videoBytes = await Http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(outputFile, videoBytes);
        Console.WriteLine($"   wrote {outputFile} ({videoBytes.Length:N0} bytes)");
        return url;
    }

    static async Task<string> PollUntilVideoUrlAsync(string requestId, int maxAttempts = 72, int delaySeconds = 5)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var response = await Http.GetAsync($"https://api.x.ai/v1/videos/{requestId}");
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"Poll HTTP {(int)response.StatusCode}: {Truncate(body, 300)}");

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.TryGetProperty("status", out var st) ? st.GetString() : "?";
            Console.WriteLine($"   poll {attempt}: {status}");

            if (status == "done")
            {
                if (doc.RootElement.TryGetProperty("video", out var video) &&
                    video.TryGetProperty("url", out var urlEl) &&
                    urlEl.ValueKind == JsonValueKind.String)
                    return urlEl.GetString()!;
                throw new InvalidOperationException("done but no video.url: " + Truncate(body, 400));
            }

            if (status is "failed" or "expired")
                throw new InvalidOperationException($"Video {status}: {Truncate(body, 400)}");

            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
        }
        throw new TimeoutException($"Video {requestId} not ready after {maxAttempts * delaySeconds}s");
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
            .FromFileInput(listFile, verifyExists: true, o => o
                .WithCustomArgument("-f concat").WithCustomArgument("-safe 0"))
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
