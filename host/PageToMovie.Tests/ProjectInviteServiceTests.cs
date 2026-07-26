using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ProjectInviteServiceTests
{
    private static (ProjectInviteService Service, string Root) MakeService()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm_invite_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root });
        return (new ProjectInviteService(opts, NullLogger<ProjectInviteService>.Instance), root);
    }

    private static void Cleanup(string root)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(root, true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task CreateAsync_requires_a_target_handle_or_email()
    {
        var (svc, root) = MakeService();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => svc.CreateAsync("Demo", "owner1", null, null));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task ConsumeAsync_accepts_a_freshly_created_invite_exactly_once()
    {
        var (svc, root) = MakeService();
        try
        {
            var invite = await svc.CreateAsync("Demo", "owner1", targetUsername: "bob", targetEmail: null);

            var first = await svc.ConsumeAsync(invite.Token, "bob_user_id");
            Assert.True(first.Ok);
            Assert.Equal("Demo", first.ProjectId);

            var second = await svc.ConsumeAsync(invite.Token, "bob_user_id");
            Assert.False(second.Ok);
            Assert.Contains("already used", second.Error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task ConsumeAsync_rejects_an_unknown_token()
    {
        var (svc, root) = MakeService();
        try
        {
            var outcome = await svc.ConsumeAsync("not-a-real-token", "someone");
            Assert.False(outcome.Ok);
            Assert.Null(outcome.ProjectId);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task ConsumeAsync_rejects_without_a_signed_in_user()
    {
        var (svc, root) = MakeService();
        try
        {
            var invite = await svc.CreateAsync("Demo", "owner1", null, "friend@example.com");
            var outcome = await svc.ConsumeAsync(invite.Token, "");
            Assert.False(outcome.Ok);
        }
        finally
        {
            Cleanup(root);
        }
    }
}
