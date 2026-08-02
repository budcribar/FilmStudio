# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 11:25:51 UTC*  
*Source Story File: `The_Tell-Tale_Heart.txt`*

> ⚠️ **GENERATION FALLBACK DETECTED:** The following models' live API generation failed, and the tool silently substituted a non-AI, book-text-only draft (identical for every failing model). Their rows below do NOT reflect that model's real output and are excluded from multi-book history:
> - **claude-opus-5**: Could not build a usable screenplay from the book. Try again or import a .fountain file.

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **91.0** | 98.0% | 86.0% | 13 pts | 1.5 |
| 🥈  | **gpt-5.6-terra** | **91.0** | 97.5% | 87.0% | 12 pts | 2.0 |
| 🥉  | **gpt-5.6-luna** | **90.2** | 97.5% | 85.0% | 11 pts | 2.5 |
| 4.  | **gemini-3.6-flash** | **82.5** | 97.5% | 72.0% | 8 pts | 4.0 |
| 5.  | **grok-4.20-reasoning** | **79.9** | 96.0% | 69.0% | 5 pts | 5.5 |
| 6.  | **gemini-3.1-pro-preview** | **79.5** | 97.0% | 68.0% | 5 pts | 5.5 |
| 7.  | **claude-opus-5 ⚠️ *(fallback draft, not real output)*** | **36.6** | 91.5% | 0.0% | 0 pts | 3.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 95% | 100% | 90% | 8.0/10 | 9.0/10 | 9.0/10 | 8.8/10 | 7.8/10 | 9.2/10 |
| **gpt-5.6-terra** | 95% | 100% | 80% | 8.5/10 | 9.2/10 | 8.8/10 | 8.5/10 | 8.5/10 | 8.5/10 |
| **gpt-5.6-luna** | 100% | 100% | 50% | 9.0/10 | 8.5/10 | 7.8/10 | 8.0/10 | 9.0/10 | 9.0/10 |
| **gemini-3.6-flash** | 100% | 100% | 50% | 6.2/10 | 7.8/10 | 8.0/10 | 7.2/10 | 5.8/10 | 8.5/10 |
| **grok-4.20-reasoning** | 95% | 100% | 50% | 5.0/10 | 7.5/10 | 7.8/10 | 6.8/10 | 6.5/10 | 8.0/10 |
| **gemini-3.1-pro-preview** | 95% | 100% | 70% | 6.5/10 | 5.5/10 | 7.2/10 | 7.2/10 | 6.2/10 | 8.0/10 |
| **claude-opus-5** | 95% | 100% | 50% | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.20-reasoning**:
  - grok-4.5: Major invented plot after the source ending: officers seize the narrator and tear up the planks to reveal the remains—events absent from the book
