# Adaptation / Stage‑1 remaining checklist

Last updated: 2026-08-03

## Done (recent)

- [x] **Stage commits** (`ptm:stage=…` including screenplay_created after report/manifest)
- [x] **Client stitch → film_build EDL** (Review WIP play + share/export; sha256 + segments)
- [x] **film_build.v1** + studio.sha256 (`assets/movie_wip.film.json`, API + WIP upload hook)
- [x] **Stage‑1 convert manifest** (`source/stage1_convert_manifest.json` — prompt/adaptation/runtime/model)
- [x] **ADAPTATION_REPORT** parse/store (`SplitAdaptationTrailers`, `source/adaptation_report.json`)

- [x] Default Stage‑1 runtime target = **unlimited** (prompt + FilmRuntime mode; retarget only when user sets minutes)

- [x] Adaptation module Phases 0–5 + Engine mapping-only façade
- [x] Stage‑1 via `AdaptationService` only + golden fixtures
- [x] xAI file_id + Responses multi-turn + admin book cache
- [x] `cast_kind: group` from extract + normalize
- [x] Groups skip portrait pin; **hidden on Characters UI**
- [x] **Fake-chat `AdaptationService.ConvertAsync` tests** (`AdaptationFakeChatTests`)
- [x] **Deterministic speaker ⊆ cast** (`SpeakersMissingFromCast` + post-extract report)

## Open — next

### Adaptation hardening
- [x] Dirty Adaptation-source benchmark gate (`TryGetCommittedStage1Surface`, `--allow-dirty`)
- [x] Membership / description scores on leaderboard summary (cast package section)

### Mary quality
- [ ] Book-to-fountain prompt tighten (dialogue invention, scene economy, natural runtime)
- [ ] Baseline → treatment benchmark run before/after prompt PR

### Runtime / length (product)
- [ ] UX: show natural length after import; optional retarget end-to-end
- [ ] Benchmark dual mode (natural vs reduced) for longer books

### Later
- [ ] Learning / critic loop (FFmpeg clip metadata, hash, post-YouTube analysis)
- [ ] Stage‑2 / clip consistency judge

## Quick pointers

| Item | Code |
|------|------|
| Fake chat Stage‑1 | `host/PageToMovie.Tests/AdaptationFakeChatTests.cs` |
| Speaker ⊆ cast | `CastPackageCrossCheck.SpeakersMissingFromCast` |
| Written on extract | `artifacts/model_operations/cast_package_membership.json` |
| Extract result fields | `MembershipOk`, `SpeakersMissingFromCast`, `MembershipScore` |
