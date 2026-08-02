# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 13:19:43 UTC*  
*Source Story File: `The_Velveteen_Rabbit.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **93.2** | 97.5% | 90.0% | 14 pts | 1.0 |
| 🥈  | **gpt-5.6-luna** | **90.2** | 97.2% | 85.0% | 12 pts | 2.0 |
| 🥉  | **gpt-5.6-terra** | **86.9** | 97.8% | 80.0% | 10 pts | 3.0 |
| 4.  | **gemini-3.1-pro-preview** | **78.2** | 97.5% | 65.0% | 6 pts | 5.0 |
| 5.  | **grok-4.20-reasoning** | **77.4** | 98.5% | 63.0% | 6 pts | 5.0 |
| 6.  | **gemini-3.6-flash** | **77.4** | 99.2% | 63.0% | 6 pts | 5.0 |
| 7.  | **claude-opus-5** | **73.1** | 97.8% | 57.0% | 2 pts | 7.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 95% | 100% | 80% | 9.0/10 | 9.5/10 | 9.0/10 | 8.8/10 | 9.0/10 | 9.0/10 |
| **gpt-5.6-luna** | 95% | 100% | 75% | 8.8/10 | 8.8/10 | 8.5/10 | 8.2/10 | 8.8/10 | 8.2/10 |
| **gpt-5.6-terra** | 95% | 100% | 85% | 8.2/10 | 8.0/10 | 7.8/10 | 7.5/10 | 8.2/10 | 8.0/10 |
| **gemini-3.1-pro-preview** | 95% | 100% | 80% | 4.5/10 | 7.2/10 | 7.0/10 | 6.5/10 | 6.8/10 | 7.2/10 |
| **grok-4.20-reasoning** | 100% | 100% | 70% | 6.2/10 | 6.2/10 | 6.2/10 | 6.5/10 | 5.2/10 | 7.5/10 |
| **gemini-3.6-flash** | 100% | 100% | 85% | 5.5/10 | 6.2/10 | 7.2/10 | 6.5/10 | 5.2/10 | 7.0/10 |
| **claude-opus-5** | 95% | 100% | 85% | 2.5/10 | 7.0/10 | 6.8/10 | 3.0/10 | 7.8/10 | 7.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gemini-3.1-pro-preview**:
  - grok-4: Invented on-camera NARRATOR frame and study location not present in source
- **grok-4.20-reasoning**:
  - grok-4.5: Critical dialogue misattribution: after Fairy asks who she is, RABBIT delivers the Fairy's self-introduction monologue
  - grok-4.5: Multiple scenes cram distinct locations/times under one heading (garden find flowing into bedroom REAL declaration)
- **gemini-3.6-flash**:
  - grok-4.5: Numbered cast tokens WILD RABBIT ONE / WILD RABBIT TWO violate closed-cast naming
  - grok-4.5: Substantial invented spoken lines (Boy stocking line, burrow speech, tear monologue as dialogue, 'My legs... I have hind legs!') not in the source
- **claude-opus-5**:
  - grok-4.5: Screenplay truncates mid-illness sequence; missing recovery, burn order, sack/tear/Fairy transformation, Real-rabbit reveal, and spring reunion ending
  - grok-4: Screenplay is truncated mid-sentence and does not complete the story arc

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | gpt-5.6-luna | gpt-5.6-terra | gemini-3.1-pro-preview | grok-4.20-reasoning | gemini-3.6-flash | claude-opus-5 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 9.1 | 8.7 | 7.7 | 6.6 | 5.2 | 5.3 | 6.2 |
| **grok-4** | 9.0 | 8.4 | 8.2 | 6.5 | 7.4 | 7.2 | 5.1 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay F because it delivers the full book arc with locked cast looks, clip-ready action, verbatim spine dialogue, and consistent sound/music density. Weakest: Screenplay C because it truncates mid-illness and never reaches the Fairy transformation or ending, so it is not a producible film.
- **grok-4:** Strongest: Screenplay F because it achieves the highest scores across all dimensions with no invented content and perfectly balanced short-clip-ready structure. Weakest: Screenplay C because it is truncated mid-sentence and fails to deliver a complete arc.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 52 (Avg `17.2` words/turn, Max `52` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 4 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Best-in-set package: full arc with book wording preserved, medium locked in Notes, and highly stable cast keys (Boy chestnut bowl-cut; Nana iron-gray bun/black dress/white apron; Hazel/Clover distinct; Doctor Lewis). Action lines are single-clip observables; illness, sack, tear, transformation, and spring coda are properly interleaved with sound/music. Minimal invention beyond necessary naming.
  - *grok-4:* Exceptional fidelity with precise book wording, consistent locked visuals restated on re-entry, concrete filmable actions, and balanced sound cues in every scene.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Cap consecutive NARRATOR V.O. blocks at two short turns per static image and mandate an intervening concrete Action or diegetic sound micro-beat so reflective passages never stall clip rhythm.
  - *grok-4:* Add a rule requiring at least one explicit (SOUND: ...) or (MUSIC: ...) cue in every scene, with two distinct cues at any emotional peak.

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 15
- **Dialogue Blocks:** 74 (Avg `16.3` words/turn, Max `65` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 5 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Near-complete beat coverage with book-faithful dialogue (Skin Horse, Boy's REAL line, Doctor burn order, Fairy speeches). Locked looks for Boy (slight, dark-curled), Nana, Skin Horse, and named wild rabbits support consistent casting. Scenes are clip-sized with concrete observables and dense (SOUND)/(MUSIC) cues at peaks. Minor invented proper names (Bramble/Fern, Doctor Hart) are closed-cast necessities, not plot invention.
  - *grok-4:* Very strong coverage and character consistency with named wild rabbits kept stable; slightly slower pacing in illness sequence due to longer action paragraphs.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require that any newly assigned proper names for unnamed book creatures/roles be introduced once in Action with 2–3 locked visual traits and then reused verbatim, and forbid adding backstory or business for those names beyond the source beat.
  - *grok-4:* Add a rule that action lines must describe only one concrete camera-observable event per line and never cram multiple distinct actions into a single paragraph.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 10
- **Dialogue Blocks:** 56 (Avg `15.5` words/turn, Max `43` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. BOY).
- **Judge Rationale:**
  - *grok-4.5:* Solid spine and mostly book dialogue, but thinner on connective beats (new-bunny bedroom, full Skin Horse cadence) and one invented Fairy reply ("No."). Character locks are weaker: Boy is only "fair-haired," wild rabbits stay generic WILD RABBIT, Doctor unnamed. The REAL declaration awkwardly bridges garden exterior and bedroom through a window in one heading, hurting clip isolation. Audio cues are present and usable.
  - *grok-4:* Strong source coverage and consistent character visuals with locked traits restated on re-entry; minor pacing drag from extended V.O. blocks in illness and garden scenes.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a hard rule that a single scene heading may not span two non-adjacent spaces (e.g. garden lawn and bedroom interior); split into separate headings whenever the camera would have to jump rooms or cross a threshold.
  - *grok-4:* Add an explicit rule that no V.O. block may exceed two sentences without an intervening one-line visual or sound micro-beat.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 42 (Avg `16.1` words/turn, Max `65` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 4 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* On-camera STUDY narrator frame is prompt-legal and bookended cleanly, and core dialogue (Skin Horse, REAL, Fairy) mostly holds. However MODEL BOAT is given invented technical monologue the book only summarizes; seaside hope, new-bunny night, and some garden texture are compressed or dropped. Wild rabbits stay generic FURRY RABBIT; Boy/Nana locks are thinner on re-entry. Usable but needs revision for fidelity and cast clarity.
  - *grok-4:* Major invented plot element (framing narrator in study) violates fidelity; otherwise solid visuals but disqualified for added content.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* When the book only summarizes that a minor character boasted or chatted without quoting words, forbid writing multi-sentence invented speeches—allow at most one brief generic line or fold the beat entirely into Action.
  - *grok-4:* Add an explicit prohibition against inventing any on-camera frame narrator or new locations unless they appear in the source text.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 35 (Avg `21.4` words/turn, Max `65` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 7 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 4 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Audio design is relatively strong and the plot outline is recognizable, but the Fairy/Rabbit line swap is a deal-breaker for performance and fidelity. Boy and supporting cast lack durable visual locks; AUNTS/UNCLES and generic WILD RABBIT weaken the closed cast. Several beats pack multi-space action into one slug, hurting short-clip directibility.
  - *grok-4:* Covers core beats but compresses summer and illness sections too aggressively, with inconsistent re-description of Rabbit's appearance across time jumps.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require a speaker-consistency check: the character cue above a dialogue block must match the book speaker for that line; if a monologue is reassigned, the draft fails validation and must be corrected before output.
  - *grok-4:* Add a rule requiring every character re-entry after a time jump to restate two to three locked visual traits in the first action line of that scene.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 10
- **Dialogue Blocks:** 39 (Avg `15.1` words/turn, Max `65` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Structure roughly follows the book and many visuals are filmable, but fidelity is damaged by frequent invented dialogue and by turning interior thought into on-camera speech. Numbered wild-rabbit cues break cast stability. Age labels (Boy 7→8) and thinner wardrobe locks further weaken casting clarity. Sound cues exist but cannot offset the dialogue/cast defects.
  - *grok-4:* Good visual consistency and closed cast but introduces invented spoken lines such as 'You're the best present of all' that paraphrase rather than preserve book wording.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a rule that interior thoughts, wishes, and unspoken reflections from the book must be NARRATOR V.O. or Action only—never converted into new spoken dialogue for the thinking character unless the book marks them as speech.
  - *grok-4:* Add a rule that any summarized exchange receives at most one brief generic period-appropriate line and never invented character-specific dialogue.

### 🎬 claude-opus-5
- **Scene Headings Count:** 7
- **Dialogue Blocks:** 34 (Avg `15.2` words/turn, Max `52` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* What exists is strong—book diction, Timothy/Skin Horse setup, clear looks, good sound beds—but the draft ends mid-sentence during the shabby-illness beat. Without crisis resolution or ending it cannot be scheduled or scored as a finished adaptation. Fidelity and pacing collapse because the arc is incomplete.
  - *grok-4:* Incomplete draft drops the fairy transformation, final spring scene, and resolution; fidelity penalized severely for missing major beats.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add an explicit pre-output checklist: before emitting FADE OUT/THE END, verify setup, mid-crisis, and resolution are all present in the draft; if the source arc is unfinished in the output, continue generating rather than stopping.
  - *grok-4:* Add a rule that the final scene must always include a short closing beat returning to any established on-camera frame before FADE OUT.

