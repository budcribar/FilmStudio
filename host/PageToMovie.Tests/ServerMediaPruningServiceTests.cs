using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests
{
    public class ServerMediaPruningServiceTests
    {
        private static (ProjectStore Projects, MediaRegistryService Registry, string Root) MakeHarness()
        {
            var root = Path.Combine(Path.GetTempPath(), "ptm_pruning_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "projects"));
            var opts = Options.Create(new PageToMovieOptions { WorkspaceRoot = root, EnableReadCaches = false });
            var projects = new ProjectStore(opts);
            var registry = new MediaRegistryService(opts, NullLogger<MediaRegistryService>.Instance);
            return (projects, registry, root);
        }

        // MediaRegistryService's Sqlite connection pool keeps pagetomovie.db open after use;
        // clear pools before deleting the temp workspace or Directory.Delete throws IOException.
        private static void Cleanup(string root)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(root, true);
        }

        private static string WriteClip(string root, string projectId, string relativePath, DateTime lastWriteUtc, string content = "dummy video bytes")
        {
            var full = Path.Combine(root, "projects", projectId, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
            File.SetLastWriteTimeUtc(full, lastWriteUtc);
            return full;
        }

        [Fact]
        public async Task Never_deletes_a_file_the_registry_has_no_record_of()
        {
            var (projects, registry, root) = MakeHarness();
            try
            {
                var path = WriteClip(root, "Demo", "assets/video/scene_01_clip_01.mp4", DateTime.UtcNow.AddDays(-3));
                var service = new ServerMediaPruningService(NullLogger<ServerMediaPruningService>.Instance, projects, registry, Options.Create(new PageToMovieOptions()));

                var deleted = await service.PerformPruningAsync(TimeSpan.FromHours(48), 99.0);

                Assert.Equal(0, deleted);
                Assert.True(File.Exists(path), "Unregistered (unsynced) media must never be deleted — it may be the only copy.");
            }
            finally
            {
                Cleanup(root);
            }
        }

        [Fact]
        public async Task Deletes_old_media_once_client_has_registered_a_synced_copy()
        {
            var (projects, registry, root) = MakeHarness();
            try
            {
                var relPath = "assets/video/scene_01_clip_01.mp4";
                var path = WriteClip(root, "Demo", relPath, DateTime.UtcNow.AddDays(-3));
                await registry.UpsertAsync("Demo", relPath, new string('a', 64), 123, "clip", 1, 1, "user1");

                var service = new ServerMediaPruningService(NullLogger<ServerMediaPruningService>.Instance, projects, registry, Options.Create(new PageToMovieOptions()));
                var deleted = await service.PerformPruningAsync(TimeSpan.FromHours(48), 99.0);

                Assert.Equal(1, deleted);
                Assert.False(File.Exists(path));
            }
            finally
            {
                Cleanup(root);
            }
        }

        [Fact]
        public async Task Keeps_registered_media_younger_than_max_age()
        {
            var (projects, registry, root) = MakeHarness();
            try
            {
                var relPath = "assets/video/scene_01_clip_01.mp4";
                var path = WriteClip(root, "Demo", relPath, DateTime.UtcNow);
                await registry.UpsertAsync("Demo", relPath, new string('b', 64), 123, "clip", 1, 1, "user1");

                var service = new ServerMediaPruningService(NullLogger<ServerMediaPruningService>.Instance, projects, registry, Options.Create(new PageToMovieOptions()));
                var deleted = await service.PerformPruningAsync(TimeSpan.FromHours(48), 99.0);

                Assert.Equal(0, deleted);
                Assert.True(File.Exists(path));
            }
            finally
            {
                Cleanup(root);
            }
        }

        [Fact]
        public async Task Never_touches_non_media_files_regardless_of_age_or_registration()
        {
            var (projects, registry, root) = MakeHarness();
            try
            {
                var scriptPath = Path.Combine(root, "projects", "Demo", "source", "screenplay.fountain");
                Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
                File.WriteAllText(scriptPath, "INT. HOUSE - DAY");
                File.SetLastWriteTimeUtc(scriptPath, DateTime.UtcNow.AddDays(-30));

                var service = new ServerMediaPruningService(NullLogger<ServerMediaPruningService>.Instance, projects, registry, Options.Create(new PageToMovieOptions()));
                var deleted = await service.PerformPruningAsync(TimeSpan.FromHours(48), 99.0);

                Assert.Equal(0, deleted);
                Assert.True(File.Exists(scriptPath));
            }
            finally
            {
                Cleanup(root);
            }
        }

        [Fact]
        public async Task Aggressively_prunes_synced_media_past_the_grace_period_even_well_under_max_age()
        {
            var (projects, registry, root) = MakeHarness();
            try
            {
                var relPath = "assets/video/scene_01_clip_01.mp4";
                var path = WriteClip(root, "Demo", relPath, DateTime.UtcNow.AddMinutes(-10));
                await registry.UpsertAsync("Demo", relPath, new string('c', 64), 123, "clip", 1, 1, "user1");

                var opts = Options.Create(new PageToMovieOptions
                {
                    MediaPruning = new MediaPruningOptions { AggressivePruneGraceMinutes = 5 },
                });
                var service = new ServerMediaPruningService(NullLogger<ServerMediaPruningService>.Instance, projects, registry, opts);
                // maxAge is 48h — the file is nowhere near that old, so only the aggressive
                // grace-period pass (Pass 0) can be what catches it.
                var deleted = await service.PerformPruningAsync(TimeSpan.FromHours(48), 99.0);

                Assert.Equal(1, deleted);
                Assert.False(File.Exists(path));
            }
            finally
            {
                Cleanup(root);
            }
        }

        [Fact]
        public async Task Keeps_synced_media_still_within_the_grace_period()
        {
            var (projects, registry, root) = MakeHarness();
            try
            {
                var relPath = "assets/video/scene_01_clip_01.mp4";
                var path = WriteClip(root, "Demo", relPath, DateTime.UtcNow.AddMinutes(-1));
                await registry.UpsertAsync("Demo", relPath, new string('d', 64), 123, "clip", 1, 1, "user1");

                var opts = Options.Create(new PageToMovieOptions
                {
                    MediaPruning = new MediaPruningOptions { AggressivePruneGraceMinutes = 5 },
                });
                var service = new ServerMediaPruningService(NullLogger<ServerMediaPruningService>.Instance, projects, registry, opts);
                var deleted = await service.PerformPruningAsync(TimeSpan.FromHours(48), 99.0);

                Assert.Equal(0, deleted);
                Assert.True(File.Exists(path));
            }
            finally
            {
                Cleanup(root);
            }
        }

        [Fact]
        public void Pruning_is_disabled_by_default()
        {
            Assert.False(new PageToMovieOptions().MediaPruning.Enabled);
        }
    }
}
