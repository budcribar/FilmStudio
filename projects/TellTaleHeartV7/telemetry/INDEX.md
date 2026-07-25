# Project telemetry

| File | Purpose |
|------|---------|
| `cost_ledger.json` | Snapshot of cost events from `pipeline_state` (list rates) |
| `models.json` | Resolved model/options snapshot at last artifact-index rebuild |
| `api_calls.jsonl` | Append-only: one JSON line per live API call (full prompts) |
| `media_ops.jsonl` | Optional local media-op log (rarely written; stitch/trim are browser-side) |
| `ffmpeg.jsonl` | **Legacy** name only if present — superseded by `media_ops.jsonl` |

`api_calls` is written during jobs (project scope).  
Stitch / silence-trim / auto-review frames run in the browser (ffmpeg.wasm).  
Rebuild this folder’s snapshots via `POST /api/projects/{id}/artifacts/index`.
