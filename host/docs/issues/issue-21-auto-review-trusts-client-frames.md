# Issue 21 — Auto-review trusts client-submitted frames without verifying the clip hash

| Field | Value |
|-------|-------|
| Severity | suggestion |
| Status | open |
| Branch | `fix/issue-21-auto-review-trusts-client-frames` |
| Related files | `host/PageToMovie.Engine/ClipAutoReviewService.cs:581` (`MaterializeClientFrames`), `:94-124` (`RunAsync`) |

## Problem

Since the server no longer has the clip file on disk to sample frames from itself, `RunAsync` now requires the browser to upload JPEG frames (base64) and throws if none are provided (no server-ffmpeg fallback). `MaterializeClientFrames` writes whatever the client sends as `"CURRENT_CLIP"` / `"PREVIOUS_CLIP_TAIL"` frames and feeds them straight to the vision model — there is no check that the submitted frames actually came from the clip whose scene/clip number is in the request (e.g. by cross-checking against `MediaRegistryService`'s registered SHA-256 for that clip). Filenames/labels are safely constrained (no path traversal), so this isn't a file-system risk — it's a trust-boundary one: an operator (or a direct API call bypassing the browser) could get an AI Pass on frames that don't match the real clip.

## Suggested fix

Low priority unless auto-review Pass/Fail feeds something beyond the operator's own workflow (e.g. eligibility for the public demo gallery publish gate added around the same time — see `DemoCatalogService`). If it does, consider requiring the client to also report the clip's registered SHA-256 alongside the frames so the server can reject a mismatch. If auto-review stays a pure operator QA aid, this is fine as-is.

## Notes

Tracked from the ffmpeg-migration code review (2026-07-25).
