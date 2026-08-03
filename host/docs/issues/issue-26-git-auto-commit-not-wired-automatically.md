# Issue 26 — Per-project Git auto-commit is real but not wired into the background save path

| Field | Value |
|-------|-------|
| Severity | suggestion |
| Status | partially done |
| Branch | `fix/issue-26-git-auto-commit-not-wired-automatically` |
| Related files | `host/PageToMovie.Engine/ProjectGitRepositoryService.cs` |

## Problem

`ProjectGitRepositoryService.CommitProjectStateAsync`/`SyncForkFromOriginAsync` are now a real,
tested LibGit2Sharp implementation (`Repository.Init`, real staged commits, real 3-way merge with
genuine conflict detection — see `ProjectGitRepositoryServiceTests`), reachable via
`POST /api/projects/{id}/commit` and `/sync-origin` (owner/admin gated). But the plan's vision —
"every time a user updates a screenplay/blueprint, PageToMovie automatically creates a Git commit"
— is deliberately **not** wired into the background save path yet.

Reason: `EnsureRepository` calls `Repository.Init(projectPath)` on the project's own directory
(`{workspaceRoot}/projects/{id}`). In this repo's current *development* layout, sample/demo
projects (`projects/Buster/`, `projects/TellTaleHeartV7/`, etc.) are themselves committed inside
the main PageToMovie app repo. Auto-committing on every clip build would `git init` a **nested**
repository inside an already-tracked directory of the outer repo — a broken gitlink waiting to
happen the first time anyone runs `git add`/`git status` at the outer level against a real sample
project locally. In the intended *production* layout (Railway persistent volume, `projects/` is
plain user data with no outer `.git` above it) this isn't a problem at all — but the service
shouldn't assume that's always true without a check.

## Suggested fix

Before wiring an automatic hook (e.g. into `FilmJobService`'s per-clip prompt-archive call, the
same point Phase 4's history archiving hooks into): have `EnsureRepository` (or its caller) detect
whether `projectPath` is already inside another Git working tree (walk up looking for a `.git`
above `workspaceRoot`, or check `git rev-parse --is-inside-work-tree` semantics) and skip/warn
instead of nesting. Separately, decide whether local dev should keep sample projects inside the
app repo at all (the plan's own vision is a dedicated `PageToMovie-Projects` repo, distinct from
the app code) — that would remove the conflict at the root rather than working around it.

## Notes

Tracked from the same pass that implemented Phase 5 for real (2026-07-26). The manual, gated API
endpoints are safe today (a developer would have to deliberately call them against a repo-tracked
sample project to hit this); only *automatic* background wiring needs the fix above first.
