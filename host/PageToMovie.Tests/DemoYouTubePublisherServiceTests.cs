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

public class DemoYouTubePublisherServiceTests
{
    private static (DemoCatalogService Demos, DemoYouTubePublisherService Publisher, string Root) MakeHarness(
        YouTubeOptions? youTube = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm_demo_yt_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var opts = Options.Create(new PageToMovieOptions
        {
            WorkspaceRoot = root,
            YouTube = youTube ?? new YouTubeOptions(), // unconfigured by default
        });
        var projects = new ProjectStore(opts);
        var demos = new DemoCatalogService(projects, NullLogger<DemoCatalogService>.Instance);
        var auth = new YouTubeAuthService(projects, opts);
        var publisher = new DemoYouTubePublisherService(demos, auth, NullLogger<DemoYouTubePublisherService>.Instance);
        return (demos, publisher, root);
    }

    private static async Task<DemoCatalogService.DemoEntry> PublishSampleAsync(DemoCatalogService demos)
    {
        var bytes = Encoding.ASCII.GetBytes("....ftypmp42" + new string('x', 2000));
        await using var stream = new MemoryStream(bytes);
        return await demos.PublishFromStreamAsync(stream, "My Film", "desc", "Demo", "user1", acceptedGuidelines: true);
    }

    [Fact]
    public void IsConfigured_false_when_YouTube_OAuth_not_set_up()
    {
        var (_, publisher, root) = MakeHarness();
        try { Assert.False(publisher.IsConfigured); }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PublishAsync_marks_failed_and_keeps_local_file_when_not_configured()
    {
        var (demos, publisher, root) = MakeHarness();
        try
        {
            var entry = await PublishSampleAsync(demos);

            await publisher.PublishAsync(entry.Id);

            var updated = demos.TryGet(entry.Id);
            Assert.NotNull(updated);
            Assert.Equal("failed", updated!.YoutubeUploadStatus);
            Assert.Null(updated.YoutubeId);
            Assert.NotNull(demos.ResolveMoviePath(entry.Id)); // never lost the only copy
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task PublishAsync_is_a_noop_for_a_demo_that_does_not_exist()
    {
        var (_, publisher, root) = MakeHarness();
        try
        {
            await publisher.PublishAsync("does_not_exist_12345"); // must not throw
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task PublishAsync_is_a_noop_once_already_migrated_to_YouTube()
    {
        var (demos, publisher, root) = MakeHarness();
        try
        {
            var entry = await PublishSampleAsync(demos);
            demos.SetYouTubeUploadStatus(entry.Id, "done", "already123", "https://youtu.be/already123");

            await publisher.PublishAsync(entry.Id);

            var updated = demos.TryGet(entry.Id);
            Assert.Equal("already123", updated!.YoutubeId); // unchanged — no re-upload attempted
            Assert.Equal("done", updated.YoutubeUploadStatus);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
