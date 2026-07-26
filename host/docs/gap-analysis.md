# Plan vs. Reality — Gap Analysis

## Legend
- ✅ **Done** — Fully implemented as specified
- 🟡 **Partial** — Skeleton/stub exists but key parts missing
- ❌ **Not Done** — Not built at all

---

## Item 1 — Media-Aware Contribution PRs & 2-Tier CDN Fallback

**Plan:** `ProjectContributionService.cs` handles PR payload (JSON diff + SHA-256 + CDN URL).
Window 1 (<24h): direct AI CDN download. Window 2 (>24h): server proxy fallback, auto-purged.

**Reality:** ❌ **Not done.**
- `ProjectContributionService.cs` does **not exist**.
- `ContributionReview.razor` does **not exist**.
- The 2-tier CDN fallback lifecycle is unimplemented.
- `MediaRegistryService.cs` stores SHA-256 hashes and paths correctly ✅, which is a necessary prerequisite — but nothing uses them to drive a PR/contribution flow.

---

## Item 2 — Sync Fork from Origin (`🔄 Sync from Origin`)

**Plan:** LibGit2Sharp rebase/merge engine. Clean = auto-update. Conflict = opens `ContributionReview.razor`.

**Reality:** 🟡 **Stub only.**
- `ProjectGitRepositoryService.cs` exists with `SyncForkFromOriginAsync()` method.
- The class comment says "using LibGit2Sharp semantics" but **LibGit2Sharp is NOT in the `.csproj`** — no NuGet package reference exists.
- The actual method body is a placeholder that logs a message and returns a `GitMergeResult`.
- `ContributionReview.razor` does not exist.
- There is no conflict resolution UI.

---

## Item 3 — Creator Profile Badges & Derived Stats

**Plan:** `CreatorProfileHeader.razor` with stats computed from SQLite. Three badges: Debut Director, Featured Filmmaker, Open Source Pioneer.

**Reality:** ❌ **Not done.**
- `CreatorProfileHeader.razor` does **not exist**.
- No badge computation logic anywhere in the engine.
- No `@username` profile page at all.
- `DemoCatalogService.cs` has upvote data that could feed stats, but nothing aggregates it into a creator profile.

---

## Item 4 — Fork Project Button & YouTube Comment Link on `/demo`

**Plan:** Prominent `🍴 Fork Project` and `💬 Comment on YouTube ↗` buttons on gallery cards.

**Reality:** 🟡 **Half done.**
- `💬 Comment on YouTube ↗` button: ✅ **Done** — wired into `Demo.razor` (line 150).
- `🍴 Fork Project` button: ❌ **Not done** — no fork button in `Demo.razor`, no fork action in API.

---

## Item 5 — Dedicated GitHub Organization (`github.com/PageToMovie`)

**Plan:** Create `PageToMovie` GitHub Org. Three repos. Dedicated access tokens for Railway isolated to Org.

**Reality:** ❌ **Not done** (infrastructure, not code — but still outstanding).
- This is a manual GitHub setup task, not purely code.
- The Railway deployment uses the personal `budcribar` repo, not a `PageToMovie` org.
- Dedicated org-scoped tokens for Railway backups: not configured.

---

## Item 6 — Modular Blazor Git UI NuGet Package (`PageToMovie.GitUi`)

**Plan:** Decoupled Razor Class Library with `<GitCommitTimeline />`, `<GitDiffViewer />`, `<GitThreeWayMergeResolver />`, `<GitBranchManager />` bound to generic interfaces.

**Reality:** ❌ **Not done.**
- No `PageToMovie.GitUi` project exists anywhere in the solution.
- No generic `IGitCommitProvider`, `IGitDiffModel`, `IGitMergeConflict` interfaces.
- The Git UI components in `ClipPromptCompareViewer.razor` are hardcoded in the main web project.

---

## Item 7 — Git LFS Strategy

**Plan:** Default = `.gitignore` ignores `assets/video/*.mp4`. Optional opt-in with `.gitattributes`.

