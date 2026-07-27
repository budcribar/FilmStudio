using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace PageToMovie.Tests.Api;

/// <summary>End-to-end: create a project, invite by email, accept as a different user, verify the fork.</summary>
public class InviteToForkApiTests : IClassFixture<PageToMovieApiFactory>
{
    private readonly PageToMovieApiFactory _factory;

    public InviteToForkApiTests(PageToMovieApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<string> CreateProjectIdAsync(HttpClient client, string name, string title)
    {
        var create = await client.PostAsJsonAsync("/api/projects", new { name, title });
        Assert.True(create.IsSuccessStatusCode, await create.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("active").GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));
        return id!;
    }

    [Fact]
    public async Task Invite_then_accept_forks_the_project_under_the_new_user()
    {
        var owner = _factory.CreateUserClient("owner-user");
        var slug = "InviteSmoke_" + Guid.NewGuid().ToString("N")[..8];
        var projectId = await CreateProjectIdAsync(owner, slug, "Invite Smoke");
        Assert.Contains("owner-user", projectId, StringComparison.OrdinalIgnoreCase);

        var inviteResp = await owner.PostAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/invites",
            new { ProjectId = projectId, TargetEmail = "collaborator@example.com" });
        Assert.Equal(HttpStatusCode.OK, inviteResp.StatusCode);

        using var inviteDoc = JsonDocument.Parse(await inviteResp.Content.ReadAsStringAsync());
        var inviteUrl = inviteDoc.RootElement.GetProperty("inviteUrl").GetString();
        Assert.False(string.IsNullOrWhiteSpace(inviteUrl));

        var token = QueryHelpers.ParseQuery(new Uri(inviteUrl!).Query)["token"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var collaborator = _factory.CreateUserClient("collaborator-user");
        var acceptResp = await collaborator.PostAsJsonAsync("/api/invites/accept", new { Token = token });
        Assert.Equal(HttpStatusCode.OK, acceptResp.StatusCode);

        using var acceptDoc = JsonDocument.Parse(await acceptResp.Content.ReadAsStringAsync());
        Assert.True(acceptDoc.RootElement.GetProperty("ok").GetBoolean());
        var forkedId = acceptDoc.RootElement.GetProperty("projectId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(forkedId));
        Assert.NotEqual(projectId, forkedId);
        Assert.StartsWith("collaborator-user/", forkedId, StringComparison.OrdinalIgnoreCase);

        // Same token cannot be used twice.
        var secondAccept = await collaborator.PostAsJsonAsync("/api/invites/accept", new { Token = token });
        Assert.Equal(HttpStatusCode.BadRequest, secondAccept.StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_or_admin_can_send_an_invite()
    {
        var owner = _factory.CreateUserClient("owner-user-2");
        var slug = "InviteAuthSmoke_" + Guid.NewGuid().ToString("N")[..8];
        var projectId = await CreateProjectIdAsync(owner, slug, "Invite Auth Smoke");

        var stranger = _factory.CreateUserClient("stranger-user");
        var inviteResp = await stranger.PostAsJsonAsync(
            $"/api/projects/{Uri.EscapeDataString(projectId)}/invites",
            new { ProjectId = projectId, TargetEmail = "someone@example.com" });

        Assert.Equal(HttpStatusCode.Forbidden, inviteResp.StatusCode);
    }
}
