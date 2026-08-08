namespace PageToMovie.Core.Utils;

/// <summary>
/// Canonical utility for sanitizing filenames and path segments against invalid filesystem characters.
/// </summary>
public static class FileNameSanitizer
{
    private static readonly HashSet<char> InvalidChars = new(Path.GetInvalidFileNameChars());

    /// <summary>
    /// Replaces invalid filename characters with the specified replacement char (default '_').
    /// Also strips directory separators Unix '/' and Windows '\'.
    /// </summary>
    public static string SanitizeFileName(string? name, char replacement = '_')
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (InvalidChars.Contains(chars[i]) || chars[i] == '/' || chars[i] == '\\')
            {
                chars[i] = replacement;
            }
        }
        return new string(chars);
    }
}
