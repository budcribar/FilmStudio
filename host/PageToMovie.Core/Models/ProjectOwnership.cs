namespace PageToMovie.Core.Models;

/// <summary>
/// Who may see a project in GET /api/projects. Matches stable user id, username handle,
/// email, and sanitized folder owner segments so identity drift (email vs handle, dots
/// in names) does not hide projects that still exist on disk.
/// </summary>
public static class ProjectOwnership
{
    /// <summary>
    /// Folder / id segment sanitize aligned with ProjectStore.SanitizeUserSegment
    /// (letters, digits, _ -; whitespace/dot/slash → _; lowercased).
    /// </summary>
    public static string SanitizeOwnerSegment(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var sb = new System.Text.StringBuilder(raw.Length);
        foreach (var ch in raw.Trim())
        {
            if (char.IsAsciiLetterOrDigit(ch) || ch is '_' or '-')
                sb.Append(ch);
            else if (char.IsWhiteSpace(ch) || ch is '.' or '/' or '@')
            {
                if (sb.Length > 0 && sb[^1] != '_')
                    sb.Append('_');
            }
        }
        var id = sb.ToString().Trim('_').ToLowerInvariant();
        if (id.Length > 64) id = id[..64].Trim('_');
        return id;
    }

    public static IReadOnlyList<string> CollectAliases(
        string? requestUserId,
        string? canonicalUserId = null,
        string? username = null,
        string? email = null)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return;
            var t = v.Trim();
            set.Add(t);
            var seg = SanitizeOwnerSegment(t);
            if (seg.Length > 0) set.Add(seg);
            // Email-shaped id/handle → also register the local-part (before @). After the email→user-id
            // migration a session can still identify as "name@host" (as requestUserId), while projects are
            // owned by the bare "name"; applied to EVERY input, not just the email param, so a stale
            // email-shaped session id still resolves to the owner handle instead of hiding all its projects.
            var at = t.IndexOf('@');
            if (at > 0)
            {
                var local = t[..at];
                set.Add(local);
                var localSeg = SanitizeOwnerSegment(local);
                if (localSeg.Length > 0) set.Add(localSeg);
            }
        }

        Add(requestUserId);
        Add(canonicalUserId);
        Add(username);
        Add(email);
        return set.ToList();
    }

    public static bool IsOwnedBy(ProjectInfo project, IEnumerable<string> aliases)
    {
        if (project is null) return false;
        var aliasSet = aliases as HashSet<string>
                       ?? new HashSet<string>(aliases, StringComparer.OrdinalIgnoreCase);
        if (aliasSet.Count == 0) return false;

        if (!string.IsNullOrWhiteSpace(project.OwnerUserId))
        {
            var owner = project.OwnerUserId.Trim();
            if (aliasSet.Contains(owner)) return true;
            var ownerSeg = SanitizeOwnerSegment(owner);
            if (ownerSeg.Length > 0 && aliasSet.Contains(ownerSeg)) return true;
        }

        // Path projects/{ownerSeg}/{slug}
        var id = (project.Id ?? "").Replace('\\', '/').Trim('/');
        var slash = id.IndexOf('/');
        if (slash > 0)
        {
            var folderOwner = id[..slash];
            if (aliasSet.Contains(folderOwner)) return true;
            var folderSeg = SanitizeOwnerSegment(folderOwner);
            if (folderSeg.Length > 0 && aliasSet.Contains(folderSeg)) return true;
        }

        return false;
    }

    public static bool IsOwnedBy(
        ProjectInfo project,
        string? requestUserId,
        string? canonicalUserId = null,
        string? username = null,
        string? email = null) =>
        IsOwnedBy(project, CollectAliases(requestUserId, canonicalUserId, username, email));
}
