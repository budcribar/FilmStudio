using System;
using System.IO;
using System.Threading.Tasks;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests
{
    public class ProjectGitRepositoryServiceTests
    {
        [Fact]
        public async Task CommitProjectStateAsync_Creates_Commit_Info()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ptm_git_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            var service = new ProjectGitRepositoryService(null!);

            try
            {
                var info = await service.CommitProjectStateAsync(tempDir, "Alice", "Updated Scene 2 beat prompts");
                Assert.NotNull(info);
                Assert.StartsWith("git_", info.CommitHash);
                Assert.Equal("Alice", info.Author);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public async Task SyncForkFromOriginAsync_Merges_Origin_Into_Fork()
        {
            string forkDir = Path.Combine(Path.GetTempPath(), "ptm_fork_" + Guid.NewGuid().ToString("N"));
            string parentDir = Path.Combine(Path.GetTempPath(), "ptm_parent_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(forkDir);
            Directory.CreateDirectory(parentDir);

            var service = new ProjectGitRepositoryService(null!);

            try
            {
                var res = await service.SyncForkFromOriginAsync(forkDir, parentDir);
                Assert.True(res.Success);
                Assert.False(res.HasConflicts);
                Assert.StartsWith("merge_", res.CommitHash);
            }
            finally
            {
                if (Directory.Exists(forkDir)) Directory.Delete(forkDir, true);
                if (Directory.Exists(parentDir)) Directory.Delete(parentDir, true);
            }
        }
    }
}
