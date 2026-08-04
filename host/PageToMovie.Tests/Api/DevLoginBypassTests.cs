using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PageToMovie.Tests.Api;

/// <summary>
/// Verifies the fakes-mode login bypass: when the server runs on fakes (the test factory sets
/// UseFakes=true), POST /api/auth/dev-login issues a deterministic dev-user session. The endpoint
/// is hard-gated on UseFakes so it can never mint a session in a real deployment.
/// </summary>
public class DevLoginBypassTests : IClassFixture<PageToMovieApiFactory>
{
    private readonly PageToMovieApiFactory _factory;

    public DevLoginBypassTests(PageToMovieApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Dev_login_issues_deterministic_dev_user_in_fakes_mode()
    {
        using var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/auth/dev-login", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("ok").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("token").GetString()));
        Assert.Equal("budcribar@gmail.com", json.GetProperty("userId").GetString());
        // Dev user is granted admin so the whole studio is browsable end-to-end.
        var roles = json.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        Assert.Contains("admin", roles);
    }
}
