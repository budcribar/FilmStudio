# Blazor component refactor — progress & metrics

**Goal:** reduce oversized Blazor page files (a source of UI bugs — too much markup +
logic + shared mutable state in one file) by extracting reusable components into a
dedicated Razor Class Library. Branch: `refactor/blazor-components`.

## Baseline (master @ de32bd2b)

`Components/Pages/*.razor`: **28,722 lines across 40 pages**, only **4** shared components.

| Page | Baseline lines |
|------|---------------:|
| Scenes.razor | 5,232 |
| Characters.razor | 2,977 |
| Review.razor | 2,733 |
| Configuration.razor | 2,212 |
| Home.razor | 1,932 |
| Admin.razor | 1,874 |

## Done so far (verified: build 0 errors, tests pass, fakes-browser smoke)

1. **New RCL `PageToMovie.Components`** — a dedicated project for shared, dependency-free
   UI components (Web references it; components keep `@namespace PageToMovie.Web.Components`
   so no consuming pages needed edits).
2. **Moved 5 pure presentational components** into it: `CapabilityLockedControl`, `CostPie`,
   `PasswordToggleButton`, `PromoCard`, `StatCard`. (Service-dependent shared components —
   `FilmLengthCard`, `StudioProcessStrip`, `VisualMediumCard`, `VoiceCaptureStep`,
   `CostLegend` — stay in Web; moving them needs DI abstraction.)
3. **New reusable `ConfirmModal`** component; applied to the delete-scene and delete-clip
   dialogs in `Scenes.razor` (testids preserved).

Component count: **4 → 6 shared** (+ pattern for more). Scenes.razor: 5,232 → 5,210.

## Findings that change the plan

- The "~63 modals in Scenes" figure was a **loose-grep overcount** (matched every `modal`
  CSS class). Scenes actually has **7** Bootstrap modal dialogs; the other big pages
  (Characters/Review/Configuration/Admin/Home) use **no** Bootstrap modals. So `ConfirmModal`
  has limited additional reach.
- The real file-size reductions therefore require extracting **large, state-heavy sections**
  (e.g. Scenes' clip-editor modal, the scene-row loop body → `SceneCard`, Characters' cast
  panels). Those rewire `[Parameter]`/`EventCallback`/two-way bindings and are **regression-prone**
  — best done with a human able to click through the result, not blind.

## Recommended next steps (need a decision)

- **Option A — component extraction (matches the original ask):** extract one large section
  at a time (start: Scenes clip-editor modal → `ClipEditorModal`; scene-row → `SceneCard`),
  each verified in the fakes browser + a Playwright check. Slower, sequential, but the "right"
  structure.
- **Option B — code-behind split (fastest file-size win, near-zero risk):** move each big page's
  `@code { }` into a `.razor.cs` partial class. Halves the `.razor` files (Scenes 5,210 → ~2,200)
  with **no behavior change**, fully compiler-validated. Not "components," but directly fixes
  "too much code in one file." Reversible per file.
- **Verification loop is proven:** run only the Api with the `http (fakes)` profile
  (`dotnet run --project PageToMovie.Api --launch-profile "http (fakes)"`) → full app at
  `http://localhost:5088/?admin=1` (auto-login bypass). Playwright harness lives in `host/playwright/`.
  Known-baseline console error to ignore: `GET /api/projects/.../cost → 400`.

_Stopped here at a verified-safe checkpoint rather than doing heavy unsupervised surgery._
