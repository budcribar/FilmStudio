using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// Real Git-backed project state: auto-commits (LibGit2Sharp) and a 3-way sync-from-origin
    /// merge for a project directory. Each project directory becomes its own standalone Git
    /// repository (not a submodule of the app's own repo) — see the doc comment on
    /// <see cref="EnsureRepository"/> for why the caller must never point this at a project
    /// directory that's itself already tracked inside another Git working tree.
    /// </summary>
    public class ProjectGitRepositoryService
    {
        private readonly ILogger<ProjectGitRepositoryService> _logger;

        private const string SyncRemoteName = "sync-origin";

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

        public ProjectGitRepositoryService(ILogger<ProjectGitRepositoryService> logger)
        {
            _logger = logger;
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
