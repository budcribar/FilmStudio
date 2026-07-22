# Benchmark run `20260722T022629Z_dab9ab`

- **UTC:** 2026-07-22 02:26:29Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5, claude-sonnet-5, claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_grounded
- **Tasks:** extend_cut
- **Note:** post Strip() fix - full model matrix

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| extend_cut | `grok-4.5` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 0.958 | **AI** | 21337ms | curated |
| extend_cut | `claude-sonnet-5` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 0.958 | **AI** | 23908ms | curated |
| extend_cut | `claude-haiku-4-5-20251001` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 0.958 | **AI** | 2857ms | curated |
| extend_cut | `claude-fable-5` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 0.958 | **AI** | 13624ms | curated |

Per-sample details: `details.json` in this run folder.
