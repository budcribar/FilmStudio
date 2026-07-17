using FilmStudio.Core.Options;
using FilmStudio.Engine;
using Microsoft.Extensions.Options;
using Xunit;

namespace FilmStudio.Tests;

public class WorkerPoolTests
{
    [Fact]
    public async Task ApiWorkerPool_respects_global_cap()
    {
        var opts = Options.Create(new FilmStudioOptions
        {
            Capacity = new CapacityOptions
            {
                MaxVideoInFlight = 2,
                MaxVideoInFlightPerUser = 2,
            },
        });
        var pool = new ApiWorkerPool(opts);
        var running = 0;
        var peak = 0;
        var gate = new object();

        async Task Work()
        {
            lock (gate)
            {
                running++;
                if (running > peak) peak = running;
            }
            await Task.Delay(80);
            lock (gate) running--;
        }

        var tasks = Enumerable.Range(0, 6)
            .Select(i => pool.RunAsync($"u{i % 3}", _ => Work(), CancellationToken.None))
            .ToArray();
        await Task.WhenAll(tasks);
        Assert.True(peak <= 2, $"peak concurrency {peak} exceeded cap 2");
    }

    [Fact]
    public async Task ApiWorkerPool_per_user_cap()
    {
        var opts = Options.Create(new FilmStudioOptions
        {
            Capacity = new CapacityOptions
            {
                MaxVideoInFlight = 8,
                MaxVideoInFlightPerUser = 1,
            },
        });
        var pool = new ApiWorkerPool(opts);
        var userRunning = 0;
        var peak = 0;
        var gate = new object();

        async Task Work()
        {
            lock (gate)
            {
                userRunning++;
                if (userRunning > peak) peak = userRunning;
            }
            await Task.Delay(60);
            lock (gate) userRunning--;
        }

        var tasks = Enumerable.Range(0, 4)
            .Select(_ => pool.RunAsync("same-user", _ => Work(), CancellationToken.None))
            .ToArray();
        await Task.WhenAll(tasks);
        Assert.True(peak <= 1, $"per-user peak {peak} exceeded 1");
    }
}
