# Multi-user architecture plan (≈100 concurrent users)

**Goal:** Evolve FilmStudio from single-operator / single-job to support ~**100 concurrent UI sessions**, with **per-user API keys**, **scene-level isolation**, **fair local workers**, a **load simulator** that does not burn real xAI credits, and an **admin console** (login, live server state, server configuration).

**Non-goals (v1 of this plan):** CRDT co-editing, multi-region, full SaaS billing portal, multi-admin RBAC beyond `admin` vs `user`.

---

## 1. Target capacity (definition of done)

| Metric | Target (single host, well-specced) |
|--------|-------------------------------------|
| Concurrent UI sessions (API + optional Blazor) | **100** |
| Concurrent **video gens** (global) | **8–16** (configurable) |
| Concurrent video gens **per user** | **1–2** |
| Concurrent ffmpeg (remux/WIP) | **2** |
| WIP rebuilds per project | **1** (single-flight + coalesce) |
| Scene gens same scene, two users | **Rejected** (scene lock) |
| Scene gens different scenes, same project | **Allowed** |
| API keys | **1 per user** (resolved at job start) |

**Success criteria for load tests:**

1. Simulator runs **100 virtual users** for 10+ minutes without process crash / unbounded memory growth.
2. Under gen mix (e.g. 20% genning), p95 queue wait and error rates stay within configured SLOs.
3. No cross-user file clobber (scene locks + atomic writes).
4. Fakes inject without code changes beyond DI / config.
5. Admin can log in, view **live** server state, and change capacity/fake settings without redeploy (persisted config).

---

## 2. Architecture (end state)

```
┌──────────────────────────────────────────────────────────────────────┐
│ Blazor Web (users)  │  Blazor Admin (/admin)  │  LoadSim (100 VUs)    │
└──────────┬──────────────────┬─────────────────────────┬──────────────┘
           │                  │                         │
           │  user JWT /      │  admin JWT +            │  X-User-Id
           │  X-User-Id       │  role=admin             │
           ▼                  ▼                         ▼
┌──────────────────────────────────────────────────────────────────────┐
│ FilmStudio.Api                                                         │
│  Auth (user + admin roles)                                             │
│  JobRouter → JobQueue (multi-job)                                      │
│  ApiWorkerPool / LocalWorkerPool                                       │
│  LockService · ProjectStore                                            │
│  ServerMetricsService (snapshots for admin)                            │
│  RuntimeConfigStore (capacity + fakes; hot-reloadable)                 │
│  SignalR: user:{id} · project:{id} · job:{id} · admin:ops              │
└───────────┬───────────────────┬────────────────────────────────────────┘
            │                   │
            ▼                   ▼
   IGrok* clients          IFfmpegRemux
   (real or fake)          (real or fake)
```

### 2.1 Resource split

| Work | Scheduler | Cap |
|------|-----------|-----|
| Video / image / vision / Stage1–2 LLM | **ApiWorkerPool** | Global + per-user |
| Scene remux, WIP | **LocalWorkerPool** | Global ffmpeg semaphore |
| Browse, review, play | Request threads | No job slot |

### 2.2 Locks

| Resource key | Mode |
|--------------|------|
| `project:{id}:scene:{n}` | Exclusive for gen/remux that scene |
| `project:{id}:wip` | Exclusive WIP rebuild |
| `project:{id}:stage` | Exclusive Stage1/2 |
| `project:{id}:char:{key}` | Exclusive lock/regen portrait |

Soft locks: owner, reason, expiresAt, heartbeat; steal with force flag (**admin only**).

### 2.3 Admin console (login, live ops, configuration)

Admin is a **first-class role**, not “whoever knows the API port.”

#### 2.3.1 Admin login

