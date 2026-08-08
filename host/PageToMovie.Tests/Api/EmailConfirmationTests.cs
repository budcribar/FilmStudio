using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PageToMovie.Engine;
using PageToMovie.Engine.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PageToMovie.Tests.Api;

public class EmailConfirmationTests : IClassFixture<PageToMovieApiFactory>
{
    private readonly PageToMovieApiFactory _factory;

    public EmailConfirmationTests(PageToMovieApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Signup_creates_unconfirmed_user_and_requires_confirmation()
    {
        using var client = _factory.CreateClient();
        var userDb = _factory.Services.GetRequiredService<UserDatabaseService>();

        var testUser = "confirmtest_" + Guid.NewGuid().ToString("N")[..8];
        var testEmail = testUser + "@example.com";

        var signupBody = new { username = testUser, password = "Password123!", email = testEmail };
        var resp = await client.PostAsJsonAsync("/api/auth/signup", signupBody);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("ok").GetBoolean());
        Assert.True(json.GetProperty("requiresEmailConfirmation").GetBoolean());

        var dbUser = await userDb.GetUserByUsernameAsync(testUser);
        Assert.NotNull(dbUser);
        Assert.False(UserDatabaseService.IsEmailConfirmed(dbUser));
        Assert.Null(dbUser.EmailConfirmedAt);
    }

    [Fact]
    public async Task Confirm_email_token_activates_user_and_is_idempotent()
    {
        using var client = _factory.CreateClient();
        var userDb = _factory.Services.GetRequiredService<UserDatabaseService>();

        var testUser = "tokenconfirm_" + Guid.NewGuid().ToString("N")[..8];
        var testEmail = testUser + "@example.com";

        var signupBody = new { username = testUser, password = "Password123!", email = testEmail };
        await client.PostAsJsonAsync("/api/auth/signup", signupBody);

        var token = await userDb.CreateAuthTokenAsync(testUser, UserDatabaseService.AuthPurposeEmailConfirm, TimeSpan.FromDays(1));

        // 1. Initial confirmation call
        var confirmResp1 = await client.PostAsJsonAsync("/api/auth/confirm-email", new { token });
        Assert.Equal(HttpStatusCode.OK, confirmResp1.StatusCode);
        var json1 = await confirmResp1.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json1.GetProperty("ok").GetBoolean());

        var dbUser = await userDb.GetUserByUsernameAsync(testUser);
        Assert.NotNull(dbUser);
        Assert.True(UserDatabaseService.IsEmailConfirmed(dbUser));
        Assert.NotNull(dbUser.EmailConfirmedAt);

        // 2. Second confirmation call with same token (idempotency check)
        var confirmResp2 = await client.PostAsJsonAsync("/api/auth/confirm-email", new { token });
        Assert.Equal(HttpStatusCode.OK, confirmResp2.StatusCode);
        var json2 = await confirmResp2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json2.GetProperty("ok").GetBoolean());
        Assert.Contains("already confirmed", json2.GetProperty("message").GetString() ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_gated_by_email_confirmation_status()
    {
        using var client = _factory.CreateClient();
        var userDb = _factory.Services.GetRequiredService<UserDatabaseService>();

        var testUser = "logingate_" + Guid.NewGuid().ToString("N")[..8];
        var testEmail = testUser + "@example.com";
        var password = "Password123!";

        // Signup user
        await client.PostAsJsonAsync("/api/auth/signup", new { username = testUser, password, email = testEmail });

        // Attempt login before email confirmation -> should require confirmation
        var loginPreResp = await client.PostAsJsonAsync("/api/auth/login", new { username = testUser, password });
        var preJson = await loginPreResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(preJson.GetProperty("requiresEmailConfirmation").GetBoolean());

        // Manually confirm email
        await userDb.ConfirmEmailAsync(testUser);

        // Attempt login after email confirmation -> should succeed with JWT
        var loginPostResp = await client.PostAsJsonAsync("/api/auth/login", new { username = testUser, password });
        Assert.Equal(HttpStatusCode.OK, loginPostResp.StatusCode);
        var postJson = await loginPostResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(postJson.GetProperty("ok").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(postJson.GetProperty("token").GetString()));
    }
}
