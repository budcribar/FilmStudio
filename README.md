# Nick and Me / Film Studio

AI film pipeline: book or screenplay → cast locks → shot plan → Grok video → review → WIP movie.

**Product runtime is .NET only** (Blazor UI + C# API/engine under `host/`).  
No Python runtime is required.

## Run (Film Studio)

Needs:

- .NET SDK (solution targets `net10.0`)
- `XAI_API_KEY` for real Stage 1 / images / video / vision (optional fakes for UI soaks)
- A modern browser for **client** media tools (Chrome/Edge preferred): stitch, silence trim, auto-review frames use **ffmpeg.wasm** in the browser — the API host does **not** install or spawn native ffmpeg

### 1) API / engine (`http://127.0.0.1:5088`)

```powershell
cd host
$env:PageToMovie__WorkspaceRoot = (Resolve-Path ..).Path
$env:PageToMovie__UseFakes = "false"   # "true" for no xAI spend
$env:XAI_API_KEY = "your-key"         # required when UseFakes=false
$env:ASPNETCORE_URLS = "http://127.0.0.1:5088"
dotnet run --project PageToMovie.Api
```

Health: `GET http://127.0.0.1:5088/health`

### 2) Blazor UI (`http://localhost:5079`)

```powershell
cd host
$env:EngineApi__BaseUrl = "http://127.0.0.1:5088"
$env:ASPNETCORE_URLS = "http://localhost:5079"
dotnet run --project PageToMovie.Web
```

Open the UI (admin learning, cast, scenes, review).  
You need **both** Api and Web. If only Web is running, API calls fail.

### Visual Studio

Open `host/PageToMovie.slnx`, set **multiple startup projects**: Api + Web.

More detail: **`host/README.md`**.

## Layout

| Path | Role |
|------|------|
| `host/` | **Film Studio** — Api, Web, Engine, Tests, LoadSim, Playwright pilot |
| `projects/<id>/` | Per-film cast, blueprint, config, state, assets, WIP |
| `projects/workspace.json` | Active project pointer |
| `prompts/` | Stage 1/2, fountain/cast, clip gen/auto-review rules, shared rules |
| `_learning/` | Host-level learning checklist (`proposal_checklist.json`) |
| `docs/` | Learning loop, loadsim, two-stage notes |
| `host/playwright/` | E2E pilot (Node + Playwright) against real or fakes API |
| `scripts/` | Optional maintenance helpers (prefer Blazor / API for product work) |

## Typical operator flow

1. Create / activate a project  
2. Import book or Fountain → sign off screenplay  
3. **Build cast** → generate + lock portraits (style gate) + voices  
4. Build shot plan (Stage 2)  
5. Generate scenes (cast must be ready)  
6. Auto-review + Pass/Fail (browser samples frames; vision runs on the server with the API key)  
7. Play / export: stitch clips in the browser (no server remux)  
8. Admin Learning: propose rules, approve into project rules / checklist  

---

## How Film Studio Converts Source Text to a Movie (Step-by-Step AI Pipeline)

```mermaid
flowchart TD
    A["Raw Story Text / PDF / Fountain"] --> B["Step 1: Text Ingestion\n(BookPrepareService)"]
    B --> C["Step 2: Stage 1 Adaptation\n(Grok 4.5 LLM Screenwriter)"]
    C --> D["Step 3: Cast Discovery & Vision Gate\n(Grok 4.5 + Grok/Gemini Image + Vision Classifier)"]
    D --> E["Step 4: Stage 2 Shot Planning\n(6 Grok 4.5 AI Classifiers)"]
    E --> F["Step 5: Video Generation\n(Grok Imagine Video / Veo + Reference Locks)"]
    F --> G["Step 6: Multi-Frame Auto-Review\n(browser frames + server vision)"]
    G --> H["Step 7: Browser Stitch / Export\n(ffmpeg.wasm in PageToMovie.Web)"]
    H --> I["🎬 Playable draft / export (client media folder)"]
```

### 1. Source Text Ingestion (`BookPrepareService`)
- **Input**: Raw text (`.txt`), PDF book, or existing Fountain screenplay (`.fountain`).
- **Processing**: Cleans Gutenberg headers/boilerplate, normalizes line breaks, extracts chapter boundaries, and formats source text chunks for adaptation.

### 2. Stage 1: Screenplay Adaptation (`BookToFountainConverter`)
- **AI Engine**: **Grok 4.5 LLM (`book_to_fountain`)**
- **Action**: Converts raw book prose into a valid **Fountain 1.1** screenplay containing filmable scene headings (`INT.`/`EXT.`), visual action prose, character dialogue, and voiceover (`V.O.`).
- **Automated AI Recovery**: Verifies screenplay formatting against strict Fountain syntax rules. If scene headings or dialogue cues contain formatting errors, specialized AI fixup passes (`book_to_fountain_locations_retry`, `book_to_fountain_speakers_retry`) resolve errors automatically without human intervention.

### 3. Character Discovery & Visual Style Lock (`CastFromScreenplayService` & `CharacterDesignService`)
- **AI Engine**: **Grok 4.5 LLM (`cast_from_screenplay`)** + **Grok Imagine Image / Gemini Image** + **Grok Vision Classifier**
- **Action**:
  1. **Character Extraction**: AI analyzes the screenplay to extract character identities, species, estimated age, build, clothing, and visual locks (unvarying physical traits).
  2. **Portrait Generation**: Generates candidate reference portraits for each character.
  3. **AI Vision Style Gate (`RequirePortraitStyleGate`)**: An AI Vision Classifier audits generated portraits against the project's global render style (e.g. *period live-action gothic* vs. *3D CG animation*) before locking, ensuring zero visual style drift across the cast.

### 4. Stage 2: Shot Planning & AI Classifier Suite (`Stage2PlannerService`)
- **AI Engine**: **15 Specialized Grok 4.5 Classifiers**
- **Action**: Transforms the Fountain screenplay into a frame-accurate, timestamped shot plan (`blueprint.clips.json`) using 15 AI classifiers:
  1. **`OnScreenCastClassifier`**: Evaluates dialogue and action per beat to determine on-screen vs. off-screen/VO characters per shot, enforcing off-camera speaker rules.
  2. **`SilentBeatActionClassifier`**: Classifies silent action beats (`action_class`) with surrounding narrative context to allocate precise duration budgets ($3\text{s}$–$8\text{s}$).
  3. **`AmbientSfxClassifier`**: Separates background ambient soundscapes from transient sound effects (SFX).
  4. **`SpeciesKindClassifier`**: Categorizes character body types (`animal`, `human`, `other`) to enforce prompt framing rules.
  5. **`ExtendCutClassifier`**: Determines continuity transitions (`extend_previous` vs. `hard_cut`).
  6. **`ShotPlanRefiningClassifier`**: Evaluates multi-clip monologues to generate progressive camera angles (Establishing Wide $\rightarrow$ Close-Up on detail $\rightarrow$ Reaction Shot), eliminating static visual prompt repetition across extended scenes.
  7. **`BeatPacingClassifier`**: Analyzes narrative rhythm, suspense, and emotional weight to assign dynamic clip duration budgets ($2\text{s}$–$12\text{s}$) tailored to scene tension.
  8. **`CinematicLightingClassifier`**: Generates rich atmospheric lighting descriptions, shadow quality, volumetric effects, and mood color palettes locked across all shots in a scene.
  9. **`CameraDirectorClassifier`**: Assigns professional lens choices (24mm wide anamorphic, 85mm portrait), camera movements (dolly push-in, low-angle tracking, tripod hold), and shot composition directives per beat.
  10. **`NegativePromptClassifier`**: Evaluates period setting and scene environment to generate era-specific anachronism negative prompts (*"no modern wristwatches, no electric light bulbs, no plastic, no zippers"*), eliminating visual immersion glitches.
  11. **`WardrobeContinuityClassifier`**: Acts as a Costume Department Supervisor to dynamically track and assign context-appropriate attire per character per scene based on location, time of day, and story beats.
  12. **`CharacterEmotionArcClassifier`**: Acts as an Acting Coach & Performance Director, calculating emotional intensity ($1$–$10$ scale) and facial micro-expressions per beat to drive acting performances in video generation.
  13. **`SoundDesignComposerClassifier`**: Acts as a Film Sound Designer & Audio Supervisor, composing 3-layer audio blueprints (`ambient_layer`, `foley_layer`, `score_layer`) per beat for synthesis planning (export stitch is client-side).
  14. **`DepthOfFieldClassifier`**: Acts as a Focus Puller & Optical Cinematographer, assigning optical aperture settings ($f/1.4$ to $f/8$), primary focal planes, and dynamic rack-focus transitions per shot.
  15. **`ColorPaletteGradingClassifier`**: Acts as a Master Colorist & Film Stock Director, assigning film stock emulsion characteristics (*Kodak Vision3 500T 5219*, *Fuji Eterna*), color palettes, and color grading prompts per scene.
- **Deterministic Pacing**: *Silent Prelude Coalescing* automatically folds 5s silent lead-in beats into Beat 2 so voiceover/dialogue begins on frame 1 of the scene.

### 5. Video Generation (`ClipVideoPromptBuilder` & `GrokVideoClient` / `GeminiVideoClient`)
- **AI Engine**: **Grok Imagine Video / Veo**
- **Action**: Constructs 4,000-character prompts incorporating style locks, on-screen cast counts, visual action prose, and locked character reference images (`<IMAGE_1>`, `<IMAGE_2>`).
- **Identity Attachment**: Attaches locked reference image plates directly to the video generation API call for 100% character face and wardrobe consistency across shots.

### 6. Multi-Frame Auto-Review (`ClipAutoReviewService`)
- **Browser**: Samples previous-clip tail + current-clip frames with **ffmpeg.wasm**, uploads JPEGs over the authenticated job API.
- **Server**: Vision review with the provider key (`CompleteWithImagesAsync`) — key never leaves the API host.
- **Quality Audit**: Character identity, continuity, style; `Pass` / `Fail` with assembly gates for Play stitch / export.

### 7. Browser stitch / export (`PageToMovieFfmpeg` / `ClientVideoStitchService`)
- **Engine**: **ffmpeg.wasm** in the Blazor client (concat, silence trim on gen save, frame sample).
- **Action**: Combine eligible clips for Play/export; gen clips can live in the user media folder with server-side SHA-256 registry only.
- **Not used**: native server `ffmpeg`, remux jobs, or bundled `ffmpeg.exe`.

---

## Playwright pilot

```powershell
cd host/playwright
npm install
$env:API_URL = "http://127.0.0.1:5088"
$env:WEB_URL = "http://localhost:5079"
$env:FULL_MOVIE = "1"          # optional
$env:PROJECT_NAME = "MyPilot"
npm run pilot
```

See `host/playwright/README.md`.

## Tests

```powershell
cd host
# Free / default — excludes paid LiveApi tests
dotnet test PageToMovie.Tests

# Paid provider calls (opt-in; costs API tokens) — see host/PageToMovie.Tests/LiveApi/README.md
$env:PAGETOMOVIE_LIVE_API_TESTS = "1"
$env:XAI_API_KEY = "xai-..."
dotnet test PageToMovie.Tests --filter "Category=LiveApi"
```

## Docs

| Doc | Topic |
|-----|--------|
| `host/README.md` | API routes, SignalR, LoadSim, capability matrix |
| `host/docs/` | Multi-user / loadsim soak |
| `prompts/README.md` | Product prompts and schemas |
| `docs/learning_loop.md` | Feedback / dirty flags (concept) |

## Config notes

- Workspace root: `PageToMovie:WorkspaceRoot` (empty → auto-detect repo root from API).  
- Fakes: `PageToMovie:UseFakes` / `PageToMovie_USE_FAKES=true`.  
- Auth (dev): admin bypass headers / appsettings under `PageToMovie:Auth`.  
