# Benchmark run `20260722T015455Z_23bce2`

- **UTC:** 2026-07-22 01:54:55Z
- **Project:** `The_Jungle_Book`
- **Models:** claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_grounded
- **Tasks:** extend_cut
- **Note:** cheap-model probe

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| extend_cut | `claude-haiku-4-5-20251001` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 0.958 | **AI** | 3017ms | curated |
| extend_cut | `claude-fable-5` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 0.958 | **AI** | 14634ms | curated |

Per-sample details: `details.json` in this run folder.
