using FilmStudio.Api.Hubs;
using FilmStudio.Core.Models;
using FilmStudio.Engine;
using Microsoft.AspNetCore.SignalR;

namespace FilmStudio.Api.Services;

public sealed class SignalRJobProgressSink : IJobProgressSink
{
    private readonly IHubContext<JobHub> _hub;

    public SignalRJobProgressSink(IHubContext<JobHub> hub) => _hub = hub;

    public Task OnJobUpdatedAsync(JobSnapshot snapshot, CancellationToken ct = default) =>
        _hub.Clients.All.SendAsync(JobHubEvents.JobUpdated, snapshot, ct);

    public Task OnJobLogAsync(string message, CancellationToken ct = default) =>
        _hub.Clients.All.SendAsync(JobHubEvents.JobLog, message, ct);
}