| Item | v1 choice |
|------|-----------|
| Role | `admin` vs `user` (claim / config) |
| Auth | Cookie or JWT after `POST /api/auth/login` |
| Credentials | Config: `FilmStudio:Admin:Username` + `PasswordHash` (or env `FILMSTUDIO_ADMIN_PASSWORD` for dev) |
| Session | Sliding expiry (e.g. 8h); logout clears cookie |
| Dev shortcut | Optional `Admin:AllowDevBypass` only in Development |
| LoadSim | **Never** uses admin credentials; stays on user headers |

**Blazor routes:**

- `/admin/login` — public login form  
- `/admin` — dashboard (authorized `admin`)  
- `/admin/config` — server configuration (authorized `admin`)  
- Nav: “Admin” only when role is admin  

**API authorization:**

- User endpoints: require authenticated user (or existing `X-User-Id` in early phases).  
- Admin endpoints: require `role=admin` (policy `AdminOnly`).  
- Mutating admin config: admin only + optional CSRF on cookie auth.

#### 2.3.2 Live server state dashboard (`/admin`)

**Purpose:** Single pane of glass while LoadSim or real users run.

**Snapshot model** `ServerStateDto` (also pushed over SignalR):

| Section | Fields |
|---------|--------|
| **Process** | uptime, GC heap, working set, thread count, env (Dev/Prod), `UseFakes` |
| **Capacity** | MaxVideoInFlight, MaxVideoInFlightPerUser, MaxFfmpegInFlight, MaxQueuePerUser (effective values) |
| **API pool** | inFlight video/image/chat, queue depth **global**, queue depth **per user** (top N), RR cursor |
| **Local pool** | ffmpeg inFlight, WIP jobs running |
| **Jobs** | running list (jobId, userId, projectId, kind, scene, clip, age, progress %) |
| **Queues** | waiting jobs count by kind |
| **Locks** | active locks (resource, userId, expiresAt, reason) |
| **Projects** | open project ids with recent activity (optional) |
| **SignalR** | approximate connection count (if available) |
| **Health** | last error rate window, 429 count (real or fake), disk free on workspace |

**Real-time updates:**

1. Admin SignalR group **`admin:ops`**.  
2. On connect, admin joins `admin:ops` (only if role admin).  
3. `ServerMetricsService` emits:
   - **Periodic tick** (e.g. every 1–2s) full or delta snapshot  
   - **Event-driven** pushes on job start/finish, lock acquire/release, config change, capacity reject  
4. Blazor admin page: `@implements` hub client; bind tables to latest snapshot; no full page poll required (optional fallback `GET /api/admin/state` every 5s if hub drops).

**UI layout (suggested):**

```text
┌─ Server ──────────────────┬─ Capacity ─────────────────┐
│ Up 2h · 1.2 GB · Fakes ON │ Video 3/12 · FFmpeg 1/2    │
├─ Running jobs ────────────┴────────────────────────────┤
│ job… u003 Buster scene gen S04 C2  45%                  │
├─ Queues by user ───────────────────────────────────────┤
│ u001: 2  u007: 1  …                                    │
├─ Locks ────────────────────────────────────────────────┤
│ project:Buster:scene:04  alice  gen  exp 12:04         │
└─ Recent rejects / 429s ────────────────────────────────┘
```

**Admin actions (dashboard, optional v1.1):**

- Cancel any job by id  
- Force-release lock (with confirm)  
- Pause API pool (drain) for maintenance  

#### 2.3.3 Server configuration page (`/admin/config`)

**Purpose:** Tune capacity and fake/chaos settings **at runtime** without rebuild; persist for restart.

**Editable groups:**

| Group | Settings |
|-------|----------|
| **Capacity** | MaxVideoInFlight, MaxVideoInFlightPerUser, MaxFfmpegInFlight, MaxQueuePerUser, MaxUiSessions (soft warn) |
| **Fakes** | UseFakes (may require note: “new clients only” or restart), VideoDelayMs, FailRate, RateLimitEveryN |
| **Jobs** | Default clip quantum, job TTL / cancel policy |
| **WIP** | Auto-coalesce on/off |
| **Admin** | Change admin password (separate form) |
| **Read-only** | Workspace root, version, git commit, xAI configured (bool, not key) |

