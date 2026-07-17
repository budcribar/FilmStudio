using System.Text.Json;
using System.Text.Json.Serialization;
using FilmStudio.LoadSim;

// Top-level statements

var opts = SimOptions.Parse(args);

Console.WriteLine($"FilmStudio.LoadSim → {opts.BaseUrl}");
Console.WriteLine($"  users={opts.Users} duration={opts.DurationSec}s scenario={opts.Scenario} project={opts.ProjectId}");

// Default project is checked-in projects/LoadSimBuster (never touch real Buster).
if (!opts.AllowRealProject &&
    (string.Equals(opts.ProjectId, "Buster", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(opts.ProjectId, "NickAndMe", StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine(
        $"Setup: refusing real project '{opts.ProjectId}'. " +
        $"Use '{ProjectSandbox.DefaultSandboxId}' (default) or pass --allowRealProject.");
    return 2;
}

// Optional maintenance: recopy sandbox from Buster (normally skip — LoadSimBuster is in git)
if (opts.PrepareSandbox)
{
    try
    {
        var workspace = ProjectSandbox.FindWorkspaceRoot(opts.WorkspaceRoot);
        if (workspace is null)
        {
            Console.Error.WriteLine(
                "Setup: could not find workspace root (folder with projects/). Pass --workspace PATH.");
            return 2;
        }

        opts.WorkspaceRoot = workspace;
        Console.WriteLine($"  workspace={workspace}");
        ProjectSandbox.Ensure(
            workspace,
            opts.SourceProjectId,
            opts.ProjectId,
            refresh: opts.RefreshSandbox);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Setup: sandbox prepare failed: {ex.Message}");
        return 2;
    }
}
else
{
    Console.WriteLine($"  project={opts.ProjectId} (checked-in sandbox; no recopy)");
}

using var http = new HttpClient
{
    BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/"),
    Timeout = TimeSpan.FromMinutes(2),
};

// Setup checks
try
{
    using var health = await http.GetAsync("health");
    if (!health.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Setup: /health returned {(int)health.StatusCode}");
        return 2;
    }

    await using var stream = await health.Content.ReadAsStreamAsync();
    using var doc = await JsonDocument.ParseAsync(stream);
    var useFakes = doc.RootElement.TryGetProperty("useFakes", out var uf) && uf.GetBoolean();
    if (opts.RequireFakes && !useFakes && !opts.IKnowWhatImDoing &&
        opts.Scenario is not ("browse" or "play"))
    {
        var genWeight = opts.Scenario == "mixed" ? opts.GenWeight : opts.Scenario == "gen" ? 1.0 : 0;
        if (genWeight > 0)
        {
            Console.Error.WriteLine(
                "Setup: API UseFakes=false but scenario includes gen. " +
                "Set FILMSTUDIO_USE_FAKES=true or pass --i-know-what-im-doing / --scenario browse.");
            return 2;
        }
    }

    Console.WriteLine($"  health ok · useFakes={useFakes}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Setup: cannot reach API: {ex.Message}");
    return 2;
}

if (opts.WarmupSec > 0)
{
    Console.WriteLine($"  warmup {opts.WarmupSec}s…");
    await Task.Delay(TimeSpan.FromSeconds(opts.WarmupSec));
}

var metrics = new MetricsCollector();
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(opts.DurationSec));
var started = DateTimeOffset.UtcNow;

var tasks = Enumerable.Range(0, opts.Users)
    .Select(i =>
    {
        // Each VU gets its own HttpClient handler-friendly instance sharing base address
        var client = new HttpClient
        {
            BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromMinutes(2),
        };
        var vu = new VirtualUser(i, opts, metrics, client);
        return Task.Run(async () =>
        {
            try { await vu.RunAsync(cts.Token); }
            finally { client.Dispose(); }
        }, CancellationToken.None);
    })
    .ToArray();

Console.WriteLine("  running…");
try
{
    await Task.WhenAll(tasks);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Run error: {ex.Message}");
}

var elapsed = DateTimeOffset.UtcNow - started;
var results = metrics.Build(opts, elapsed);
var passed = GateEvaluator.Evaluate(results, opts);

var jsonOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};
var json = JsonSerializer.Serialize(results, jsonOpts);
await File.WriteAllTextAsync(opts.OutPath, json);

Console.WriteLine();
Console.WriteLine("=== LoadSim summary ===");
Console.WriteLine($"  elapsed={results.ElapsedSec:0.0}s actions={results.Http.Total}");
Console.WriteLine($"  errorRate={results.Http.ErrorRate:P2} (excl. 409={results.Http.Intentional409})");
Console.WriteLine($"  latency p50={results.Http.P50Ms}ms p95={results.Http.P95Ms}ms browseP95={results.Http.BrowseP95Ms}ms");
Console.WriteLine($"  jobs submitted={results.Jobs.Submitted} rejected={results.Jobs.Rejected} 5xx={results.Jobs.Server5xx}");
Console.WriteLine($"  health ok={results.Health.Ok} fail={results.Health.Fail}");
Console.WriteLine($"  peakApiInFlight={results.Server.PeakApiInFlight} cap={results.Server.ConfiguredMaxVideoInFlight}");
Console.WriteLine();
Console.WriteLine("Gates:");
foreach (var g in results.Gates)
    Console.WriteLine($"  {(g.Pass ? "PASS" : "FAIL")} {g.Name}: {g.Detail}");
Console.WriteLine();
Console.WriteLine($"Results → {Path.GetFullPath(opts.OutPath)}");
Console.WriteLine(passed ? "RESULT: PASS" : "RESULT: FAIL");

return passed ? 0 : 1;
