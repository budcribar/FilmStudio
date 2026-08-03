# Adaptation reliability refactor

Date: 2026-08-02

## Outcome

The adaptation pipeline now has explicit deterministic, model-backed, and shared model-execution boundaries; a reusable validated model-operation lifecycle; a migrated pilot classifier; generalized pronunciation resolution; immutable dialogue-comparison normalization; and reproducible offline model-response replay.

No paid model or media-generation endpoint was invoked while completing this work.

## Completed phases

1. **Execution boundaries** — introduced `PageToMovie.Engine.Deterministic`, `PageToMovie.Engine.ModelBacked`, and `PageToMovie.Engine.ModelExecution`; added an architecture test and model-call inventory.
2. **Validated lifecycle** — added transport retry, parsing, domain validation, focused corrective requests, revalidation, deterministic fallback, cancellation, attempt provenance, and response hashes.
3. **Pilot migration** — split `AiActionOverheadClassifier` from `ActionOverheadHeuristic`; the model-backed classifier now uses the validated lifecycle and exposes provenance.
4. **Classifier namespace migration** — moved the model-backed classifier family out of the root engine namespace and repaired product, test, and benchmark-tool imports.
5. **General pronunciation** — added a versioned embedded heteronym lexicon, contextual sense scoring, immutable token offsets, confidence, and unresolved-candidate reporting.
6. **Hack removal** — deleted the public regex compatibility detector and removed story-specific pronunciation phrases from production code.
7. **Dialogue fidelity** — introduced an auditable comparison-only normalizer, opt-in historical-form equivalence, and an immutable-dialogue validator. Replaced unsafe suffix rules that could turn `filled` into `filed`.
8. **Reproducibility** — added prompt and behavior versions, input/response hashes, deterministic manifests, and recorded-response replay through current parsers and validators.
9. **Readiness gate** — completed the full offline solution suite and benchmark self-test.

## Verification baseline

| Check | Result |
|---|---:|
| Full offline solution tests | 1,234 passed; 0 failed; 0 skipped |
| Pronunciation and prompt tests | 77 passed |
| Classifier regression tests | 33 passed |
| Shared lifecycle/pilot tests | 13 passed |
| ScreenplayBenchmark self-test | 11 passed |
| API build | Passed |
| `git diff --check` | Passed |

This is the pre-paid-run reproducibility baseline. The next scored benchmark must use a new run identity and record the code commit, prompt versions, model/provider, parameters, source hash, pronunciation lexicon version, validation attempts, fallback source, and complete Fountain plus vision-metadata candidate package.

## Operational rules

- Code under `Deterministic` must not declare model-client or HTTP dependencies.
- A type under `ModelBacked` may cause a paid request; callers must treat it accordingly.
- Transport retry and semantic correction are different attempt types.
- Corrective prompts contain exact validation issues and the rejected response.
- Models select known pronunciation sense IDs; trusted data supplies IPA.
- Ambiguous pronunciation is reported unresolved rather than guessed.
- Dialogue normalization is for comparison only and cannot replace source dialogue.
- Benchmark caches are invalid when structured metadata or behavior versions are missing.

## Follow-on migrations

The six-step adoption pass is complete:

1. **Catalog-aware baseline** — offline fixtures explicitly select catalog entries; unknown IDs fail and no provider/model compatibility fallback is allowed.
2. **Large structured operations** — `StructuredOperationArtifacts` supplies common required-data validation plus deterministic input/output hashes and per-operation manifests.
3. **Stage 1** — validates non-empty Fountain and scene coverage and records `stage1_adaptation.json` before accepting the result.
4. **Cast extraction** — requires the versioned schema and non-empty `character_seed_tokens`, records `cast_extraction.json`, and never invents omitted cast.
5. **Stage 2** — requires `stage2_meta` and scenes and records the selected catalog video model in `stage2_shot_plan.json`.
6. **Multimodal review and replay** — clip/movie review saves pass through the same structural gate; the Mary Had a Little Lamb fixture provides a small arbitrary-story cast replay; uploaded text is assigned a stable server book ID derived from its SHA-256 hash.

The mixed services stay in the root orchestration namespace because they contain deterministic file and domain work. Only their model-backed operations and shared execution components belong under `ModelBacked` and `ModelExecution`.

Future model-operation edits should migrate the touched operation rather than add another local retry/parse/fallback implementation.

## Book identity and analysis reuse

Text uploads are registered once in the server SQLite database. `book_texts` stores the canonical UTF-8 text, SHA-256, byte count, and stable `book_<hash-prefix>` ID; `book_text_access` links that identity to authorized users/projects. Upload responses return `bookId` and `bookSha256`, and `GET /api/books/{id-or-hash}` resolves the canonical text for application or benchmark clients. Equal bytes always produce the same ID. Access is user-scoped, so deduplication does not expose another user's book.

Derived artifacts such as Fountain use `book_derived_artifacts`, not the raw-book key. Their derivation identity hashes the book ID, artifact kind, catalog model ID, prompt version and SHA-256, temperature, and behavior/schema versions. Exact derivations are reusable; changing any input creates a new artifact ID. Project cloning calls the book/project link endpoint and transfers IDs rather than copying text or derived payloads.

Cache visibility follows project visibility: **Private** is owner-only, **Public** is cross-user read-only, and **Forkable** is cross-user readable and may be linked into a new project. The existing project value `Open` maps to `Forkable` at the cache boundary. Visibility changes update the project's cache links, and derived artifacts inherit access through their source book link.

Application book-to-Fountain generation now performs this lookup automatically. It registers prepared text, computes the complete derivation identity, reuses only a complete Fountain + `VISION_META` package, and persists successful non-heuristic conversions. Prompt, model, runtime/title/author inputs, temperature, and schema changes invalidate the hit. Background import, Stage 1, and synchronous from-book endpoints all use the same service. Invite, community, and gallery forks link existing book/artifact identities into the new private project instead of duplicating cache rows.

The screenplay benchmark uses the same `pagetomovie.db` registry before its legacy file cache. Benchmark entries default to the `benchmark` cache owner and `Forkable` visibility; operators can override those values or disable the shared layer. `--no-cache` remains the reproducibility escape hatch and bypasses every cache.

## SQLite dependency verification

The repository-level override resolves `SQLitePCLRaw.bundle_e_sqlite3`, `lib.e_sqlite3`, `core`, and `provider.e_sqlite3` to 2.1.12. A forced restore removed stale 2.1.11 assets; NuGet's transitive vulnerability audit reports no vulnerable packages for every solution project, and the full 1,185-test offline suite passes.
