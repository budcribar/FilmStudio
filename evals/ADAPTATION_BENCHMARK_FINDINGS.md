# Adaptation Benchmark Findings and Improvement Protocol

> Reliability update (2026-08-02): the pre-paid-run refactor is complete. The full offline solution suite passes 1,185 tests, ScreenplayBenchmark passes all 11 self-checks, pronunciation handling is data-driven, dialogue comparison is immutable and auditable, and model-operation replay/manifests are available. Treat the next scored execution as a new baseline generation; do not compare cache-derived scores without the complete Fountain plus vision-metadata package and behavior versions.

Last reviewed: 2026-08-02

This document summarizes the current screenplay-adaptation and on-screen-cast benchmark results, records the main failure patterns, and defines a repeatable procedure for improving both product quality and benchmark reproducibility.

The two benchmark families are separate:

- `evals/` contains the screenplay adaptation benchmark, peer judgments, deterministic audits, cached screenplays, and history.
- `host/evals/classifier_benchmarks/` contains classifier benchmarks. Its `onscreen_cast` task uses mean set F1.

Do not combine their scores into one number.

## Work completed to date

Status notation:

- [x] Verified in committed code or persisted benchmark artifacts.
- [~] Partially implemented; useful infrastructure exists, but the full reproducibility requirement is not yet satisfied.
- [ ] Not yet completed.

### Benchmark provenance and configuration

- [x] **Benchmarks require a committed screenplay prompt revision.** The main benchmark and adaptation-session pilot refuse to start when `prompts/book_to_fountain.txt` has uncommitted changes. New runs record the short Git revision that last changed the prompt. Implemented in `5a4eef6c` and extended to the adaptation-session pilot in `32d9e369`.
- [x] **Legacy prompt provenance is backfilled.** Older runs without a prompt revision are mapped to the most recent prompt commit preceding their timestamp and marked with `PromptVersionInferred = true`, so inferred history remains distinguishable from directly recorded provenance. Implemented in `a37a36d1`.
- [x] **Generation and judge temperatures are independent.** `--temperature` controls generation, `--judge-temperature` controls judging, and judge temperature defaults to zero. Both values are persisted in benchmark history. Implemented in `5ce12f7f` and expanded in `732f2ddd`.
- [x] **Reasoning effort is persisted and surfaced.** Runs at different reasoning-effort settings remain distinguishable rather than being silently blended.
- [x] **Model metadata is catalog-driven.** The adaptation-session pilot validates that its author and judge models are enabled, compatible chat models from `models_catalog.json`; incompatible providers fail before paid work begins. Tightened in `32d9e369`.
- [~] **Source identity is recorded.** The adaptation-session pilot records `BookSha256`, but the main screenplay benchmark does not yet persist a complete dataset manifest containing hashes for every source, gold file, rubric, parser, and scorer.

### Cache integrity and invalidation

- [x] **Generation caches are keyed by prompt revision and generation temperature.** A prompt or temperature change cannot silently reuse an older screenplay. Implemented in `a37a36d1` and `732f2ddd`.
- [x] **Reasoning effort participates in cache identity.** A max-effort run cannot silently reuse or overwrite a default-effort candidate or judgment.
- [x] **Judge caches are keyed separately from generation temperature.** Their identity includes prompt revision, judge temperature, effort, rubric version, and the hash of the actual candidate screenplay set. A changed candidate or rubric invalidates the cached judgment. Implemented across `5ce12f7f`, `732f2ddd`, and the current judgment rubric/cache code.
- [x] **Adaptation-session stage caches track generation provenance.** Model, committed prompt revision, generation temperature, judge temperature, and target runtime changes invalidate cached generation stages. Dual-attach artifacts and associated judge reviews are also cleared when provenance changes. Implemented in `32d9e369`.
- [x] **Fallback-poisoned screenplay caches are rejected.** The harness identifies deterministic fallback drafts, refuses to cache them as genuine model output, ignores old poisoned cache entries, and excludes fallbacks from comparisons and leaderboards.
- [x] **Operators can explicitly bypass caches.** The benchmark supports `--no-cache` for controlled regeneration.

### Run validity, reporting, and comparison

- [x] **Dry runs are identified and excluded from global leaderboards.** Mock reports carry an explicit warning and do not count as live benchmark history.
- [x] **Fallback drafts are visibly labeled and excluded from model comparisons.** Reports distinguish a failed generation's heuristic fallback from genuine model output.
- [x] **Prompt, temperature, and effort are visible in history/dashboard views.** These configuration differences are not hidden behind one blended score series.
- [x] **Prompt-version comparisons use matched data.** Dashboard deltas count only books and effort levels where both prompt versions have real, non-fallback results, preventing missing data from silently skewing a comparison.
- [x] **Self-bias is measured and reported.** The history model records judge matrices and self-versus-peer bias summaries rather than assuming a model is an impartial judge of its own screenplay.
- [x] **Historical experiments and raw artifacts are retained.** Cached screenplays, judge JSON, per-run `run_data.json`, Markdown reports, benchmark history, and the HTML dashboard were captured in `6d836817` and subsequent runs.
- [~] **Invalid states are partly separated from ordinary scores.** Mock and generation fallback states are handled correctly, but production-readiness failures such as a missing required sidecar can still coexist with a high composite score.

