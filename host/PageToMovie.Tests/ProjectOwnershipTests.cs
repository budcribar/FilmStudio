using PageToMovie.Core.Models;
using Xunit;

namespace PageToMovie.Tests;

public class ProjectOwnershipTests
{
    [Fact]
    public void SanitizeOwnerSegment_replaces_dots_and_at()
    {
        Assert.Equal("budcribarmsn_com", ProjectOwnership.SanitizeOwnerSegment("budcribarmsn.com"));
        Assert.Equal("budcribar_msn_com", ProjectOwnership.SanitizeOwnerSegment("budcribar@msn.com"));
    }

    [Fact]
    public void IsOwnedBy_matches_folder_owner_segment_alias()
    {
        var p = new ProjectInfo
        {
            Id = "budcribarmsn_com/Mary",
            OwnerUserId = "budcribarmsn.com",
        };
        var aliases = ProjectOwnership.CollectAliases(
            requestUserId: "budcribarmsn.com",
            canonicalUserId: "budcribarmsn.com",
            username: "budcribarmsn.com",
            email: null);
        Assert.True(ProjectOwnership.IsOwnedBy(p, aliases));
    }

    [Fact]
    public void IsOwnedBy_matches_when_jwt_is_username_and_owner_is_userid()
    {
        var p = new ProjectInfo
        {
            Id = "budcribar/Buster",
            OwnerUserId = "budcribar",
        };
        var aliases = ProjectOwnership.CollectAliases(
            requestUserId: "BudCribar",
            canonicalUserId: "budcribar",
            username: "BudCribar",
            email: "bud@example.com");
        Assert.True(ProjectOwnership.IsOwnedBy(p, aliases));
    }

    [Fact]
    public void IsOwnedBy_matches_email_local_part_folder()
    {
        var p = new ProjectInfo
        {
            Id = "alice/Project",
            OwnerUserId = "alice@example.com",
        };
        var aliases = ProjectOwnership.CollectAliases(
            requestUserId: "alice",
            canonicalUserId: "alice",
            username: "alice",
            email: "alice@example.com");
        Assert.True(ProjectOwnership.IsOwnedBy(p, aliases));
    }

    [Fact]
    public void IsOwnedBy_matches_when_session_id_is_email_and_owner_is_handle()
    {
        // The exact email→user-id drift: after migration the session still identifies as the email
        // (as requestUserId) while projects are owned by the bare handle, and the user-record lookup
        // failed so nothing but requestUserId is available. Without local-part extraction here, every
        // one of this user's projects gets filtered out of GET /api/projects.
        var p = new ProjectInfo { Id = "budcribar/Mary10", OwnerUserId = "budcribar" };
        var aliases = ProjectOwnership.CollectAliases(requestUserId: "budcribar@msn.com");
        Assert.True(ProjectOwnership.IsOwnedBy(p, aliases));
    }

    [Fact]
    public void IsOwnedBy_rejects_other_users()
    {
        var p = new ProjectInfo { Id = "other/Mary", OwnerUserId = "other" };
        var aliases = ProjectOwnership.CollectAliases("budcribar", "budcribar", "Bud", null);
        Assert.False(ProjectOwnership.IsOwnedBy(p, aliases));
    }
}
