# Mary cast package demo (offline)

Illustrative production cast package for **Mary Had a Little Lamb**.

Not a live model run — used to exercise `CastPackageCrossCheck` until API keys are available
for a full Stage 1 → cast extract in this environment.

## Production adaptation package (canonical)

| Artifact | Role |
|---|---|
| `source/book_full.txt` | Prepared book |
| `source/screenplay.fountain` | Stage 1 screenplay |
| `source/vision_meta.json` | Visual medium (when present) |
| `source/cast_seeds.json` | Cast package (this layer) |
| `blueprint.clips.grok.json` | Stage 2 (not in this demo) |

Stage 1 benchmark scores without `cast_seeds.json` do **not** evaluate this layer.
