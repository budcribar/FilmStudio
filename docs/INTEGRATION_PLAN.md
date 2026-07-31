# Page to Movie — integration plan

Branch: `feature/page-to-movie-integrations`  
Baseline: `main` (UI wizard + simulated pipeline, local-only)

## Media architecture (client-first)

**Principle:** large binaries (MP3 / MP4 / capture / clone output) stay **on the client**.  
Project JSON / Zustand holds only **MediaRef ids**. Client FFmpeg (planned) stitches without server media CPU/storage.

| Layer | Responsibility |
|-------|----------------|
| `src/lib/ptm/media/client-media-store.ts` | IndexedDB blob store (+ memory fallback) |
| `src/lib/ptm/media/client-stitch.ts` | Mock audio concat; ffmpeg.wasm hook documented |
| `src/lib/ptm/media/mock-mp3.ts` | Synthetic MPEG frames for mock TTS |
| `src/data/models/voice-models.json` | Model catalog (mock + ElevenLabs placeholder) |
| `src/lib/ptm/providers/voice-clone.ts` | Clone/TTS; writes MP3 **into client store** |

**Do not** put base64 `dataUrl`s into `localStorage` / Zustand.

---

## Already in the UI

| Step | Status |
|------|--------|
| Book / cast / estimate / confirm | Demo |
| Voice capture (mic \| upload) → client media | **Done** |
| Mock clone → fake MP3 → client stitch → `<audio>` | **Done** |
| Film picture pipeline | Simulated storyboard player |
| ElevenLabs live | Model JSON only (`enabled: false`) |

---

## Phase 2

### 2a Capture + client storage — **done**
- MediaRecorder / file → `putMediaBlobSafe` → `mediaId` on `VoiceSample.asset`
- Consent gate; preview via object URL from IDB

### 2b Mock clone API — **done**
- `runMockVoicePipeline`: createClone + speakLine → mock MP3 blobs in IDB
- `stitchAudioClipsClient` → `voice.stitchedVoMediaId`
- Player shows **Client VO track**

### 2c Live ElevenLabs — **todo**
- Enable model in `voice-models.json`
- Server proxy for API key; response body → still `putMediaBlobSafe` on client
- Same stitch path

### 2d Real client FFmpeg — **todo**
- Add `@ffmpeg/ffmpeg` wasm
- Replace mock-concat in `client-stitch.ts` for A/V mux with plates

---

## Phase 3–5

Face swap (client or thin API → client plates), full movie download, auth/payments — see prior plan sections. Prefer writing results into **client-media-store** whenever possible.

---

## Near-term order

1. ~~Client media store + mock MP3 clone~~ **done**  
2. `@ffmpeg/ffmpeg` for real stitch (audio under picture)  
3. Face-swap plates → same media store  
4. Enable ElevenLabs model entry + proxy  
5. Payments / auth  

---

## Merge policy

- `main` always runs without API keys (mock provider).  
- Model JSON is source of truth for provider switch.  
- Store version **4**: mediaIds only; migrate drops legacy dataUrls.
