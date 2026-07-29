using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Core.Utils;

namespace PageToMovie.Engine
{
    public class GitCommitInfo
    {
        public string CommitHash { get; set; } = "";
        public string Author { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime CommittedAt { get; set; } = DateTime.UtcNow;
    }

    public class GitMergeResult
    {
        public bool Success { get; set; }
        public bool HasConflicts { get; set; }
        public string CommitHash { get; set; } = "";
        public string Message { get; set; } = "";
    }

    public class GitPushResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string? Branch { get; set; }
        /// <summary>Browser URL for commit history on the host (GitHub).</summary>
        public string? HistoryUrl { get; set; }
        public string? CommitHash { get; set; }
    }

    /// <summary>
    /// Real Git-backed project state: commits (LibGit2Sharp), 3-way sync-from-origin merge,
    /// and optional push to a shared Projects remote for version history.
    /// Each project directory is its own Git repo (video ignored). See <see cref="EnsureRepository"/>.
    /// </summary>
    public class ProjectGitRepositoryService
    {
        private readonly ILogger<ProjectGitRepositoryService> _logger;
        private readonly GitOptions _git;

        private const string SyncRemoteName = "sync-origin";
        private const string GithubRemoteName = "github-projects";

        /// <summary>
        /// Video/audio binaries never belong in the project's own Git history — they live in the
        /// client's local media folder (see host README "Client Media Storage"). Keeps each
        /// project's repo small enough to actually diff/merge.
        /// </summary>
        private static readonly string[] IgnoredGlobs =
        {
            "assets/video/",
            "*.mp4",
            "*.webm",
            "*.mov",
            "*.wav",
            "*.avi",
        };

        public ProjectGitRepositoryService(
            ILogger<ProjectGitRepositoryService> logger,
            IOptions<PageToMovieOptions>? opts = null)
        {
            _logger = logger;
            _git = opts.GetOrDefault().Git ?? new GitOptions();
        }

        /// <summary>
        /// Stages and commits every tracked (non-ignored) change in the project directory.
        /// If nothing changed since the last commit, returns the existing HEAD instead of
        /// creating an empty commit.
        /// </summary>
        public Task<GitCommitInfo> CommitProjectStateAsync(string projectPath, string author, string commitMessage)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
                throw new DirectoryNotFoundException($"Project directory not found: {projectPath}");

            EnsureRepository(projectPath);

            using var repo = new Repository(projectPath);
            Commands.Stage(repo, "*");

            var status = repo.RetrieveStatus();
            if (repo.Head.Tip is not null && !status.IsDirty)
            {
                var tip = repo.Head.Tip;
                _logger.LogDebug("No changes to commit for {Path}; HEAD stays {Hash}", projectPath, tip.Sha);
                return Task.FromResult(new GitCommitInfo
                {
                    CommitHash = tip.Sha,
                    Author = tip.Author.Name,
                    Message = tip.Message.TrimEnd('\n'),
                    CommittedAt = tip.Author.When.UtcDateTime,
                });
            }

            var who = string.IsNullOrWhiteSpace(author) ? "PageToMovie" : author.Trim();
            var signature = new Signature(who, EmailFor(who), DateTimeOffset.UtcNow);
            var commit = repo.Commit(
                string.IsNullOrWhiteSpace(commitMessage) ? "Project update" : commitMessage,
                signature,
                signature,
                new CommitOptions { AllowEmptyCommit = true });

            _logger.LogInformation(
                "Committed project state for {Path}. Commit: {Hash} - {Message}",
                projectPath, commit.Sha, commitMessage);

            return Task.FromResult(new GitCommitInfo
            {
                CommitHash = commit.Sha,
                Author = who,
                Message = commitMessage,
                CommittedAt = DateTime.UtcNow,
            });
        }

