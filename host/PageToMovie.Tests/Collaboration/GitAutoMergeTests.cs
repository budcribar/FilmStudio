using LibGit2Sharp;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using PageToMovie.Engine.Collaboration;
using Xunit;

namespace PageToMovie.Tests.Collaboration;

public sealed class GitAutoMergeTests : IDisposable
{
    private readonly string _root;
    private readonly ProjectGitRepositoryService _git;

    // Minimal logger that does not depend on Microsoft.Extensions.Logging package resolution
    sealed class SilentLogger : Microsoft.Extensions.Logging.ILogger<ProjectGitRepositoryService>
    {
        public System.IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            System.Exception? exception,
            System.Func<TState, System.Exception?, string> formatter) { }
    }

    public GitAutoMergeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ptm-git-auto-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _git = new ProjectGitRepositoryService(
            new SilentLogger(),
            Options.Create(new PageToMovieOptions()));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    static void InitRepo(string path, string content, string fileName = "script.txt")
    {
        Directory.CreateDirectory(path);
        Repository.Init(path);
        File.WriteAllText(Path.Combine(path, fileName), content);
        using var repo = new Repository(path);
        Commands.Stage(repo, "*");
        var sig = new Signature("tester", "t@test.local", DateTimeOffset.UtcNow);
        repo.Commit("initial", sig, sig);
    }

    static void CommitFile(string path, string fileName, string content, string message)
    {
        File.WriteAllText(Path.Combine(path, fileName), content);
        using var repo = new Repository(path);
        Commands.Stage(repo, fileName);
        var sig = new Signature("tester", "t@test.local", DateTimeOffset.UtcNow);
        repo.Commit(message, sig, sig);
    }

    [Fact]
    public async Task Sync_without_strategy_does_not_throw()
    {
        var parent = Path.Combine(_root, "parent");
        var fork = Path.Combine(_root, "fork");
        InitRepo(parent, "line1\nline2\n");
        InitRepo(fork, "line1\nline2\n");
        CommitFile(parent, "script.txt", "line1\nline2\nline3\n", "parent adds line3");

        var res = await _git.SyncForkFromOriginAsync(fork, parent);
        Assert.NotNull(res.Message);
    }

    [Fact]
    public async Task AutoResolve_PreferOurs_does_not_throw()
    {
        var parent = Path.Combine(_root, "p2");
        var fork = Path.Combine(_root, "f2");
        InitRepo(parent, "shared\nbase-line\nshared2\n");
        InitRepo(fork, "shared\nbase-line\nshared2\n");
        CommitFile(parent, "script.txt", "shared\nparent-line\nshared2\n", "parent edit");
        CommitFile(fork, "script.txt", "shared\nfork-line\nshared2\n", "fork edit");

        var res = await _git.SyncForkFromOriginWithAutoResolveAsync(
            fork, parent, AutoTextMerger.Strategy.PreferOurs);
        Assert.NotNull(res.Message);
        if (res.Success)
        {
            Assert.False(res.HasConflicts);
            var text = File.ReadAllText(Path.Combine(fork, "script.txt"));
            Assert.DoesNotContain("<<<<<<<", text);
        }
        else if (res.HasConflicts)
        {
            Assert.NotNull(res.RemainingConflictPaths);
        }
    }

    [Fact]
    public async Task AutoResolve_on_parent_advance_does_not_throw()
    {
        var parent = Path.Combine(_root, "p3");
        var fork = Path.Combine(_root, "f3");
        InitRepo(parent, "A\nB\nC\n");
        InitRepo(fork, "A\nB\nC\n");
        CommitFile(parent, "script.txt", "A\nB\nC\nD\n", "parent appends D");

        var res = await _git.SyncForkFromOriginWithAutoResolveAsync(
            fork, parent, AutoTextMerger.Strategy.PreferOurs);
        Assert.NotNull(res.Message);
    }
}
