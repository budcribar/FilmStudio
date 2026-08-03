using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class ProjectGitNestingTests
{
    [Fact]
    public void TryEnsureRepository_skips_when_nested_under_outer_git()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm-gitnest-" + Guid.NewGuid().ToString("N"));
        var outerGit = Path.Combine(root, ".git");
        var project = Path.Combine(root, "projects", "alice", "Mary");
        Directory.CreateDirectory(outerGit);
        Directory.CreateDirectory(project);

        Assert.True(ProjectGitRepositoryService.IsNestedInOuterGitWorktree(project));
        Assert.False(ProjectGitRepositoryService.TryEnsureRepository(project, out var reason));
        Assert.Contains("nested", reason ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(project, ".git")));
    }

    [Fact]
    public void TryEnsureRepository_inits_when_not_nested()
    {
        var root = Path.Combine(Path.GetTempPath(), "ptm-gitok-" + Guid.NewGuid().ToString("N"));
        var project = Path.Combine(root, "projects", "budcribar", "Mary");
        Directory.CreateDirectory(project);

        Assert.False(ProjectGitRepositoryService.IsNestedInOuterGitWorktree(project));
        Assert.True(ProjectGitRepositoryService.TryEnsureRepository(project, out var reason), reason);
        Assert.True(LibGit2Sharp.Repository.IsValid(project));
        Assert.True(File.Exists(Path.Combine(project, ".gitignore")));
    }
}
