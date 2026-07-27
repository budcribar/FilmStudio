using System;

namespace PageToMovie.Core.Models;

public sealed class VolumeDiskStatusDto
{
    public string? VolumePath { get; set; }
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsedPercent { get; set; }
    public string FormattedTotal { get; set; } = "—";
    public string FormattedFree { get; set; } = "—";
    public string FormattedUsed { get; set; } = "—";
    public bool IsAvailable { get; set; }
    public string? Error { get; set; }
}

public sealed class VolumeDiskSnapshotDto
{
    public string SnapshotDate { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
    public string VolumePath { get; set; } = "";
    public long TotalBytes { get; set; }
    public long FreeBytes { get; set; }
    public long UsedBytes { get; set; }
    public double UsedPercent { get; set; }
    public string FormattedUsed { get; set; } = "—";
    public string FormattedFree { get; set; } = "—";
    public string FormattedTotal { get; set; } = "—";
    public long? DailyChangeBytes { get; set; }
    public string? FormattedDailyChange { get; set; }
}
