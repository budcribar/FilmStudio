# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 11:11:26 UTC*  
*Source Story File: `Nick_and_Me.txt`*

> ⚠️ **GENERATION FALLBACK DETECTED:** The following models' live API generation failed, and the tool silently substituted a non-AI, book-text-only draft (identical for every failing model). Their rows below do NOT reflect that model's real output and are excluded from multi-book history:
> - **claude-opus-5**: Anthropic messages HTTP 400: {"type":"error","error":{"type":"invalid_request_error","message":"Your credit balance is too low to access the Anthropic API. Please go to Plans & Billing to upgrade or purchase credits."},"request_id":"req_011Cda2ZcnNFUNtsmdnSyQxe"}

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **89.2** | 93.5% | 86.0% | 14 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **88.9** | 99.8% | 82.0% | 11 pts | 2.5 |
| 🥉  | **gpt-5.6-luna** | **84.4** | 99.0% | 75.0% | 10 pts | 3.0 |
| 4.  | **gemini-3.1-pro-preview** | **83.1** | 98.5% | 73.0% | 9 pts | 3.5 |
| 5.  | **gemini-3.6-flash** | **75.7** | 100.0% | 60.0% | 6 pts | 5.0 |
| 6.  | **grok-4.20-reasoning** | **70.7** | 99.2% | 52.0% | 4 pts | 6.0 |
| 7.  | **claude-opus-5 ⚠️ *(fallback draft, not real output)*** | **31.6** | 79.0% | 0.0% | 0 pts | 3.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 95% | 100% | 90% | 9.0/10 | 9.0/10 | 8.5/10 | 8.2/10 | 8.5/10 | 8.5/10 |
| **gpt-5.6-terra** | 100% | 100% | 95% | 8.0/10 | 8.5/10 | 8.2/10 | 8.0/10 | 7.8/10 | 8.5/10 |
| **gpt-5.6-luna** | 100% | 100% | 80% | 6.0/10 | 8.2/10 | 7.5/10 | 7.0/10 | 7.8/10 | 8.2/10 |
| **gemini-3.1-pro-preview** | 100% | 100% | 70% | 7.2/10 | 7.5/10 | 7.2/10 | 6.5/10 | 7.8/10 | 7.5/10 |
| **gemini-3.6-flash** | 100% | 100% | 100% | 2.8/10 | 6.5/10 | 7.2/10 | 6.5/10 | 5.5/10 | 7.2/10 |
| **grok-4.20-reasoning** | 100% | 100% | 85% | 2.0/10 | 5.8/10 | 6.0/10 | 5.8/10 | 5.0/10 | 6.5/10 |
| **claude-opus-5** | 95% | 50% | 50% | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-luna**:
  - grok-4: Extensive invented named supporting characters (MS. HART, DR. CARSON, OFFICER DUPUIS, BOUNCER HARRIS, GUARD MILLER) and framing device not in source
- **gemini-3.6-flash**:
  - grok-4.5: Protagonist given the name PETER and self-identifies on camera from the opening scene, violating source withholding of the name until the final pages
  - grok-4.5: Invented closing line 'God bless us, every one' imported from an unrelated work
  - grok-4: Invented protagonist name PETER not present in source; multiple paraphrased and invented dialogue lines