**Persistence:**

- Write to `FilmStudio:RuntimeConfigPath` (e.g. `host/FilmStudio.Api/runtime-config.json`) or under workspace `.filmstudio/runtime-config.json`.  
- `IRuntimeConfigStore`: load on startup → merge over appsettings → **hot apply** to worker pools (update semaphores / caps).  
- Audit: append `admin_config_audit.jsonl` (who, when, old→new).

**API:**

| Method | Path | Auth |
|--------|------|------|
| POST | `/api/auth/login` | public |
| POST | `/api/auth/logout` | auth |
| GET | `/api/auth/me` | auth (returns roles) |
| GET | `/api/admin/state` | admin |
| GET | `/api/admin/config` | admin |
| PUT | `/api/admin/config` | admin |
| POST | `/api/admin/jobs/{id}/cancel` | admin |
| POST | `/api/admin/locks/release` | admin (optional) |

**Validation:** caps must be ≥ 1 where required; FailRate in [0,1]; reject dangerous values (e.g. MaxVideoInFlight > 100 without confirm).

**SignalR:** after config PUT, broadcast `AdminConfigChanged` on `admin:ops` and optionally bump capacity gauges for all admins.

### 2.4 Jobs

Replace singleton “one snapshot” with:

```text
JobRecord {
  JobId, UserId, ProjectId, Kind, Scene?, Clip?,
  Status, QueuePosition, CreatedAt, StartedAt, FinishedAt,
  Error?, Progress Message/Index/Total
}
```

- Multiple jobs **running** up to caps.
- List: mine / project / all (admin).
- SignalR: progress only to `job:{id}` + `user:{userId}` (+ project group optional).

### 2.5 Per-user API keys

```text
IUserApiKeyProvider.GetKeyAsync(userId) → string?
```

- Real: vault / user secrets / DB.
- LoadSim: synthetic keys `sim-user-{n}` (fakes ignore value).
- **Never** log full keys.

Video client construction: factory `IGrokVideoClientFactory.Create(apiKey)` or pass key per call.

### 2.6 Fairness (local workers, multi-key world)

With **per-user keys**, Grok fairness is mostly per-key. Still apply:

- Global `MaxVideoInFlight` (protect the machine).
- Per-user `MaxVideoInFlight`.
- Optional **round-robin dequeue among users with pending work** when assigning the next free global slot (CPU fairness).

If later you run a **shared** key mode, same RR is mandatory for Grok fairness.

### 2.7 WIP

- Per-project single-flight + coalesce (`needsAnotherWip`).
- Remux only **stale** scenes, then concat.
- Not on API pool.

---

## 3. Solution layout (new projects)

```text
host/
  FilmStudio.Core/          # models, options, interfaces (expand)
  FilmStudio.Engine/        # domain + real clients
  FilmStudio.Api/           # HTTP + SignalR host
  FilmStudio.Web/           # Blazor UI
  FilmStudio.Fakes/         # NEW: fake Grok, fake ffmpeg, in-mem locks optional
  FilmStudio.LoadSim/       # NEW: concurrent user simulator (console)
  FilmStudio.Tests/         # NEW: unit + integration (WebApplicationFactory)
  docs/multi-user-100-plan.md  # this file
```

---

## 4. Injectable abstractions (fakes)

Introduce interfaces **at the edges** that cost money or CPU. Keep domain services depending on interfaces.

### 4.1 Core interfaces (`FilmStudio.Core` or `FilmStudio.Engine/Abstractions`)

