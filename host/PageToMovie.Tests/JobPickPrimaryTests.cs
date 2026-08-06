using System;
using PageToMovie.Core.Models;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// PickPrimary chooses the one job the UI shows. A batch that fails at a gate finishes instantly,
/// leaving only errored jobs — PickPrimary must still surface one so the Scenes page can render the
/// failure. If it returned null when nothing is running, the error is swallowed and the operator
/// sees nothing (the "no feedback on failed generate" bug).
/// </summary>
public class JobPickPrimaryTests
{
    private static JobSnapshot Job(string status, DateTimeOffset at) => new()
    {
        JobId = Guid.NewGuid().ToString("N")[..8],
        Status = status,
        Kind = "batch",
        QueuedAt = at,
        StartedAt = at,
        FinishedAt = at,
    };

    [Fact]
    public void Surfaces_newest_terminal_error_when_nothing_running()
    {
        var older = Job("error", DateTimeOffset.UtcNow.AddMinutes(-5));
        var newer = Job("error", DateTimeOffset.UtcNow);

        var primary = JobListHelpers.PickPrimary(new[] { older, newer });

        Assert.NotNull(primary);
        Assert.Equal("error", primary!.Status);
        Assert.Equal(newer.JobId, primary.JobId);
    }

    [Fact]
    public void Prefers_running_over_finished()
    {
        var errored = Job("error", DateTimeOffset.UtcNow);
        var running = Job("running", DateTimeOffset.UtcNow.AddMinutes(-1));

        var primary = JobListHelpers.PickPrimary(new[] { errored, running });

        Assert.Equal("running", primary!.Status);
    }

    [Fact]
    public void Empty_list_returns_null()
    {
        Assert.Null(JobListHelpers.PickPrimary(Array.Empty<JobSnapshot>()));
    }
}
