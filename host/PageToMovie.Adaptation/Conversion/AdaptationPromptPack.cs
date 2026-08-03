using System.Reflection;

namespace PageToMovie.Adaptation.Conversion;

/// <summary>
/// Loads the book → Fountain prompt pack. Prefers embedded resource in this assembly,
/// optional override via <c>PAGETOMOVIE_PROMPTS_DIR</c> (same env as Engine PromptFiles).
/// </summary>
public static class AdaptationPromptPack
{
    public const string BookToFountainRelativePath = "prompts/book_to_fountain.txt";
    public const string EmbeddedLogicalName = "PageToMovie.Adaptation.Prompts.book_to_fountain.txt";

    private static readonly Assembly ThisAssembly = typeof(AdaptationPromptPack).Assembly;

    /// <summary>Optional directory of loose prompt files (overrides embed).</summary>
    public static string? PromptsDirOverride { get; set; }

    public static async Task<string> LoadBookToFountainSystemPromptAsync(
        int totalRuntimeMinutes,
        string? fallbackBody = null,
        CancellationToken ct = default)
    {
        totalRuntimeMinutes = Math.Clamp(totalRuntimeMinutes, 3, 180);
        ct.ThrowIfCancellationRequested();

        string body;
        try
        {
            body = ReadBookToFountainBody();
        }
        catch (InvalidOperationException) when (!string.IsNullOrWhiteSpace(fallbackBody))
        {
            body = fallbackBody!;
        }

        // Method is async-shaped for call-site compatibility; I/O is sync (embed/override).
        await Task.CompletedTask.ConfigureAwait(false);
        return body.Replace("{{TOTAL_RUNTIME_MINUTES}}", totalRuntimeMinutes.ToString());
    }

    public static string ReadBookToFountainBody()
    {
        var fromOverride = TryReadOverrideFile(BookToFountainRelativePath);
        if (!string.IsNullOrEmpty(fromOverride))
            return fromOverride;

        using var stream = ThisAssembly.GetManifestResourceStream(EmbeddedLogicalName);
        if (stream is not null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        var available = string.Join(", ", ThisAssembly.GetManifestResourceNames()
            .Where(n => n.Contains("Prompt", StringComparison.OrdinalIgnoreCase)));
        throw new InvalidOperationException(
            $"Prompt not embedded: {BookToFountainRelativePath}. " +
            $"Available: {(string.IsNullOrEmpty(available) ? "(none — rebuild Adaptation with prompts/)" : available)}. " +
            "Or set PAGETOMOVIE_PROMPTS_DIR to a folder with the .txt file.");
    }

    private static string? TryReadOverrideFile(string relativePath)
    {
        var dir = PromptsDirOverride
                  ?? Environment.GetEnvironmentVariable("PAGETOMOVIE_PROMPTS_DIR")
                  ?? Environment.GetEnvironmentVariable("PageToMovie_PROMPTS_DIR");
        if (string.IsNullOrWhiteSpace(dir))
            return null;

        try
        {
            var leaf = Path.GetFileName(relativePath.Replace('\\', '/'));
            var path = Path.Combine(dir.Trim(), leaf);
            if (File.Exists(path))
                return File.ReadAllText(path);
            path = Path.Combine(dir.Trim(), relativePath.Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        catch
        {
            /* ignore override failures — fall through to embed */
        }

        return null;
    }
}
