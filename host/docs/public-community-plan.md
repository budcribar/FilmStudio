# Public community & multiplayer features (plan — not implemented)

Backlog for **project collaborators**, **public demos / modes**, **fork → contribution → owner merge**, and **upvotes**.  
Nothing in this doc is required for current production unless explicitly scheduled.

**Status key:** `idea` · `planned` · `in progress` · `done`

**Priority note:** **Project collaborators** (same project, trusted invite) ship **before** open fork/merge. Upvotes can ship independently for the gallery.

---

## Feature list

| # | Feature | Status | Notes |
|---|---------|--------|--------|
| 1 | **Project collaborators** | **planned** | Owner invites users to **same** project; see § Project collaborators. **Higher priority than fork/merge.** |
| 2 | **Demo ratings (upvotes only)** | **done** (basic) | ★ on `/demo`; sort top/new. See § Demo ratings. |
| 3 | Project / listing mode: **private · public · open** | **planned** | Locked matrix: see § Visibility modes. |
| 4 | Content hash of exportable package | idea | Freshness for contributions (fork PRs). |
| 5 | **Fork** (plan-only package v1) | idea | Copy story/cast/blueprint; no clip binaries; **open** only. |
| 6 | Fork banner + “forked from” metadata | idea | |
| 7 | **Contribution** (prompt/JSON PR) | idea | Field-level accept by owner. |
| 8 | Contribution accept / reject + conflict if origin moved | idea | See conflict notes under fork. |
| 9 | Sync fork from origin (rebase helper) | idea | |
| 10 | Media-aware fork / PR (hash upload) | idea | Later; storage + quotas. |
| 11 | Public project gallery (forkable packs) | idea | Distinct from finished-movie demos. |
| 12 | Project ratings (reuse upvote model) | idea | After public/open listings exist. |
| 13 | Text reviews on ratings | idea | Moderation burden; not v1. |
| 14 | User reputation from ratings | idea | Derived later. |

---

## Roles (lifecycle, not account types)

| Role | Meaning |
|------|---------|
| **Anon** | Browse **public** / **open** demos in gallery; play if streamable; no fork, no upvote. |
| **Signed-in** | Same + **upvote** demos; **fork** only when mode is **open**; may be **invited** as project member. |
| **Project owner** | `ownerUserId`; invite/remove members; visibility/public/open; delete project; publish demo (as today). |
| **Project editor** (collaborator) | Member of **same** project; edit/gen/review per policy; **not** owner admin actions. |
| **Fork owner / community contributor** | Same user **after** **fork** of an **open** project; edit copy; submit contribution (later). |
| **Upstream owner** | Accept/reject community contributions (later). |

“Collaborator” = invited to the **same** project.  
“Community contributor” = forked **open** project and optional PR — different path.

---

## Project collaborators (planned — high priority)

### Intent
Trusted multiplayer on **one** project (co-writer, editor). **Not** a fork: no copy, no merge UI for day-to-day work.

### Why before fork/merge
- Solves “add my partner” without public/open.
- Works on **private** projects.
- Smaller than content-hash PRs and gallery packaging.
- Scene locks / jobs already partly multi-user; need **ACL** so non-owners can open the project.

### Model (v1)

```text
project.json (or SQLite project_members)
  ownerUserId: "alice"
  members: [
    { "userId": "bob", "role": "editor", "addedAt": "..." }
  ]
```

| Role | Can |
|------|-----|
| **owner** | Full control: members, visibility/public/open, delete project, transfer (later), publish demo as owner |
| **editor** | Use studio on this project: read/write blueprint, cast, gen jobs, review, play; **cannot** remove owner, delete project, or change membership/visibility |
| **viewer** (optional v1.1) | Read-only play / view |

- **One owner** (existing `ownerUserId`).
- Invite by **username** / user id (users already in SQLite).
- Owner **remove** member; member **leave**.
- Project list: projects where user is **owner or member**.

