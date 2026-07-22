# Benchmark run `20260722T015106Z_55356a`

- **UTC:** 2026-07-22 01:51:06Z
- **Project:** `The_Jungle_Book`
- **Models:** claude-haiku-4-5-20251001, claude-fable-5
- **Prompts:** v2_product
- **Tasks:** silent_beat_action
- **Note:** cheap-model probe

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| silent_beat_action | `claude-haiku-4-5-20251001` | `v2_product` | 0 | accuracy | 147 | 0.469 | 0.701 | **AI** | 62693ms | curated |
| silent_beat_action | `claude-fable-5` | `v2_product` | 0 | accuracy | 147 | 0.469 | 0.864 | **AI** | 163581ms | curated |

Per-sample details: `details.json` in this run folder.
