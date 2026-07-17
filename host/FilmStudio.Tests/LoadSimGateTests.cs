using FilmStudio.LoadSim;
using Xunit;

namespace FilmStudio.Tests;

/// <summary>E2-style unit tests for LoadSim gate evaluation (no HTTP).</summary>
public class LoadSimGateTests
{
    [Fact]
    public void Gates_pass_on_clean_results()
    {
        var opts = new SimOptions { MaxErrorRate = 0.01, MaxBrowseP95Ms = 500 };
        var r = new LoadSimResults
        {
            Http = new HttpStats
            {
                Total = 100,
                Errors = 0,
                ErrorRate = 0,
                BrowseP95Ms = 40,
            },
            Health = new HealthStats { Ok = 50, Fail = 0 },
            Jobs = new JobStats { Server5xx = 0 },
            Server = new ServerStats { ConfiguredMaxVideoInFlight = 8, PeakApiInFlight = 3 },
        };
        Assert.True(GateEvaluator.Evaluate(r, opts));
        Assert.True(r.Passed);
        Assert.All(r.Gates, g => Assert.True(g.Pass));
    }

    [Fact]
    public void Gates_fail_on_high_error_rate()
    {
        var opts = new SimOptions { MaxErrorRate = 0.01, MaxBrowseP95Ms = 500 };
        var r = new LoadSimResults
        {
            Http = new HttpStats { Total = 100, Errors = 10, ErrorRate = 0.10, BrowseP95Ms = 20 },
            Health = new HealthStats { Ok = 10, Fail = 0 },
            Jobs = new JobStats(),
        };
        Assert.False(GateEvaluator.Evaluate(r, opts));
        Assert.Contains(r.Gates, g => g.Name == "http_error_rate" && !g.Pass);
    }

    [Fact]
    public void Gates_fail_on_browse_p95()
    {
        var opts = new SimOptions { MaxErrorRate = 0.05, MaxBrowseP95Ms = 100 };
        var r = new LoadSimResults
        {
            Http = new HttpStats { Total = 50, Errors = 0, ErrorRate = 0, BrowseP95Ms = 800 },
            Health = new HealthStats { Ok = 5, Fail = 0 },
            Jobs = new JobStats(),
        };
        Assert.False(GateEvaluator.Evaluate(r, opts));
        Assert.Contains(r.Gates, g => g.Name == "browse_p95" && !g.Pass);
    }

    [Fact]
    public void MetricsCollector_records_and_builds()
    {
        var m = new MetricsCollector();
        for (var i = 0; i < 20; i++)
            m.Record("browse", 200, 10 + i);
        m.Record("gen", 409, 5, intentionalConflict: true);
        m.Record("gen", 202, 100);
        m.NoteServerCapacity(12, 4);

        var r = m.Build(new SimOptions { Users = 5, DurationSec = 30 }, TimeSpan.FromSeconds(30));
        Assert.True(r.Http.Total >= 22);
        Assert.Equal(1, r.Http.Intentional409);
        Assert.Equal(1, r.Jobs.Submitted);
        Assert.Equal(1, r.Jobs.Rejected);
        Assert.Equal(12, r.Server.ConfiguredMaxVideoInFlight);
        Assert.Equal(4, r.Server.PeakApiInFlight);
        Assert.True(r.Http.BrowseP95Ms >= r.Http.BrowseP50Ms);
    }
}
