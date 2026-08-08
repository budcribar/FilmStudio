using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Engine;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PageToMovie.Tests.Api;

public class PerUserActiveProjectTests : IClassFixture<PageToMovieApiFactory>
{
    private readonly PageToMovieApiFactory _factory;

    public PerUserActiveProjectTests(PageToMovieApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Active_project_is_isolated_per_user()
    {
        var userDb = _factory.Services.GetRequiredService<UserDatabaseService>();
        var user1 = "user1_" + Guid.NewGuid().ToString("N")[..6];
        var user2 = "user2_" + Guid.NewGuid().ToString("N")[..6];

        await userDb.InsertUserAsync(new UserEntity
        {
            UserId = user1,
            Username = user1,
            PasswordHash = "hash",
            Role = "User",
            CreatedAt = DateTime.UtcNow,
        });

        await userDb.InsertUserAsync(new UserEntity
        {
            UserId = user2,
            Username = user2,
            PasswordHash = "hash",
            Role = "User",
            CreatedAt = DateTime.UtcNow,
        });

        using var client1 = _factory.CreateUserClient(user1);
        using var client2 = _factory.CreateUserClient(user2);

        // User 1 creates Project 1
        var proj1Resp = await client1.PostAsJsonAsync("/api/projects", new { name = "ProjectOne", title = "Project One" });
        Assert.Equal(HttpStatusCode.OK, proj1Resp.StatusCode);
        var proj1Json = await proj1Resp.Content.ReadFromJsonAsync<JsonElement>();
        var proj1Id = proj1Json.GetProperty("active").GetProperty("id").GetString();
        Assert.NotNull(proj1Id);

        // User 2 creates Project 2
        var proj2Resp = await client2.PostAsJsonAsync("/api/projects", new { name = "ProjectTwo", title = "Project Two" });
        Assert.Equal(HttpStatusCode.OK, proj2Resp.StatusCode);
        var proj2Json = await proj2Resp.Content.ReadFromJsonAsync<JsonElement>();
        var proj2Id = proj2Json.GetProperty("active").GetProperty("id").GetString();
        Assert.NotNull(proj2Id);

        // Verify User 1's active project is Project 1
        var list1Resp = await client1.GetAsync("/api/projects");
        var list1Json = await list1Resp.Content.ReadFromJsonAsync<JsonElement>();
        var active1 = list1Json.GetProperty("active").GetProperty("id").GetString();
        Assert.Equal(proj1Id, active1);

        // Verify User 2's active project is Project 2 (not affected by User 1)
        var list2Resp = await client2.GetAsync("/api/projects");
        var list2Json = await list2Resp.Content.ReadFromJsonAsync<JsonElement>();
        var active2 = list2Json.GetProperty("active").GetProperty("id").GetString();
        Assert.Equal(proj2Id, active2);
    }
}