### Deterministic validation and experimental tooling

- [x] **Book adaptation returns Fountain and visual metadata as one structured result.** `BookToFountainConverter.ConvertWithMetadataAsync` returns `AdaptationConversionResult` with clean Fountain, parsed `ProjectVisionMeta.Document`, status, and error information. The optional metadata callback and string-only compatibility wrapper have been removed after migrating all production, benchmark, and test callers.
- [x] **The main screenplay benchmark preserves visual metadata.** New result folders and complete cross-run cache entries store a `.vision_meta.json` file beside each `.fountain` file. Fountain-only legacy cache entries are not accepted as complete package cache hits.
- [x] **Judges receive the complete adaptation package.** Judge inputs now contain separately labeled Fountain and visual-metadata sections, and judge-cache identity hashes the complete package rather than the Fountain body alone.
- [x] **Closing-transition auditing uses parsed Fountain structure.** The scorer checks parsed transition/end-card elements instead of relying only on a raw-text regex, eliminating known false negatives. Implemented in `a37a36d1`.
- [x] **Sidecar planning and validation tools exist.** `SidecarPlanningPilot` can generate a source-grounded planning package, and `SidecarArtifactValidator` validates required files and structural expectations. Implemented in `a37a36d1`.
- [x] **Prompt-improvement review tooling exists.** `PromptImprovementReview` can review cached candidates against a committed prompt revision without requiring a complete new benchmark design. Implemented in `a37a36d1`.
- [x] **Adaptation-session outputs persist cost and byte summaries.** Token usage, cached tokens, model cost, and payload/file size information are written to disk for later comparison. Implemented in `9886c078` and expanded by subsequent benchmark work.
- [x] **`VISION_META` is preserved across both adaptation paths.** The product persists extracted metadata to its project sidecars, the adaptation-session artifacts retain it, and the main screenplay benchmark now carries it through generation, cache, result artifacts, candidate-package hashing, and judge payload assembly.
- [~] **Historical Fountain-only benchmark caches are recognized as incomplete.** They are ignored for new complete-package cache hits, but no manifest migration or historical-result validity annotation has yet been written.
- [ ] **All production-blocking prompt rules are enforced deterministically.** Multi-location headings, invented source names, missing `VISION_META`, overloaded action blocks, and every clip-length violation do not yet consistently invalidate or cap a candidate.

### Reliability work adjacent to benchmarking

- [x] **Malformed structured model responses no longer pass through silent destructive extraction.** JSON extraction was hardened in `f4c29e48`.
- [x] **Transient model-call failures have a shared retry policy.** Chat clients and the adaptation path use centralized transient HTTP/network retry handling, reducing failures caused by one-off provider errors.
- [x] **Multi-chunk adaptation quality failures are visible.** Soft quality-gate failures are logged rather than disappearing during long-book adaptation.

This ledger records implementation status, not proof that each change improved quality. A completed safeguard may still need controlled validation under the procedure below.

## Current adaptation results

The latest completed screenplay runs use prompt revision `dfdb0b9a3e`.

| Book | Latest winner | Latest score | Historical best | Gap |
|---|---|---:|---:|---:|
| A Christmas Carol | `grok-4.5` | 85.8 | 92.6, `gpt-5.6-sol` | -6.8 |
| Nick and Me | `grok-4.5` | 80.5 | 91.4, `gpt-5.6-sol` | -10.9 |
| The Call of the Wild | `gpt-5.6-terra` | 90.0 | 91.9, `gpt-5.6-luna` | -1.9 |
| The Tell-Tale Heart | `grok-4.5` | 88.2 | 93.4, `claude-opus-5` | -5.2 |
| The Velveteen Rabbit | `grok-4.5` | 91.6 | 93.6, `gpt-5.6-sol` | -2.0 |

Across the recorded runs for prompt revision `dfdb0b9a3e`:

| Model | Runs | Mean composite | Mean fidelity | Mean character clarity | Mean directibility | Mean pacing |
|---|---:|---:|---:|---:|---:|---:|
| `grok-4.5` | 20 | 85.01 | 7.18 | 7.40 | 6.72 | 7.30 |
| `gpt-5.6-terra` | 20 | 84.28 | 6.18 | 7.12 | 7.77 | 7.31 |

