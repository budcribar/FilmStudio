namespace PageToMovie.Engine.Collaboration;

public interface IProjectPresenceService
{
    Task HeartbeatAsync(string projectId, string userId, string? connectionId, CancellationToken ct = default);
    Task LeaveAsync(string projectId, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectPresenceEntry>> ListAsync(string projectId, CancellationToken ct = default);
}
