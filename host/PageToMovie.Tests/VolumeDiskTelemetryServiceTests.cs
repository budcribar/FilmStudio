using System;
using System.IO;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class VolumeDiskTelemetryServiceTests : IDisposable
{
    private readonly string _tempWorkspace;

    public VolumeDiskTelemetryServiceTests()
    {
        _tempWorkspace = Path.Combine(Path.GetTempPath(), "ptm-disk-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempWorkspace);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempWorkspace)) Directory.Delete(_tempWorkspace, recursive: true); }
        catch { /* ignore */ }
    }

    [Fact]
    public void GetDiskStatus_returns_valid_capacity_and_free_space()
    {
        var service = new VolumeDiskTelemetryService(Options.Create(new PageToMovieOptions { WorkspaceRoot = _tempWorkspace }));
        var status = service.GetDiskStatus();

        Assert.True(status.IsAvailable);
        Assert.NotNull(status.VolumePath);
        Assert.True(status.TotalBytes > 0);
        Assert.True(status.FreeBytes >= 0);
        Assert.True(status.UsedBytes >= 0);
        Assert.True(status.UsedPercent >= 0 && status.UsedPercent <= 100);
        Assert.NotEqual("—", status.FormattedTotal);
    }

    [Fact]
    public void RecordDailySnapshotIfNeeded_records_and_retrieves_history()
    {
        var service = new VolumeDiskTelemetryService(Options.Create(new PageToMovieOptions { WorkspaceRoot = _tempWorkspace }));
        service.RecordDailySnapshotIfNeeded();

        var history = service.GetDiskHistory(30);
        Assert.NotEmpty(history);

        var snap = history[0];
        Assert.Equal(DateTime.UtcNow.ToString("yyyy-MM-dd"), snap.SnapshotDate);
        Assert.True(snap.TotalBytes > 0);
        Assert.NotEqual("—", snap.FormattedUsed);
    }
}
