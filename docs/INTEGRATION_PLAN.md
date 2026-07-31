# Integration plan (merge into existing UI)

## Done (safe, non-breaking)

| Area | Status |
|------|--------|
| Wizard cast → voice → estimate → confirm | Live |
| Client media store (IndexedDB MP3/MP4) | Live |
| Server projects + locks | Live |
| Single `models.json` + Settings keys | Live |
| Voice pipeline (mock + ElevenLabs when keyed) | Live |

## Catalog rule

**One file:** `src/data/models/models.json`  
Capabilities: `voice` | `video` | `chat` | `image` | `face_swap`  
Add new providers as rows with a `capability` field — never a second models file.

## Next pieces

1. Wire video/chat model prefs the same way as voice (`ptm_provider_prefs` extras or columns)
2. Face-swap provider entries + server proxy
3. Server wallet repo for `ptm_wallets` (still client wallet today)
4. Real FFmpeg.wasm stitch when needed