- **grok-4.20-reasoning**:
  - grok-4.5: Protagonist named PETER and addresses camera from the first scene, violating source name-withholding
  - grok-4.5: Invented closing line 'God bless us, every one' from an unrelated work
  - grok-4.5: Multiple major beats dropped or replaced by camera-address framing not grounded in the book
  - grok-4: Invented protagonist name PETER; added non-source Dickens quote at end; extensive invented framing and plot compression

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | gpt-5.6-terra | gpt-5.6-luna | gemini-3.1-pro-preview | gemini-3.6-flash | grok-4.20-reasoning | claude-opus-5 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 8.9 | 8.2 | 8.2 | 7.6 | 5.6 | 5.0 | N/A |
| **grok-4** | 8.3 | 8.2 | 6.7 | 7.0 | 6.3 | 5.3 | N/A |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay F because it delivers the highest fidelity to source beats and diction while locking consistent, re-stated character visuals and supplying clip-ready action plus dense sound cues. Weakest: Screenplay E because it violates name-withholding from the first line, invents an unrelated closing quotation, and replaces observable action with camera-address telling.
- **grok-4:** Strongest: Screenplay E because it maintains THE NARRATOR cue, avoids invented names or plot, and delivers consistent visual/action grounding across the full arc. Weakest: Screenplay F because it assigns an invented name to the protagonist, inserts external quotes, and fabricates a framing device that fundamentally alters the source.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 23
- **Dialogue Blocks:** 111 (Avg `12.6` words/turn, Max `47` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Highest-fidelity adaptation: nearly every major beat, dream motif, and book-voice cadence is preserved; name stays THE NARRATOR until the final boarding-pass reveal. Character locks are exhaustive and restated on re-entry (Nick scars/build/jacket, Sionna eyes/necklace/wardrobe, Narrator lean/brown-hair/pizza shirt). Action lines are single-clip concrete and ordered. Dialogue quotes or tightly compresses source. Sound/music cues densify correctly at crash, stabbing and prison peaks. Structure supports short-clip breakdown while retaining emotional momentum.
  - *grok-4:* Highest fidelity with THE NARRATOR cue, minimal inventions, consistent locations and looks, and strong visual grounding in every scene.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Cap total scene count guidance more tightly (e.g. prefer 18–28 headings for a novella-length source) so even high-fidelity drafts automatically prune secondary connective tissue before peak beats.
  - *grok-4:* Add an explicit rule that any V.O. passage longer than three sentences must be broken by at least one visual or sound micro-beat.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 19
- **Dialogue Blocks:** 72 (Avg `14.2` words/turn, Max `42` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *grok-4.5:* Faithful spine with clear visual crash and stabbing peaks; supporting names stay minimal and closed. NARRATOR dark-blond and Nick scar/build descriptions hold across ages; Sionna traits recur. Action is highly clip-able (single observable beats, no multi-location cram). Dialogue is natural but occasionally paraphrases book cadence. Sound design is densest and most concrete of the set, with layered cues at every emotional turn. Structure is tight for short-form without padding.
  - *grok-4:* Strong fidelity with consistent NARRATOR, locked visual traits on re-entries, and filmable action lines; minor supporting names but no plot inventions.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate that any paraphrased dialogue beat must be flagged internally and limited to one clause; prefer cutting the entire minor exchange over rewriting source wording.
  - *grok-4:* Add an explicit rule that when a character reappears after a time jump, the first Action line must restate 2-3 locked visual traits.

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 29
- **Dialogue Blocks:** 0 (Avg `0` words/turn, Max `0` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Strong coverage of the full arc from Milwaukee setup through crash, stabbing, breakup and prison forgiveness with Seattle frame bookends; minor named supports (MS. HART, GUARD MILLER, BOZO) stay closed-cast. Character looks lock consistently (Nick scars/reddish hair, Sionna pale/light-blue eyes/fish necklace) with explicit older/young NARRATOR splits. Action lines are mostly single-beat and camera-visible; dialogue stays close to source diction. Sound cues appear in every scene and densify at peaks. Minor compression of secondary dreams and library beats keeps runtime viable without inventing plot.
  - *grok-4:* Severe fidelity penalty for invented characters and plot framing; covers beats but adds non-source elements that alter structure.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a hard rule that every age-spanning character re-introduction must open its first Action line with an explicit locked trait restatement plus age token (e.g. OLDER NARRATOR / YOUNG NARRATOR) so reference images never drift across time jumps.
  - *grok-4:* Add an explicit rule that supporting characters with dialogue must use only names present in the source text or generic descriptors without new proper names unless required for closed cast.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 77 (Avg `19.4` words/turn, Max `58` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 10 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Solid arc coverage and late name reveal; dialogue often quotes the book almost verbatim. Character descriptions appear but re-entry restatements are thinner, risking look drift. Action is mostly concrete yet a few scenes compress multiple emotional beats. Pacing is functional though some connective tissue feels rushed. Sound is consistently present and useful.
  - *grok-4:* Solid coverage with consistent NARRATOR use and visual descriptions; some pacing compression but no major inventions.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require that every returning character’s first Action line after a time jump or location change must restate at least two locked visual traits (build, hair, scar, signature wardrobe color) before any dialogue.
  - *grok-4:* Add an explicit rule requiring at least one concrete diegetic sound cue in every scene even for quiet domestic beats.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 16
- **Dialogue Blocks:** 80 (Avg `12.3` words/turn, Max `29` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 6 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Core events are present but fidelity collapses on the early name reveal and the foreign Christmas-Carol button. Character looks are serviceable yet age splits lack consistent re-lock language. Some action is filmable, yet several scenes pack multiple turns. Dialogue drifts into paraphrase and invention. Sound cues exist but cannot rescue the structural violations.
  - *grok-4:* Severe fidelity penalty for naming unnamed narrator and altering dialogue; structure is workable but core identity invention breaks rules.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Insert an explicit prohibition: if the source withholds the protagonist’s given name until late (or forever), the cue must remain NARRATOR / THE NARRATOR until the exact moment the book first utters it; never invent closing lines from other texts.
  - *grok-4:* Add an explicit rule that unnamed protagonists must use THE NARRATOR cue exclusively and never receive invented proper names.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 19
- **Dialogue Blocks:** 63 (Avg `12` words/turn, Max `41` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *grok-4.5:* Fidelity is broken by early naming, foreign ending, and heavy omission of source texture. Character looks are sketched but inconsistent across cuts. Many beats rely on direct-to-camera telling rather than observable action, harming clip directibility. Pacing is choppy with abrupt CUT TOs. Dialogue mixes source fragments with invention. Sound exists but cannot compensate.
  - *grok-4:* Severe fidelity penalty for naming the narrator, adding external quotes, and major structural inventions; unusable as-is.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a hard ban on direct-to-camera address or fourth-wall frame unless the source itself establishes an on-camera confessor position; default all reflective material to NARRATOR V.O. over concrete action.
  - *grok-4:* Add an explicit rule prohibiting any dialogue, quotes, or lines from outside the source text.

### 🎬 claude-opus-5
- **Scene Headings Count:** 566
- **Dialogue Blocks:** 566 (Avg `56.9` words/turn, Max `90` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Excessive scene count (566 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ High average dialogue length (56.9 words/turn); speech beats risk clip overrun.
  - ⚠️ 415 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.

