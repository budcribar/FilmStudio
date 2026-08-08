using System.Security.Cryptography;
using System.Text.Json;

namespace PageToMovie.Engine.Collaboration;

/// <summary>
/// Project ACL: owner / editors / viewers + pending email/username invites.
/// </summary>
public sealed class ProjectAclService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _projectsRoot;
    private readonly IProjectUserDirectory? _users;
    private readonly IProjectInviteMailer? _email;

    public ProjectAclService(string projectsRoot, IProjectUserDirectory? users = null, IProjectInviteMailer? email = null)
    {
        _projectsRoot = projectsRoot ?? throw new ArgumentNullException(nameof(projectsRoot));
        _users = users;
        _email = email;
    }

    // Back-compat ctor used by some DI registrations
    public ProjectAclService(string projectsRoot) : this(projectsRoot, null, null) { }

    public async Task<ProjectAcl> GetAsync(string projectId, CancellationToken ct = default)
    {
        var path = AclPath(projectId);
        if (!File.Exists(path))
            return new ProjectAcl { Owner = InferOwner(projectId), Rev = 1 };

        await using var fs = File.OpenRead(path);
        var acl = await JsonSerializer.DeserializeAsync<ProjectAcl>(fs, JsonOpts, ct)
                  ?? new ProjectAcl { Owner = InferOwner(projectId) };
        acl.Editors ??= new List<string>();
        acl.Viewers ??= new List<string>();
        acl.PendingInvites ??= new List<PendingInvite>();
        return acl;
    }

    public async Task<ProjectAcl> AddEditorAsync(string projectId, string userId, string callerUserId, CancellationToken ct = default)
    {
        var acl = await GetAsync(projectId, ct);
        EnsureOwner(acl, callerUserId);
        userId = Norm(userId);
        if (string.IsNullOrEmpty(userId)) throw new InvalidOperationException("userId required.");
        if (!acl.Editors.Contains(userId, StringComparer.OrdinalIgnoreCase))
            acl.Editors.Add(userId);
        acl.Viewers.RemoveAll(v => string.Equals(v, userId, StringComparison.OrdinalIgnoreCase));
        acl.Rev++;
        await SaveAsync(projectId, acl, ct);
        return acl;
    }

    public async Task<ProjectAcl> RemoveEditorAsync(string projectId, string userId, string callerUserId, CancellationToken ct = default)
    {
        var acl = await GetAsync(projectId, ct);
        EnsureOwner(acl, callerUserId);
        userId = Norm(userId);
        acl.Editors.RemoveAll(e => string.Equals(e, userId, StringComparison.OrdinalIgnoreCase));
        acl.Rev++;
        await SaveAsync(projectId, acl, ct);
        return acl;
    }

    public async Task<ProjectAcl> AddViewerAsync(string projectId, string userId, string callerUserId, CancellationToken ct = default)
    {
        var acl = await GetAsync(projectId, ct);
        EnsureOwner(acl, callerUserId);
        userId = Norm(userId);
        if (string.IsNullOrEmpty(userId)) throw new InvalidOperationException("userId required.");
        if (acl.Editors.Contains(userId, StringComparer.OrdinalIgnoreCase))
            return acl;
        if (!acl.Viewers.Contains(userId, StringComparer.OrdinalIgnoreCase))
            acl.Viewers.Add(userId);
        acl.Rev++;
        await SaveAsync(projectId, acl, ct);
        return acl;
    }

    public async Task<ProjectAcl> RemoveViewerAsync(string projectId, string userId, string callerUserId, CancellationToken ct = default)
    {
        var acl = await GetAsync(projectId, ct);
        EnsureOwner(acl, callerUserId);
        userId = Norm(userId);
        acl.Viewers.RemoveAll(v => string.Equals(v, userId, StringComparison.OrdinalIgnoreCase));
        acl.Rev++;
        await SaveAsync(projectId, acl, ct);
        return acl;
    }

    public async Task<InviteResult> InviteByUsernameAsync(
        string projectId,
        string usernameOrEmail,
        string role,
        string callerUserId,
        string? publicBaseUrl = null,
        CancellationToken ct = default)
    {
        var acl = await GetAsync(projectId, ct);
        EnsureOwner(acl, callerUserId);
        usernameOrEmail = Norm(usernameOrEmail);
        if (string.IsNullOrEmpty(usernameOrEmail))
            return new InviteResult { Ok = false, Error = "Username or email required." };

        role = string.Equals(role, "viewer", StringComparison.OrdinalIgnoreCase) ? "viewer" : "editor";

        ProjectUserInfo? user = null;
        if (_users is not null)
        {
            user = await _users.FindByUsernameAsync(usernameOrEmail, ct)
                   ?? await _users.FindByEmailAsync(usernameOrEmail, ct)
                   ?? await _users.FindByIdAsync(usernameOrEmail, ct);
        }

        if (user is not null && !string.IsNullOrWhiteSpace(user.UserId))
        {
            if (role == "viewer")
                await AddViewerAsync(projectId, user.UserId, callerUserId, ct);
            else
                await AddEditorAsync(projectId, user.UserId, callerUserId, ct);

            acl = await GetAsync(projectId, ct);
            acl.PendingInvites.RemoveAll(i => Matches(i, usernameOrEmail, user.UserId, user.Email));
            acl.Rev++;
            await SaveAsync(projectId, acl, ct);
            return new InviteResult
            {
                Ok = true, Status = "granted", UserId = user.UserId, Role = role,
                Message = $"Granted {role} to {user.UserId}."
            };
        }

        // Pending
        acl = await GetAsync(projectId, ct);
        var existing = acl.PendingInvites.FirstOrDefault(i =>
            string.Equals(i.Username, usernameOrEmail, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Email, usernameOrEmail, StringComparison.OrdinalIgnoreCase));
        var token = existing?.Token ?? CreateToken();
        var isEmail = usernameOrEmail.Contains('@');
        var invite = new PendingInvite
        {
            Username = isEmail ? null : usernameOrEmail,
            Email = isEmail ? usernameOrEmail : existing?.Email,
            Role = role,
            Token = token,
            InvitedBy = callerUserId,
            CreatedUtc = existing?.CreatedUtc ?? DateTimeOffset.UtcNow,
            LastSentUtc = DateTimeOffset.UtcNow
        };
        acl.PendingInvites.RemoveAll(i =>
            string.Equals(i.Username, usernameOrEmail, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Email, usernameOrEmail, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Token, token, StringComparison.Ordinal));
        acl.PendingInvites.Add(invite);
        acl.Rev++;
        await SaveAsync(projectId, acl, ct);

        var acceptUrl = string.IsNullOrWhiteSpace(publicBaseUrl)
            ? $"/invite/{token}"
            : $"{publicBaseUrl.TrimEnd('/')}/invite/{token}";

        var emailed = false;
        if (isEmail && _email is not null)
        {
            var subject = $"You're invited to a PageToMovie project ({projectId})";
            var body =
                $"You've been invited as {role} on project \"{projectId}\".\n\n" +
                $"Accept:\n{acceptUrl}\n\n" +
                "Sign in first (if needed), then open the link while signed in.\n";
            try { await _email.SendAsync(usernameOrEmail, subject, body, ct); emailed = true; }
            catch { /* pending remains */ }
        }

        return new InviteResult
        {
            Ok = true,
            Status = "pending",
            Role = role,
            Token = token,
            InviteLink = acceptUrl,
            EmailSent = emailed,
            Message = emailed
                ? $"Invite email sent to {usernameOrEmail}."
                : isEmail
                    ? $"Pending invite for {usernameOrEmail} (email not sent — check mail config)."
                    : $"Pending invite for '{usernameOrEmail}'. Share the invite link."
        };
    }

    public async Task<InviteResult> ResendInviteAsync(
        string projectId, string usernameOrEmail, string callerUserId,
        string? publicBaseUrl = null, CancellationToken ct = default)
    {
        var acl = await GetAsync(projectId, ct);
        EnsureOwner(acl, callerUserId);
        var key = Norm(usernameOrEmail);
        var inv = acl.PendingInvites.FirstOrDefault(i =>
            string.Equals(i.Username, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Email, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Token, key, StringComparison.Ordinal));
        if (inv is null)
            return new InviteResult { Ok = false, Error = "No pending invite found." };
        return await InviteByUsernameAsync(projectId, inv.Email ?? inv.Username ?? usernameOrEmail,
            inv.Role, callerUserId, publicBaseUrl, ct);
    }

    public async Task<ProjectAcl> RevokeInviteAsync(string projectId, string key, string callerUserId, CancellationToken ct = default)
    {
        var acl = await GetAsync(projectId, ct);
        EnsureOwner(acl, callerUserId);
        key = Norm(key);
        acl.PendingInvites.RemoveAll(i =>
            string.Equals(i.Username, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Email, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(i.Token, key, StringComparison.Ordinal));
        acl.Rev++;
        await SaveAsync(projectId, acl, ct);
        return acl;
    }

    public async Task<(string ProjectId, PendingInvite Invite)?> FindInviteByTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || !Directory.Exists(_projectsRoot)) return null;
        token = token.Trim();
        foreach (var dir in Directory.EnumerateDirectories(_projectsRoot))
        {
            var projectId = Path.GetFileName(dir);
            if (string.IsNullOrWhiteSpace(projectId) || projectId.StartsWith('.')) continue;
            try
            {
                var acl = await GetAsync(projectId, ct);
                var inv = acl.PendingInvites.FirstOrDefault(i => string.Equals(i.Token, token, StringComparison.Ordinal));
                if (inv is not null) return (projectId, inv);
            }
            catch { }
        }
        return null;
    }

    public async Task<(bool Ok, string? ProjectId, string? Error)> AcceptInviteAsync(
        string token, string acceptingUserId, string? acceptingEmail = null, string? acceptingUsername = null,
        CancellationToken ct = default)
    {
        acceptingUserId = Norm(acceptingUserId);
        if (string.IsNullOrEmpty(acceptingUserId))
            return (false, null, "Not signed in.");

        var found = await FindInviteByTokenAsync(token, ct);
        if (found is null) return (false, null, "Invite not found or already used.");

        var (projectId, inv) = found.Value;
        var acl = await GetAsync(projectId, ct);
        if (string.Equals(inv.Role, "viewer", StringComparison.OrdinalIgnoreCase))
        {
            if (!acl.Editors.Contains(acceptingUserId, StringComparer.OrdinalIgnoreCase) &&
                !acl.Viewers.Contains(acceptingUserId, StringComparer.OrdinalIgnoreCase))
                acl.Viewers.Add(acceptingUserId);
        }
        else
        {
            if (!acl.Editors.Contains(acceptingUserId, StringComparer.OrdinalIgnoreCase))
                acl.Editors.Add(acceptingUserId);
            acl.Viewers.RemoveAll(v => string.Equals(v, acceptingUserId, StringComparison.OrdinalIgnoreCase));
        }
        acl.PendingInvites.RemoveAll(i => string.Equals(i.Token, token, StringComparison.Ordinal));
        acl.Rev++;
        await SaveAsync(projectId, acl, ct);
        return (true, projectId, null);
    }

    public bool CanEdit(ProjectAcl acl, string userId)
    {
        userId = Norm(userId);
        if (string.IsNullOrEmpty(userId)) return false;
        return string.Equals(acl.Owner, userId, StringComparison.OrdinalIgnoreCase)
               || acl.Editors.Contains(userId, StringComparer.OrdinalIgnoreCase);
    }

    public bool CanView(ProjectAcl acl, string userId) =>
        CanEdit(acl, userId) || acl.Viewers.Contains(Norm(userId), StringComparer.OrdinalIgnoreCase);

    private async Task SaveAsync(string projectId, ProjectAcl acl, CancellationToken ct)
    {
        var dir = Path.Combine(_projectsRoot, projectId);
        Directory.CreateDirectory(dir);
        var path = AclPath(projectId);
        var tmp = path + ".tmp";
        await using (var fs = File.Create(tmp))
            await JsonSerializer.SerializeAsync(fs, acl, JsonOpts, ct);
        File.Copy(tmp, path, overwrite: true);
        try { File.Delete(tmp); } catch { }
    }

    private string AclPath(string projectId) => Path.Combine(_projectsRoot, projectId, "project-acl.json");
    private static string InferOwner(string projectId)
    {
        var parts = projectId.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : projectId;
    }
    private static void EnsureOwner(ProjectAcl acl, string callerUserId)
    {
        if (!string.Equals(acl.Owner, Norm(callerUserId), StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Only the project owner can modify ACL.");
    }
    private static string Norm(string? s) => (s ?? "").Trim();
    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(24);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
    private static bool Matches(PendingInvite i, string? username, string? userId, string? email)
    {
        if (!string.IsNullOrWhiteSpace(userId) && string.Equals(i.Username, userId, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrWhiteSpace(username) && string.Equals(i.Username, username, StringComparison.OrdinalIgnoreCase)) return true;
        if (!string.IsNullOrWhiteSpace(email) && string.Equals(i.Email, email, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}

public sealed class ProjectAcl
{
    public string Owner { get; set; } = "";
    public List<string> Editors { get; set; } = new();
    public List<string> Viewers { get; set; } = new();
    public List<PendingInvite> PendingInvites { get; set; } = new();
    public int Rev { get; set; } = 1;
}

public sealed class PendingInvite
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string Role { get; set; } = "editor";
    public string Token { get; set; } = "";
    public string? InvitedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? LastSentUtc { get; set; }
}

public sealed class InviteResult
{
    public bool Ok { get; set; }
    public string? Status { get; set; }
    public string? UserId { get; set; }
    public string? Role { get; set; }
    public string? Token { get; set; }
    public string? InviteLink { get; set; }
    public bool EmailSent { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
}

public sealed class ProjectUserInfo
{
    public string UserId { get; set; } = "";
    public string? Username { get; set; }
    public string? Email { get; set; }
}

public interface IProjectUserDirectory
{
    Task<ProjectUserInfo?> FindByIdAsync(string userId, CancellationToken ct = default);
    Task<ProjectUserInfo?> FindByUsernameAsync(string username, CancellationToken ct = default);
    Task<ProjectUserInfo?> FindByEmailAsync(string email, CancellationToken ct = default);
}

public interface IProjectInviteMailer
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
