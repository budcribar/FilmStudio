using PageToMovie.Engine.ModelExecution;
using Xunit;

namespace PageToMovie.Tests;

public sealed class ValidatedCoverageOperationTests
{
    [Fact]
    public async Task Partial_primary_requests_only_missing_ids_on_correction_and_merges_results()
    {
        var requestedPerAttempt = new List<IReadOnlyList<string>>();
        var responses = new Queue<string>(["a=first", "b=second"]);

        var (lifecycle, compatibility) = await ValidatedCoverageOperation.ExecuteAsync(
            "test_coverage",
            "1",
            ["a", "b"],
            (context, missing) =>
            {
                requestedPerAttempt.Add(missing.ToArray());
                return Task.FromResult(new ModelResponse<string>(responses.Dequeue(), "catalog-model"));
            },
            raw => raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Split('=', 2))
                .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase),
            correctiveMaxAttempts: 1,
            transportMaxAttempts: 1,
            transportBackoffMs: 0);

        Assert.Equal(ModelResultSource.CorrectiveResponse, lifecycle.Source);
        Assert.Equal(["a", "b"], requestedPerAttempt[0]);
        Assert.Equal(["b"], requestedPerAttempt[1]);
        Assert.True(compatibility.FullyCovered);
        Assert.Equal("first", compatibility.Result!["a"]);
        Assert.Equal("second", compatibility.Result!["b"]);
        Assert.Equal(2, lifecycle.Attempts.Count);
    }

    [Fact]
    public async Task Unresolved_coverage_returns_partial_result_with_fallback_provenance()
    {
        var (lifecycle, compatibility) = await ValidatedCoverageOperation.ExecuteAsync(
            "test_coverage",
            "1",
            ["a", "b"],
            (_, _) => Task.FromResult(new ModelResponse<string>("a=first", "catalog-model")),
            raw => new Dictionary<string, string> { ["a"] = "first" },
            correctiveMaxAttempts: 1,
            transportMaxAttempts: 1,
            transportBackoffMs: 0);

        Assert.Equal(ModelResultSource.DeterministicFallback, lifecycle.Source);
        Assert.False(compatibility.FullyCovered);
        Assert.Equal(["b"], compatibility.Missing);
        Assert.Equal("first", compatibility.Result!["a"]);
    }
}
