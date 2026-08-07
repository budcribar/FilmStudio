namespace PageToMovie.Engine.Collaboration;

public interface IProjectLeaseService
{
    /// <summary>Acquire lease. Returns existing holder lease if conflict (caller maps to 423).</summary>
    Task<(bool Acquired, ProjectLease Lease)> TryAcquireAsync(
        string projectId, string resourceKey, string userId, TimeSpan ttl, CancellationToken ct = default);

    Task<bool> ReleaseAsync(string projectId, string resourceKey, string userId, CancellationToken ct = default);
    Task<(bool Renewed, ProjectLease? Lease)> TryRenewAsync(
        string projectId, string resourceKey, string userId, TimeSpan ttl, CancellationToken ct = default);
    Task<(bool Transferred, ProjectLease? Lease)> TryTransferAsync(
        string projectId, string resourceKey, string fromUserId, string toUserId, TimeSpan ttl, CancellationToken ct = default);
    Task<ProjectLease?> GetAsync(string projectId, string resourceKey, CancellationToken ct = default);
}
