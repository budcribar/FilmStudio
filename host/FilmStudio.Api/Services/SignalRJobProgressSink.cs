using FilmStudio.Api.Hubs;
using FilmStudio.Core.Models;
using FilmStudio.Engine;
using Microsoft.AspNetCore.SignalR;
// JobHubEvents lives in FilmStudio.Core.Models

namespace FilmStudio.Api.Services;

public sealed class SignalRJobProgressSink : IJobProgressSink
{
    private readonly IHubContext<JobHub> _hub;

    public SignalRJobProgressSink(IHubContext<JobHub> hub) => _hub = hub;

    public async Task OnJobUpdatedAsync(JobSnapshot snapshot, CancellationToken ct = default)
    {
        // Compat: all clients (legacy single-user UI)
        await _hub.Clients.All.SendAsync(JobHubEvents.JobUpdated, snapshot, ct);

        if (!string.IsNullOrWhiteSpace(snapshot.JobId))
            await _hub.Clients.Group($"job:{snapshot.JobId}")
                .SendAsync(JobHubEvents.JobUpdated, snapshot, ct);

        if (!string.IsNullOrWhiteSpace(snapshot.UserId))
            await _hub.Clients.Group($"user:{snapshot.UserId}")
                .SendAsync(JobHubEvents.JobUpdated, snapshot, ct);
    }

    public async Task OnJobLogAsync(string message, CancellationToken ct = default)
    {
        await _hub.Clients.All.SendAsync(JobHubEvents.JobLog, message, ct);
    }
}
