# Benchmark run `20260722T024652Z_69384f`

- **UTC:** 2026-07-22 02:46:52Z
- **Project:** `The_Jungle_Book`
- **Models:** grok-4.5, claude-sonnet-5, claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_grounded
- **Tasks:** extend_cut
- **Note:** clarify group-scene new-voice rule after s63_b35 verification

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| extend_cut | `grok-4.5` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 1.000 | **AI** | 23847ms | curated |
| extend_cut | `claude-sonnet-5` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 0.958 | **AI** | 18276ms | curated |
| extend_cut | `claude-haiku-4-5-20251001` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 0.958 | **AI** | 2796ms | curated |
| extend_cut | `claude-fable-5` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 1.000 | **AI** | 14871ms | curated |

Per-sample details: `details.json` in this run folder.
