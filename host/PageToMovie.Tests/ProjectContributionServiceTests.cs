using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ProjectContributionServiceTests
{
    private static (ProjectContributionService Service, string TargetDir, string OriginDir, string Root) MakeHarness()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm_contrib_test_" + Guid.NewGuid().ToString("N"));
        var targetDir = Path.Combine(root, "target_project");
        var originDir = Path.Combine(root, "origin_project");

        Directory.CreateDirectory(Path.Combine(targetDir, "source"));
        Directory.CreateDirectory(Path.Combine(originDir, "source"));

        var service = new ProjectContributionService(NullLogger<ProjectContributionService>.Instance);
        return (service, targetDir, originDir, root);
    }

    [Fact]
    public async Task Computes_Structured_Line_Diffs_Correctly()
    {
        var (service, targetDir, originDir, root) = MakeHarness();
        try
        {
            // Write screenplay files
            var originScreenplay = "INT. ROOM - DAY\nAlice enters.\nALICE\nHello world.";
            var targetScreenplay = "INT. ROOM - DAY\nAlice enters slowly.\nALICE\nHello world!\nBOB\nHi Alice!";

            await File.WriteAllTextAsync(Path.Combine(originDir, "source", "screenplay.fountain"), originScreenplay);
            await File.WriteAllTextAsync(Path.Combine(targetDir, "source", "screenplay.fountain"), targetScreenplay);

            var diff = await service.ComputeDiffAsync("fork_proj", "parent_proj", targetDir, originDir);

            Assert.NotNull(diff);
            Assert.Equal("fork_proj", diff.ProjectId);
            Assert.Equal("parent_proj", diff.ParentProjectId);
            Assert.NotEmpty(diff.FileDiffs);

            var screenplayDiff = Assert.Single(diff.FileDiffs, f => f.FilePath == "source/screenplay.fountain");
            Assert.Equal("Screenplay", screenplayDiff.Category);
            Assert.Equal("modified", screenplayDiff.Status);
            Assert.NotEmpty(screenplayDiff.Lines);
        }
        finally
        {
            if (Directory.Exists(root))
                try { Directory.Delete(root, true); } catch { }
        }
    }
}
