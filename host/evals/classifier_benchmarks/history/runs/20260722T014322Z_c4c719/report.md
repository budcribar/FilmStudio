# Benchmark run `20260722T014322Z_c4c719`

- **UTC:** 2026-07-22 01:43:22Z
- **Project:** `The_Jungle_Book`
- **Models:** claude-sonnet-5
- **Prompts:** v2_grounded, v3_speaker_cue
- **Tasks:** extend_cut
- **Note:** v3 speaker-cue prompt fix attempt

| Task | Model | Prompt | Temp | Metric | n | Baseline | AI | Winner | Latency | Gold |
|------|-------|--------|------|--------|---|----------|----|--------|---------|------|
| extend_cut | `claude-sonnet-5` | `v2_grounded` | 0 | accuracy | 24 | 0.917 | 0.000 | **baseline** | 24899ms | curated |
| extend_cut | `claude-sonnet-5` | `v3_speaker_cue` | 0 | accuracy | 24 | 0.917 | 0.917 | **tie** | 21951ms | curated |

## Compare — `extend_cut` / `claude-sonnet-5`

| Prompt | Temp | AI score | vs best | Winner vs baseline |
|--------|------|----------|---------|--------------------|
| `v3_speaker_cue` | 0 | 0.917 | best | tie |
| `v2_grounded` | 0 | 0.000 | -0.917 | baseline |

Per-sample details: `details.json` in this run folder.
