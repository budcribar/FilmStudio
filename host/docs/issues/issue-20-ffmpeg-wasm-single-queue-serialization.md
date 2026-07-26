# Issue 20 — All browser media ops serialize through one ffmpeg.wasm queue

| Field | Value |
|-------|-------|
| Severity | suggestion |
| Status | open |
| Branch | `fix/issue-20-ffmpeg-wasm-single-queue-serialization` |
| Related files | `host/PageToMovie.Web/wwwroot/js/pagetomovie-ffmpeg.js:12-26` (`_opQueue` / `_runExclusiveAsync`) |

## Problem

Since the native-ffmpeg-on-server removal, stitching, duration probes, silence trim, and auto-review frame sampling all funnel through one `_runExclusiveAsync` queue on a single single-threaded ffmpeg.wasm instance in the browser tab (necessary: the wasm core can't run concurrent MEMFS ops). The old server-side `WorkerPools`/`MaxFfmpegInFlight` allowed several clips to process concurrently across worker processes; now a full-movie auto-review batch or a scene's worth of silence-trims runs one clip at a time, entirely dependent on the user keeping that tab open. For a 30-clip movie this is a meaningfully longer wall-clock time than the old path.

## Suggested fix

Not a bug to "fix" so much as a scaling ceiling to watch. If it becomes a real complaint: multi-threaded ffmpeg.wasm core exists but requires `Cross-Origin-Opener-Policy`/`Cross-Origin-Embedder-Policy` headers (cross-origin isolation), which has ripple effects on SignalR, media proxy tokens, and any cross-origin embeds (YouTube upload flow) — a bigger change than it sounds. Cheaper first step: make sure batch UI (auto-review batch, gen-scene save) shows clear per-clip progress so the serialization is visible, not just slow.

## Notes

Tracked from the ffmpeg-migration code review (2026-07-25). Architectural trade-off, deliberately accepted for the "no native ffmpeg on server" constraint.
