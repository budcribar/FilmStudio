# Benchmark run `20260721T222801Z_acdf1d`

- **UTC:** 2026-07-21 22:28:01Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5
- **Prompts:** v1_product, v2_grounded
- **Tasks:** extend_cut
- **Note:** curated extend gold; v1 vs v2

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| extend_cut | `grok-4.5` | `v1_product` | 0 | accuracy | 24 | 0.917 | 1.000 | **AI** | 35182ms | curated |
| extend_cut | `grok-4.5` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 0.958 | **AI** | 17833ms | curated |

## Compare — `extend_cut` / `grok-4.5`

| Prompt | Temp | AI score | vs best | Winner vs baseline |
|--------|------|----------|---------|--------------------|
| `v1_product` | 0 | 1.000 | best | AI |
| `v2_grounded` | 0 | 0.958 | -0.042 | AI |

Per-sample details: `details.json` in this run folder.
