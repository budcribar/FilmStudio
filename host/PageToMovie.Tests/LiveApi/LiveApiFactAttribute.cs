using Xunit;

namespace PageToMovie.Tests.LiveApi;

/// <summary>
/// Fact that skips unless <see cref="LiveApiGate"/> is enabled.
/// Tag the test class with <c>[Trait("Category", LiveApiGate.Category)]</c> so default
/// <c>dotnet test</c> (VSTestTestCaseFilter Category!=LiveApi) excludes it.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LiveApiFactAttribute : FactAttribute
{
    public LiveApiFactAttribute()
    {
        if (!LiveApiGate.IsEnabled)
            Skip = LiveApiGate.SkipReason;
    }
}

/// <summary>Theory counterpart of <see cref="LiveApiFactAttribute"/>.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LiveApiTheoryAttribute : TheoryAttribute
{
    public LiveApiTheoryAttribute()
    {
        if (!LiveApiGate.IsEnabled)
            Skip = LiveApiGate.SkipReason;
    }
}
