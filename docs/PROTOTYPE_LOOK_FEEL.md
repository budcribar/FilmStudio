# Prototype look-feel branch

**Branch:** `feature/prototype-look-feel` (from `master`)  
**Status:** Integrated look-and-feel pass complete — ready to check out in VS.

## What this is

A **UI look-and-feel + product-language** port of the Grok sandbox prototype onto the **real Blazor PageToMovie app**. Engine/Api behavior is unchanged.

## Product path (everywhere in chrome)

```
Book → Cast & voice → Estimate → Film → Review
```

| Step | Route | Notes |
|------|-------|--------|
| Book | `/adaptation` → import / screenplay / shots | Hub uses `SuggestedStepPath` |
| Cast & voice | `/characters` | Looks required; voice optional add-on |
| Estimate | `/cost` | Quote / actuals before full spend |
| Film | `/scenes` | Clips + client stitch |
| Review | `/review` | Edit / publish afterward |
| Settings | `/configuration` | Keys, models, media folder |
| Demo | `/demo` | Public gallery |

## What shipped (all passes)

### Theme & shell
- Cinema steel CSS tokens (`app.css`)
- Sidebar gradient + active accent
- Nav: **Home · Demo · Settings** + Book / Cast / Film / Review / Estimate

### Home
- Cinema hero + north-star lede
- Path step cards (Book → Cast → Voice → Estimate·film)
- Active project tiles: Book · Cast & voice · **Estimate** · Film · Review

### Adaptation
- Numbered step strip: Book → Screenplay → Cast & voice → Shot plan
- Unlock rules **unchanged**
- Import drop-in copy; hub wait message

### Studio pages
- Cast & voice header + optional voice badge
- Film (Scenes) / Review / Estimate page heads
- Login subtitles aligned to studio story

## Files (primary)

```
host/PageToMovie.Web/wwwroot/app.css
host/PageToMovie.Web/Components/Layout/MainLayout.razor.css
host/PageToMovie.Web/Components/Layout/NavMenu.razor
host/PageToMovie.Web/Components/Layout/NavMenu.razor.css
host/PageToMovie.Web/Components/Pages/Home.razor
host/PageToMovie.Web/Components/Pages/AdaptationShell.razor
host/PageToMovie.Web/Components/Pages/Adaptation.razor
host/PageToMovie.Web/Components/Pages/AdaptationImport.razor
host/PageToMovie.Web/Components/Pages/AdaptationScreenplay.razor
host/PageToMovie.Web/Components/Pages/AdaptationShots.razor
host/PageToMovie.Web/Components/Pages/Characters.razor
host/PageToMovie.Web/Components/Pages/Scenes.razor
host/PageToMovie.Web/Components/Pages/Review.razor
host/PageToMovie.Web/Components/Pages/Cost.razor
host/PageToMovie.Web/Components/Pages/Configuration.razor
host/PageToMovie.Web/Components/Pages/Demo.razor
host/PageToMovie.Web/Components/Pages/Login.razor
docs/PROTOTYPE_LOOK_FEEL.md
```

## Not in this branch

- Replacing Blazor with TanStack  
- New Engine/API endpoints  
- Changing `SuggestedStepPath` / job unlock rules  
- Force-push to `master`  

## Check out (VS)

```bash
git fetch origin
git checkout feature/prototype-look-feel
```

```bash
cd host
export PageToMovie__WorkspaceRoot="$(cd .. && pwd)"
# optional: export PageToMovie__UseFakes=true
dotnet run --project PageToMovie.Api
```

Open the UI (default often `http://127.0.0.1:5088` or set `ASPNETCORE_URLS=http://0.0.0.0:8080`).

## Smoke checklist

1. Home hero + 5 workflow tiles  
2. Nav labels (Settings, Book, Cast, Estimate…)  
3. Adaptation strip numbered cards; locked steps still locked  
4. Cast page voice “optional add-on”  
5. Estimate / Film / Review page heads  
6. Demo gallery hero  
7. Existing jobs still run (no Engine changes)  
