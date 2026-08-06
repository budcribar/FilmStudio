# PageToMovie backlog

Single prioritized backlog. Older checklist docs were merged here and removed.

Last consolidated: 2026-08-06.

---

## Done recently (do not re-open)

- ActiveProjectState load lifecycle (`IsLoading` / `IsReady` / `EnsureLoadedAsync`)
- EnsureLoaded on Cost, Characters, Scenes; MainLayout loading cursor
- Navigate-away P0/P1: page dispose / CTS, SoftReload guards, single-flight EnsureLoaded
- Scenes SoftReload body restored (job list + OpenSceneAsync)
- Cost awaits EnsureLoaded in OnParametersSetAsync + dispose
- Catalog fail-fast (no invented model capabilities / soft defaults)
- Cost from model JSON only (no hard-coded cost constants)
- Provider heuristic audit → prefer catalog capabilities
- Lab mode direction + admin-only visibility (partial)
- UI audit sequences S1–S2 exercised; many S3–S5 notes captured
- Mary4 UI batch (Agree&Continue, MinMinutes, character @key, etc.)

---

## P0 — Correctness / safety (do next)

1. **[Test suite] ClipEdit / ShotPlan test compile**
   - `ModelBoundsTests` / `ClipEditRequestTests` (and related) conflict with `Adaptation.Models` (`DialogueDelivery`, etc.).
   - Either align tests with real types or remove until ClipEdit is finished so `dotnet test` is green.

2. **[Navigate-away P2] EngineApiClient CancellationToken**
   - Page CTS exists, but many HTTP helpers still ignore tokens.
   - Thread `CancellationToken` through status / soft-reload / character image / job-list calls used by studio pages.

3. **[Cost / Agree UX] Lab vs production pricing**
   - Surface lab / missing pricing clearly so users never treat $0 lab as free production.
   - Only admins enable lab mode and see lab-only models (finish any remaining UI gates).

4. **[Catalog] Unknown model ID fail-fast everywhere**
   - Confirm no remaining code paths invent defaults for unknown model IDs.

---

## P1 — Product / pipeline

5. **[Pipeline] Stage-1 = full-book Fountain; length later**
   - Do not bake target runtime into first convert.
   - Separate later stage: compress / retarget length (mini-series vs short) from complete Fountain.

6. **[Adaptation] Post–Mary $ run**
   - Evaluate `ADAPTATION_REPORT` and other prompts after live Mary run.
   - Remaining handoff items from adaptation extract (see historical notes in git if needed).

7. **[Mary / live] Image + length smoke**
   - Live Mary image and length smoke tests still open.

8. **[Scenes] Embellishment stage**
   - Product stage not implemented (listed as future).

9. **[Look & medium]**
   - Visual medium exists; “Look & medium” as full stage still future work.

10. **[Cost split]**
    - Remaining product cost-split UX / accounting if not fully closed.

11. **[Characters] ChatEngine rename audit**
    - Confirm no remaining calls intended for `IChatCompletionEngine` still use `Engine` after inject rename.

---

## P1 — Models catalog / admin

12. **[Admin] Models catalog UI**
    - Add / edit / delete / review model.
    - On modify/review/add: verify required parameters; set “last reviewed” date.

13. **[Admin] Scan for updates**
    - Button to search vendor docs / endpoints for changed params and new models.
    - Color code: green same, yellow not found, red different; accept to apply.
    - Nested field accept UX (e.g. `videoCostPerSecondByResolution.720p`) without raw JSON.

14. **[Catalog] Self-test on deploy / change**
    - Every model: required values present and valid; fail fast before movie generation.
    - Lab-mode exemption for incomplete models when testing.

15. **[Catalog] Parameter completeness**
    - max reference image dimension (where required)
    - max extension seconds / supportsVideoContinue consistency (≤0 = no extend)
    - Audio duration limits where applicable
    - Cost rows with source comment + last-reviewed date only in JSON

16. **[Fakes] Capability matrix**
    - Multiple fake models with distinct capabilities; UI respects combinations.
    - No provider-ID heuristic where capability belongs on the model.

---

## P2 — UI polish / guards

17. **[Sequences] Remaining S3–S5 / button-state gaps**
    - Explicit sequence testing still thin in places (gen-scene, Agree, length after nav).
    - Input range validation / disabled states where audit noted gaps.

18. **[Optional] Server 409 on duplicate gen-scene**
    - UI busy-disable is primary; server 409 as belt-and-suspenders.

19. **[ActiveProjectState] Page-local Changed handlers**
    - Ensure no page assumes mount after leave beyond current dispose guards.

20. **[Watch] ObjectDisposedException logs**
    - After rapid Cost ↔ Characters ↔ Scenes navigation.

---

## P3 — Experiments / non-blocking

21. **[Grok loop / optimus] Long-dialogue video prototype**
    - Branch experiment; de-dup / silence mapping; not production path.

22. **[Docs] Prompt token resolution**
    - All `{{tokens}}` via AdaptationPromptTokens; leftovers throw (maintain discipline).

23. **[Infra] Sandbox / agent .NET bootstrap**
    - `ensure-dotnet.sh` / AGENTS rules; keep working for agent image.

---

## Priority order (execution)

| Order | Item |
|------:|------|
| 1 | Test suite ClipEdit compile (green `dotnet test`) |
| 2 | Cost/Agree lab vs production + admin-only lab models |
| 3 | Catalog fail-fast / self-test gaps |
| 4 | EngineApiClient CancellationToken wiring |
| 5 | Admin models catalog UI + review date |
| 6 | Scan-for-updates (P0/P1 of scan plan) |
| 7 | Capability fakes matrix |
| 8 | Pipeline: full Fountain then length stage |
| 9 | Live Mary image/length smoke |
| 10 | Scene Embellishment / Look & medium stages |
| 11 | Remaining UI sequence guards |
| 12 | Optional gen-scene 409 |
| 13 | Experiments (Grok loop) as time allows |

---

## Sources merged into this file

- `mary4-ui-checklist.md`
- `adaptation-remaining-checklist.md`
- `ui-fix-order-checklist.md`
- `ui-testing-branch-checklist.md`
- `ui-audit/sequence-guards-checklist.md`
- `ui-audit/capability-fakes-plan.md`
- Session: navigate-away P0–P2, ActiveProjectState, SoftReload, Cost EnsureLoaded
- Project memory: pipeline Stage-1 Fountain, Mary remaining, catalog/lab direction
