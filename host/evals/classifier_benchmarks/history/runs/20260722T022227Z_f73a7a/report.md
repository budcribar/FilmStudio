# Benchmark run `20260722T022227Z_f73a7a`

- **UTC:** 2026-07-22 02:22:27Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5, claude-sonnet-5, claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_grounded
- **Tasks:** ambient_sfx
- **Note:** post Strip() fix - full model matrix

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| ambient_sfx | `grok-4.5` | `v2_grounded` | 0 | mean_token_jaccard | 30 | 0.742 | 0.872 | **AI** | 37768ms | curated |
| ambient_sfx | `claude-sonnet-5` | `v2_grounded` | 0 | mean_token_jaccard | 30 | 0.742 | 0.867 | **AI** | 17391ms | curated |
| ambient_sfx | `claude-haiku-4-5-20251001` | `v2_grounded` | 0 | mean_token_jaccard | 30 | 0.742 | 0.783 | **AI** | 6260ms | curated |
| ambient_sfx | `claude-fable-5` | `v2_grounded` | 0 | mean_token_jaccard | 30 | 0.742 | 0.864 | **AI** | 23054ms | curated |

Per-sample details: `details.json` in this run folder.
