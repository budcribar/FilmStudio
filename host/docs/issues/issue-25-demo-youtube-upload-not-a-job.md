# Issue 25 — Demo → YouTube upload is fire-and-forget, not a trackable job

| Field | Value |
|-------|-------|
| Severity | suggestion |
| Status | open |
| Branch | `fix/issue-25-demo-youtube-upload-not-a-job` |
| Related files | `host/PageToMovie.Engine/DemoYouTubePublisherService.cs`; `host/PageToMovie.Api/Program.cs` (`/api/demos` POST, `/api/admin/demos/{demoId}/review`) |

## Problem

When a demo is auto-approved (trusted SHA) or admin-approved, the YouTube upload is kicked off via
a bare `_ = Task.Run(...)` — it isn't tracked through `FilmJobService`/SignalR like every other
long-running operation in this app (gen-scene, stage1/2, WIP YouTube upload). There's no progress,
no cancellation, and no live status push; the only way to see upload progress is polling
`DemoEntry.YoutubeUploadStatus`/`YoutubeUploadError` via the admin demos list. If the process
restarts mid-upload, the demo is silently stuck at `"uploading"` forever (no retry, no timeout
recovery) — the file is still on disk so nothing is lost, but the entry needs a manual re-review
status flip to retry.

## Suggested fix

Add a `demo_youtube_upload` job kind to `FilmJobService`/`JobStore` (mirroring `RunYouTubeUploadAsync`
for the WIP movie) so upload progress shows in the existing job UI, and add a startup sweep that
resets any demo stuck in `"uploading"` back to `"failed"` (or retries) after a restart.

## Notes

Tracked from the same pass that wired Phase 3 (2026-07-26) — a deliberate scope cut to land the
core "demos migrate to YouTube automatically" behavior without building a second parallel job
system in the same change.
