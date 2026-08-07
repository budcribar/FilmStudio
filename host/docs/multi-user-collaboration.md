# Multi-user collaboration

PageToMovie supports multiple users on the same project via ACL, leases, presence, Git auto-merge, scene version history, and a cost ledger split.

## Roles (ACL)

| Role | Capabilities |
|------|----------------|
| **Owner** | Full control, grant/revoke editors & viewers, force-release leases |
| **Editor** | Mutate project content, acquire leases, push/sync |
| **Viewer** | Read-only |

Stored in `project-acl.json` under the project directory. Share UI: `/studio/share` (`Share.razor` + `SharePanel`).

### HTTP (ACL / leases / presence / rev)

| Method | Path | Access |
|--------|------|--------|
| GET | `/api/projects/{id}/acl` | viewer+ |
| POST | `/api/projects/{id}/acl/editors` body `{ "userId" }` | owner |
| DELETE | `/api/projects/{id}/acl/editors/{userId}` | owner |
| POST | `/api/projects/{id}/acl/viewers` | owner |
| DELETE | `/api/projects/{id}/acl/viewers/{userId}` | owner |
| POST | `/api/projects/{id}/leases/{resourceKey}/acquire` | editor+ |
| POST | `.../release` `.../renew` `.../transfer` | holder / editor |
| GET | `.../leases/{resourceKey}` | viewer+ |
| GET | `.../presence` | viewer+ |
| GET/POST | `.../rev` | viewer+ / editor+ |

Resource keys for leases: `project`, `script`, or `scene:{n}`. Second acquirer gets **HTTP 423** with holder identity.

**Legacy projects:** if no `project-acl.json` exists, access falls back to the `owner` segment of `projectId` (`owner/name`).

## Presence (SignalR)

- Hub: `/hubs/project`
- Methods: `JoinProject` / `LeaveProject` / `Heartbeat`
- Events: `PresenceChanged`, `LeaseChanged`
- Server: `ProjectPresenceService` (in-memory heartbeats)

**UI polish still open** — Share / Scenes can list "who's online" from `GET .../presence` or hub events (infrastructure is ready; presentation is not finished). See notes at the end of this doc.

## Git auto-merge

When a fork syncs from origin, non-overlapping text/JSON conflicts can be resolved automatically.

### Engine

- `AutoTextMerger` — three-way merge (`base` / `ours` / `theirs`)
  - Strategies: **Auto**, PreferOurs, PreferTheirs, Union
  - Non-overlapping hunks auto-resolve; overlapping hunks keep conflict markers
  - JSON: key-by-key merge where safe
- `ProjectGitRepositoryService.SyncForkFromOriginWithAutoResolveAsync` — stages resolved paths and commits when clean

### API

- `POST /api/merge/text` — `{ baseText, oursText, theirsText, strategy? }`
- `POST /api/merge/json` — same for JSON documents
- `POST /api/merge/auto` — project-level helper where wired

### UI

**Contribution Review** shows remaining conflict paths, auto-resolved count, and strategy buttons when conflicts remain.

## Scene version history (P3)

Snapshots of a scene's state (and optional media) for list / restore.

### Storage

```
{projectDir}/scene-versions/{sceneKey}/{versionId}/
  meta.json
  scene-state.json   (optional)
  media/…            (optional copies)
```

`sceneKey` is typically `scene:{n}` where `n` is the scene number (Scenes page).

### Engine

`SceneVersionStore` (`Engine/Collaboration`):

- `SnapshotAsync` — write meta + optional state/media
- `ListHistoryAsync` — newest first
- `RestoreAsync` — return stored state; UI reloads project data after success

### API

| Method | Path |
|--------|------|
| `GET` | `/api/projects/{projectId}/scenes/{sceneKey}/versions` |
| `POST` | `/api/projects/{projectId}/scenes/{sceneKey}/versions` body: `{ note?, createdBy?, sceneStateJson? }` |
| `POST` | `/api/projects/{projectId}/scenes/{sceneKey}/versions/{versionId}/restore` |

### UI

On **Scenes**, with a scene selected:

1. **Scene history** toggles `SceneVersionHistory`
2. **Snapshot** — POST with optional note
3. **Restore** — confirmation dialog (version id + note + timestamp), then POST restore and reload that scene's data while keeping the same scene selected

## Cost split (adaptation vs video)

Estimates and actuals are always split into two line items (even when a side is `$0`).

### Engine

- `ProjectCostAggregator.BuildSummary(projectId, projectsRoot, ledger?)`
  - **AdaptationEstimateUsd** — planning rate × scene count
  - **VideoEstimateUsd** — planning rate × clip count (video + audio)
  - **TotalEstimateUsd** — sum
  - Actuals from ledger when present
  - `EstimateLines` / `ActualLines` for tables and charts
- `CostLedgerService` — append-only JSONL at `{project}/cost-ledger.jsonl`
  - Categories `adaptation` / `llm` / `stage1` → adaptation
  - Everything else → video

Planning rates are placeholders until catalog-backed pricing is wired (other track).

### API

| Method | Path | Body / result |
|--------|------|----------------|
| `GET` | `/api/projects/{id}/costs/summary` | Full cost summary DTO |
| `POST` | `/api/projects/{id}/costs/record` | `{ category, usd, note?, modelId? }` |

### UI

`/cost/breakdown` (`ProjectCosts.razor`) charts adaptation vs video for estimates and spend.

## Headers for multi-user testing

| Header | Purpose |
|--------|---------|
| `X-User-Id` | Logical user id (leases, ACL, presence) |
| `X-User-Name` | Display name when granting editors |

Playwright and unit tests use these in lab/fake mode instead of full auth.

## Presence UI polish (what's left)

Infrastructure is in place; the product gap is **presentation**.

**Already available**

- `GET /api/projects/{id}/presence` — current online user ids
- SignalR hub `/hubs/project` with `JoinProject` / `Heartbeat` / `PresenceChanged`

**Suggested UI (not built on this branch)**

1. **Share page** — a small "Online now" list next to editors/viewers (avatar or initials + user id), refreshed on `PresenceChanged` or a 15–30s poll of `GET .../presence`
2. **Scenes / editor chrome** — optional badge "3 online" that expands to names; helps before acquiring a lease
3. **Stale handling** — treat missing heartbeats as offline (server already expires entries); UI should not show ghosts
4. **Self indicator** — show "you" distinctly so the list is not confusing in dual-user tests

Until that lands, presence is observable only via API/hub, not a first-class studio surface.

## Still open (this track)

- Auto-snapshot on successful generate (video/audio/plan)
- Presence list on Share / Scenes (UI polish above)
- Catalog prices for cost aggregator — **other agent**
