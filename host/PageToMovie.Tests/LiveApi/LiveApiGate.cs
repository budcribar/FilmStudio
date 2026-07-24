namespace PageToMovie.Tests.LiveApi;

/// <summary>
/// Live (paid) provider API tests are off unless explicitly enabled.
/// Default <c>dotnet test</c> excludes Category=LiveApi (see csproj VSTestTestCaseFilter).
/// </summary>
public static class LiveApiGate
{
    /// <summary>Set to <c>1</c> or <c>true</c> to allow LiveApi tests to run.</summary>
    public const string EnableEnvVar = "PAGETOMOVIE_LIVE_API_TESTS";

    /// <summary>xAI / Grok key (same as production).</summary>
    public const string XaiKeyEnvVar = "XAI_API_KEY";

    public const string Category = "LiveApi";

    public static bool IsEnabled =>
        IsTruthy(Environment.GetEnvironmentVariable(EnableEnvVar)) &&
        !string.IsNullOrWhiteSpace(ResolveXaiApiKey());

    public static string? ResolveXaiApiKey()
    {
        var key = Environment.GetEnvironmentVariable(XaiKeyEnvVar)
                  ?? Environment.GetEnvironmentVariable("PageToMovie_XAI_API_KEY")
                  ?? Environment.GetEnvironmentVariable("GROK_API_KEY");
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    public static string SkipReason =>
        $"Live API tests are disabled. To run: set {EnableEnvVar}=1 and {XaiKeyEnvVar}=… " +
        $"then: dotnet test --filter Category={Category}";

    private static bool IsTruthy(string? v) =>
        string.Equals(v, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(v, "yes", StringComparison.OrdinalIgnoreCase);
}
