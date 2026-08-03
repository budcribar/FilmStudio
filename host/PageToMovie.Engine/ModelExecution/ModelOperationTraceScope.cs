using System.Collections.Concurrent;

namespace PageToMovie.Engine.ModelExecution;

public sealed record ModelOperationTrace(
    string OperationName,
    string PromptVersion,
    string? Model,
    ModelResultSource Source,
    int ModelCalls,
    int SemanticAttempts,
    string? InputHash,
    IReadOnlyDictionary<string, string> BehaviorVersions,
    int ValidationIssueCount);

/// <summary>
/// Collects validated-operation provenance across concurrent child tasks in one logical flow.
/// AsyncLocal carries the collector into classifier tasks while the concurrent bag makes writes safe.
/// </summary>
public sealed class ModelOperationTraceScope : IDisposable
{
    private sealed class Collector
    {
        public ConcurrentBag<ModelOperationTrace> Traces { get; } = [];
    }

    private static readonly AsyncLocal<Collector?> Current = new();
    private readonly Collector? _previous;
    private readonly Collector _collector;

    private ModelOperationTraceScope()
    {
        _previous = Current.Value;
        _collector = new Collector();
        Current.Value = _collector;
    }

    public static ModelOperationTraceScope Begin() => new();

    public IReadOnlyList<ModelOperationTrace> Snapshot() =>
        _collector.Traces
            .OrderBy(trace => trace.OperationName, StringComparer.Ordinal)
            .ThenBy(trace => trace.InputHash, StringComparer.Ordinal)
            .ToArray();

    internal static ValidatedModelResult<TResult> Record<TResult>(ValidatedModelResult<TResult> result)
        where TResult : class
    {
        Current.Value?.Traces.Add(new ModelOperationTrace(
            result.OperationName,
            result.PromptVersion,
            result.Model,
            result.Source,
            result.ModelCalls,
            result.Attempts.Count,
            result.InputHash,
            result.BehaviorVersions,
            result.ValidationIssues.Count));
        return result;
    }

    public void Dispose() => Current.Value = _previous;
}