**Reality:** 🟡 **Partially done.**
- The `.gitignore` likely ignores MP4s (standard project behavior), but this was not explicitly verified in code.
- No `.gitattributes` file for LFS opt-in was added to the project template scaffolding.
- No in-app setting exists for users to opt into LFS.

---

## Item 8 — YouTube Upload Metadata Form (`PublishDemoModal.razor`)

**Plan:** Modal collecting title, description, COPPA declaration, AI Synthetic Content disclosure, category, tags, privacy.

**Reality:** 🟡 **Was done, then removed.**
- `PublishDemoModal.razor` was built in Phase 3 commit `e848337`.
- It was then **deleted** in the latest pull (`d0b04ac`) and replaced by `DemoYouTubePublisherService.cs` which drives upload from the server side.
- The YouTube metadata fields (title, description, madeForKids, isAiSyntheticContent) are now passed via `Review.razor` → API → `DemoYouTubePublisherService`.
- The COPPA and AI disclosure **UI gate** is gone — it now happens silently server-side.

---

## Item 9 — Terms of Service & IP Licensing Agreement

**Plan:** `TermsAgreementModal.razor` gates users on login. Recorded in SQLite `terms_accepted_at`.

**Reality:** ✅ **Done.**
- `TermsAgreementModal.razor` exists and is wired into `MainLayout.razor`.
- `UserDatabaseService.AcceptTermsAsync()` and `HasAcceptedTermsAsync()` implemented.
- `terms_accepted_at` and `terms_version` columns added to SQLite.
- API endpoint `POST /api/users/terms/accept` wired in `Program.cs`.

---

## Item 10 — Cryptographic Provenance & Instant Auto-Approval

**Plan:** SHA-256 hashes logged for every AI-generated clip. On demo submission: if 100% of clip hashes match the audit ledger → instant auto-approve + YouTube upload, bypassing manual admin queue.

**Reality:** 🟡 **Hash infrastructure exists, auto-approval logic does not.**
- `MediaRegistryService.cs` ✅ stores SHA-256, size, scene, clip, kind per media object.
- `ClientMediaFolderService.cs` ✅ computes SHA-256 client-side and registers via API.
- The **auto-approval check** (verify 100% of clip hashes → skip admin queue) is **not implemented**.
- `DemoYouTubePublisherService.cs` uploads to YouTube but does not consult the hash ledger.
- Manual admin review queue (`/admin/demos`) is still the only approval path.

---

## Item 11 — YouTube API Auto-Upload & Video Replacement

**Plan:** Verified submission → auto-approve → `YouTubeUploadService.cs` streams to YouTube. On re-publish: upload V2, update gallery pointer, delete old video ID.

**Reality:** 🟡 **Partial — upload works, replacement and hash-gating don't.**
- `YouTubeUploadService.cs` was built, then **deleted** and replaced by `DemoYouTubePublisherService.cs` which uses `Google.Apis.YouTube.v3` (official SDK, better than the hand-rolled HTTP client).
- Upload to YouTube: ✅ **Done** via `DemoYouTubePublisherService.PublishAsync()`.
- After upload, deletes local MP4 and updates demo record: ✅ **Done**.
- Gallery streams via YouTube embed: ✅ **Done** in `Demo.razor`.
- **Video replacement (V2 upload + delete old ID):** ❌ Not implemented in the new service.
- **Hash-gated auto-approval trigger:** ❌ Not implemented (see Item 10).

---

## Item 12 — Git-Backed Server Engine (LibGit2Sharp)

**Plan:** Every project backed by a Git repo. Auto-commit on every save. 3-way merge on collaboration. Remote GitHub push for off-site backup.

**Reality:** 🟡 **Stub only.**
- `ProjectGitRepositoryService.cs` exists with `CommitProjectStateAsync()` and `SyncForkFromOriginAsync()`.
- **LibGit2Sharp is not installed** — missing from `.csproj`. The service is a no-op placeholder.
- No auto-commit hook on screenplay/blueprint/cast saves.
- No remote push to GitHub.
- No 3-way merge implementation.

---

