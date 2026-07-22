# Benchmark run `20260722T022734Z_ad3b25`

- **UTC:** 2026-07-22 02:27:34Z
- **Project:** `Buster2`
- **Models:** grok-4.5, claude-sonnet-5, claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_picture_book
- **Tasks:** plate_rank
- **Note:** post Strip() fix - full model matrix

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| plate_rank | `grok-4.5` | `v2_picture_book` | 0 | mean_recall_at_3_capped | 2 | 0.500 | 1.000 | **AI** | 7504ms | curated |
| plate_rank | `claude-sonnet-5` | `v2_picture_book` | 0 | mean_recall_at_3_capped | 2 | 0.500 | 1.000 | **AI** | 3386ms | curated |
| plate_rank | `claude-haiku-4-5-20251001` | `v2_picture_book` | 0 | mean_recall_at_3_capped | 2 | 0.500 | 1.000 | **AI** | 10570ms | curated |
| plate_rank | `claude-fable-5` | `v2_picture_book` | 0 | mean_recall_at_3_capped | 2 | 0.500 | 1.000 | **AI** | 8872ms | curated |

Per-sample details: `details.json` in this run folder.
