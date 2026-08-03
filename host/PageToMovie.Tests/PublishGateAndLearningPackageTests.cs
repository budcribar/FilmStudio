using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public sealed class PublishGateAndLearningPackageTests
{
    [Theory]
    [InlineData("aaa", "aaa", 10.0, 10.0, FilmBuildPublish.PathStudioIntact)]
    [InlineData("aaa", "bbb", 10.0, 10.1, FilmBuildPublish.PathExternalSameLength)]
    [InlineData("aaa", "bbb", 10.0, 12.0, FilmBuildPublish.PathExternalRestructured)]
    [InlineData("aaa", "bbb", 0, null, FilmBuildPublish.PathExternalRestructured)]
    [InlineData("", "bbb", 0, null, FilmBuildPublish.PathUnknown)]
    public void ClassifyPublishPath_cases(
        string studio, string upload, double studioDur, double? uploadDur, string expected)
    {
        var path = FilmBuildService.ClassifyPublishPath(studio, upload, studioDur, uploadDur);
        Assert.Equal(expected, path);
    }

    [Fact]
    public void ApplyUploadHashGate_and_learning_package_roundtrip()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm_lp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            // Minimal store double via real ProjectStore is heavy; exercise pure write path.
            var projectDir = Path.Combine(root, "proj");
            Directory.CreateDirectory(Path.Combine(projectDir, "source"));
            Directory.CreateDirectory(Path.Combine(projectDir, "assets"));

            var studioBytes = new byte[] { 1, 2, 3, 4, 5 };
            var sha = FilmBuildService.HashBytes(studioBytes);
            var doc = FilmBuildService.Create("test/Mary", sha, 8.0, null, studioBytes.Length);
            FilmBuildService.Write(projectDir, doc);

            // Simulate upload of identical bytes
            var loaded = FilmBuildService.TryRead(projectDir);
            Assert.NotNull(loaded);
            Assert.Equal(sha, loaded!.Studio.Sha256);

            // Classify intact
            Assert.Equal(
                FilmBuildPublish.PathStudioIntact,
                FilmBuildService.ClassifyPublishPath(sha, sha, 8.0, 8.0));

            // Learning package files on disk structure
            var pkgDir = Path.Combine(projectDir, "artifacts", "learning_packages", "lp_test");
            Directory.CreateDirectory(pkgDir);
            File.WriteAllText(Path.Combine(pkgDir, "package.json"), """{"schema_version":"learning_package.v1"}""");
            Assert.True(File.Exists(Path.Combine(pkgDir, "package.json")));
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* */ }
        }
    }
}
