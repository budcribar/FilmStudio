# Prototype look-feel branch

**Branch:** `feature/prototype-look-feel` (from `master`)

## What this is

A **UI look-and-feel** port of the Grok sandbox TanStack prototype onto the **real Blazor PageToMovie app** — not a rewrite of Engine/Api.

## Product path (prototype language)

```
Book → Cast → Voice (optional) → Estimate → Film / Review
```

Mapped onto existing routes:

| Prototype step | Existing route |
|----------------|----------------|
| Book | `/adaptation` (import + screenplay + shots) |
| Cast | `/characters` |
| Voice | still on Characters (voices panel) |
| Estimate | `/cost` |
| Film | `/scenes` |
| Review | `/review` |
| Settings | `/configuration` (nav label **Settings**) |

## Files touched

- `host/PageToMovie.Web/wwwroot/app.css` — cinema steel tokens + path-step/hero styles  
- `host/PageToMovie.Web/Components/Layout/MainLayout.razor.css` — sidebar gradient  
- `host/PageToMovie.Web/Components/Layout/NavMenu.razor(.css)` — order/labels, accent  
- `host/PageToMovie.Web/Components/Pages/Home.razor` — hero copy + path steps  

## Not in this branch

- Replacing Blazor with TanStack  
- New backend endpoints  
- Force-push to `master`  

## Run (local / VS)

```bash
cd host
export PageToMovie__WorkspaceRoot="$(cd .. && pwd)"
export ASPNETCORE_URLS="http://0.0.0.0:8080"
dotnet run --project PageToMovie.Api
```

Open the UI on port 8080 (or your usual `5088` if preferred).


## Pass 2 (this update)

- **Adaptation step strip** restyled as numbered cinema cards: Book → Screenplay → Cast & voice → Shot plan  
- Unlock rules unchanged (`OutlineEnabled` / `ShotsEnabled` / job lock)  
- Hub `/adaptation` wait copy  
- Import north-star drop copy  
- Settings / Estimate & cost labels  
- Characters page title → Cast & voice  
