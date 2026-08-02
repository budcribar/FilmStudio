# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 05:31:29 UTC*  
*Source Story File: `The_Tell-Tale_Heart.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **90.9** | 99.8% | 85.0% | 4 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **82.4** | 100.0% | 71.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 95% | 9.0/10 | 7.4/10 | 8.1/10 | 8.4/10 | 8.9/10 | 9.2/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 5.5/10 | 6.1/10 | 7.4/10 | 7.4/10 | 7.7/10 | 8.4/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - gpt-5.6-terra: The required VISION_META sidecar is absent, leaving the production package incomplete for the stated downstream portrait and clip pipeline.
- **gpt-5.6-terra**:
  - gpt-5.6-terra: The screenplay invents the old man's personal name, MR. VALE, despite the source deliberately leaving him unnamed; this is a major invented character detail and materially fails strict source-fidelity requirements.
  - gpt-5.6-terra: The protagonist is referred to as THE NARRATOR in Action but speaks under the separate character cue NARRATOR, creating an unstable closed-cast identity token for downstream casting.
  - gpt-5.6-terra: The required VISION_META sidecar is absent, leaving the production package incomplete for the stated downstream portrait and clip pipeline.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gpt-5.6-terra |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | 8.4 | **6.2** *(self)* |
| **grok-4.5** | **8.6** *(self)* | 8.0 |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-terra rated its own screenplay 6.2/10 vs. a 8.0/10 average from 1 other judge(s) (-1.8) — notably harsher on itself than peers were.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay A because it most faithfully preserves Poe's complete escalation while pairing concrete visual actions with unusually effective, escalating diegetic sound design. Weakest: Screenplay B because it invents MR. VALE for the deliberately unnamed old man and splits the protagonist into inconsistent THE NARRATOR/NARRATOR casting tokens.
- **grok-4.5:** Strongest: Screenplay B because it best preserves Poe’s voice, peak density, and clip-ready sound/visual interleaving with almost no invented plot. Weakest: Screenplay A because stacked V.O. over thinner micro-beats and slightly looser dialogue invention blunt pacing and directibility despite superior cast-look specificity.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 35 (Avg `21.3` words/turn, Max `36` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is a highly faithful, well-shaped adaptation that preserves the full narrative spine: the eye fixation, seven nights of surveillance, eighth-night intrusion, murder, concealment, police search, auditory breakdown, and confession. Poe's language is retained with strong compression, and the escalating heart sound is exceptionally well staged through concrete visual and audio micro-beats. The principal weakness for clip production is that several action paragraphs package multiple consequential actions into one block, especially the concealment sequence and portions of the final breakdown; these should be atomized into explicit 5–10 second beats. Character continuity is generally sound for the Narrator and Old Man, but the three officers receive stable tokens without sufficiently distinct individual visual identifiers, and the Narrator's locked traits are not refreshed after lengthy intervening sequences. The officers' quoted search line is a minor invented dialogue addition, though it does not materially alter the source.
  - *grok-4.5 (self):* Highest fidelity to Poe’s diction and beat order: opening confession, seven-night stakeout, eighth-night slip, eye-ray, watch-in-cotton heartbeat, bed murder, dismemberment/tub, officers, and final shrieked admission all land with book wording preserved and minimal paraphrase. Directibility and pacing excel via short observable actions interleaved with V.O. and escalating diegetic heartbeat/sound beds; peak scenes carry multiple distinct SOUND/MUSIC cues. Dialogue stays performable and clip-length while keeping iconic lines intact; officer speech is kept to a brief necessary beat. Main weakness is cast-look locking: OLD MAN is thinly described beyond frailty/white hair/eye; OFFICER REYNOLDS/HAYES/BRIGGS share only generic ‘police coats and hats’ with no individual build/face/wardrobe traits, and re-entry restatements of locked traits are weaker than A—risking portrait drift downstream.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add an atomic-clip rule: every Action paragraph must depict only one camera-observable event or reaction suitable for a single 5–10 second clip; split sequential actions, prop changes, and time-progressions into separate Action and sound beats.
  - *grok-4.5 (self):* Mandate that every named supporting speaker (officers, servants, neighbors, etc.) receive 2–3 distinct stable visual traits (build, facial hair/face shape, wardrobe color or cut) at first on-screen appearance, restated in the first Action line on any later re-entry after a gap.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 47 (Avg `13.3` words/turn, Max `26` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* The draft covers the essential plot cleanly and has usable, concrete visual beats, strong sound beds, and a generally efficient progression from surveillance to confession. However, its source-fidelity score is severely constrained by inventing the old man's name as MR. VALE, a source character distinction Poe intentionally does not provide. It also breaks casting continuity by alternating between THE NARRATOR in Action and NARRATOR as the dialogue token, which can create duplicate character identities in a locked-reference workflow. The invented "Well enough" response for the old man is a further unnecessary addition where the source supplies no spoken reply. Compared with A, the murder and escalation are less richly interleaved with physical micro-beats and the final police sequence resolves more abruptly, though its sound design remains consistently effective.
  - *grok-4.5:* Strong closed-cast locking and consistent wardrobe/traits: THE NARRATOR (gaunt, slick black hair, hollow cheeks, black waistcoat) and MR. VALE (thin, gray robe, filmed pale-blue eye) stay visually stable, and the three officers are individually differentiated (broad/mustached, lean/clean-shaven, elderly/stiff-backed). Story spine and iconic lines are largely intact, with clear single-location scenes suited to short clips. Weaknesses: several peak stretches stack consecutive V.O. blocks over relatively static images with fewer micro-beats than the source’s ‘many minutes’ density wants; a few invented spoken lines (MR. VALE’s ‘Well enough,’ fuller officer briefing) slightly exceed the summarized-exchange cap; eighth-night and confession escalation are competent but less granular than B. Sound cues are present in every scene and functional at peaks, though not as densely interleaved as B.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Clarify that unnamed source characters must retain stable descriptive role tokens rather than fabricated personal names, and require every Action reference and dialogue cue for a character to use the exact same token throughout the script.
  - *grok-4.5:* Require that any stretch of continuous NARRATOR/character V.O. longer than two short sentences must be broken by at least one concrete camera-observable Action or diegetic (SOUND:) micro-beat before the next V.O. block, especially at central violent or confession peaks.

