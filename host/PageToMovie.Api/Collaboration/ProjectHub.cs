using Microsoft.AspNetCore.SignalR;
using PageToMovie.Engine.Abstractions;
using PageToMovie.Engine.Collaboration;

namespace PageToMovie.Api.Collaboration;

public sealed class ProjectHub : Hub
{
    private readonly IProjectPresenceService _presence;
    private readonly IProjectAclService _acl;
    private readonly IUserContext _user;

    public ProjectHub(IProjectPresenceService presence, IProjectAclService acl, IUserContext user)
    {
        _presence = presence;
        _acl = acl;
        _user = user;
    }

    public async Task JoinProject(string projectId)
    {
        var userId = _user.UserId;
        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Not authenticated");
        if (!await _acl.CanAccessAsync(projectId, userId, ProjectAccessLevel.Viewer))
            throw new HubException("Forbidden");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));
        await _presence.HeartbeatAsync(projectId, userId, Context.ConnectionId);
        await Clients.Group(GroupName(projectId)).SendAsync("PresenceChanged", projectId);
    }

    public async Task LeaveProject(string projectId)
    {
        var userId = _user.UserId;
        if (!string.IsNullOrWhiteSpace(userId))
            await _presence.LeaveAsync(projectId, userId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(projectId));
        await Clients.Group(GroupName(projectId)).SendAsync("PresenceChanged", projectId);
    }

    public async Task Heartbeat(string projectId)
    {
        var userId = _user.UserId;
        if (string.IsNullOrWhiteSpace(userId)) return;
        await _presence.HeartbeatAsync(projectId, userId, Context.ConnectionId);
    }

    public static string GroupName(string projectId) => "project:" + projectId;
}