- **gemini-3.1-pro-preview**:
  - grok-4.5: Unstable cast tokens: dialogue cues alternate REYNOLDS vs OFFICER REYNOLDS; Action lines repeatedly use lowercase 'Narrator' instead of the cue token NARRATOR
  - grok-4: Inconsistent character cue casing (Narrator vs NARRATOR) violates closed-cast stability

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | gpt-5.6-terra | gpt-5.6-luna | gemini-3.6-flash | grok-4.20-reasoning | gemini-3.1-pro-preview | claude-opus-5 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 8.5 | 8.8 | 8.2 | 6.6 | 6.1 | 6.2 | N/A |
| **grok-4** | 8.8 | 8.5 | 8.8 | 7.9 | 7.8 | 7.3 | N/A |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay B because it best balances book-faithful diction, locked recurring cast looks, on-frame narrator structure, and clip-sized visual beats without inventing plot. Weakest: Screenplay E because it adds a post-confession arrest and body-reveal the source never contains, a severe fidelity disqualifier.
- **grok-4:** Strongest: Screenplay E because it best balances concrete filmable actions, consistent frame, precise sound cues, and source fidelity without invented elements. Weakest: Screenplay C because of inconsistent narrator cue casing and lower character disambiguation stability.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 35 (Avg `23.5` words/turn, Max `42` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
- **Judge Rationale:**
  - *grok-4.5:* Excellent interrogation-room frame interleaved with bedchamber action; NARRATOR and officers have locked, restated looks; watch-as-heartbeat sound design is the strongest of the set and maps cleanly to short clips. Fidelity dips where the old man’s private self-soothing (book narrator inference) becomes spoken OLD MAN dialogue and where officers deliver exposition lines the book keeps as prose. Still production-viable and highly directable with only light dialogue surgery needed.
  - *grok-4:* Excellent directibility with concrete actions, consistent frame in interrogation room, precise sound cues, and high fidelity to source wording.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Forbid converting a narrator’s inferred or imagined thoughts attributed to another figure into spoken on-camera dialogue for that figure; keep such lines as NARRATOR V.O. or silent Action.
  - *grok-4:* Add a rule requiring two distinct sound or music cues at every major emotional peak rather than one.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 39 (Avg `25.6` words/turn, Max `43` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 4 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *grok-4.5:* Best overall package: stable NARRATOR’S CHAMBER frame with cutbacks at setup/escalation/aftermath, locked recurring looks for NARRATOR and MR. VALE, and distinctly described officers. Book voice is preserved on-frame and in V.O.; naming the unnamed old man is within the allowed stable-token rule and does not invent plot. Visual actions are single-purpose and clip-sized; heartbeat and lantern hinge cues track the arc. Minor trims of secondary clauses and light officer presence keep runtime tight without breaking the spine.
  - *grok-4:* Strong consistent descriptions and frame usage with MR. VALE; good balance of action and V.O. but slightly compresses some tension beats.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate that when an on-camera narrator frame is established, every major turn (setup, mid-crisis, peak, aftermath) must include at least one short on-frame dialogue beat using book diction, not only V.O. over the dramatized location.
  - *grok-4:* Add a rule that every scene heading must be followed by at least one camera-observable action line before any dialogue or V.O.

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 7
- **Dialogue Blocks:** 43 (Avg `33.8` words/turn, Max `59` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ High average dialogue length (33.8 words/turn); speech beats risk clip overrun.
  - ⚠️ 19 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE OLD MAN).
- **Judge Rationale:**
  - *grok-4.5:* Near-verbatim coverage of Poe’s beats and diction with no invented plot; iconic confession and concealment land cleanly. NARRATOR and THE OLD MAN looks are stable, but the three officers are barely differentiated beyond shared coats, weakening cast locks. Action is filmable yet often static under long unbroken V.O. blocks, which hurts 5–10s clip segmentation. Sound beds (creaks, death-watch, escalating heartbeat) are concrete and scene-complete. Strong fidelity draft that still needs tighter visual micro-beats between monologue turns.
  - *grok-4:* Excellent fidelity with book text preserved in V.O., consistent character descriptions and locations, strong sound cues per scene; minor deduction for heavy V.O. reliance reducing visual action density in some clips.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require that any V.O. stretch longer than two sentences be broken by a fresh camera-observable Action micro-beat (gesture, light shift, breath, prop move) so downstream 5–10s clips never sit on a frozen image under a monologue paragraph.
  - *grok-4:* Add an explicit rule requiring at least one concrete visual action line between any two V.O. blocks longer than a single sentence to ensure filmable micro-beats.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 8
- **Dialogue Blocks:** 21 (Avg `32.6` words/turn, Max `111` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ High average dialogue length (32.6 words/turn); speech beats risk clip overrun.
  - ⚠️ 8 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Core murder–concealment–confession arc is present and the cell frame bookends cleanly, with usable sound/music peaks. Fidelity and dialogue suffer from multiple invented spoken lines (officer exposition, morning banter, ‘lovely morning’ pacing chatter) that the book delivers as narration or not at all, plus heavy compression of the seven-night and hour-long wait. Character intros are adequate but re-entry restatements are thin. Workable bones that need dialogue stripped back to book wording.
  - *grok-4:* Solid frame structure and directible actions but introduces invented dialogue lines and officer names not required by source, lowering fidelity and authenticity scores.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Strengthen the summarized-exchange rule: when the book only narrates that people spoke or searched without quoting words, default to zero invented spoken lines and forbid turning book narration into multi-line exposition dialogue for supporting characters.
  - *grok-4:* Add a rule that summarized exchanges receive at most one brief generic period-appropriate line only if action alone cannot carry the beat, defaulting to zero invented lines.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 7
- **Dialogue Blocks:** 23 (Avg `47` words/turn, Max `120` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ High average dialogue length (47 words/turn); speech beats risk clip overrun.
  - ⚠️ 13 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Severe fidelity failure: the draft continues past Poe’s final confession into an invented arrest and corpse reveal. Earlier sections track the story and sound design is solid, with a usable asylum frame, but invented officer dialogue and the fabricated climax disqualify the package regardless of mid-film craft. Clip directibility is otherwise fine; the ending rewrite is the deal-breaker.
  - *grok-4:* Uses THE NARRATOR consistently but adds some invented dialogue and compresses key beats, with slightly weaker pacing for short-clip format.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Explicitly forbid extending past the source’s final narrative beat with invented aftermath (arrests, discoveries, trials, epilogues) unless that continuation is present in the provided text.
  - *grok-4:* Add a rule to preserve exact book wording for all iconic or first-person lines without paraphrase unless runtime forces dropping the entire beat.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 26 (Avg `24.8` words/turn, Max `43` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 8 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Structure and sound are serviceable and most plot beats appear, but character identity is broken for production locks—officer prefixes drop mid-draft and Action refers to 'Narrator' while cues say NARRATOR. Several invented polite exchanges and dream/country lines pad beyond the book’s summarized chat. Directibility is mostly concrete, yet the token drift is a closed-cast hard defect that blocks greenlight.
  - *grok-4:* Follows arc but has casing inconsistency on narrator cue, some paraphrased dialogue, and less precise location reuse across scenes.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add an explicit consistency check: every on-page reference to a speaking character in Action must use the identical ALL-CAPS token as that character’s dialogue cue (including rank/title prefixes), with no lowercase display-name variants.
  - *grok-4:* Add a rule mandating identical ALL-CAPS spelling for every character cue on every appearance with no variants allowed.

### 🎬 claude-opus-5
- **Scene Headings Count:** 20
- **Dialogue Blocks:** 20 (Avg `58.2` words/turn, Max `84` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ High average dialogue length (58.2 words/turn); speech beats risk clip overrun.
  - ⚠️ 16 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.