Historical-best comparisons are directional, not controlled experiments. Candidate models, judge models, prompt revisions, and sometimes the scoring implementation differ between runs. A historical maximum must not be described as a regression until the old and new configurations are rerun under the same protocol.

### Repeated adaptation failures

- `grok-4.5` tends to preserve more source material but overpacks locations, actions, time changes, and dialogue. Its weakest aggregate dimension is video directibility.
- `gpt-5.6-terra` tends to produce cleaner clip-scale structure but drops major source beats, invents personal names for unnamed roles, or misattributes dialogue. Its weakest aggregate dimension is fidelity.
- Both models have repeatedly been judged as omitting the mandatory `VISION_META` sidecar.
- Adaptation-session artifacts do contain `VISION_META`, while many screenplay benchmark candidates do not. The harness must determine whether this is a generation, extraction, caching, or judge-payload defect.
- Deterministic scores are frequently near 100 even when judges identify production-blocking failures. The current structural score therefore does not enforce all requirements described as hard requirements.

## Current on-screen-cast F1 results

The newest F1 run is `grok-4.5` with prompt `v2_grounded`: **0.9762 over 21 cases**. The historical headline best is `claude-fable-5`: **0.9900 over the older 20-case set**.

On the 20 shared cases, the scores are 0.975 and 0.990. The 0.015 difference is one partial error on a small sample and is not strong evidence that one configuration is generally better.

Both configurations fail on the same case:

```json
{
  "id": "s28_b11",
  "goldLabel": "Character_Akela, Character_Gray_Brother, Character_Mowgli",
  "latestAiLabel": "Character_Mowgli",
  "latestAiScore": 0.5,
  "topAiLabel": "Character_Mowgli, Character_Akela",
  "topAiScore": 0.8
}
```

The remaining differences between their shared outputs are label ordering and do not affect set F1. The newest run also correctly handles the added off-screen-narrator case.

## Improvement priorities

### 1. Make hard requirements deterministic

Add preflight validators that fail or trigger a retry for:

- Missing or invalid `VISION_META` delimiters and JSON.
- Multiple locations or time periods in one scene heading.
- Dialogue turns above the configured clip word budget.
- Inconsistent character tokens or age variants.
- Invented personal names that do not occur in the source.
- Essential actions or entrances placed only in parentheticals.
- Action blocks containing multiple time jumps or too many distinct visual events.

A production-blocking failure must either invalidate the candidate or cap its composite score. It must not survive as a prose warning beside a nominal 100% structural score.

### 2. Add a source ledger before drafting

Have the generator construct a private structured ledger containing:

- Major plot turns and thematic revelations.
- Iconic set pieces and final-act proof sequences.
- Source-named characters and stable neutral tokens for unnamed roles.
- Exact speaker attribution for retained dialogue.
- Recurring characters, age variants, and visual continuity locks.

The completed screenplay should then be checked against the ledger. Compression may shorten connective material, but it should not silently delete a required story turn.

### 3. Add a clip-directibility rewrite pass

Require the final pass to enforce:

- One location and continuous time per heading.
- One continuous, camera-observable event per action paragraph.
- Separate beats for time jumps, montages, entrances, and major reactions.
- No ellipsis-only dialogue.
- No essential action hidden in parentheticals.
- Dialogue and action units that fit one 5-10 second clip.

### 4. Improve on-screen-cast context

For classifier input, send the previous visual beat and previous on-screen cast as separate structured fields. The prompt should distinguish:

- Current physical action: include the actor.
- Background, possession, memory, or prop mention: exclude.
- Previous-beat continuity: include only when the prior cast state and current action establish continued presence.

This specifically targets the shared `s28_b11` failure without adding story-specific names to product logic.

## Reproducible improvement procedure

### Phase 0: Freeze the experiment

1. Start from a clean commit. Record the full Git commit SHA; do not benchmark a dirty prompt or harness.
2. Assign a human-readable experiment ID and state one hypothesis, such as: "a deterministic source ledger improves fidelity without reducing directibility."
3. Freeze the book suite and store a SHA-256 hash for each source file.
4. Freeze the gold labels, rubric, deterministic scorer, judge prompt, parser, and score-composition formula. Record a hash for each.
5. Freeze exact model IDs, provider, model snapshot/version when available, reasoning effort, generation temperature, judge temperature, maximum output tokens, timeout, and retry policy.
6. Freeze the author and judge matrix. Do not add or remove a judge midway through a comparison.
7. Record runtime and dependency versions, operating system, and benchmark-tool build SHA.

### Phase 1: Establish a trustworthy baseline