| Interface | Real | Fake behavior |
|-----------|------|----------------|
| `IGrokVideoClient` | `GrokVideoClient` | Delay N ms; write tiny valid/minimal mp4 or copy fixture; optional 429 injection |
| `IGrokImageClient` | real | Return 1×1 PNG bytes / fixture |
| `IGrokChatClient` | real | Return canned JSON for Stage1/2 shapes |
| `IGrokVisionClient` | real | Return canned plate assignments |
| `IFfmpegRemux` | `FfmpegRemuxService` | Concat by file copy/list only, or invoke real ffmpeg on fixtures |
| `IJobProgressSink` | SignalR | `NullSink` / `RecordingSink` (for tests) |
| `IUserContext` | header/JWT | `SimUserContext` / `TestUserContext` |
| `IUserApiKeyProvider` | config/DB | `DictionaryKeyProvider` |
| `ILockService` | file/`pipeline_state` | `InMemoryLockService` |
| `IClock` | `SystemClock` | `FakeClock` (optional) |
| `IRandom` | system | seeded (flaky test control) |
| `IRuntimeConfigStore` | file-backed JSON | in-memory for tests |
| `IServerMetricsService` | live counters | fixed snapshot for unit tests |
| `IAdminAuthService` | password hash + JWT/cookie | `TestAdminAuth` (always admin in test) |

### 4.2 Registration

```csharp
// appsettings / env
"FilmStudio": {
  "UseFakes": true,
  "Fakes": {
    "VideoDelayMs": 200,
    "VideoFailRate": 0.0,
    "RateLimitEveryN": 0
  },
  "Capacity": {
    "MaxVideoInFlight": 12,
    "MaxVideoInFlightPerUser": 1,
    "MaxFfmpegInFlight": 2,
    "MaxQueuePerUser": 5
  }
}
```

```csharp
if (opts.UseFakes)
{
    services.AddSingleton<IGrokVideoClient, FakeGrokVideoClient>();
    // ...
}
else
{
    services.AddHttpClient<IGrokVideoClient, GrokVideoClient>(...);
}
```

### 4.3 Fake video client contract

```text
SubmitGenerationAsync(prompt, duration, ...) 
  → requestId = guid
  record call for assertions

PollForVideoUrlAsync(requestId)
  → after delay, "file://fixtures/clip_10s.mp4" or http://localhost/fixtures/...

DownloadToFileAsync(url, path)
  → File.Copy(fixture, path)  // real mp4 header so UI play works
```

**Fixture pack:** `FilmStudio.Fakes/Fixtures/clip_short.mp4` (~1–2s), `clip_10s.mp4` (optional).

### 4.4 Chaos knobs (for simulator + tests)

| Knob | Purpose |
|------|---------|
| `FailRate` | Random gen failures |
| `RateLimitEveryN` | Synthetic 429 |
| `SlowUserIds` | Extra delay for some users |
| `LockConflictRate` | (test only) |

---

## 5. Implementation phases (PR-sized)

### Phase A — Foundations (no multi-user UX yet)

**A1. Abstractions + DI**

- Extract `IGrokVideoClient`, `IGrokImageClient`, `IGrokChatClient`, `IGrokVisionClient` from concrete classes (or wrap them).
- `IFfmpegRemux` over remux methods used by jobs.
- Register real implementations as today when `UseFakes=false`.

**A2. FilmStudio.Fakes**

- Implement fakes + fixtures.
- Unit smoke: fake video produces file on disk.

**A3. Job model multi-instance**

- `JobRecord` + `IJobStore` (in-memory concurrent dictionary first).
- Replace global single `_snapshot` with:
  - `TryEnqueue`, `GetJob`, `ListJobs(userId|projectId)`, `Cancel(jobId)`.
- Keep **backward-compatible** `GET /api/jobs` → “primary” or “latest for caller”.
- Add `GET /api/jobs/{id}`, `GET /api/jobs?mine=1`.

**A4. Capacity options**

- `CapacityOptions` as above; enforce in enqueue.

**Exit A:** app runs with fakes; single-user behavior preserved; tests use fakes.

---

### Phase B — Identity + keys + admin login

**B1. User context**

