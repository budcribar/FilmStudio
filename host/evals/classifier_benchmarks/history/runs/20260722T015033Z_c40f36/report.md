# Benchmark run `20260722T015033Z_c40f36`

- **UTC:** 2026-07-22 01:50:33Z
- **Project:** `The_Jungle_Book`
- **Models:** claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_grounded
- **Tasks:** onscreen_cast
- **Note:** cheap-model probe

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| onscreen_cast | `claude-haiku-4-5-20251001` | `v2_grounded` | 0 | mean_set_f1 | 20 | 0.812 | 0.910 | **AI** | 4877ms | curated |
| onscreen_cast | `claude-fable-5` | `v2_grounded` | 0 | mean_set_f1 | 20 | 0.812 | 0.990 | **AI** | 24883ms | curated |

Per-sample details: `details.json` in this run folder.
