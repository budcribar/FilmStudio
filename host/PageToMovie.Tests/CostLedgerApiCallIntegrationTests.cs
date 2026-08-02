using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// Covers the gap where chat/vision/classifier spend was tracked in <c>user_api_calls</c> (SQLite)
/// but never rolled into a project's <c>cost_ledger</c> — so the Cost page's "Spent" total always
/// read $0 for screenplay/review even though real dollars were spent. <see cref="ProjectTelemetryService.LogApiCallAsync"/>
/// now appends a ledger event for non-video/image kinds via <see cref="CostReportService.RecordApiCallSpendAsync"/>,
/// while leaving video/image alone since <c>FilmJobService</c>/<c>CharacterDesignService</c> already
/// log those via <see cref="CostReportService.RecordVideoGenerationAsync"/>/<see cref="CostReportService.RecordImageGenerationAsync"/>
/// — double-logging those would double-count spend.
/// </summary>
public sealed class CostLedgerApiCallIntegrationTests : IDisposable
{
    private readonly string _root;

    public CostLedgerApiCallIntegrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ptm-costledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "projects", "Demo"));
        File.WriteAllText(Path.Combine(_root, "projects", "Demo", "project.json"), """{"id":"Demo"}""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { /* best effort */ }
    }

    private (ProjectStore store, CostReportService costs, ProjectTelemetryService telemetry) BuildServices()
    {
        var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = _root });
        var store = new ProjectStore(opts);
        var costs = new CostReportService(store);
        var telemetry = new ProjectTelemetryService(
            store, NullLogger<ProjectTelemetryService>.Instance, userDb: null, costs: costs);
        return (store, costs, telemetry);
    }

    [Fact]
    public async Task LogApiCallAsync_ChatKind_AppendsCostLedgerEvent()
    {
        var (_, costs, telemetry) = BuildServices();

        await telemetry.LogApiCallAsync(new ApiCallTelemetry
        {
            ProjectId = "Demo",
            Kind = "chat",
            Mode = "book_to_fountain",
            Model = "grok-4.5",
            Provider = "grok",
            InputTokens = 10_000,
            OutputTokens = 5_000,
            Ok = true,
        });

        var ledger = await costs.GetCostLedgerAsync("Demo");
        Assert.Single(ledger);
        Assert.Equal("chat", ledger[0].Kind);
        Assert.True(ledger[0].Usd > 0, "chat call with real token counts should price to a non-zero estimate");
    }

    [Fact]
    public async Task LogApiCallAsync_VideoKind_DoesNotAppendCostLedgerEvent()
    {
        // Video spend is recorded by CostReportService.RecordVideoGenerationAsync (called from
        // FilmJobService) with a richer event (resolution, ref-image flag, etc.) — LogApiCallAsync
        // must not also append here, or the ledger double-counts the same generation.
        var (_, costs, telemetry) = BuildServices();

        await telemetry.LogApiCallAsync(new ApiCallTelemetry
        {
            ProjectId = "Demo",
            Kind = "video",
            Model = "grok-imagine-video",
            Provider = "grok",
            Resolution = "480p",
            DurationSec = 6,
            Ok = true,
        });

        var ledger = await costs.GetCostLedgerAsync("Demo");
        Assert.Empty(ledger);
    }

    [Fact]
    public async Task LogApiCallAsync_ImageKind_DoesNotAppendCostLedgerEvent()
    {
        var (_, costs, telemetry) = BuildServices();

        await telemetry.LogApiCallAsync(new ApiCallTelemetry
        {
            ProjectId = "Demo",
            Kind = "image",
            Model = "grok-imagine-image-quality",
            Provider = "grok",
            ImageCount = 1,
            Ok = true,
        });

        var ledger = await costs.GetCostLedgerAsync("Demo");
        Assert.Empty(ledger);
    }

    [Fact]
    public async Task GetApiCostByProviderAsync_GroupsSpendByProviderThenCategory()
    {
        var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = _root });
        var db = new UserDatabaseService(opts, null, NullLogger<UserDatabaseService>.Instance);

        await db.InsertUserApiCallAsync(new ApiCallTelemetry
        {
            UserId = "user_1", ProjectId = "Demo", Kind = "chat", Category = CostCategories.Screenplay,
            Provider = "grok", Model = "grok-4.5", EstimatedUsd = 0.10, Ok = true,
        });
        await db.InsertUserApiCallAsync(new ApiCallTelemetry
        {
            UserId = "user_1", ProjectId = "Demo", Kind = "chat", Category = CostCategories.Screenplay,
            Provider = "grok", Model = "grok-4.5", EstimatedUsd = 0.20, Ok = true,
        });
        await db.InsertUserApiCallAsync(new ApiCallTelemetry
        {
            UserId = "user_1", ProjectId = "Demo", Kind = "vision", Category = CostCategories.Review,
            Provider = "gemini", Model = "gemini-2.5-flash", EstimatedUsd = 0.05, Ok = true,
        });

        var stats = await db.GetApiCostByProviderAsync(userId: "user_1", projectId: "Demo");

        Assert.Equal(3, stats.TotalCalls);
        Assert.Equal(0.35, stats.TotalUsd, 4);
        Assert.True(stats.ByProvider.ContainsKey("grok"));
        Assert.True(stats.ByProvider.ContainsKey("gemini"));
        Assert.Equal(2, stats.ByProvider["grok"].Count);
        Assert.Equal(0.30, stats.ByProvider["grok"].TotalUsd, 4);
        Assert.Equal(1, stats.ByProvider["gemini"].Count);
        Assert.Equal(0.05, stats.ByProvider["gemini"].TotalUsd, 4);
        Assert.True(stats.ByProvider["grok"].ByCategory.ContainsKey(CostCategories.Screenplay));
        Assert.Equal(0.30, stats.ByProvider["grok"].ByCategory[CostCategories.Screenplay].TotalUsd, 4);
    }
}
