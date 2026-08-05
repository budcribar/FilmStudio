# UI audit report (fakes mode)

**Pass 1 (route walk):** 2026-08-05T13:19:56Z  
**Pass 2 (docs + sequence plan / static guards):** 2026-08-05 (this update)  
**Base:** `http://127.0.0.1:5088` · `useFakes=true`

## Coverage honesty

| Kind of test | Pass 1 | Status |
|--------------|--------|--------|
| Routes load without hard exception text | Yes | Done |
| Create/delete project (API) | Yes | Done |
| Create project via UI selectors | Partial (missed `home-new-project` / Manage) | Open |
| **Sequence matrix** (no project → book → screenplay → estimate → film) | **No** | **In progress** |
| **Button enable/disable vs readiness** | Not verified in browser | **In progress** |
| **Input boundaries** (empty, 0, negative, over max) | Not exercised | **In progress** |
| Double-submit / busy re-entry | No | Pending |
| Full fake movie gen → review | No | Pending (fixtures enhanced) |

Pass 1 was **route coverage**, not sequence / control-state testing.

## Pass 1 issues (automated)

1. **[low]** (home) 1 visible img missing alt  
2. **[medium]** (`/film`) Main content empty — no `@page "/film"` (Film stage is `/scenes`)  
3. **[medium]** (`/billing`) Main content empty — costs live at `/account/costs`  
4. **[high]** (projects) Audit script did not find create control — real control is `data-testid="home-new-project"`; form often under Manage  
5. **[medium]** (cost) No length number input when project id not bound yet  
6. **[medium]** (console) At least one 404 resource  

## Static review — guards that exist

- `ActiveProjectState`: `CanCharacters`, `CanEstimate`, `CanScenes` + `*BlockedReason`  
- `StudioProcessStrip`: disabled steps → `javascript:void(0)` when not ready  
- Import: `ImportReady`, dropzone/file input disabled when busy / not ready  
- Screenplay: edit/insert tools `disabled="@(!CanEdit)"`  
- Many actions: `disabled="@(Busy || JobRunning)"`  
- `FilmLengthCard`: `min="1"` `max="180"`, save `Math.Clamp(_edit, 1, 180)`  

## Static review — weak / missing guards (suspect bugs)

1. **Agree & Continue** (`Cost.razor`) — only `disabled="@_busy"`; navigates to `scenes` even when `CanScenes` is false.  
2. **Create project blank name** — handler returns silently if `_newName` whitespace; no “name required” message.  
3. **Deep links** (`/cost`, `/scenes`, `/characters`) with no active project — soft empty vs hard CTA.  
4. **Strip vs page CTAs** — strip can show Film disabled while Estimate primary still advances.  

## Sequences still requiring explicit browser tests

1. No project → Import / Characters / Cost / Scenes (every primary button + empty copy)  
2. Project, no book → same  
3. Book, no approved screenplay → Cast / Estimate / Film  
4. Screenplay OK, no shots → Estimate vs Film  
5. Ready to film → Generate once; disabled while `JobRunning`; no double-submit  
6. Length input: `""`, `0`, `-1`, `1`, `180`, `181`, non-numeric  
7. Create: empty, spaces, duplicate, double-click  
8. Back-nav: Film → Import → change book → Film (stale enable?)  
9. Busy/job: competing actions disabled  

## Notes (pass 1)

- Agree & Continue control present when project bound  
- API create/activate/delete OK under fakes  
- Import file input present  
- Enhanced fake MP4 fixtures shipped (`0bf913a`) for later gen soaks  