### AuthZ
- Every project-scoped API: allow if `admin` **or** `userId == owner` **or** `userId` in members with sufficient role.
- Demo publish: keep owner (or allow editor later — default **owner only** for v1).
- Jobs/locks: members may acquire scene locks like owner.

### Credits / API keys (v1 policy)
- **Acting user** pays: gen uses **that user’s** provider keys and credits (Bob gens → Bob’s spend).
- Document clearly in UI (“Your API keys and credits are used when you generate”).
- Later option: “bill owner” with explicit consent.

### Client media folder
- Each collaborator uses **their** browser media folder; server registry is still **per project**.
- Expect each editor to connect a folder; gen handoff unchanged.

### Suggested API (future)
```http
GET    /api/projects/{id}/members
POST   /api/projects/{id}/members      { "username" or "userId", "role": "editor" }
DELETE /api/projects/{id}/members/{userId}
POST   /api/projects/{id}/members/leave
GET    /api/projects                        # include owned + member-of
```

### Out of scope for collaborators v1
- Live cursors / presence avatars  
- Per-scene or per-clip ACL  
- Email magic-link invites (username invite is enough)  
- Automatic fork from invite  
- Split billing  

### Depends on
- Existing `ownerUserId` on project + user accounts.

### Does not depend on
- Public/open modes, fork, contributions, upvotes.

### Collaborators vs fork

| | Collaborator | Fork |
|--|--------------|------|
| Project | **Same** id | **New** id |
| Trust | Invite-only | Open community |
| Merge | N/A (same project) | Contribution accept |
| Modes | Works on **private** | Requires **open** |

---

## Visibility modes (locked)

Owner chooses one mode for the **public surface** of their work (demo listing and, when present, linked project package).

| Mode | Others can **play** (demo stream / gallery) | Others can **fork** (studio package) |
|------|-----------------------------------------------|--------------------------------------|
| **Private** | No | No |
| **Public** | Yes | No |
| **Open** | Yes | Yes |

### Rules
- **Default:** **private** (not in gallery; no public play; no fork).
- **Public:** listed (after any demo moderation); watch-only; **no** Fork button.
- **Open:** same as public **plus** Fork (plan-only package v1 when implemented).
- No separate “unlisted” mode in v1 (add later only if share-by-link without gallery is needed).
- Demo **publish** today still goes **pending → public** for the movie file; align product language so “make public” / “make open” sets this matrix (implementation later).
- Upvotes apply only when the demo is playable in the public gallery (**public** or **open** approved demos).
- **Collaborators** can work on a project in any mode; membership is independent of public/open.

### Naming
| Internal | UI (examples) |
|----------|----------------|
| `private` | Private |
| `public` | Public (watch only) |
| `open` | Open (watch + fork) |

---

## Demo ratings (basic implementation shipped)

Implemented: `DemoUpvoteService` (SQLite `demo_upvotes`), `POST/DELETE /api/demos/{id}/upvote`, gallery `sort=top|new`, Demo page ★ button.

### Intent
Lightweight quality signal for **approved public demos**. Independent of fork/merge and of collaborators.

### Model: **upvotes only** (chosen)
- One control: **★ / upvote** (toggle on/off), not 1–5 stars and not downvotes.
- **Signed-in only**; **at most one upvote per user per demo** (add or remove).
- UI shows **upvote count** (and whether *I* upvoted).
- Gallery **rankings by most upvotes** (descending count). Tie-break: newer publish, or title.

Why this shape:
- Simple mental model (“star this demo”).
- No revenge downs / 1★ brigading.
- Ranking is just a sort key — no averages or Bayesian priors required for v1.
- Matches “most stars” language without a 5-star scale.

### v1 product rules (when built)
- Target: demos that are gallery-playable — modes **public** or **open** (catalog status approved/`public` as today).
- **No self-upvote** (recommended).
- **No free-text review**.
- Unpublish / remove → drop from gallery; keep or delete votes (prefer delete or hide).
- Does **not** gate play; admin approve/reject stays the publish gate.
- Optional secondary sorts later: **New**, **Trending** (upvotes in last N days).

