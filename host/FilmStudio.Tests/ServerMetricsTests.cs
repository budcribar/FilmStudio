using FilmStudio.Core.Models;
using FilmStudio.Engine;
using FilmStudio.Engine.Abstractions;
using Xunit;

namespace FilmStudio.Tests;

public class ServerMetricsTests
{
    [Fact]
    public void Timings_p50_p95_from_samples()
    {
        var metrics = new ServerMetricsService();
        var jobs = new JobStore();
        var locks = new InMemoryLockService();
        var queued = DateTimeOffset.UtcNow.AddMinutes(-10);

        for (var i = 0; i < 10; i++)
        {
            var started = queued.AddSeconds(i);
            // Finish with increasing run times via NoteJobFinished (uses UtcNow for finish)
            metrics.NoteJobFinished("scene", "u1", success: i != 9, queued, started);
        }

        var snap = metrics.GetSnapshot(
            jobs,
            locks,
            new CapacityOptionsSnapshot { MaxVideoInFlight = 4 },
            new ProcessMetricsSnapshot { Environment = "Test" });

        Assert.True(snap.TimingsByKind.ContainsKey("scene"));
        var t = snap.TimingsByKind["scene"];
        Assert.Equal(10, t.CompletedInWindow);
        Assert.Equal(1, t.FailuresInWindow);
        Assert.True(t.TotalP50Ms >= 0);
        Assert.True(t.TotalP95Ms >= t.TotalP50Ms);
    }

    [Fact]
    public void Capacity_and_lock_counters()
    {
        var metrics = new ServerMetricsService();
        metrics.NoteCapacityReject();
        metrics.NoteCapacityReject();
        metrics.NoteLockConflict();
        metrics.NoteApiSlotAcquired("u1");
        metrics.NoteApiSlotAcquired("u2");

        var snap = metrics.GetSnapshot(
            new JobStore(),
            new InMemoryLockService(),
            new CapacityOptionsSnapshot(),
            new ProcessMetricsSnapshot());

        Assert.Equal(2, snap.CapacityRejects);
        Assert.Equal(1, snap.LockConflicts);
        Assert.Equal(2, snap.ApiInFlight);
    }

    [Fact]
    public void Snapshot_includes_running_jobs_and_locks()
    {
        var metrics = new ServerMetricsService();
        var jobs = new JobStore();
        jobs.Create(new JobRecord
        {
            Status = "running",
            Kind = "scene",
            UserId = "u1",
            ProjectId = "Buster",
            Scene = 2,
        });
        var locks = new InMemoryLockService();
        locks.TryAcquire(LockKeys.Scene("Buster", 2), "u1", TimeSpan.FromMinutes(5), "gen");

        var snap = metrics.GetSnapshot(
            jobs,
            locks,
            new CapacityOptionsSnapshot { MaxVideoInFlight = 4 },
            new ProcessMetricsSnapshot());

        Assert.Single(snap.Jobs);
        Assert.Single(snap.Locks);
        Assert.Equal("u1", snap.QueueByUser.First().UserId);
    }
}
