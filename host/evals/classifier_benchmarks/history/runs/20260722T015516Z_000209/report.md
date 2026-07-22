# Benchmark run `20260722T015516Z_000209`

- **UTC:** 2026-07-22 01:55:16Z
- **Project:** `Buster2`
- **Models:** claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_picture_book
- **Tasks:** plate_rank
- **Note:** cheap-model probe

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| plate_rank | `claude-haiku-4-5-20251001` | `v2_picture_book` | 0 | mean_recall_at_3_capped | 2 | 0.500 | 0.000 | **baseline** | 6394ms | curated |
| plate_rank | `claude-fable-5` | `v2_picture_book` | 0 | mean_recall_at_3_capped | 2 | 0.500 | 1.000 | **AI** | 9194ms | curated |

Per-sample details: `details.json` in this run folder.
