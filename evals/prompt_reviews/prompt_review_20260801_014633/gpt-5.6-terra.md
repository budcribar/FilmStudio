# gpt-5.6-terra prompt-improvement recommendation

- Prompt commit: `34a11c4`
- Source benchmark: 2026-07-31 23:55:54 UTC / nick_and_me

## Evidence supplied

Composite: 89.3; syntax: 100.0; LLM consensus: 82.0.
Quality dimensions (0-10): fidelity 8.5, character continuity 8.2, directibility 8.0, pacing 7.5, dialogue 8.2, sound/music 8.8.
Deterministic audit: 25 scenes; 119 dialogue blocks; max dialogue 25 words.
Diagnostics: Detected 2 descriptive instrumental music/sound cue(s).


## Recommendation

1. Root causes
- Character continuity is weakened by at least one non-independent speaking token: `LINDSEY'S BOYFRIEND` is a possessive relational cue rather than a stable cast key, despite the existing supporting-role rule.
- Pacing likely suffers from transitional scenes with limited dramatic payload, such as the prison exterior and airport departure, contributing to the 7.5 pacing score across 25 scenes.
- Some Action relies on internal or interpretive prose (“startled by her confidence,” “his face breaks,” “desperate”) rather than directly playable, camera-observable behavior, consistent with the 8.0 directibility result.

2. Minimal prompt patch
```text
ADD under ACTION LINES:
- Do not label an internal state in Action when an observable behavior can carry it; write the look, pause, gesture, posture, or physical change instead.

ADD under CAST LOOKS & VOICES:
- Every supporting dialogue cue must be an independently stable token, never a possessive relational label such as `X'S BOYFRIEND`; if unnamed in the source, use one neutral stable role token consistently.

ADD under RUNTIME SHAPE:
- If a scene only establishes arrival, departure, or travel and contains no independent source turn, omit or fold it into the nearest dramatic scene rather than spending a separate heading on it.
```

3. Expected trade-offs
- Likely improves: character continuity, directibility, and pacing.
- Possible regression: fidelity may decline slightly if a transitional image or source narration is removed too aggressively during scene consolidation.
- Syntax and sound/music compliance should remain stable, provided retained scenes keep their required grounding Action and audio cue.

4. Benchmark hypothesis
On the same source, the patched prompt should produce zero possessive/relational dialogue cues, improve character continuity and directibility by at least 0.2 points each, and improve pacing by at least 0.3 points versus 7.5, while retaining syntax at 100 and keeping fidelity within 0.2 points of 8.5.