- Middleware: `X-User-Id` header (dev/sim) and/or JWT later.
- `IUserContext.UserId` required for gen endpoints.
- Roles: `user` | `admin` (claim or config list `Admin:UserIds`).

**B2. API key provider**

- `ConfigUserApiKeyProvider`: map `userId → key` from config / env `USERKEY_{id}`.
- Default fallback: process `XAI_API_KEY` for local single-user.

**B3. Pass key into Grok clients**

- Prefer `Submit...(apiKey:)` or factory per request so fakes can ignore and reals use user key.

**B4. Admin authentication**

- `POST /api/auth/login` { username, password } → cookie/JWT with `role=admin` or `role=user`.
- Password stored hashed (`ASP.NET Core Identity` password hasher is enough; no full Identity DB required for v1).
- `GET /api/auth/me` → `{ userId, roles[] }`.
- Blazor: `/admin/login` + `AuthorizeView Roles="admin"` / cascading auth state.
- Policy `AdminOnly` on all `/api/admin/*`.

**Exit B:** two users with headers; admin can log in and hit `/api/admin/state` (even if state is partial).

---

### Phase C — Locks + multi-job concurrency

**C1. `ILockService`**

- Persist under `pipeline_state.json` → `locks` **or** in-memory for fakes/tests.
- `TryAcquire(resource, userId, ttl)`, `Renew`, `Release`, `Get`.

**C2. Enforce locks**

- Scene gen / scene remux: require `scene` lock.
- WIP: require `wip` lock; single-flight coalesce.
- Stage1/2: `stage` lock.

**C3. Worker pools**

- **ApiWorkerPool:** up to `MaxVideoInFlight` concurrent tasks; dequeue with per-user RR among non-empty user queues; quantum = **one clip** (preferred).
- **LocalWorkerPool:** ffmpeg semaphore.

**C4. SignalR groups**

- On connect: join `user:{userId}` (from claim/header).
- Job progress → `job:{id}` and `user:{userId}`.
- Optional project broadcast for “someone remuxed”.
- If admin: join **`admin:ops`**.

**C5. ServerMetricsService (feed for admin)**

- Maintain atomic counters: inFlight, queue depths, 429s, rejects.
- Hook job store + lock service + worker pools.
- `GetSnapshot()` + event `SnapshotUpdated`.
- SignalR hub method or hosted service push to `admin:ops` every 1–2s while any admin connected (or always at low rate).

**Exit C:** two users gen different scenes concurrently (fakes); same scene → 409; admin dashboard shows live jobs (read-only).

---

### Phase D — Web UX (users) + admin console

**D1. User UX (minimum)**

- Show **my jobs** + queue position.
- Scene lock badge on Scenes list.
- Disable Gen when locked by other user.

**D2. Admin live dashboard (`/admin`)**

- Login gate → real-time panels (process, capacity, running jobs, queues, locks).
- SignalR client subscribed to `admin:ops` / `AdminState` messages.
- Fallback poll `GET /api/admin/state`.
- Actions: cancel job; force-release lock (confirm modal).

**D3. Admin server configuration (`/admin/config`)**

- Forms bound to `RuntimeConfigDto`.
- Save → `PUT /api/admin/config` → persist + hot-apply + audit line + SignalR notify.
- Show effective vs file values; “restart required” badge if a setting cannot hot-reload (e.g. switching UseFakes mid-flight may be restart-only).

**D4. Nav + security**

- Hide admin links from non-admins.
- `[Authorize(Roles = "admin")]` on admin pages and APIs.
- Rate-limit login attempts (simple in-memory).

**Exit D:** human multi-user + admin can watch LoadSim live and tune caps without rebuild.

---

### Phase E — LoadSim + soak (+ admin validation)

- Ship `FilmStudio.LoadSim` (below).
- CI job (optional, nightly): 50 VUs × 2 min with fakes.
- Manual soak: 100 VUs × 10 min; capture metrics.
- **Admin check:** during soak, open `/admin` and confirm counters move (inFlight, queues, jobs); change `MaxVideoInFlight` live and observe queue behavior.

