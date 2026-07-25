using System.Reflection;

namespace PageToMovie.Engine;

/// <summary>
/// Resolve shipped prompt files. Workspace root (e.g. Railway <c>/data</c>) holds projects,
/// not necessarily <c>prompts/</c> — those ship next to the app binary, in the repo, or as
/// embedded resources in this assembly.
/// </summary>
public static class PromptFiles
{
    /// <summary>Optional absolute/relative override (env <c>PAGETOMOVIE_PROMPTS_DIR</c>).</summary>
    public static string? PromptsDirOverride { get; set; }

    private static readonly Assembly EngineAssembly = typeof(PromptFiles).Assembly;

    /// <summary>
    /// Find <paramref name="relativePath"/> such as <c>prompts/fountain_to_cast.txt</c>.
    /// Tries workspace, app base directory, and parent folders (dev repo layout).
    /// Returns null if only the embedded resource exists (use <see cref="ReadAsync"/>).
    /// </summary>
    public static string? Resolve(string relativePath, string? workspaceRoot = null)
    {
        relativePath = (relativePath ?? "").Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        if (relativePath.Length == 0) return null;

        var leaf = Path.GetFileName(relativePath);

        foreach (var root in CandidateRoots(workspaceRoot))
        {
            try
            {
                var full = Path.GetFullPath(Path.Combine(root, relativePath));
                if (File.Exists(full))
                    return full;

                // root may already be …/prompts
                if (!string.IsNullOrEmpty(leaf))
                {
                    var alt = Path.GetFullPath(Path.Combine(root, leaf));
                    if (File.Exists(alt))
                        return alt;
                }
            }
            catch
            {
                /* ignore bad paths */
            }
        }

        return null;
    }

    /// <summary>Logical name for embedded core prompts (see Engine.csproj).</summary>
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

    public static async Task<string> ReadAsync(
        string relativePath,
        string? workspaceRoot = null,
        CancellationToken ct = default)
    {
        var path = Resolve(relativePath, workspaceRoot);
        if (path is not null)
            return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);

        // Docker / Railway: prompts may only be embedded (no file next to DLL)
        var embedded = TryReadEmbedded(relativePath);
        if (!string.IsNullOrEmpty(embedded))
            return embedded;

        var tried = string.Join(" | ", CandidateRoots(workspaceRoot).Take(12));
        var resNames = string.Join(", ", EngineAssembly.GetManifestResourceNames().Take(20));
        throw new InvalidOperationException(
            $"Prompt not found: {relativePath}. " +
            "File search under: " + tried + ". " +
            "Embedded resources: " + (string.IsNullOrEmpty(resNames) ? "(none)" : resNames) + ". " +
            "Redeploy so prompts ship with the app, or set PAGETOMOVIE_PROMPTS_DIR.");
    }

    public static IEnumerable<string> CandidateRoots(string? workspaceRoot = null)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();

        void Push(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return;
            try
            {
                var full = Path.GetFullPath(p.Trim());
                if (seen.Add(full))
                    list.Add(full);
            }
            catch
            {
                /* skip */
            }
        }

        var env = PromptsDirOverride
                  ?? Environment.GetEnvironmentVariable("PAGETOMOVIE_PROMPTS_DIR")
                  ?? Environment.GetEnvironmentVariable("PageToMovie_PROMPTS_DIR");
        if (!string.IsNullOrWhiteSpace(env))
            Push(env);

        // App publish dir: /app/prompts when Docker copies prompts next to the DLL
        Push(AppContext.BaseDirectory);
        Push(Path.Combine(AppContext.BaseDirectory, "prompts"));

        // Common container layouts
        Push("/app");
        Push("/app/prompts");

        if (!string.IsNullOrWhiteSpace(workspaceRoot))
        {
            Push(workspaceRoot);
            Push(Path.Combine(workspaceRoot.Trim(), "prompts"));
        }

        // Walk up from BaseDirectory for local repo (…/bin/Debug → host → repo with prompts/)
        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
            {
                Push(dir.FullName);
                Push(Path.Combine(dir.FullName, "prompts"));
            }
        }
        catch
        {
            /* ignore */
        }

        return list;
    }
}
