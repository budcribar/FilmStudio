using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using PageToMovie.Adaptation.Conversion;

namespace PageToMovie.Adaptation;

/// <summary>
/// Short stable identity for the Stage‑1 Adaptation surface (DLL + embedded prompt).
/// ScreenplayBenchmark includes this in disk-cache file names and historical runs so
/// converter / heuristic / embedded-prompt changes bust cache without a prompt-git-only lie.
/// </summary>
public static class AdaptationVersion
{
    private static readonly Lazy<string> CachedId = new(ComputeId);

    /// <summary>12-char lowercase hex id (see <see cref="ComputeId"/> for method).</summary>
    public static string Current => CachedId.Value;

    /// <summary>
    /// <para><b>Method:</b> SHA-256 over the UTF-8 material string</para>
    /// <code>
    /// {AssemblyName}|{InformationalVersion}|{sha256_hex(embedded book_to_fountain body)}
    /// </code>
    /// truncated to the first 12 hex characters (lowercase).
    ///
    /// <list type="bullet">
    /// <item><b>Assembly name</b> — keeps the fingerprint namespaced if other modules adopt the same pattern.</item>
    /// <item><b>InformationalVersion</b> — MSBuild/SourceLink product version (typically includes source revision, e.g. <c>1.0.0+abcdef…</c>).</item>
    /// <item><b>Embedded prompt content hash</b> — Stage‑1 <c>book_to_fountain.txt</c> body from the assembly
    /// resource (not disk override). A re-embed invalidates identity even when the version attribute is unchanged.</item>
    /// </list>
    /// Disk prompt overrides are out of scope here. The ScreenplayBenchmark refuses to start
    /// when Stage‑1 prompts or <c>host/PageToMovie.Adaptation/</c> sources are dirty
    /// (<c>TryGetCommittedStage1Surface</c>; override with <c>--allow-dirty</c> for local experiments only).
    /// </summary>
    public static string ComputeId()
    {
        var assembly = typeof(AdaptationVersion).Assembly;
        var name = assembly.GetName().Name ?? "PageToMovie.Adaptation";
        var infoVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";

        var promptBody = ReadEmbeddedBookToFountainBody(assembly);
        var promptSha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(promptBody)))
            .ToLowerInvariant();
        var material = $"{name}|{infoVersion}|{promptSha}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)))
            .ToLowerInvariant();
        return hash.Length >= 12 ? hash[..12] : hash.PadRight(12, '0');
    }

    private static string ReadEmbeddedBookToFountainBody(Assembly assembly)
    {
        using var stream = assembly.GetManifestResourceStream(AdaptationPromptPack.EmbeddedLogicalName);
        if (stream is null)
            return "";
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