**Exit E:** documented numbers + pass/fail thresholds + admin console verified under load.

---

## 6. FilmStudio.LoadSim (client simulator)

### 6.1 Project type

- `host/FilmStudio.LoadSim/FilmStudio.LoadSim.csproj` — **console** `net10.0`
- References: `FilmStudio.Core` (DTOs only) or raw HttpClient + SignalR.Client
- **No** dependency on Engine (client-only)

### 6.2 CLI

```text
dotnet run --project host/FilmStudio.LoadSim -- \
  --baseUrl http://127.0.0.1:5088 \
  --users 100 \
  --duration 600 \
  --scenario mixed \
  --projectPrefix sim \
  --thinkTimeMs 500 \
  --genWeight 0.15 \
  --playWeight 0.4 \
  --browseWeight 0.35 \
  --reviewWeight 0.1 \
  --maxGenPerUser 1
```

### 6.3 Virtual user (VU) model

Each VU:

```text
UserId = $"u{index:D3}"
Header X-User-Id: u001
Optional X-Api-Key: sim-u001   // if API accepts override for fakes
ProjectId = per-user project OR shared "Buster" (flag --sharedProject)
```

**Lifecycle loop** until duration elapsed:

1. Think delay (`thinkTimeMs` ± jitter)
2. Weighted random action from scenario
3. Record metrics (latency, status code, errors)

### 6.4 Scenarios

| Scenario | Actions |
|----------|---------|
| `browse` | GET health, projects, scenes, scene detail |
| `play` | GET clip/composite/wip video (first bytes / HEAD or short range) |
| `review` | POST clip pass/fail (if project allows) |
| `gen` | POST gen-scene (onlyMissing) for assigned scene set |
| `remux` | POST remux scene / WIP (low weight) |
| `mixed` | weights from CLI |

**Scene assignment:** `scene = (userIndex % sceneCount) + 1` or fixed range per user to reduce lock conflicts; optional `--forceLockCollisions` for stress.

### 6.5 SignalR (optional mode)

- `--signalr true`: connect hub, join implied user group, count progress messages.
- Measure reconnects under load.

### 6.6 Metrics output

- Console summary + `loadsim-results.json`:

```json
{
  "users": 100,
  "durationSec": 600,
  "actions": { "browse": 12000, "gen": 80, "play": 4000 },
  "http": { "p50Ms": 12, "p95Ms": 80, "p99Ms": 200, "errors": 3 },
  "jobs": { "submitted": 80, "completed": 76, "failed": 2, "rejected": 2 },
  "server": { "notes": "optional /api/capacity snapshots" }
}
```

### 6.7 Pass/fail gates (defaults)

| Gate | Default |
|------|---------|
| Error rate | &lt; 1% (excluding intentional 409 lock) |
| Process under test still healthy | GET /health 200 |
| p95 browse | &lt; 500 ms (fakes, local) |
| No runaway memory | manual / dotnet-counters in soak doc |

### 6.8 Running against fakes

```text
# Terminal 1
set FilmStudio__UseFakes=true
set FilmStudio__Capacity__MaxVideoInFlight=12
dotnet run --project host/FilmStudio.Api

# Terminal 2
dotnet run --project host/FilmStudio.LoadSim -- --users 100 --duration 300 --scenario mixed
```

**Do not** point LoadSim at production with real keys and high gen weight.

---

## 7. API additions (summary)

