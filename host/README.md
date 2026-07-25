# PageToMovie (.NET solution)

Visual Studio / `dotnet` solution: **Blazor WASM UI + C# API/engine**, with live **SignalR** job progress.
**No Python runtime is required** for the product path under `host/`.

```text
host/
  PageToMovie.slnx          # open this in Visual Studio
  PageToMovie.Core/         # shared models + options
  PageToMovie.Engine/       # project store + jobs (gen, review drafts, cast, book prepare)
  PageToMovie.Api/          # REST + SignalR hub (:5088); hosts WASM UI
  PageToMovie.Web/          # Blazor WebAssembly UI + ffmpeg.wasm media tools
  PageToMovie.Fakes/        # fake Grok clients + fixtures
  PageToMovie.LoadSim/      # concurrent virtual-user load client
  PageToMovie.Tests/        # unit tests
  docs/                    # multi-user plan, loadsim soak guide
```

## Architecture

| Project | Role |
|---------|------|
| **PageToMovie.Web** | Blazor UI (projects, adaptation, scenes, characters, review, cost) + client stitch/trim/frames |
| **PageToMovie.Api** | Backend: REST + `/hubs/jobs` SignalR (no native ffmpeg) |
| **PageToMovie.Engine** | C# job runner (Stage 1/2 adaptation, classifiers, video prompts, vision review, book prepare) |
| **PageToMovie.Core** | DTOs / options |

> **Pipeline overview**: See the root [README.md](../README.md#how-film-studio-converts-source-text-to-a-movie-step-by-step-ai-pipeline). Media compose/trim runs in the browser; vision/gen keys stay on the API.

## Run (two terminals)

### 1) API / engine first (set API key for real gen)

```powershell
cd C:\Users\budcr\source\repos\NickAndMe\host\PageToMovie.Api
$env:XAI_API_KEY = "your-key"   # required for Stage 1 / images / video / vision
dotnet run
# Must listen on http://127.0.0.1:5088
# GET http://127.0.0.1:5088/health
# SignalR: /hubs/jobs
```

You need **two processes**: Api **and** Web. If only Web is running, health checks fail with connection refused.

**All video stitch / silence-trim / auto-review frame sampling is in the browser** (`ffmpeg.wasm` in PageToMovie.Web).  
The API host never installs or spawns native `ffmpeg`. Gen clips may live in a user media folder; the server keeps hashes/metadata.

### 2) Blazor UI

```powershell
cd C:\Users\budcr\source\repos\NickAndMe\host\PageToMovie.Web
dotnet run
# e.g. https://localhost:7206  or  http://localhost:5079
```

Web calls API at `EngineApi:BaseUrl` = `http://127.0.0.1:5088` (see `appsettings.json` + `appsettings.Development.json`).

### Visual Studio

Open `host/PageToMovie.slnx`, set **multiple startup projects**: Api + Web.

## REST (Api)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/health` | Liveness + workspace |
| GET | `/api/projects` | List / active |
| POST | `/api/projects/{id}/activate` | Switch project |
| GET | `/api/jobs?mine=1` | List my jobs (Phase F: bare `/api/jobs` → **400**) |
| GET | `/api/jobs?projectId=` | List jobs for project |
| GET | `/api/jobs/{jobId}` | Job detail |
| POST | `/api/jobs/{jobId}/cancel` | Cancel one job |
| POST | `/api/jobs/book-prepare` | PDF extract / vision OCR |
| POST | `/api/jobs/stage1` | Stage 1 scene bible |
| POST | `/api/jobs/stage2` | Stage 2 clip plan |
| POST | `/api/jobs/gen-scene` | Generate scene clips (client may save MP4 to media folder) |
| POST | `/api/jobs/clip-auto-review` | Auto-review one clip (body includes browser-sampled frames) |
| POST | `/api/jobs/voice-preview` | Short voice sample clip (MP4; no server audio extract) |
| POST | `/api/jobs/youtube-upload` | Upload export/WIP to YouTube when configured |
| POST | `/api/jobs/cancel` | Cancel all / active |
| GET | `/api/stage2-status` | Blueprint present? |

## SignalR

Hub: `/hubs/jobs`  
Events: `JobUpdated` (JobSnapshot), `JobLog` (string)

## Config

`PageToMovie.Api/appsettings.json` → `PageToMovie:WorkspaceRoot` (empty = auto-detect repo root).

### YouTube upload (Review screen)

The Review screen's **Upload to YouTube** button uploads `assets/movie_wip.mp4` via the
YouTube Data API v3 (resumable upload, `youtube.upload` scope). It's off by default — no
button appears until an admin connects a channel. To enable it:

1. In [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services →
   Credentials, create an **OAuth client ID** (Application type: **Web application**), and
   enable the **YouTube Data API v3**.
2. Add an authorized redirect URI matching your Api host, e.g.
   `http://127.0.0.1:5088/api/youtube/oauth2callback`.
3. Set in `PageToMovie.Api/appsettings.json` (or env vars
   `PageToMovie__YouTube__ClientId` / `__ClientSecret` / `__RedirectUri`):
   ```json
   "PageToMovie": {
     "YouTube": {
       "ClientId": "...apps.googleusercontent.com",
       "ClientSecret": "...",
       "RedirectUri": "http://127.0.0.1:5088/api/youtube/oauth2callback"
     }
   }
   ```
4. Sign in as admin, open **Review**, click **Connect YouTube**, and approve access. The
   refresh token is stored under `{workspace}/.PageToMovie/youtube_token/` — one shared
   channel per PageToMovie instance, not per-user.

## LoadSim (Phase E)

```powershell
# Terminal 1 — API with fakes
$env:PageToMovie_USE_FAKES = "true"
dotnet run --project PageToMovie.Api

# Terminal 2 — virtual users
dotnet run --project PageToMovie.LoadSim -- --users 25 --duration 90 --scenario mixed --out loadsim-results.json
```

Uses checked-in **`projects/LoadSimBuster`** (isolated from real Buster). See `docs/loadsim-soak.md`.

## Capability matrix (native C#)

| Feature | Status |
|---------|--------|
| PDF extract + vision OCR + page render | Yes |
| Stage 1 scene bible (Grok chat) | Yes |
| Stage 2 clip planner | Yes |
| Multi-ref video + audio prompt build | Yes |
| Character portrait gen / lock | Yes |
| Browser stitch / silence-trim / auto-review frames (ffmpeg.wasm) | Yes |
| Review / edit log / approve | Yes |
| SignalR live UI | Yes |

See the repo-root `README.md` for the supported run path and workspace layout.
