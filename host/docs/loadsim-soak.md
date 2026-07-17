# LoadSim & soak guide (Phase E)

## Prerequisites

- API with **fakes** for any gen-heavy run (avoids xAI spend).
- Built solution: `dotnet build host/FilmStudio.slnx`

## Quick CI-style run (local)

Terminal 1:

```powershell
cd host
$env:FILMSTUDIO_USE_FAKES = "true"
$env:FilmStudio__Capacity__MaxVideoInFlight = "8"
$env:FilmStudio__Fakes__VideoDelayMs = "50"
dotnet run --project FilmStudio.Api
```

Terminal 2:

```powershell
cd host
dotnet run --project FilmStudio.LoadSim -- `
  --baseUrl http://127.0.0.1:5088 `
  --users 25 `
  --duration 90 `
  --scenario mixed `
  --project Buster `
  --out loadsim-results.json
```

Exit code **0** = gates pass. Results JSON is written to `--out`.

## Scenarios

| Scenario | Behavior |
|----------|----------|
| `browse` | health, projects, scenes, detail |
| `play` | range GET clip/composite |
| `gen` | POST gen-scene (onlyMissing) |
| `remux` | POST remux scene |
| `review` | POST clip review pass |
| `mixed` | weighted mix (CLI weights) |

## Gates (defaults)

| Gate | Default |
|------|---------|
| HTTP error rate (excl. intentional 409) | &lt; 1% (`--maxErrorRate`) |
| `/health` samples | all 200 |
| Browse p95 | &lt; 500 ms (`--maxBrowseP95Ms`; CI uses 800) |
| 5xx | 0 |
| Peak API in-flight vs cap | ≤ cap + 2 (when sampled) |

## Manual soak (100 users × 10 min)

**Only with fakes** unless you accept real API cost.

```powershell
# Terminal 1
$env:FILMSTUDIO_USE_FAKES = "true"
$env:FilmStudio__Capacity__MaxVideoInFlight = "12"
$env:FilmStudio__Capacity__MaxVideoInFlightPerUser = "1"
dotnet run --project FilmStudio.Api

# Terminal 2
dotnet run --project FilmStudio.LoadSim -- `
  --users 100 `
  --duration 600 `
  --scenario mixed `
  --genWeight 0.12 `
  --thinkTimeMs 400 `
  --maxGenPerUser 3 `
  --out loadsim-soak-100x10.json
```

### Admin validation during soak

1. Open Web → `/admin/login` (admin/admin in Development).
2. Confirm **jobs / locks / apiInFlight** move.
3. Open `/admin/config`, change **MaxVideoInFlight** live; observe queue / rejects.
4. Archive `loadsim-soak-100x10.json` + note capacity settings.

## Safety

- LoadSim refuses gen against non-fake API unless `--i-know-what-im-doing`.
- Prefer `--scenario browse` for pure path stress without gen.

## CI

GitHub Actions: `.github/workflows/loadsim.yml`

- Unit tests (incl. metrics + gate unit tests)
- Start API with fakes → LoadSim 25×90s mixed → upload `loadsim-results.json`
