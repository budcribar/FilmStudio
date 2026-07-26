using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Logging.Abstractions;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests
{
    public class ProjectGitRepositoryServiceTests
    {
        private static string NewTempDir(string prefix)
        {
            var dir = Path.Combine(Path.GetTempPath(), prefix + "_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DeleteDir(string dir)
        {
            try { Directory.Delete(dir, true); } catch { /* best effort on Windows file locks */ }
        }

        [Fact]
        public async Task CommitProjectStateAsync_creates_a_real_commit()
        {
            var dir = NewTempDir("ptm_git");
            try
            {
                File.WriteAllText(Path.Combine(dir, "project.json"), """{"id":"Demo"}""");
                var service = new ProjectGitRepositoryService(NullLogger<ProjectGitRepositoryService>.Instance);

                var info = await service.CommitProjectStateAsync(dir, "Alice", "Initial project state");

                Assert.NotNull(info);
                Assert.Equal(40, info.CommitHash.Length); // real SHA-1 hex, not a fake "git_" prefix
                Assert.Equal("Alice", info.Author);

                using var repo = new Repository(dir);
                Assert.Single(repo.Commits);
                Assert.Equal(info.CommitHash, repo.Head.Tip.Sha);
                var blob = (Blob)repo.Head.Tip["project.json"].Target;
                Assert.Contains("Demo", blob.GetContentText());
            }
            finally
            {
                DeleteDir(dir);
            }
        }

        [Fact]
        public async Task CommitProjectStateAsync_is_a_noop_when_nothing_changed()
        {
            var dir = NewTempDir("ptm_git");
            try
            {
                File.WriteAllText(Path.Combine(dir, "project.json"), """{"id":"Demo"}""");
                var service = new ProjectGitRepositoryService(NullLogger<ProjectGitRepositoryService>.Instance);

                var first = await service.CommitProjectStateAsync(dir, "Alice", "Initial");
                var second = await service.CommitProjectStateAsync(dir, "Alice", "Nothing changed");

                Assert.Equal(first.CommitHash, second.CommitHash);
                using var repo = new Repository(dir);
                Assert.Single(repo.Commits); // no empty second commit was created
            }
            finally
            {
                DeleteDir(dir);
            }
        }

        [Fact]
        public async Task CommitProjectStateAsync_never_tracks_video_binaries()
        {
            var dir = NewTempDir("ptm_git");
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "assets", "video"));
                File.WriteAllText(Path.Combine(dir, "project.json"), """{"id":"Demo"}""");
                File.WriteAllText(Path.Combine(dir, "assets", "video", "scene_01_clip_01.mp4"), "fake video bytes");

                var service = new ProjectGitRepositoryService(NullLogger<ProjectGitRepositoryService>.Instance);
                await service.CommitProjectStateAsync(dir, "Alice", "Initial");

                using var repo = new Repository(dir);
                Assert.NotNull(repo.Head.Tip["project.json"]);
                Assert.Null(repo.Head.Tip["assets/video/scene_01_clip_01.mp4"]);
            }
            finally
            {
                DeleteDir(dir);
            }
        }

        [Fact]
        public async Task SyncForkFromOriginAsync_fails_cleanly_when_parent_has_no_commits()
        {
            var fork = NewTempDir("ptm_fork");
            var parent = NewTempDir("ptm_parent");
            try
            {
                var service = new ProjectGitRepositoryService(NullLogger<ProjectGitRepositoryService>.Instance);
                var res = await service.SyncForkFromOriginAsync(fork, parent);

                Assert.False(res.Success);
                Assert.False(res.HasConflicts);
            }
            finally
            {
                DeleteDir(fork);
                DeleteDir(parent);
            }
        }

        [Fact]
        public async Task SyncForkFromOriginAsync_merges_non_conflicting_changes_from_parent()
        {
            var fork = NewTempDir("ptm_fork");
            var parent = NewTempDir("ptm_parent");
            try
            {
                var service = new ProjectGitRepositoryService(NullLogger<ProjectGitRepositoryService>.Instance);

                File.WriteAllText(Path.Combine(parent, "parent_only.txt"), "from parent");
                await service.CommitProjectStateAsync(parent, "Owner", "Parent update");

                File.WriteAllText(Path.Combine(fork, "fork_only.txt"), "from fork");
                await service.CommitProjectStateAsync(fork, "Collaborator", "Fork edit");

                var res = await service.SyncForkFromOriginAsync(fork, parent);

                Assert.True(res.Success);
                Assert.False(res.HasConflicts);
                Assert.True(File.Exists(Path.Combine(fork, "parent_only.txt")), "parent's file must be merged in");
                Assert.True(File.Exists(Path.Combine(fork, "fork_only.txt")), "fork's own file must be preserved");
            }
            finally
            {
                DeleteDir(fork);
                DeleteDir(parent);
            }
        }

        [Fact]
        public async Task SyncForkFromOriginAsync_reports_conflicts_without_committing()
        {
            var fork = NewTempDir("ptm_fork");
            var parent = NewTempDir("ptm_parent");
            try
            {
                var service = new ProjectGitRepositoryService(NullLogger<ProjectGitRepositoryService>.Instance);

                File.WriteAllText(Path.Combine(parent, "shared.txt"), "parent version");
                await service.CommitProjectStateAsync(parent, "Owner", "Parent edit");

                File.WriteAllText(Path.Combine(fork, "shared.txt"), "fork version");
                await service.CommitProjectStateAsync(fork, "Collaborator", "Fork edit");

                using var forkRepoBefore = new Repository(fork);
                var headBefore = forkRepoBefore.Head.Tip.Sha;

                var res = await service.SyncForkFromOriginAsync(fork, parent);

                Assert.False(res.Success);
                Assert.True(res.HasConflicts);

                using var forkRepoAfter = new Repository(fork);
                // Must not have silently committed a resolution on the caller's behalf.
                Assert.Equal(headBefore, forkRepoAfter.Head.Tip.Sha);
            }
            finally
            {
                DeleteDir(fork);
                DeleteDir(parent);
            }
        }
    }
}
