# Benchmark run `20260722T022502Z_146ee3`

- **UTC:** 2026-07-22 02:25:02Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5, claude-sonnet-5, claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_grounded
- **Tasks:** onscreen_cast
- **Note:** post Strip() fix - full model matrix

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| onscreen_cast | `grok-4.5` | `v2_grounded` | 0 | mean_set_f1 | 20 | 0.812 | 0.975 | **AI** | 35742ms | curated |
| onscreen_cast | `claude-sonnet-5` | `v2_grounded` | 0 | mean_set_f1 | 20 | 0.812 | 0.950 | **AI** | 18644ms | curated |
| onscreen_cast | `claude-haiku-4-5-20251001` | `v2_grounded` | 0 | mean_set_f1 | 20 | 0.812 | 0.910 | **AI** | 5547ms | curated |
| onscreen_cast | `claude-fable-5` | `v2_grounded` | 0 | mean_set_f1 | 20 | 0.812 | 0.990 | **AI** | 24314ms | curated |

Per-sample details: `details.json` in this run folder.
