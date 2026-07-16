# Film Studio (.NET solution)

Visual Studio / `dotnet` solution: **Blazor UI + C# API/engine**, with live **SignalR** job progress.

```text
host/
  FilmStudio.slnx          # open this in Visual Studio
  FilmStudio.Core/         # shared models + options
  FilmStudio.Engine/       # C# project store + Grok video jobs
  FilmStudio.Api/          # REST + SignalR hub (:5088)
  FilmStudio.Web/          # Blazor Server UI
  python_engine_api.py     # optional legacy Python HTTP bridge
```

## Architecture

| Project | Role |
|---------|------|
| **FilmStudio.Web** | Blazor UI (projects, start gen, live log) |
| **FilmStudio.Api** | Backend: REST + `/hubs/jobs` SignalR |
| **FilmStudio.Engine** | Native C# job runner + Grok client |
| **FilmStudio.Core** | DTOs / options |

The C# engine **reads** Stage 2 blueprints under `projects/<id>/` and **writes** clip mp4s.  
It does **not** yet reimplement every Python feature (multi-ref plates, full WIP remux, Stage 1 LLM). Use **Streamlit + Python** for those until ported.

## Run (two terminals)

### 1) API / engine first (set API key for real gen)

```powershell
cd C:\Users\budcr\source\repos\NickAndMe\host\FilmStudio.Api
$env:XAI_API_KEY = "your-key"   # required to generate
dotnet run
# Must listen on http://127.0.0.1:5088
# GET http://127.0.0.1:5088/health
# SignalR: /hubs/jobs
```

You need **two processes**: Api **and** Web. If only Web is running, health checks fail with connection refused.

### 2) Blazor UI

```powershell
cd C:\Users\budcr\source\repos\NickAndMe\host\FilmStudio.Web
dotnet run
# e.g. https://localhost:7206  or  http://localhost:5079
```

Web calls API at `EngineApi:BaseUrl` = `http://127.0.0.1:5088` (see `appsettings.json` + `appsettings.Development.json`).

### Visual Studio

Open `host/FilmStudio.slnx`, set **multiple startup projects**: Api + Web.

## REST (Api)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/health` | Liveness + workspace |
| GET | `/api/projects` | List / active |
| POST | `/api/projects/{id}/activate` | Switch project |
| GET | `/api/jobs` | Job snapshot |
| POST | `/api/jobs/gen-scene` | `{ projectId, scene, onlyMissing }` |
| POST | `/api/jobs/cancel` | Cancel |
| GET | `/api/stage2-status` | Blueprint present? |

## SignalR

Hub: `/hubs/jobs`  
Events: `JobUpdated` (JobSnapshot), `JobLog` (string)

## Config

`FilmStudio.Api/appsettings.json` → `FilmStudio:WorkspaceRoot` (empty = auto-detect repo root).

## Parity notes

| Feature | C# engine | Python engine |
|---------|-----------|---------------|
| List projects / activate | Yes | Yes |
| Generate clips from blueprint prompts | Yes (text Grok) | Yes (full prompt build + multi-ref) |
| Character portrait gen / lock | Yes (C# Grok image + lock) | Yes |
| Multi-ref video plates | Not yet | Yes |
| Stage 1 / Stage 2 planning | Still Python / Streamlit | Yes |
| WIP remux / QA | Not yet | Yes |
| SignalR live UI | Yes | N/A (Streamlit poll) |

Python remains the **full pipeline** until those pieces are ported; C# is the **product backend** path for multi-user UI.