### Ranking
| Sort | Definition |
|------|------------|
| **Top (default for “ranked”)** | `upvoteCount` DESC, then `createdAt` DESC |
| **New** | `createdAt` DESC (ignore votes) |
| **Trending** (later) | upvotes with `updatedAt` in last 7d, or time-decayed score |

No minimum vote threshold required for “Top” if the only signal is count (a single upvote legitimately ranks above zero). Optional: pin admin “featured” above organic Top.

### Suggested API (future)
```http
POST   /api/demos/{id}/upvote      # idempotent: ensure my upvote exists
DELETE /api/demos/{id}/upvote      # remove my upvote
GET    /api/demos?sort=top|new     # include upvoteCount, upvotedByMe
GET    /api/demos/{id}             # same
```

### Storage (future)
```text
demo_upvotes (
  demo_id   TEXT NOT NULL,
  user_id   TEXT NOT NULL,
  created_at TEXT NOT NULL,
  PRIMARY KEY (demo_id, user_id)
)
```
- `upvoteCount` = `COUNT(*)` per demo (or denormalized counter on demo meta, updated on vote).

### Out of scope for demo-ratings v1
- 1–5 star scales  
- Downvotes  
- Clip-level votes  
- Contribution/PR votes  
- Fork inherits upvotes (no)  
- Anon voting  
- Text reviews  

### Depends on
- Existing public demo catalog + auth JWT (already present).

### Does not depend on
- Project fork/merge or collaborators.

---

## Fork → merge (summary — planned later)

Community path for **strangers** on **open** projects. Prefer **collaborators** for trusted co-production on one project.

### When fork is allowed
- Only if owner mode is **open** (play yes, fork yes).
- **Public** (play only): no fork CTA.
- **Private:** no play, no fork.
- Demo page may show **Fork** when the listing is **open** and a forkable project package exists; play uses the demo movie snapshot either way.

### v1 fork
- Copy L0–L4: source/screenplay, cast, blueprint, rules, config (strip secrets).
- Skip or reset: cost ledger, private review state, client-only MP4 bytes (plan-only).
- New `projectId`, `ownerUserId` = forker; record `sourceProjectId` + hash at fork.

### v1 contribution
- Structured diff (blueprint clip fields, cast fields, optional screenplay).
- Owner accepts selected paths; reject if upstream `contentHash` ≠ contribution base (rebase).
- Field-level conflict resolution (3-way vs base): keep origin / take fork / skip — no silent text merge.

### Merge atoms: characters vs scenes (locked direction)
| Layer | Default merge / conflict unit | UI |
|-------|-------------------------------|-----|
| **Characters** | **Character package** (description, visual_lock, voice, optional ref); optional expand to fields | Group under Cast |
| **Scenes / clips** | **Clip field** (e.g. `S02C03.visual_prompt`, dialogue bundle); not whole scene by default | Group by scene → clips |
| **Whole scene** | Bulk “select all clips in Sxx” only — not a primitive blob replace | |
| **Coupling** | Character and clip accepts are **independent**; optional “N clips in this PR use this character” hint | |
| **Structure** | v1: edit existing `SxxCyy` / characters only — no silent add/delete/reorder in PR | |

Characters are **global** (high blast radius); scenes are a **timeline of clips**. Prefer character-level packages and clip-level fields; do not force atomic “whole scene” or “character + all clips” accepts in v1.

## Project Collaborators & Invite-to-Fork Workflow

### Intent
Instead of forcing multiple users to edit the same live project files simultaneously (which risks file lock conflicts and overwritten edits), PageToMovie uses an **Invite-to-Fork & Async Diff-Merge** collaboration model.

