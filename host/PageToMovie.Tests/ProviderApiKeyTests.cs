using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// Capability availability is per-user (BYOK) but must also honor a shared server env key as a
/// fallback when the user has no per-request key of their own — that's what makes a capability
/// "configured" for a user relying on the deployment's key. These cover that fallback + precedence
/// at the shared resolver the provider clients use.
/// </summary>
[Collection("env-serial")]
public sealed class ProviderApiKeyTests
{
    [Fact]
    public void ResolveFal_FallsBackToServerEnvKey_WhenNoRequestScopeOverride()
    {
        var prev = Environment.GetEnvironmentVariable("FAL_API_KEY");
        var prevAlias = Environment.GetEnvironmentVariable("FAL_KEY");
        try
        {
            Environment.SetEnvironmentVariable("FAL_API_KEY", "  fal-server-env-key  ");
            Environment.SetEnvironmentVariable("FAL_KEY", null);
            // No ApiKeyScope override set → the server env key is the effective key (trimmed).
            Assert.Equal("fal-server-env-key", ProviderApiKey.ResolveFal());
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAL_API_KEY", prev);
            Environment.SetEnvironmentVariable("FAL_KEY", prevAlias);
        }
    }

    [Fact]
    public void ResolveFal_UsesAliasEnvVar_WhenCanonicalUnset()
    {
        var prev = Environment.GetEnvironmentVariable("FAL_API_KEY");
        var prevAlias = Environment.GetEnvironmentVariable("FAL_KEY");
        try
        {
            Environment.SetEnvironmentVariable("FAL_API_KEY", null);
            Environment.SetEnvironmentVariable("FAL_KEY", "fal-alias-key");
            Assert.Equal("fal-alias-key", ProviderApiKey.ResolveFal());
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAL_API_KEY", prev);
            Environment.SetEnvironmentVariable("FAL_KEY", prevAlias);
        }
    }

    [Fact]
    public void ResolveFal_Null_WhenNoScopeAndNoEnv()
    {
        var prev = Environment.GetEnvironmentVariable("FAL_API_KEY");
        var prevAlias = Environment.GetEnvironmentVariable("FAL_KEY");
        try
        {
            Environment.SetEnvironmentVariable("FAL_API_KEY", null);
            Environment.SetEnvironmentVariable("FAL_KEY", null);
            Assert.Null(ProviderApiKey.ResolveFal());
        }
        finally
        {
            Environment.SetEnvironmentVariable("FAL_API_KEY", prev);
            Environment.SetEnvironmentVariable("FAL_KEY", prevAlias);
        }
    }
}

/// <summary>Serializes env-var-mutating tests so they don't race with each other.</summary>
[CollectionDefinition("env-serial", DisableParallelization = true)]
public sealed class EnvSerialCollection { }
