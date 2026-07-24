# Live API tests (paid)

These tests call real provider APIs (xAI Grok, etc.) and **cost money**.  
They are **not** run by default.

## Default (free)

```bash
dotnet test host/PageToMovie.Tests
```

The project sets:

```xml
<VSTestTestCaseFilter>Category!=LiveApi</VSTestTestCaseFilter>
```

so anything tagged `Category=LiveApi` is excluded.

## Opt-in (paid)

```bash
# Windows PowerShell
$env:PAGETOMOVIE_LIVE_API_TESTS = "1"
$env:XAI_API_KEY = "xai-..."

# One smoke case (Buster only — cheaper)
dotnet test host/PageToMovie.Tests --filter "Category=LiveApi&FullyQualifiedName~Live_buster"

# Full gold corpus (one live extract per book — more expensive)
dotnet test host/PageToMovie.Tests --filter "Category=LiveApi"
```

Both env vars are required:

| Variable | Meaning |
|----------|---------|
| `PAGETOMOVIE_LIVE_API_TESTS=1` | Explicit opt-in |
| `XAI_API_KEY` | Provider key |

If you pass the filter without enabling the gate, tests are **Skipped** (not failed).

## How to add a live test

1. Put it under `LiveApi/`.
2. Tag the class: `[Trait("Category", LiveApiGate.Category)]`.
3. Use `[LiveApiFact]` / `[LiveApiTheory]` so missing keys skip cleanly.
4. Never call paid APIs from free unit tests.

## Free vs live split

| Free (always) | Live (opt-in) |
|---------------|---------------|
| Gold corpus backfill (speaker-only model) | Real Grok cast extract |
| Name-hint harvesting | Other future evals (video, OCR, …) |
| Fakes / in-process API |
