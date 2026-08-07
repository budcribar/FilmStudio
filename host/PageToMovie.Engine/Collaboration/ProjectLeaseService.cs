using System.Text.Json;

namespace PageToMovie.Engine.Collaboration;

public sealed class ProjectLeaseService : IProjectLeaseService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly ProjectStore _store;
    private readonly object _gate = new();

    public ProjectLeaseService(ProjectStore store) => _store = store;

    private string LeasePath(string projectId, string resourceKey)
    {
        var safe = string.Join("_", resourceKey.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return Path.Combine(_store.GetProjectDir(projectId), "leases", safe + ".json");
    }

    public Task<ProjectLease?> GetAsync(string projectId, string resourceKey, CancellationToken ct = default)
    {
        var path = LeasePath(projectId, resourceKey);
        if (!File.Exists(path)) return Task.FromResult<ProjectLease?>(null);
        var json = File.ReadAllText(path);
        var lease = JsonSerializer.Deserialize<ProjectLease>(json, JsonOpts);
        if (lease is not null && lease.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            try { File.Delete(path); } catch { /* ignore */ }
            return Task.FromResult<ProjectLease?>(null);
        }
        return Task.FromResult(lease);
    }

    public async Task<(bool Acquired, ProjectLease Lease)> TryAcquireAsync(
        string projectId, string resourceKey, string userId, TimeSpan ttl, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var path = LeasePath(projectId, resourceKey);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var existing = GetAsync(projectId, resourceKey, ct).GetAwaiter().GetResult();
            if (existing is not null
                && !string.Equals(existing.HolderUserId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return (false, existing);
            }

            var lease = new ProjectLease
            {
                ResourceKey = resourceKey,
                HolderUserId = userId,
                AcquiredAt = existing?.AcquiredAt ?? DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(ttl),
            };
            WriteLease(path, lease);
            return (true, lease);
        }
    }

    public Task<bool> ReleaseAsync(string projectId, string resourceKey, string userId, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var path = LeasePath(projectId, resourceKey);
            var existing = GetAsync(projectId, resourceKey, ct).GetAwaiter().GetResult();
            if (existing is null) return Task.FromResult(true);
            if (!string.Equals(existing.HolderUserId, userId, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(false);
            try { File.Delete(path); } catch { /* ignore */ }
            return Task.FromResult(true);
        }
    }

    public Task<(bool Renewed, ProjectLease? Lease)> TryRenewAsync(
        string projectId, string resourceKey, string userId, TimeSpan ttl, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var path = LeasePath(projectId, resourceKey);
            var existing = GetAsync(projectId, resourceKey, ct).GetAwaiter().GetResult();
            if (existing is null
                || !string.Equals(existing.HolderUserId, userId, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<(bool, ProjectLease?)>((false, existing));
            existing.ExpiresAt = DateTimeOffset.UtcNow.Add(ttl);
            WriteLease(path, existing);
            return Task.FromResult<(bool, ProjectLease?)>((true, existing));
        }
    }

    public Task<(bool Transferred, ProjectLease? Lease)> TryTransferAsync(
        string projectId, string resourceKey, string fromUserId, string toUserId, TimeSpan ttl, CancellationToken ct = default)
    {
        lock (_gate)
        {
            var path = LeasePath(projectId, resourceKey);
            var existing = GetAsync(projectId, resourceKey, ct).GetAwaiter().GetResult();
            if (existing is null
                || !string.Equals(existing.HolderUserId, fromUserId, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<(bool, ProjectLease?)>((false, existing));
            existing.HolderUserId = toUserId;
            existing.AcquiredAt = DateTimeOffset.UtcNow;
            existing.ExpiresAt = DateTimeOffset.UtcNow.Add(ttl);
            WriteLease(path, existing);
            return Task.FromResult<(bool, ProjectLease?)>((true, existing));
        }
    }

    private static void WriteLease(string path, ProjectLease lease)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(lease, JsonOpts));
        File.Move(tmp, path, overwrite: true);
    }
}
