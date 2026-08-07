using System.Text.Json;

namespace PageToMovie.Engine.Collaboration;

public sealed class ProjectAclService : IProjectAclService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ProjectStore _store;

    public ProjectAclService(ProjectStore store) => _store = store;

    private string AclPath(string projectId) =>
        Path.Combine(_store.GetProjectDir(projectId), "project-acl.json");

    public async Task<ProjectAclDocument?> GetAclAsync(string projectId, CancellationToken ct = default)
    {
        var path = AclPath(projectId);
        if (!File.Exists(path)) return null;
        await using var fs = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ProjectAclDocument>(fs, JsonOpts, ct);
    }

    public async Task<ProjectAclDocument> GetOrCreateAclAsync(string projectId, string ownerUserId, CancellationToken ct = default)
    {
        var existing = await GetAclAsync(projectId, ct);
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.OwnerUserId) && !string.IsNullOrWhiteSpace(ownerUserId))
            {
                existing.OwnerUserId = ownerUserId;
                await SaveAclAsync(projectId, existing, ct);
            }
            return existing;
        }

        var dir = _store.GetProjectDir(projectId);
        Directory.CreateDirectory(dir);
        var acl = new ProjectAclDocument
        {
            OwnerUserId = ownerUserId,
            Rev = 1,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        await SaveAclAsync(projectId, acl, ct);
        return acl;
    }

    public async Task SaveAclAsync(string projectId, ProjectAclDocument acl, CancellationToken ct = default)
    {
        acl.UpdatedAt = DateTimeOffset.UtcNow;
        var path = AclPath(projectId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        await using (var fs = File.Create(tmp))
            await JsonSerializer.SerializeAsync(fs, acl, JsonOpts, ct);
        File.Move(tmp, path, overwrite: true);
    }

    public async Task<ProjectAccessLevel> GetAccessLevelAsync(string projectId, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return ProjectAccessLevel.None;
        var acl = await GetAclAsync(projectId, ct);
        if (acl is null)
        {
            // Legacy projects: treat path owner segment as owner when projectId is "owner/name"
            var slash = projectId.IndexOf('/');
            if (slash > 0)
            {
                var pathOwner = projectId[..slash];
                if (string.Equals(pathOwner, userId, StringComparison.OrdinalIgnoreCase))
                    return ProjectAccessLevel.Owner;
            }
            return ProjectAccessLevel.None;
        }

        if (string.Equals(acl.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            return ProjectAccessLevel.Owner;
        if (acl.Editors.Any(e => string.Equals(e, userId, StringComparison.OrdinalIgnoreCase)))
            return ProjectAccessLevel.Editor;
        if (acl.Viewers.Any(v => string.Equals(v, userId, StringComparison.OrdinalIgnoreCase)))
            return ProjectAccessLevel.Viewer;
        return ProjectAccessLevel.None;
    }

    public async Task<bool> CanAccessAsync(string projectId, string userId, ProjectAccessLevel minimum, CancellationToken ct = default)
    {
        var level = await GetAccessLevelAsync(projectId, userId, ct);
        return level >= minimum;
    }

    public async Task InviteEditorAsync(string projectId, string ownerUserId, string editorUserId, CancellationToken ct = default)
    {
        var acl = await GetOrCreateAclAsync(projectId, ownerUserId, ct);
        EnsureOwner(acl, ownerUserId);
        if (string.Equals(acl.OwnerUserId, editorUserId, StringComparison.OrdinalIgnoreCase))
            return;
        acl.Viewers.RemoveAll(v => string.Equals(v, editorUserId, StringComparison.OrdinalIgnoreCase));
        if (!acl.Editors.Any(e => string.Equals(e, editorUserId, StringComparison.OrdinalIgnoreCase)))
            acl.Editors.Add(editorUserId);
        acl.Rev++;
        await SaveAclAsync(projectId, acl, ct);
    }

    public async Task RemoveEditorAsync(string projectId, string ownerUserId, string editorUserId, CancellationToken ct = default)
    {
        var acl = await GetOrCreateAclAsync(projectId, ownerUserId, ct);
        EnsureOwner(acl, ownerUserId);
        acl.Editors.RemoveAll(e => string.Equals(e, editorUserId, StringComparison.OrdinalIgnoreCase));
        acl.Rev++;
        await SaveAclAsync(projectId, acl, ct);
    }

    public async Task InviteViewerAsync(string projectId, string ownerUserId, string viewerUserId, CancellationToken ct = default)
    {
        var acl = await GetOrCreateAclAsync(projectId, ownerUserId, ct);
        EnsureOwner(acl, ownerUserId);
        if (string.Equals(acl.OwnerUserId, viewerUserId, StringComparison.OrdinalIgnoreCase))
            return;
        if (acl.Editors.Any(e => string.Equals(e, viewerUserId, StringComparison.OrdinalIgnoreCase)))
            return; // already higher
        if (!acl.Viewers.Any(v => string.Equals(v, viewerUserId, StringComparison.OrdinalIgnoreCase)))
            acl.Viewers.Add(viewerUserId);
        acl.Rev++;
        await SaveAclAsync(projectId, acl, ct);
    }

    public async Task RemoveViewerAsync(string projectId, string ownerUserId, string viewerUserId, CancellationToken ct = default)
    {
        var acl = await GetOrCreateAclAsync(projectId, ownerUserId, ct);
        EnsureOwner(acl, ownerUserId);
        acl.Viewers.RemoveAll(v => string.Equals(v, viewerUserId, StringComparison.OrdinalIgnoreCase));
        acl.Rev++;
        await SaveAclAsync(projectId, acl, ct);
    }

    private static void EnsureOwner(ProjectAclDocument acl, string userId)
    {
        if (!string.Equals(acl.OwnerUserId, userId, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Only the project owner can modify ACL.");
    }
}