| Endpoint | Purpose | Auth |
|----------|---------|------|
| `GET /api/capacity` | public/limited caps (optional) | user or anon |
| `GET /api/jobs` | filter mine/project | user |
| `GET /api/jobs/{id}` | detail | user (own) or admin |
| `POST /api/jobs/gen-scene` | + require user; acquire scene lock | user |
| `POST /api/jobs/remux` | locks | user |
| `POST /api/auth/login` | admin/user login | public |
| `POST /api/auth/logout` | clear session | auth |
| `GET /api/auth/me` | userId + roles | auth |
| `GET /api/admin/state` | full live server snapshot | **admin** |
| `GET /api/admin/config` | runtime config | **admin** |
| `PUT /api/admin/config` | update capacity/fakes | **admin** |
| `POST /api/admin/jobs/{id}/cancel` | force cancel | **admin** |
| `POST /api/admin/locks/release` | force unlock | **admin** |
| Headers (sim/dev) | `X-User-Id`, optional `X-Api-Key` | — |
| SignalR group | `admin:ops` | **admin** only |

---

## 8. Testing strategy

| Layer | What |
|-------|------|
| Unit | RR scheduler, lock TTL, coalesce WIP flag, queue caps, config validation |
| Integration | `WebApplicationFactory` + fakes; 2 users concurrent gen different scenes; admin login → state/config |
| Load | LoadSim 100 VUs fakes; admin dashboard open during soak |
| Manual | 2 browsers + admin browser; real or fake keys |
| Security | Non-admin 401/403 on `/api/admin/*`; login brute-force limited |

---

## 9. Risks & mitigations

| Risk | Mitigation |
|------|------------|
| Blazor Server memory at 100 circuits | Prefer LoadSim → **API only**; measure Web separately; later WASM |
| File corruption | Scene locks + atomic file writes |
| Fake mp4 won’t play | Ship real tiny fixture files |
| Global job rewrite breaks UI | Compatibility shim on `/api/jobs` |
| Real key leak in sim | Forbid gen scenario without `UseFakes` unless `--i-know-what-im-doing` |
| Admin password in repo | Env/secret only; hashed at rest; no default prod password |
| Admin SignalR spam at 1Hz × heavy snapshot | Delta payloads; only push when admins connected; cap 1–2 Hz |
| Hot config breaks running jobs | Document restart-required flags; apply caps on *next* dequeue |

---

## 10. Suggested calendar (indicative)

| Phase | Effort (order of magnitude) |
|-------|-----------------------------|
| A Foundations + fakes | 3–5 days |
| B Identity + keys + **admin login** | 2–3 days |
| C Locks + workers + **metrics feed** | 4–6 days |
| D User UX + **admin dashboard + config** | 4–5 days |
| E LoadSim + soak + admin under load | 2–3 days |

**First vertical slice (1 week goal):** A1–A3 + B1/B4 stub admin login + `GET /api/admin/state` skeleton + LoadSim browse 50 VUs.

---

## 11. Immediate next PR (start here)

1. Create `FilmStudio.Fakes` + `IGrokVideoClient` extraction + `UseFakes` switch.  
2. Create `FilmStudio.LoadSim` with **browse-only** 100 VUs (no gen).  
3. Add `GET /api/capacity` stub (static caps + process uptime).  
4. Stub **admin login** + `GET /api/admin/state` (process uptime only) + empty `/admin` page.

Then iterate multi-job + locks + live metrics + config page + gen actions in sim.

---

## 12. Decision log

| Decision | Choice |
|----------|--------|
| Primary bottleneck with per-user keys | Server workers + Blazor, not shared Grok |
| Fairness | Global max in-flight + per-user cap + optional RR among users |
| WIP | Single-flight coalesce, local pool |
| Auth v1 users | `X-User-Id` header (sim + dev); JWT/cookie later |
| Auth admin | Dedicated login; `role=admin`; cookie or JWT |
| Admin live updates | SignalR group `admin:ops` + 1–2s snapshot ticks |
| Server config | Runtime file + hot-apply caps; audit log |
| Test money | Fakes always for CI/load |
| Work-stealing deque for WIP | Rejected; use single-flight + optional parallel stale remux |

---

*Document version: 2026-07-17b — adds admin login, live ops dashboard, server configuration page.*