        /// <summary>
        /// Reads the text content of a file at a specific Git commit hash.
        /// </summary>
        public string? GetFileContentAtCommit(string projectPath, string commitHash, string relativeFilePath)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath) || !Repository.IsValid(projectPath))
                return null;

            using var repo = new Repository(projectPath);
            var commit = repo.Lookup<Commit>(commitHash);
            if (commit is null) return null;

            var relPath = relativeFilePath.Replace('\\', '/');
            var treeEntry = commit[relPath];
            if (treeEntry?.Target is not Blob blob)
                return null;

            return blob.GetContentText();
        }

        /// <summary>
        /// Retrieves uncommitted file changes in the project's working directory.
        /// </summary>
        public (bool HasChanges, List<string> ModifiedFiles) GetUncommittedStatus(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath) || !Repository.IsValid(projectPath))
                return (false, new List<string>());

            using var repo = new Repository(projectPath);
            var status = repo.RetrieveStatus();
            var modified = status
                .Where(s => s.State != FileStatus.Ignored && s.State != FileStatus.Unaltered)
                .Select(s => s.FilePath.Replace('\\', '/'))
                .ToList();

            return (modified.Count > 0, modified);
        }

        /// <summary>
        /// Retrieves the recent Git commit history for a project repository.
        /// </summary>
        public Task<IReadOnlyList<GitCommitInfo>> GetCommitHistoryAsync(string projectPath, int maxCount = 20)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath) || !Repository.IsValid(projectPath))
                return Task.FromResult<IReadOnlyList<GitCommitInfo>>(Array.Empty<GitCommitInfo>());

            using var repo = new Repository(projectPath);
            if (repo.Head.Tip is null)
                return Task.FromResult<IReadOnlyList<GitCommitInfo>>(Array.Empty<GitCommitInfo>());

            var list = repo.Commits
                .Take(Math.Clamp(maxCount, 1, 100))
                .Select(c => new GitCommitInfo
                {
                    CommitHash = c.Sha,
                    Author = c.Author.Name,
                    Message = c.Message.TrimEnd('\n'),
                    CommittedAt = c.Author.When.UtcDateTime,
                })
                .ToList();

            return Task.FromResult<IReadOnlyList<GitCommitInfo>>(list);
        }

        /// <summary>
        /// Reverts all project files to the exact state at <paramref name="commitHash"/>, creating a new commit.
        /// Preserves full Git history while restoring project files to the prior commit state.
        /// </summary>
        public Task<GitCommitInfo> RevertToCommitAsync(string projectPath, string commitHash, string author)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
                throw new DirectoryNotFoundException($"Project directory not found: {projectPath}");

            EnsureRepository(projectPath);

            using var repo = new Repository(projectPath);
            var targetCommit = repo.Lookup<Commit>(commitHash);
            if (targetCommit is null)
                throw new ArgumentException($"Commit {commitHash} not found in repository.", nameof(commitHash));

            var checkoutOpts = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
            repo.CheckoutPaths(targetCommit.Sha, new[] { "*" }, checkoutOpts);
            Commands.Stage(repo, "*");

            var who = string.IsNullOrWhiteSpace(author) ? "PageToMovie" : author.Trim();
            var signature = new Signature(who, EmailFor(who), DateTimeOffset.UtcNow);
            var shortHash = targetCommit.Sha.Length >= 8 ? targetCommit.Sha[..8] : targetCommit.Sha;
            var shortMsg = targetCommit.MessageShort;
            var msg = $"Undo: Revert project state to commit {shortHash} ({shortMsg})";

            var newCommit = repo.Commit(msg, signature, signature, new CommitOptions { AllowEmptyCommit = true });

            _logger.LogInformation("Reverted project {Path} to commit {Hash}. New commit: {NewSha}", projectPath, commitHash, newCommit.Sha);

            return Task.FromResult(new GitCommitInfo
            {
                CommitHash = newCommit.Sha,
                Author = who,
                Message = msg,
                CommittedAt = DateTime.UtcNow,
            });
        }

        /// <summary>
        /// Undoes the last project change by reverting to HEAD~1 (parent of current HEAD).
        /// Returns null if there is no parent commit to revert to.
        /// </summary>
        public async Task<GitCommitInfo?> UndoLastCommitAsync(string projectPath, string author)
        {
            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath) || !Repository.IsValid(projectPath))
                return null;

            string parentSha;
            using (var repo = new Repository(projectPath))
            {
                var head = repo.Head.Tip;
                if (head is null || !head.Parents.Any())
                    return null;

                parentSha = head.Parents.First().Sha;
            }

            return await RevertToCommitAsync(projectPath, parentSha, author).ConfigureAwait(false);
        }

        /// <summary>
        /// Fetches the parent project's repository and merges it into the fork's current branch
        /// (LibGit2Sharp's real 3-way merge — computes a common ancestor when the fork and parent
        /// share history, or a base-less merge otherwise). Never auto-resolves conflicts: if the
        /// merge leaves conflicted paths, this returns <see cref="GitMergeResult.HasConflicts"/> =
        /// true and does not commit — the caller must resolve and commit separately.
        /// </summary>
        public Task<GitMergeResult> SyncForkFromOriginAsync(string forkProjectPath, string parentProjectPath)
        {
            if (string.IsNullOrWhiteSpace(forkProjectPath) || !Directory.Exists(forkProjectPath))
                throw new DirectoryNotFoundException($"Fork project directory not found: {forkProjectPath}");
            if (string.IsNullOrWhiteSpace(parentProjectPath) || !Directory.Exists(parentProjectPath))
                throw new DirectoryNotFoundException($"Parent project directory not found: {parentProjectPath}");

            EnsureRepository(forkProjectPath);
            EnsureRepository(parentProjectPath);

            using (var parentCheck = new Repository(parentProjectPath))
            {
                if (parentCheck.Head.Tip is null)
                {
                    return Task.FromResult(new GitMergeResult
                    {
                        Success = false,
                        Message = "Parent project has no commits yet — nothing to sync.",
                    });
                }
            }

            using var repo = new Repository(forkProjectPath);
            var remote = repo.Network.Remotes[SyncRemoteName]
                         ?? repo.Network.Remotes.Add(SyncRemoteName, parentProjectPath);
            if (!string.Equals(remote.Url, parentProjectPath, StringComparison.OrdinalIgnoreCase))
                repo.Network.Remotes.Update(SyncRemoteName, r => r.Url = parentProjectPath);

            try
            {
                var refSpecs = remote.FetchRefSpecs.Select(s => s.Specification);
                Commands.Fetch(repo, SyncRemoteName, refSpecs, null, null);

                var remoteBranch = repo.Branches
                    .FirstOrDefault(b => b.IsRemote && b.RemoteName == SyncRemoteName);
                if (remoteBranch?.Tip is null)
                {
                    return Task.FromResult(new GitMergeResult
                    {
                        Success = false,
                        Message = "Fetched from parent but found no branch to merge.",
                    });
                }

                var signature = new Signature("PageToMovie", "noreply@pagetomovie.local", DateTimeOffset.UtcNow);
                var mergeResult = repo.Merge(remoteBranch.Tip, signature, new MergeOptions
                {
                    FileConflictStrategy = CheckoutFileConflictStrategy.Normal,
                    CommitOnSuccess = true,
                });

                if (mergeResult.Status == MergeStatus.Conflicts)
                {
                    var conflictCount = repo.Index.Conflicts.Count();
                    _logger.LogWarning(
                        "Sync-from-origin left {Count} conflicted path(s) in {Path}", conflictCount, forkProjectPath);
                    return Task.FromResult(new GitMergeResult
                    {
                        Success = false,
                        HasConflicts = true,
                        Message = $"{conflictCount} file(s) need manual conflict resolution before this can be committed.",
                    });
                }

                var headSha = repo.Head.Tip?.Sha ?? "";
                var message = mergeResult.Status switch
                {
                    MergeStatus.UpToDate => "Already up to date with origin.",
                    MergeStatus.FastForward => "Fast-forwarded to origin (no local changes to preserve).",
                    _ => "Synced latest changes from origin.",
                };
                _logger.LogInformation("Synced fork {Fork} from origin {Parent}: {Status}", forkProjectPath, parentProjectPath, mergeResult.Status);
                return Task.FromResult(new GitMergeResult
                {
                    Success = true,
                    HasConflicts = false,
                    CommitHash = headSha,
                    Message = message,
                });
            }
            finally
            {
                try { repo.Network.Remotes.Remove(SyncRemoteName); } catch { /* best effort cleanup */ }
            }
        }

        /// <summary>
        /// Push the project's HEAD to the configured Projects remote on branch
        /// <c>{DefaultBranchPrefix}{projectId}</c> (e.g. <c>proj/alice/Buster</c>).
        /// Does not require GitHub for local commit/merge — only for remote history.
        /// </summary>
        public Task<GitPushResult> PushProjectAsync(string projectPath, string projectId)
        {
            if (!_git.Enabled)
            {
                return Task.FromResult(new GitPushResult
                {
                    Success = false,
                    Message = "GitHub project backup is not enabled (PageToMovie:Git:Enabled).",
                });
            }

            var url = (_git.ProjectsRepoUrl ?? "").Trim();
            var token = (_git.Token ?? "").Trim();
            if (url.Length == 0 || token.Length == 0)
            {
                return Task.FromResult(new GitPushResult
                {
                    Success = false,
                    Message = "Git:ProjectsRepoUrl and Git:Token must be configured to push.",
                });
            }

            if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
                throw new DirectoryNotFoundException($"Project directory not found: {projectPath}");

            EnsureRepository(projectPath);
            var branch = BuildRemoteBranchName(projectId, _git.DefaultBranchPrefix);
            using var repo = new Repository(projectPath);
            if (repo.Head.Tip is null)
            {
                return Task.FromResult(new GitPushResult
                {
                    Success = false,
                    Message = "Nothing to push — create a commit first.",
                    Branch = branch,
                });
            }

            var remote = repo.Network.Remotes[GithubRemoteName]
                         ?? repo.Network.Remotes.Add(GithubRemoteName, url);
            if (!string.Equals(remote.Url, url, StringComparison.OrdinalIgnoreCase))
                repo.Network.Remotes.Update(GithubRemoteName, r => r.Url = url);

            // Push current HEAD tip to remote branch proj/{user}/{slug} without force.
            var pushRef = $"{repo.Head.Tip.Sha}:refs/heads/{branch}";
            var options = new PushOptions
            {
                CredentialsProvider = (_, _, _) => new UsernamePasswordCredentials
                {
                    Username = string.IsNullOrWhiteSpace(_git.TokenUsername) ? "x-access-token" : _git.TokenUsername,
                    Password = token,
                },
            };

            try
            {
                repo.Network.Push(remote, pushRef, options);
                var historyUrl = BuildGitHubHistoryUrl(url, branch);
                _logger.LogInformation(
                    "Pushed project {Id} to {Remote} branch {Branch} @ {Sha}",
                    projectId, url, branch, repo.Head.Tip.Sha);
                return Task.FromResult(new GitPushResult
                {
                    Success = true,
                    Message = "Pushed project package to remote (video excluded).",
                    Branch = branch,
                    HistoryUrl = historyUrl,
                    CommitHash = repo.Head.Tip.Sha,
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push failed for project {Id}", projectId);
                return Task.FromResult(new GitPushResult
                {
                    Success = false,
                    Message = ex.Message,
                    Branch = branch,
                    CommitHash = repo.Head.Tip?.Sha,
                });
            }
        }

        public static string BuildRemoteBranchName(string projectId, string? prefix = null)
        {
            var p = string.IsNullOrWhiteSpace(prefix) ? "proj/" : prefix.Trim();
            if (!p.EndsWith('/')) p += "/";
            var id = (projectId ?? "").Trim().Trim('/').Replace('\\', '/');
            // Branch names: allow slash for user/slug (Git supports hierarchical refs)
            var safe = new string(id.Select(c =>
                char.IsAsciiLetterOrDigit(c) || c is '/' or '_' or '-' or '.' ? c : '-').ToArray());
            while (safe.Contains("//", StringComparison.Ordinal))
                safe = safe.Replace("//", "/", StringComparison.Ordinal);
            return p + safe.Trim('/');
        }

        /// <summary>Best-effort commits URL for github.com remotes.</summary>
        public static string? BuildGitHubHistoryUrl(string repoUrl, string branch)
        {
            try
            {
                var u = repoUrl.Trim();
                if (u.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    u = u[..^4];
                if (u.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
                    u = "https://github.com/" + u["git@github.com:".Length..];
                if (!u.Contains("github.com", StringComparison.OrdinalIgnoreCase))
                    return null;
                return $"{u.TrimEnd('/')}/commits/{branch}";
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Public entry for create/fork — init repo + video gitignore.</summary>
        public static void EnsureRepositoryAt(string projectPath) => EnsureRepository(projectPath);

        /// <summary>
        /// Initializes a standalone Git repository at <paramref name="projectPath"/> if one doesn't
        /// already exist, with a .gitignore excluding video/audio binaries.
        /// <para>
        /// Caller responsibility: <paramref name="projectPath"/> must be a project's own directory
        /// under the workspace's <c>projects/</c> folder in a deployment where that folder is plain
        /// user data (Railway persistent volume), never a path that's already tracked inside a
        /// different Git working tree (e.g. this very app repo's own checked-in sample/demo
        /// projects) — nesting a repository inside an already-tracked directory produces a broken
        /// gitlink in the outer repo. See issue tracking this exact caveat before wiring automatic
        /// calls into the request pipeline.
        /// </para>
        /// </summary>
        private static void EnsureRepository(string projectPath)
        {
            if (!Repository.IsValid(projectPath))
                Repository.Init(projectPath);

            var gitignorePath = Path.Combine(projectPath, ".gitignore");
            if (!File.Exists(gitignorePath))
                File.WriteAllText(gitignorePath, string.Join("\n", IgnoredGlobs) + "\n");
        }

        private static string EmailFor(string author)
        {
            var slug = new string(author.Trim().ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray());
            return $"{slug}@pagetomovie.local";
        }
    }
}
