# Benchmark run `20260722T021725Z_46e8d2`

- **UTC:** 2026-07-22 02:17:25Z
- **Project:** `Buster2`
- **Models:** claude-haiku-4-5-20251001
- **Prompts:** v2_picture_book
- **Tasks:** plate_rank
- **Note:** verify Strip() fence-prose fix

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| plate_rank | `claude-haiku-4-5-20251001` | `v2_picture_book` | 0 | mean_recall_at_3_capped | 2 | 0.500 | 1.000 | **AI** | 6568ms | curated |

Per-sample details: `details.json` in this run folder.
