namespace PageToMovie.Engine;

/// <summary>
/// Resolve shipped prompt files. Workspace root (e.g. Railway <c>/data</c>) holds projects,
/// not necessarily <c>prompts/</c> — those ship next to the app binary or in the repo.
/// </summary>
public static class PromptFiles
{
    /// <summary>Optional absolute/relative override (env <c>PAGETOMOVIE_PROMPTS_DIR</c>).</summary>
    public static string? PromptsDirOverride { get; set; }

    /// <summary>
    /// Find <paramref name="relativePath"/> such as <c>prompts/fountain_to_cast.txt</c>.
    /// Tries workspace, app base directory, and parent folders (dev repo layout).
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

    public static async Task<string> ReadAsync(
        string relativePath,
        string? workspaceRoot = null,
        CancellationToken ct = default)
    {
        var path = Resolve(relativePath, workspaceRoot);
        if (path is null)
        {
            var tried = string.Join(" | ", CandidateRoots(workspaceRoot).Take(12));
            throw new InvalidOperationException(
                $"Prompt not found: {relativePath}. Searched under: {tried}. " +
                "Ensure prompts are published with the app (Docker) or set PAGETOMOVIE_PROMPTS_DIR.");
        }

        return await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
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
