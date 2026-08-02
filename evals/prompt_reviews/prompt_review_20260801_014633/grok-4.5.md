# grok-4.5 prompt-improvement recommendation

- Prompt commit: `34a11c4`
- Source benchmark: 2026-08-01 00:45:46 UTC / nick_and_me

## Evidence supplied

Composite: 88.2; syntax: 100.0; LLM consensus: 80.0.
Quality dimensions (0-10): fidelity 8.2, character continuity 8.8, directibility 7.8, pacing 7.2, dialogue 8.0, sound/music 8.2.
Deterministic audit: 37 scenes; 92 dialogue blocks; max dialogue 32 words.
Diagnostics: Detected 1 age-qualified character header(s) (e.g. YOUNG NICK). | Detected 2 descriptive instrumental music/sound cue(s).


## Recommendation

1. Root causes
- Pacing (7.2): 37 scenes keep many short connective/memoir beats (library, sidewalk VO, yoga calm-after, Seattle montage fragments) as separate headings instead of merged same-place units, so runtime thins across setup rather than spine.
- Directibility (7.8): Compound slash/“AND” headings (FUNERAL HOME / CEMETERY, HIGHWAY / AIRPORT, KIRK STREET AND PIZZA PARLOR) and a dream scene that contains the wake-up line break one-location scheduling and camera continuity.
- Consensus gap (80 vs syntax 100): judges likely split on whether those micro-scenes and multi-place slugs are faithful compression or unfilmable sprawl; sound/dialogue already pass hard caps, so the drag is structural not cue/word-count compliance.

2. Minimal prompt patch
Add under SCENE HEADINGS (after the VARIOUS/MULTIPLE ban):
```
- HARD: one heading = one filmable place. Never join two places with "/" or " AND "
  (Bad: INT. FUNERAL HOME / CEMETERY - DAY; EXT. HIGHWAY / AIRPORT - DAY;
   EXT. KIRK STREET AND PIZZA PARLOR - DAY). Split into two scenes or keep one
  place and fold the other move into Action/transition.
```
Add under RUNTIME SHAPE (merge bullet):
```
- Memoir / first-person span: merge brief VO-led connective beats that share a
  place or a single travel purpose into one scene with timed Action marks
  (“Months later.” / “Seven years later.”). Prefer ~20–32 headings for a
  life-span short film unless location truly changes every beat.
- Dream/wake (HARD): end the dream under its own heading with dream image only;
  put the jolt awake, gasp, and bed/room Action under the real-location heading
  that follows — never cue a waking line inside the dream slug.
```

3. Expected trade-offs
- Likely improve: pacing, directibility, LLM consensus (clearer scheduleable spine).
- Possible regress: fidelity if over-merge drops a book-specific micro-location; character continuity unchanged; sound/music may dip slightly if merged scenes under-cue after combining beats; scene-count diagnostic should fall.

4. Benchmark hypothesis
On the same title/runtime, next run should show pacing ≥7.8 and directibility ≥8.2, scene count ≤32, zero “/” or “ AND ” location headings, and composite ≥90, with syntax still 100 and max dialogue ≤35. Falsified if pacing stays &lt;7.5 or compound headings still appear.
