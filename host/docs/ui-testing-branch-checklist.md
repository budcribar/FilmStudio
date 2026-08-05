# UITestingBranch — remaining readiness tests

- [x] **1. Finish/seed stage2 under fakes → Film + Generate**
  - Empty project config blocked Stage2 (“no model selected”).
  - Seed: `PUT .../config` with catalog models, then `POST /api/jobs/stage2`.
  - Verified: `stage2_ready=true`, 5 scenes / 12 clips.

- [x] **2. Fake cast voice/image locks → Generate gate**
  - Characters: `Character_Narrator`, `Character_Officer`.
  - `POST .../characters/{key}/voice` with `voiceProfile`.
  - `POST .../characters/{key}/upload-ref` multipart PNG → locked preferred look.
  - `POST /api/jobs/gen-scene` accepted with `requireLockedCharacters: true` (cast gate passed).

- [x] **3. S5 double-submit when Generate enables**
  - Under fakes, scene gen still required **`XAI_API_KEY` env present** (even a dummy value) — UseFakes does not skip the key presence check.
  - With `XAI_API_KEY=test-fake-key-not-real` + fakes: gen-scene **completed** (“done … clip(s)”).
  - Concurrent double `gen-scene` for same scene: **both accepted** by API (queued/ran); no 409. UI should still disable Generate while `JobRunning` (not fully asserted in browser this pass).

- [x] **4. Longer wait for S3 strip on Home**
  - After activate + readiness, wait up to ~8s for `[data-testid="studio-step-film"]`.
  - **PASS:** Film strip found; `href=scenes`, not disabled once stage2 ready.
  - Estimate strip also present.

- [x] **5. Cost Agree + length after sign-off with UI activate**
  - **PASS:** length `input[type=number]` visible; set to 2.
  - **PASS:** `[data-testid="cost-agree-continue"]` visible + enabled; click → `/scenes`.
  - API note: `PUT .../film-runtime` needs JSON body `{"targetMinutes": N}` with **N ≥ 2** in validation message (2–180).

## Fakes caveats discovered

| Issue | Detail |
|-------|--------|
| Stage2 models | Project config must set `model_name` + `planning_model_name` |
| Scene gen key check | `XAI_API_KEY` must be non-empty even when `UseFakes=true` |
| Concurrent gen | API allows multiple gen-scene jobs; rely on UI busy disable for double-submit UX |

## Artifacts

- `artifacts/ui-audit/items2to5-report.md`
- `artifacts/ui-audit/item4-home-strip.png`, `item5-cost.png`
