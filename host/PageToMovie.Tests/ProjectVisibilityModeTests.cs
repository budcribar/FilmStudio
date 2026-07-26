using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ProjectVisibilityModeTests
{
    private static (ProjectStore Store, string Root) MakeHarness()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm_visibility_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root });
        var store = new ProjectStore(opts);
        return (store, root);
    }

    [Fact]
    public async Task Saves_And_Reads_VisibilityMode_Defaulting_To_Private()
    {
        var (store, root) = MakeHarness();
        try
        {
            var proj = await store.CreateProjectAsync("orig_proj", "Original Project", ownerUserId: "alice");

            Assert.Equal("Private", proj.VisibilityMode);

            // Change to Public Read-Only
            var updated = await store.SetProjectVisibilityModeAsync("orig_proj", "Public");
            Assert.Equal("Public", updated.VisibilityMode);

            // Re-read project
            var reloaded = await store.GetProjectAsync("orig_proj");
            Assert.NotNull(reloaded);
            Assert.Equal("Public", reloaded!.VisibilityMode);

            // Change to Open (Forkable)
            var openProj = await store.SetProjectVisibilityModeAsync("orig_proj", "Open");
            Assert.Equal("Open", openProj.VisibilityMode);
        }
        finally
        {
            if (Directory.Exists(root))
                try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task ForkProjectAsync_Enforces_Open_Visibility_For_Non_Owners()
    {
        var (store, root) = MakeHarness();
        try
        {
            await store.CreateProjectAsync("private_proj", "Private Project", ownerUserId: "alice");

            // Attempt to fork Private project by bob (should throw)
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => store.ForkProjectAsync("private_proj", "bob"));
            Assert.Contains("Forking disabled", ex.Message);

            // Change visibility to Open
            await store.SetProjectVisibilityModeAsync("private_proj", "Open");

            // Fork Open project by bob (should succeed)
            var forked = await store.ForkProjectAsync("private_proj", "bob");
            Assert.NotNull(forked);
            Assert.Equal("bob", forked.OwnerUserId);
            Assert.Equal("private_proj", forked.ParentProjectId);
        }
        finally
        {
            if (Directory.Exists(root))
                try { Directory.Delete(root, true); } catch { }
        }
    }
}
