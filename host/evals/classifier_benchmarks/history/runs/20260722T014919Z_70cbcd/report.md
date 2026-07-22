# Benchmark run `20260722T014919Z_70cbcd`

- **UTC:** 2026-07-22 01:49:19Z
- **Project:** `The_Jungle_Book`
- **Models:** claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_grounded
- **Tasks:** ambient_sfx
- **Note:** cheap-model probe

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| ambient_sfx | `claude-haiku-4-5-20251001` | `v2_grounded` | 0 | mean_token_jaccard | 30 | 0.742 | 0.839 | **AI** | 5309ms | curated |
| ambient_sfx | `claude-fable-5` | `v2_grounded` | 0 | mean_token_jaccard | 30 | 0.742 | 0.864 | **AI** | 28921ms | curated |

Per-sample details: `details.json` in this run folder.
