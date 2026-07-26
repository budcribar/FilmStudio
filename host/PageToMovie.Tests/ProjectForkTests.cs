using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ProjectForkTests
{
    private static (ProjectStore Store, string Root) MakeStore()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm_fork_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "projects"));
        var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root, EnableReadCaches = false });
        return (new ProjectStore(opts), root);
    }

    [Fact]
    public async Task ForkProjectAsync_copies_text_and_excludes_video()
    {
        var (store, root) = MakeStore();
        try
        {
            var source = await store.CreateProjectAsync("Original", ownerUserId: "owner1");
            await store.SetProjectVisibilityModeAsync(source.Id, "Open");
            var sourceDir = source.Path;
            Directory.CreateDirectory(Path.Combine(sourceDir, "source"));
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "source", "screenplay.fountain"), "INT. HOUSE - DAY");
            Directory.CreateDirectory(Path.Combine(sourceDir, "assets", "video"));
            await File.WriteAllTextAsync(Path.Combine(sourceDir, "assets", "video", "scene_01_clip_01.mp4"), "fake video bytes");

            var fork = await store.ForkProjectAsync(source.Id, "collaborator1");

            Assert.Equal("collaborator1", fork.OwnerUserId);
            Assert.Equal(source.Id, fork.ParentProjectId);
            Assert.NotEqual(source.Id, fork.Id);

            Assert.True(File.Exists(Path.Combine(fork.Path, "source", "screenplay.fountain")));
            Assert.False(File.Exists(Path.Combine(fork.Path, "assets", "video", "scene_01_clip_01.mp4")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ForkProjectAsync_does_not_change_the_active_project()
    {
        var (store, root) = MakeStore();
        try
        {
            var source = await store.CreateProjectAsync("Original", ownerUserId: "owner1");
            await store.SetProjectVisibilityModeAsync(source.Id, "Open");
            var other = await store.CreateProjectAsync("StillActive", ownerUserId: "owner1");
            // CreateProjectAsync activates each project it makes — "StillActive" is now active.

            await store.ForkProjectAsync(source.Id, "collaborator1");

            Assert.Equal(other.Id, store.ActiveProjectId);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ForkProjectAsync_throws_for_unknown_source_project()
    {
        var (store, root) = MakeStore();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.ForkProjectAsync("DoesNotExist", "collaborator1"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
