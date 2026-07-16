using FilmStudio.Engine;
using Microsoft.AspNetCore.SignalR;

namespace FilmStudio.Api.Hubs;

public sealed class JobHub : Hub
{
    private readonly FilmJobService _jobs;

    public JobHub(FilmJobService jobs) => _jobs = jobs;

    public Task<FilmStudio.Core.Models.JobSnapshot> GetSnapshot() =>
        Task.FromResult(_jobs.GetSnapshot());
}
