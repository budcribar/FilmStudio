# Benchmark run `20260721T213532Z_254377`

- **UTC:** 2026-07-21 21:35:32Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5
- **Prompts:** v1_product, v2_grounded
- **Tasks:** onscreen_cast
- **Note:** curated cast gold; v1 vs v2_grounded

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| onscreen_cast | `grok-4.5` | `v1_product` | 0 | mean_set_f1 | 20 | 0.812 | 0.910 | **AI** | 28558ms | curated |
| onscreen_cast | `grok-4.5` | `v2_grounded` | 0 | mean_set_f1 | 20 | 0.812 | 0.975 | **AI** | 44913ms | curated |

## Compare — `onscreen_cast` / `grok-4.5`

| Prompt | Temp | AI score | vs best | Winner vs baseline |
|--------|------|----------|---------|--------------------|
| `v2_grounded` | 0 | 0.975 | best | AI |
| `v1_product` | 0 | 0.910 | -0.065 | AI |

Per-sample details: `details.json` in this run folder.
