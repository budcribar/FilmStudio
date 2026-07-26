using System;
using System.IO;
using System.Threading.Tasks;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests
{
    public class ServerMediaPruningServiceTests
    {
        [Fact]
        public void PruneOldMediaFiles_Deletes_Files_Older_Than_Threshold()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), "ptm_pruning_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            string oldMp4 = Path.Combine(tempDir, "old_clip.mp4");
            string newMp4 = Path.Combine(tempDir, "new_clip.mp4");
            string textScript = Path.Combine(tempDir, "script.fountain");

            File.WriteAllText(oldMp4, "dummy old video content");
            File.WriteAllText(newMp4, "dummy new video content");
            File.WriteAllText(textScript, "dummy screenplay script");

            // Backdate old_clip.mp4 to 3 days ago (72h old)
            File.SetLastWriteTimeUtc(oldMp4, DateTime.UtcNow.AddDays(-3));
            File.SetLastWriteTimeUtc(newMp4, DateTime.UtcNow);
            File.SetLastWriteTimeUtc(textScript, DateTime.UtcNow.AddDays(-5)); // text files should never be deleted

            var service = new ServerMediaPruningService(null, tempDir);

            try
            {
                // Act
                int deleted = service.PerformPruning(tempDir, TimeSpan.FromHours(48), 99.0);

                // Assert
                Assert.Equal(1, deleted);
                Assert.False(File.Exists(oldMp4));
                Assert.True(File.Exists(newMp4));
                Assert.True(File.Exists(textScript)); // Screenplay intact!
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
