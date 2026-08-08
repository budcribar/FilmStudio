using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Engine;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PageToMovie.Tests.Api;

public class PasswordResetTests : IClassFixture<PageToMovieApiFactory>
{
    private readonly PageToMovieApiFactory _factory;

    public PasswordResetTests(PageToMovieApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Forgot_password_endpoint_accepts_request_and_generates_token()
    {
        using var client = _factory.CreateClient();
        var userDb = _factory.Services.GetRequiredService<UserDatabaseService>();

        var testUser = "resettest_" + Guid.NewGuid().ToString("N")[..8];
        var testEmail = testUser + "@example.com";

        await client.PostAsJsonAsync("/api/auth/signup", new { username = testUser, password = "OldPassword123!", email = testEmail });
        await userDb.ConfirmEmailAsync(testUser);

        var forgotResp = await client.PostAsJsonAsync("/api/auth/forgot-password", new { username = testUser });
        Assert.Equal(HttpStatusCode.OK, forgotResp.StatusCode);

        var json = await forgotResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Reset_password_with_token_updates_password_and_allows_login()
    {
        using var client = _factory.CreateClient();
        var userDb = _factory.Services.GetRequiredService<UserDatabaseService>();

        var testUser = "resetconfirm_" + Guid.NewGuid().ToString("N")[..8];
        var testEmail = testUser + "@example.com";
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";

        // Signup and confirm user
        await client.PostAsJsonAsync("/api/auth/signup", new { username = testUser, password = oldPassword, email = testEmail });
        await userDb.ConfirmEmailAsync(testUser);

        // Generate password reset token
        var resetToken = await userDb.CreateAuthTokenAsync(testUser, UserDatabaseService.AuthPurposePasswordReset, TimeSpan.FromHours(1));

        // Submit password reset with token
        var resetResp = await client.PostAsJsonAsync("/api/auth/reset-password", new { token = resetToken, newPassword });
        Assert.Equal(HttpStatusCode.OK, resetResp.StatusCode);

        var json = await resetResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("ok").GetBoolean());

        // Attempt login with old password -> should fail
        var loginOldResp = await client.PostAsJsonAsync("/api/auth/login", new { username = testUser, password = oldPassword });
        var oldJson = await loginOldResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(oldJson.GetProperty("ok").GetBoolean());

        // Attempt login with new password -> should succeed
        var loginNewResp = await client.PostAsJsonAsync("/api/auth/login", new { username = testUser, password = newPassword });
        Assert.Equal(HttpStatusCode.OK, loginNewResp.StatusCode);
        var newJson = await loginNewResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(newJson.GetProperty("ok").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(newJson.GetProperty("token").GetString()));
    }
}
