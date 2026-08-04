# De-dup / overlap handling for the Grok loop pipeline

The naive “clamp loud samples” step is **not** de-duplication. Real continuity
needs to remove **repeated content** across generated clips (same line spoken
twice, same shot beat, same ambient bed), not peak-limit PCM.

## What “dup” means here

1. **Dialog echo** — Part 2 re-speaks lines already in part 1 (Whisper hears
   them again; concat sounds like a stutter).
2. **Visual/story echo** — Part 2 regenerates the same beat instead of advancing.
3. **Audio bed overlap** — Crossfade region has two full mixes stacked.

Treat (1) and (2) in **text/plan space** before generate; treat (3) in **time
alignment** at the splice.

## Recommended pipeline (practical order)

### A. Prompt-level anti-echo (cheapest)

When requesting part 2:

- Pass a **strict “already covered” block**: transcript + scene headings already
  filmed; instruct the model: *do not repeat these lines or beats; continue after
  the last clear line*.
- Pass a **one-line “next beat only”** target from the screenplay (Fountain
  action/dialog after the cut point).
- Prefer generating part 2 from **screenplay remainder**, using part 1 transcript
  only as continuity constraint — not as text to re-perform.

### B. Transcript de-dup before part 2 (text)

1. Whisper part 1 → `T1` with word timestamps if available.
2. Normalize: lower-case, strip stage directions, collapse whitespace.
3. Against planned dialog for the next window `P2`:
   - Drop any sentence in the continuation prompt that is ≥ ~0.85 similar
     (token Jaccard / embedding cosine) to a sentence already in `T1`.
4. Optionally run a small LLM judge: “list lines already satisfied; emit only
   remaining lines for the next 8–10s.”

### C. Forced alignment / splice de-dup (audio-time)

1. Whisper both parts with **word-level timestamps**.
2. Find the longest suffix of `T1` that matches a prefix of `T2` (string or
   phoneme alignment).
3. If overlap ≥ N words or ≥ t seconds:
   - Trim the start of part 2 video/audio to the first non-overlapping word
     timestamp (ffmpeg `atrim` / `trim` + `asetpts`).
4. Crossfade 100–250 ms only on the residual bed — do not stack full mixes.

### D. Screenplay-gated generation (PageToMovie-shaped)

Align the experiment with the product:

- Part 1 = clips for scenes/lines `[0, k)`.
- Part 2 = clips for lines `[k, k+m)` only (cast-locked, no re-prompt of prior
  dialog).
- De-dup becomes **scheduling**, not audio surgery: never ask the model for
  lines already rendered.

This is the durable approach inside Film Studio.

### E. What not to do

- Peak limiting / gate as “overlap removal.”
- Concat raw part1+part2 without checking transcript overlap.
- Mux part1-only audio onto a two-part video timeline.

## Minimal implementation sketch for this tool

1. `TranscribeWithTimestamps` (Whisper `verbose_json` → words[]).
2. `FindOverlap(T1, T2)` → seconds to trim from start of part 2.
3. FFMpegCore: trim part 2, then concat, then short crossfade.
4. Log overlap seconds + matched text for benchmark reports under `evals/`.

## Success metrics

- Word-overlap at splice < 3 words (or 0 after aggressive mode).
- No repeated full sentences across the join (string match after normalize).
- Human A/B: “does the story advance?” vs baseline concat.
