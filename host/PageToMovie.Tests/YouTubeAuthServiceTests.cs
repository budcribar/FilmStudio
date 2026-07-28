using Google.Apis.Auth.OAuth2.Responses;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class YouTubeAuthServiceTests
{
    [Theory]
    [InlineData(" \"12345.apps.googleusercontent.com\" ", "12345.apps.googleusercontent.com")]
    [InlineData("'secret_12345'\r\n", "secret_12345")]
    [InlineData("  https://localhost/callback \t", "https://localhost/callback")]
    public void CleanCredentials_TrimsQuotesWhitespaceAndLinebreaks(string rawInput, string expectedClean)
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "ptm_yt_auth_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var opts = Options.Create(new PageToMovieOptions
            {
                WorkspaceRoot = root,
                YouTube = new YouTubeOptions
                {
                    ClientId = rawInput,
                    ClientSecret = rawInput,
                    RedirectUri = rawInput,
                }
            });
            var projects = new ProjectStore(opts);
            var auth = new YouTubeAuthService(projects, opts);

            // Act & Assert
            Assert.Equal(expectedClean, auth.CleanClientId);
            Assert.Equal(expectedClean, auth.CleanClientSecret);
            Assert.Equal(expectedClean, auth.CleanRedirectUri);
            Assert.True(auth.IsConfigured);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SqliteDataStore_PersistsTokensAcrossInstantiations()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "ptm_sqlite_datastore_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var dataDir = Path.Combine(root, "data");
            Directory.CreateDirectory(dataDir);
            var store1 = new SqliteDataStore(dataDir);

            var sampleToken = new TokenResponse
            {
                AccessToken = "access_xyz_123",
                RefreshToken = "refresh_abc_987",
                TokenType = "Bearer",
                ExpiresInSeconds = 3600
            };

            // Act: Store token in store instance 1
            await store1.StoreAsync<TokenResponse>("test_user", sampleToken);

            // Create a brand new store instance pointing to the same SQLite database
            var store2 = new SqliteDataStore(dataDir);
            var retrieved = await store2.GetAsync<TokenResponse>("test_user");

            // Assert: Token survives store instantiation & restart
            Assert.NotNull(retrieved);
            Assert.Equal("access_xyz_123", retrieved.AccessToken);
            Assert.Equal("refresh_abc_987", retrieved.RefreshToken);

            // Act 2: Clear token
            await store2.ClearAsync();
            var cleared = await store2.GetAsync<TokenResponse>("test_user");
            Assert.Null(cleared);
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { /* sqlite file connection pool lock on Windows */ }
        }
    }
}
