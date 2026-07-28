using System.Reflection;

namespace PageToMovie.Engine;

/// <summary>
/// Load shipped prompts from embedded resources in this assembly.
/// Optional file override: set env <c>PAGETOMOVIE_PROMPTS_DIR</c> to a folder containing
/// the prompt filenames (for local edit without rebuild). Workspace <c>/data</c> is never required.
/// </summary>
public static class PromptFiles
{
    private static readonly Assembly EngineAssembly = typeof(PromptFiles).Assembly;

    /// <summary>Optional directory of loose prompt files (overrides embed). Env: PAGETOMOVIE_PROMPTS_DIR.</summary>
    public static string? PromptsDirOverride { get; set; }

    public static string EmbeddedLogicalName(string relativePath)
    {
        var leaf = Path.GetFileName(relativePath.Replace('\\', '/'));
        return "PageToMovie.Prompts." + leaf;
    }

    public static string? TryReadEmbedded(string relativePath)
    {
        var name = EmbeddedLogicalName(relativePath);
        using var stream = EngineAssembly.GetManifestResourceStream(name);
        if (stream is null) return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Read a prompt synchronously (no actual async I/O ever happens here — embedded-resource and
    /// override-file reads are both synchronous under the hood). Callers that don't already need to
    /// be async should use this directly instead of blocking on <see cref="ReadAsync"/>.
    /// </summary>
    public static string? TryRead(string relativePath) =>
        TryReadOverrideFile(relativePath) ?? TryReadEmbedded(relativePath);

    /// <summary>
    /// Read a prompt. Prefer optional override dir, then embedded resource.
    /// <paramref name="workspaceRoot"/> is ignored (kept for call-site compatibility).
    /// </summary>
    public static Task<string> ReadAsync(
        string relativePath,
        string? workspaceRoot = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var text = TryRead(relativePath);
        if (!string.IsNullOrEmpty(text))
            return Task.FromResult(text);

        var res = string.Join(", ", EngineAssembly.GetManifestResourceNames()
            .Where(n => n.Contains("Prompt", StringComparison.OrdinalIgnoreCase)));
        throw new InvalidOperationException(
            $"Prompt not embedded: {relativePath}. " +
            $"Available: {(string.IsNullOrEmpty(res) ? "(none — rebuild Engine with prompts/)" : res)}. " +
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
            // Allow full relative path under override dir
            path = Path.Combine(dir.Trim(), relativePath.Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar));
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        catch
        {
            /* ignore */
        }

        return null;
    }
}
