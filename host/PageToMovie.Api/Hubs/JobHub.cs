using System.Security.Claims;
using PageToMovie.Core.Auth;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace PageToMovie.Api.Hubs;

public sealed class JobHub : Hub
{
    public const string AdminOpsGroup = "admin:ops";

    private readonly FilmJobService _jobs;
    private readonly IUserContext _user;

    public JobHub(FilmJobService jobs, IUserContext user)
    {
        _jobs = jobs;
        _user = user;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = ResolveUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        if (IsAdmin())
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminOpsGroup);

        await base.OnConnectedAsync();
    }

    public Task JoinJob(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return Task.CompletedTask;
        return Groups.AddToGroupAsync(Context.ConnectionId, $"job:{jobId.Trim()}");
    }

    public Task LeaveJob(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return Task.CompletedTask;
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job:{jobId.Trim()}");
    }

    public Task<PageToMovie.Core.Models.JobSnapshot> GetSnapshot() =>
        _jobs.GetSnapshotAsync(Context.ConnectionAborted);

    private string ResolveUserId()
    {
        // Authenticated JWT identity always wins — a client-supplied userId (query string or
        // header) must never override it, or any client could join another user's SignalR
        // group by passing ?userId=<victim> and receive their job-progress broadcasts.
        // Same priority order as HttpUserContext.UserId.
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var sub = Context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Context.User.FindFirstValue("sub")
                      ?? Context.User.Identity.Name;
            if (!string.IsNullOrWhiteSpace(sub))
                return sub.Trim();
        }

        var http = Context.GetHttpContext();
        if (http?.Request.Headers.TryGetValue(AuthHeaders.UserId, out var h) == true &&
            !string.IsNullOrWhiteSpace(h))
            return h.ToString().Trim();

        // Query-string fallback: some SignalR browser transports (long-polling reconnects,
        // certain fallback negotiations) don't reliably carry custom headers, so the client also
        // sends userId on the URL. Unauthenticated-only — never trusted over a real JWT above.
        if (http?.Request.Query.TryGetValue("userId", out var q) == true &&
            !string.IsNullOrWhiteSpace(q))
            return q.ToString().Trim();

        try { return _user.UserId; }
        catch { return "local"; }
    }

    private bool IsAdmin()
    {
        if (Context.User?.IsInRole(AppRoles.Admin) == true)
            return true;
        try { return _user.IsAdmin; }
        catch { return false; }
    }
}
