using System.Reflection;
using System.Text.RegularExpressions;

namespace PageToMovie.Adaptation.Conversion;

/// <summary>
/// Loads the book → Fountain prompt pack. Prefers embedded resource in this assembly,
/// optional override via <c>PAGETOMOVIE_PROMPTS_DIR</c> (same env as Engine PromptFiles).
/// </summary>
public static class AdaptationPromptPack
{
    public const string BookToFountainRelativePath = "prompts/book_to_fountain.txt";
    public const string EmbeddedLogicalName = "PageToMovie.Adaptation.Prompts.book_to_fountain.txt";

    /// <summary>
    /// Injected when no artificial runtime target is set (product default).
    /// Model must finish the whole story without padding to a minute budget.
    /// </summary>
    public const string UnlimitedRuntimeDirective =
        "unlimited — adapt at natural length; finish the whole story; " +
        "do NOT invent incidents, reprises, or business to fill time; " +
        "do NOT pad to any minute band";

    private static readonly Assembly ThisAssembly = typeof(AdaptationPromptPack).Assembly;

    /// <summary>Optional directory of loose prompt files (overrides embed).</summary>
    public static string? PromptsDirOverride { get; set; }

    /// <summary>
    /// Build the Stage‑1 system prompt. Pass <paramref name="totalRuntimeMinutes"/> null or ≤0
    /// for <see cref="UnlimitedRuntimeDirective"/> (default product behavior).
    /// </summary>
    public static async Task<string> LoadBookToFountainSystemPromptAsync(
        int? totalRuntimeMinutes = null,
        string? fallbackBody = null,
        CancellationToken ct = default)
    {
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

        await Task.CompletedTask.ConfigureAwait(false);
        return ApplyRuntimeTokens(body, totalRuntimeMinutes);
    }

    /// <summary>
    /// Substitute runtime tokens. Unlimited when minutes is null or ≤0.
    /// Supports <c>{{RUNTIME_TARGET_DIRECTIVE}}</c> (v4) and <c>{{TOTAL_RUNTIME_MINUTES}}</c> (legacy).
    /// </summary>
    public static string ApplyRuntimeTokens(string body, int? totalRuntimeMinutes)
    {
        var unlimited = totalRuntimeMinutes is null or <= 0;
        var minutes = unlimited
            ? 0
            : Math.Clamp(totalRuntimeMinutes!.Value, 1, 180);

        var directive = unlimited
            ? UnlimitedRuntimeDirective
            : $"Target about {minutes} minutes of finished film. Keep the adaptation tight; do not pad beyond that budget.";

        body = body.Replace("{{RUNTIME_TARGET_DIRECTIVE}}", directive, StringComparison.Ordinal);

        if (unlimited)
        {
            // Legacy template phrases that assumed a numeric {{TOTAL_RUNTIME_MINUTES}}.
            body = Regex.Replace(
                body,
                @"in roughly \{\{TOTAL_RUNTIME_MINUTES\}\} minutes\s+of finished film",
                "at natural length with no artificial minute target",
                RegexOptions.IgnoreCase);
            body = Regex.Replace(
                body,
                @"Target about \{\{TOTAL_RUNTIME_MINUTES\}\} minutes of finished film\.?",
                "Runtime target: " + UnlimitedRuntimeDirective + ".",
                RegexOptions.IgnoreCase);
            body = body.Replace("{{TOTAL_RUNTIME_MINUTES}}", "unlimited (natural length)", StringComparison.Ordinal);
        }
        else
        {
            body = body.Replace("{{TOTAL_RUNTIME_MINUTES}}", minutes.ToString(), StringComparison.Ordinal);
        }

        return body;
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
                  ?? Environment.GetEnvironmentVariable("PAGETOMOVIE_PROMPTS_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return null;
        var full = Path.Combine(dir, Path.GetFileName(relativePath));
        return File.Exists(full) ? File.ReadAllText(full) : null;
    }
}