## Item 13 — Admin Cross-User Export & Local Storage Handoff

**Plan:** Admin re-assigns project `ownerUserId`. Lightweight ZIP (<5 MB). Target user opens project → `ClientMediaFolderService.cs` binds their local hard drive folder.

**Reality:** 🟡 **Client folder binding done, admin handoff not done.**
- `ClientMediaFolderService.cs` ✅ fully implemented — folder picker, JS interop, auto-save on job completion, SHA-256 registration.
- `ProjectArchiveService.cs` ✅ exists (was in the file listing).
- Admin re-assign `ownerUserId` on export: ❌ Not verified as implemented.
- The "lightweight ZIP handoff" flow (admin generates ZIP → target user binds folder) is not confirmed end-to-end.

---

## Item 14 — Client MP4 Storage & Railway Disk Guard

**Plan:** MP4s live in browser (IndexedDB/OPFS/local folder). Server only caches transiently. `ServerMediaPruningService.cs` prunes after 48h or at 80% disk.

**Reality:** 🟡 **Partial — both paths exist but server is still primary.**
- `ClientMediaFolderService.cs` ✅ fully implemented with ffmpeg.wasm silence trimming and SHA-256.
- `pagetomovie-media.js` ✅ exists (File System Access API JS interop).
- `ServerMediaPruningService.cs` ✅ implemented and hardened.
- **However:** Server-side `assets/video/` is still the primary write path. The client folder is an opt-in secondary. Until a user connects a media folder, all clips accumulate on Railway disk.

---

## Item 15 — Invite-to-Fork Collaboration

**Plan:** Owner invites by `@handle` or email. User B accepts → lightweight fork created. Independent local work, zero file lock conflicts.

**Reality:** 🟡 **UI shell only, no actual fork logic.**
- `ProjectCollaboratorsModal.razor` ✅ exists with `@handle` search UI.
- `GET /api/users/search` ✅ wired in `Program.cs`.
- `POST /api/projects/{id}/invites` ✅ wired but returns a dummy token — no email sent, no fork created.
- The actual **fork creation** (copy screenplay + cast + blueprint to new project) is ❌ not implemented.
- The **invite email delivery** using the existing `ResendEmailSender` is ❌ not wired up.

---

## Summary Table

| # | Feature | Status | Key Gap |
|---|---|---|---|
| 1 | Media-Aware Contribution PRs | ❌ Not Done | `ProjectContributionService.cs` doesn't exist |
| 2 | Sync Fork from Origin | 🟡 Stub | LibGit2Sharp not installed; no real merge |
| 3 | Creator Profile Badges | ❌ Not Done | `CreatorProfileHeader.razor` doesn't exist |
| 4 | Fork & YouTube Comment Buttons | 🟡 Half | Comment ✅, Fork button ❌ |
| 5 | GitHub Org Strategy | ❌ Not Done | Manual infra task, not set up |
| 6 | `PageToMovie.GitUi` NuGet Package | ❌ Not Done | Project doesn't exist |
| 7 | Git LFS Strategy | 🟡 Partial | No `.gitattributes`, no in-app opt-in |
| 8 | YouTube Metadata Modal | 🟡 Changed | Modal removed, now server-side; COPPA gate gone from UI |
| 9 | Terms of Service Gate | ✅ Done | Fully wired |
| 10 | Cryptographic Auto-Approval | 🟡 Partial | Hash storage ✅, auto-approve logic ❌ |
| 11 | YouTube Auto-Upload & Replace | 🟡 Partial | Upload ✅, V2 replacement ❌, hash gate ❌ |
| 12 | Git-Backed Server Engine | 🟡 Stub | LibGit2Sharp not installed; all methods no-op |
| 13 | Admin Export & Handoff | 🟡 Partial | Client folder ✅, admin re-assign flow unclear |
| 14 | Client MP4 Storage (Primary) | 🟡 Partial | Infrastructure ✅, server still primary path |
| 15 | Invite-to-Fork Collaboration | 🟡 Stub | Search ✅, invite token ✅, fork creation ❌, email ❌ |