### Workflow
1. **Invite & User Search**: Project Owner A opens the **Collaborate & Invite** modal in the UI.
   - *Public Handle Search*: Owner A can type `@username` to search existing creator handles. The API queries SQLite `users` table (`username` column) and returns public handles only — **raw email addresses are never returned to the browser**.
   - *Blind Email Delivery*: Owner A can type a recipient's direct email address (`partner@example.com`). The server dispatches the invite link via Resend API without revealing to the client whether an account exists for that email.
2. **Instant Fork**: User B accepts the invite via in-app notification or email link (`/join?token=inv_...`). A lightweight fork (`Project A (Fork)`) is created in User B's project area (< 5 MB ZIP size, containing screenplay, cast seeds, reference images, and shot plan blueprint; excluding video binaries).
3. **Independent Local Work**: User A and User B work independently on their own client storage (IndexedDB / OPFS / local PC folder). Neither user blocks or locks the other's workspace.
4. **Contribution Submission**: User B completes edits (e.g. prompt tuning or beat timing changes) and clicks "Submit Contribution to Owner".
5. **Diff Review & Merge**: Owner A receives a notification, views a side-by-side visual diff grouped by **Cast** and **Scenes/Clips**, and accepts/merges the changes into master `Project A`.

---

## Demo Gallery & YouTube Video Hosting

### Intent
Zero-server-disk public demo gallery powered by YouTube video embeds. Eliminates Railway disk usage and server streaming bandwidth for public demo videos while driving views and engagement to your YouTube Channel.

### Model: **YouTube Embeds + Upvotes**
- **Public Video Stream**: Demos are published with a YouTube Video ID (`youtube_id`) or YouTube URL.
- **Embedded Player**: The public `/demo` page renders an embedded, privacy-enhanced YouTube iframe player (`<iframe src="https://www.youtube-nocookie.com/embed/{youtubeId}"></iframe>`).
- **Server Footprint**: **0 MB for video files**. `demo.json` / SQLite stores only metadata (title, author, screenplay snippet, upvote count, YouTube ID).
- **Publishing Methods**:
  1. *Manual URL*: Admin/user inputs YouTube link or ID upon demo approval.
  2. *API Auto-Upload (Optional)*: Railway server uploads approved demo to YouTube Channel via YouTube Data API v3 (`videos.insert`) and immediately deletes the temporary local MP4 file.

---

## Suggested ship order

1. **Demo upvotes + rank by most upvotes** — **done (basic)** on `/demo`.  
2. **YouTube Demo Gallery Hosting** — store `youtube_id` in demo metadata, render YouTube iframe player in `/demo`, zero server video disk usage.
3. **Client Media Storage & Server Media Pruner** — Railway background 48h TTL media pruner (`ServerMediaPruningService.cs`) + client-side IndexedDB/OPFS fallback.
4. **Lightweight Project Export & Fork Packaging** — update `ProjectArchiveService` to export light ZIP packages (< 5 MB) excluding `.mp4` binaries.
5. **Project collaborators** (owner + editors, invite by username, project list includes member-of).  
6. **Wire private / public / open** (play/fork matrix) on publish/listing.  
7. **Prompt/JSON contributions** + owner merge + conflict UX.

---

## Related code today

| Area | Location |
|------|----------|
| Project owner | `project.json` / `ownerUserId` via `ProjectStore` |
| Publish demo permission | `CanUserPublishDemoAsync` (owner match) |
| Users | SQLite `pagetomovie.db` / `UserDatabaseService` |
| Demo catalog / moderate | `DemoCatalogService`, admin demos UI, YouTube embed in `Demo.razor` |
| Server Media Pruner | `ServerMediaPruningService.cs` (Railway 48h TTL purge) |
| Client media hashes | media registry (per project; per-browser folder / IndexedDB) |
| Project export & import | `ProjectArchiveService.cs` (lightweight packaging) |

---

*Last updated: 2026-07-26 — updated with Client MP4 Storage, Server Media Pruner, YouTube Demo Hosting, and Lightweight Project Export/Fork packaging.*
