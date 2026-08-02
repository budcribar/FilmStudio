namespace PageToMovie.Engine.ModelExecution;

public enum ModelValidationSeverity
{
    Warning,
    Error,
}

public sealed record ModelValidationIssue(
    string Code,
    string Message,
    string? Path = null,
    ModelValidationSeverity Severity = ModelValidationSeverity.Error);

public enum ModelAttemptKind
{
    Primary,
    Correction,
}

public enum ModelResultSource
{
    PrimaryResponse,
    CorrectiveResponse,
    DeterministicFallback,
    Failed,
}

public sealed record ModelAttemptContext<TRaw>(
    ModelAttemptKind Kind,
    int SemanticAttempt,
    TRaw? PreviousResponse,
    IReadOnlyList<ModelValidationIssue> ValidationIssues);

public sealed record ModelResponse<TRaw>(TRaw Raw, string? Model = null);

public sealed record ModelParseResult<TResult>(
    TResult? Value,
    IReadOnlyList<ModelValidationIssue> Issues)
    where TResult : class
{
    public static ModelParseResult<TResult> Success(TResult value) =>
        new(value, Array.Empty<ModelValidationIssue>());

    public static ModelParseResult<TResult> Failure(params ModelValidationIssue[] issues) =>
        new(null, issues);
}

public interface IModelOperation<TInput, TRaw>
{
    string OperationName { get; }
    string PromptVersion => "unversioned";

    Task<ModelResponse<TRaw>> ExecuteAsync(
        TInput input,
        ModelAttemptContext<TRaw> context,
        CancellationToken ct);
}

public interface IModelResponseParser<in TRaw, TResult>
    where TResult : class
{
    ModelParseResult<TResult> Parse(TRaw response);
}

public interface IModelResultValidator<in TResult>
    where TResult : class
{
    IReadOnlyList<ModelValidationIssue> Validate(TResult result);
}

public interface IDeterministicFallback<in TInput, out TResult>
    where TResult : class
{
    TResult Create(TInput input, IReadOnlyList<ModelValidationIssue> unresolvedIssues);
}

public sealed record ModelOperationOptions
{
    public int TransportMaxAttempts { get; init; } = AiRetryPolicy.DefaultTransientMaxAttempts;
    public int CorrectiveMaxAttempts { get; init; } = 1;
    public int TransportBackoffMs { get; init; } = AiRetryPolicy.DefaultTransientBackoffMs;
    public IReadOnlyDictionary<string, string> BehaviorVersions { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record ModelOperationAttempt(
    ModelAttemptKind Kind,
    int SemanticAttempt,
    int TransportAttempts,
    string? Model,
    string? RawResponseHash,
    IReadOnlyList<ModelValidationIssue> ValidationIssues,
    string? Error);

public sealed record ValidatedModelResult<TResult>(
    TResult? Value,
    ModelResultSource Source,
    string OperationName,
    string? Model,
    IReadOnlyList<ModelOperationAttempt> Attempts,
    IReadOnlyList<ModelValidationIssue> ValidationIssues,
    string? Error)
    where TResult : class
{
    public bool Success => Value is not null && Source != ModelResultSource.Failed;
    public int ModelCalls => Attempts.Sum(attempt => attempt.TransportAttempts);
    public string? InputHash { get; init; }
    public string PromptVersion { get; init; } = "unversioned";
    public IReadOnlyDictionary<string, string> BehaviorVersions { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