1. Select the committed production prompt as control.
2. Clear only experiment-specific generated caches, or use a new cache namespace. Never silently mix generations from different prompt or model configurations.
3. Run a dry-run to verify paths, hashes, model availability, and that no fallback output can be scored as a genuine generation.
4. Run one inexpensive smoke book through the complete harness.
5. Verify manually that the candidate saved to disk is byte-for-byte the candidate sent to judges.
6. Verify that required sidecars remain attached after extraction and caching.
7. Run the frozen full suite with at least three independent generation replicates per book/model configuration. Temperature zero still requires replicates because hosted inference can vary.
8. Rejudge each immutable candidate at least three times, or use three frozen independent judges. Keep generation variance separate from judge variance.
9. Store every raw request, raw response, extracted candidate, validation result, judge response, latency, token count, retry, and error without secrets.

### Phase 2: Analyze the baseline

1. Report mean, median, standard deviation, minimum, and maximum for composite and every dimension.
2. Report deterministic pass rates separately from qualitative scores.
3. Report per-book and macro-averaged results so one long or easy book cannot dominate.
4. For F1, report micro F1, macro/set F1, precision, recall, and exact-match rate.
5. When gold cases change, report the shared immutable subset separately from the expanded set.
6. Produce a per-sample error ledger with false positives, false negatives, omitted beats, invented names, attribution errors, and structural failures.
7. Mark invalid generations, fallbacks, parse failures, and missing judges as invalid. Do not convert them into low-scoring ordinary candidates.

### Phase 3: Design one change

1. Choose the largest repeated error category, not the most memorable single sample.
2. Change one independent variable: prompt, validator, retry policy, model, or context format.
3. Keep all other frozen inputs identical to the baseline.
4. Add or update offline unit tests for the intended rule before making paid calls.
5. Do not add book titles, character names, page numbers, or story-specific phrases to product code or prompts.
6. Give the candidate prompt a new committed revision and hash.

### Phase 4: Paired evaluation

1. Generate control and candidate outputs for the same books with the same replicate indices.
2. Randomize anonymous labels independently for every judge and persist the mapping.
3. Judge control and candidate in the same request or balanced request blocks when context limits permit.
4. Counterbalance presentation order to reduce position bias.
5. Exclude self-judging from the primary score, or report it separately from independent-judge consensus.
6. Calculate paired per-book and per-sample deltas with confidence intervals or bootstrap intervals.
7. Inspect regressions even if the aggregate score improves.

### Phase 5: Promotion gates

Promote a candidate only when all of the following hold:

- No production-readiness hard failure survives validation.
- Macro composite score improves by a predefined minimum practical delta.
- The target dimension improves on a majority of books and replicates.
- No critical dimension regresses beyond its predefined tolerance.
- Source fidelity, clip directibility, and character continuity meet minimum floors on every book.
- The result remains positive when self-judgments are removed.
- The result remains positive on a small untouched holdout set.
- Raw artifacts and the exact run can be reconstructed from committed metadata.

If the candidate fails, retain the artifacts and hypothesis outcome, then start a new experiment with one new change. Do not tune repeatedly against the holdout set.

### Phase 6: Product verification

1. Run all relevant offline tests.
2. Manually inspect at least one generated screenplay from a story not used to author the change.
3. Confirm that Fountain parsing, cast extraction, sidecar extraction, and shot planning consume the output correctly.
4. Update the leaderboard only after the run passes validity checks.
5. Record the promoted prompt revision and benchmark evidence in the change log.

## Required run manifest

Every run should persist a manifest similar to:

```json
{
  "experimentId": "source-ledger-v1",
  "gitSha": "<full commit sha>",
  "promptHash": "<sha256>",
  "judgePromptHash": "<sha256>",
  "scorerVersion": "<git sha or content hash>",
  "datasetHash": "<manifest sha256>",
  "models": [
    {
      "role": "author",
      "id": "grok-4.5",
      "temperature": 0.2,
      "reasoningEffort": "",
      "maxOutputTokens": 0
    }
  ],
  "judgeTemperature": 0,
  "replicateCount": 3,
  "cacheNamespace": "source-ledger-v1",
  "startedUtc": "<ISO-8601>",
  "valid": true,
  "invalidReasons": []
}
```

The manifest should reference artifact paths and hashes rather than duplicating large payloads.

## Immediate next experiment

The first controlled change should be deterministic validation, not a broad prompt rewrite:

1. Reproduce the missing-`VISION_META` path locally without a paid call using existing cached candidates.
2. Trace the candidate through raw response, extraction, cache, report copy, and judge payload.
3. Add a validator and offline regression tests for sidecar preservation.
4. Add deterministic checks for multi-location headings and dialogue length.
5. Recompute structural results against existing cached artifacts without changing qualitative judgments.
6. Only after the harness correctly identifies invalid candidates, test a source-ledger prompt variant in a paired run.

This order improves the measurement instrument before using it to choose a new production prompt.
