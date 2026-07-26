using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class DemoCatalogServiceTests
{
    private static (DemoCatalogService Demos, string Root) MakeHarness()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm_demo_catalog_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root });
        var projects = new ProjectStore(opts);
        var demos = new DemoCatalogService(projects, NullLogger<DemoCatalogService>.Instance);
        return (demos, root);
    }

    private static async Task<DemoCatalogService.DemoEntry> PublishSampleAsync(DemoCatalogService demos)
    {
        var bytes = Encoding.ASCII.GetBytes("....ftypmp42" + new string('x', 2000));
        await using var stream = new MemoryStream(bytes);
        return await demos.PublishFromStreamAsync(
            stream, "My Film", "desc", "Demo", "user1", acceptedGuidelines: true,
            madeForKids: true, isAiSyntheticContent: false, privacyStatus: "unlisted",
            tags: new() { "a", "b" });
    }

    [Fact]
    public async Task Records_YouTube_metadata_declared_at_submit_time()
    {
        var (demos, root) = MakeHarness();
        try
        {
            var entry = await PublishSampleAsync(demos);

            Assert.True(entry.MadeForKids);
            Assert.False(entry.IsAiSyntheticContent);
            Assert.Equal("unlisted", entry.PrivacyStatus);
            Assert.Equal(new[] { "a", "b" }, entry.Tags);
            Assert.Equal("none", entry.YoutubeUploadStatus);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SetYouTubeUploadStatus_done_deletes_local_file_but_entry_stays_valid()
    {
        var (demos, root) = MakeHarness();
        try
        {
            var entry = await PublishSampleAsync(demos);
            var moviePath = demos.ResolveMoviePath(entry.Id);
            Assert.NotNull(moviePath);
            Assert.True(File.Exists(moviePath));

            var updated = demos.SetYouTubeUploadStatus(entry.Id, "done", "yt123", "https://youtu.be/yt123");

            Assert.NotNull(updated);
            Assert.Equal("yt123", updated!.YoutubeId);
            Assert.Equal("https://youtu.be/yt123", updated.YoutubeUrl);
            Assert.Equal("done", updated.YoutubeUploadStatus);
            Assert.False(File.Exists(moviePath!)); // local copy removed — server footprint goal

            // Entry must still resolve (it now lives on YouTube, not on disk).
            var reread = demos.TryGet(entry.Id);
            Assert.NotNull(reread);
            Assert.Equal("yt123", reread!.YoutubeId);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SetYouTubeUploadStatus_failed_keeps_local_file_as_fallback()
    {
        var (demos, root) = MakeHarness();
        try
        {
            var entry = await PublishSampleAsync(demos);

            var updated = demos.SetYouTubeUploadStatus(entry.Id, "failed", error: "quota exceeded");

            Assert.NotNull(updated);
            Assert.Equal("failed", updated!.YoutubeUploadStatus);
            Assert.Equal("quota exceeded", updated.YoutubeUploadError);
            Assert.Null(updated.YoutubeId);
            Assert.NotNull(demos.ResolveMoviePath(entry.Id)); // still server-hosted
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Entry_with_no_local_file_and_no_YoutubeId_is_treated_as_missing()
    {
        var (demos, root) = MakeHarness();
        try
        {
            var entry = await PublishSampleAsync(demos);
            var moviePath = demos.ResolveMoviePath(entry.Id)!;
            File.Delete(moviePath); // simulate corruption/partial write without a YouTube migration

            Assert.Null(demos.TryGet(entry.Id));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
