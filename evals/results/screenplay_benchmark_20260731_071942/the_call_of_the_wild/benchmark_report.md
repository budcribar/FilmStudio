# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 13:54:46 UTC*  
*Source Story File: `The_Call_of_the_Wild.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-luna** | **91.7** | 99.2% | 87.0% | 13 pts | 1.5 |
| 🥈  | **gpt-5.6-terra** | **89.7** | 100.0% | 83.0% | 10 pts | 3.0 |
| 🥉  | **gpt-5.6-sol** | **88.9** | 99.8% | 82.0% | 9 pts | 3.5 |
| 4.  | **claude-opus-5** | **87.9** | 98.5% | 81.0% | 11 pts | 2.5 |
| 5.  | **gemini-3.6-flash** | **84.6** | 97.0% | 76.0% | 7 pts | 4.5 |
| 6.  | **gemini-3.1-pro-preview** | **78.7** | 98.8% | 65.0% | 4 pts | 6.0 |
| 7.  | **grok-4.20-reasoning** | **74.1** | 97.2% | 59.0% | 2 pts | 7.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-luna** | 100% | 100% | 85% | 9.0/10 | 8.8/10 | 8.5/10 | 8.2/10 | 8.8/10 | 8.8/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 8.5/10 | 8.8/10 | 8.2/10 | 7.8/10 | 8.5/10 | 8.0/10 |
| **gpt-5.6-sol** | 100% | 100% | 95% | 8.2/10 | 8.8/10 | 8.2/10 | 7.5/10 | 8.2/10 | 8.0/10 |
| **claude-opus-5** | 100% | 100% | 70% | 8.0/10 | 8.5/10 | 8.0/10 | 7.0/10 | 8.5/10 | 8.5/10 |
| **gemini-3.6-flash** | 95% | 100% | 70% | 8.0/10 | 7.8/10 | 7.5/10 | 7.2/10 | 7.8/10 | 7.5/10 |
| **gemini-3.1-pro-preview** | 100% | 100% | 75% | 6.5/10 | 6.0/10 | 7.0/10 | 6.2/10 | 7.0/10 | 6.5/10 |
| **grok-4.20-reasoning** | 95% | 100% | 75% | 5.5/10 | 5.5/10 | 6.0/10 | 5.5/10 | 6.5/10 | 6.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **claude-opus-5**:
  - grok-4.5: Draft is truncated mid-moose-hunt and never reaches Yeehat attack, Thornton's death, or Ghost Dog ending
  - grok-4.5: Opening estate scene is cut off mid-sentence
- **gemini-3.6-flash**:
  - grok-4.5: Skips or collapses major beats (Narwhal voyage, full mail-train arc, Forty-Mile rescue detail)
  - grok-4.5: Generic character tokens (STRANGER, MAN IN RED SWEATER) and inconsistent Buck coat color
- **gemini-3.1-pro-preview**:
  - grok-4.5: Omits or collapses large stretches (full kidnapping sequence detail, Dave's death, mail-train suffering, Forty-Mile rescue, thousand-pound wager staging)
  - grok-4.5: Several multi-location scenes and thin cast descriptions
- **grok-4.20-reasoning**:
  - grok-4.5: Severe compression that drops or summarizes entire chapters into single VO blocks
  - grok-4.5: Multiple scenes cram several locations and time jumps into one heading
  - grok-4.5: Weak cast locking and thin sound beds

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-luna | gpt-5.6-terra | gpt-5.6-sol | claude-opus-5 | gemini-3.6-flash | gemini-3.1-pro-preview | grok-4.20-reasoning |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 8.8 | 8.3 | 8.4 | 7.6 | 7.2 | 6.2 | 5.2 |
| **grok-4** | 8.6 | 8.2 | 7.9 | 8.6 | 8.1 | 6.9 | 6.5 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay A because it delivers the most complete, book-faithful arc with consistent cast locking, clip-friendly action, and rich per-scene sound design. Weakest: Screenplay E because extreme compression turns major chapters into unfilmable VO summaries and collapses multiple locations into single headings, making AI clip breakdown unusable.
- **grok-4:** Strongest: Screenplay C because of superior fidelity, consistent character visuals, and clip-optimized structure with explicit sound cues. Weakest: Screenplay B because of excessive summarization that drops beats and lacks consistent visual locking for characters.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 31
- **Dialogue Blocks:** 75 (Avg `14.9` words/turn, Max `40` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Near-complete coverage of the novel's spine from Santa Clara through the Ghost Dog coda, with book-faithful dialogue and dense interleaved (SOUND)/(MUSIC) cues on every scene. Character looks (Buck's white chest, red sweater, Mercedes/Hal/Charles) stay locked. Slight compression of secondary trail incidents and a few multi-beat scenes keep it just shy of perfect for 5-10s clip slicing, but it is the most production-ready full arc.
  - *grok-4:* Excellent fidelity, locked visuals for Buck, concrete actions, and strong sound design; pacing and transitions optimized for short clips.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require that any scene intended for multi-clip coverage end each micro-beat on a single observable action or sound so editors can cut on the beat without rewriting action lines.
  - *grok-4:* Add an explicit rule requiring a short closing beat returning to any established on-camera frame immediately before FADE OUT, unless no such frame was ever established.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 20
- **Dialogue Blocks:** 52 (Avg `11.6` words/turn, Max `30` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Strong closed-cast naming (Harris, Murphy, Tom) and consistent Buck markings; hits kidnapping, club, Curly, Spitz death, Dave, Hal/Mercedes collapse, Thornton love, thousand-pound pull, Yeehats, and pack join. Slightly thinner on the mail-train middle and moose hunt than A/C, and a few scenes pack two locations, but still fully filmable and faithful.
  - *grok-4:* High fidelity to book beats with consistent Buck visuals (brown muzzle, white blaze) and explicit sound cues in every scene; minor pacing compression for clip length.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a hard rule that travel or time-compression montages must be written as 2-3 concrete micro-locations with their own headings rather than a single summary slug spanning days.
  - *grok-4:* Add an explicit rule requiring every recurring character to have 2-3 locked visual traits restated in the first action line of any scene after a time gap or re-entry.

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 42 (Avg `16.1` words/turn, Max `38` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *grok-4.5:* Excellent locked visual identity for Buck (dark sable, brown muzzle/brows, white blaze restated on re-entry) and clear supporting cast. Full arc with strong peak interleaving on Spitz fight, pull, and Yeehat attack. Slightly less dense sound design than A and minor trail compression, but highly directable and faithful.
  - *grok-4:* Good fidelity with consistent visuals and sound cues; pacing works for clips but some transitions feel abrupt in mail-hauling sections.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require every scene, including quiet camp or travel beats, to carry at least one concrete diegetic (SOUND:) cue; forbid generic placeholders such as 'music plays'.
  - *grok-4:* Add an explicit rule to merge consecutive beats sharing the same location and time of day into one scene unless place, time, or dramatic purpose clearly changes.

### 🎬 claude-opus-5
- **Scene Headings Count:** 42
- **Dialogue Blocks:** 121 (Avg `16.6` words/turn, Max `52` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 10 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Best-in-class fidelity and sound design for the middle third (Le Barge invasion, Dolly madness, Spitz death, Dave's heartbreak, Hal/Mercedes cruelty, thousand-pound pull) with excellent book diction, but the screenplay literally ends mid-action on the moose hunt and omits the entire resolution. Unusable as a finished 10-minute film.
  - *grok-4:* Exceptional fidelity with detailed book-accurate visuals, consistent character traits, and interleaved sound cues; strong clip-friendly breakdown.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate an explicit end-to-end arc check: after the final action line the writer must confirm setup, crisis, and resolution are all present before emitting FADE OUT; reject any draft that stops mid-sequence.
  - *grok-4:* Add an explicit rule requiring at least one (SOUND:) or (MUSIC:) cue in every scene, with two distinct audio beats at major emotional peaks.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 37 (Avg `18.8` words/turn, Max `58` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 9 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *grok-4.5:* Covers the emotional spine and lands the Ghost Dog button, but compresses too aggressively, invents minor connective tissue, and lets cast looks drift (silk-black coat vs earlier dark). Sound cues exist but are thinner at peaks. Workable only after heavy revision.
  - *grok-4:* Strong fidelity and consistent Buck description with good sound integration; minor compression in later trail sections affects pacing slightly.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Strengthen the CHARACTER RE-ENTRY rule to require the same 2-3 locked visual traits on every reappearance and forbid epithet-only cues (MAN IN RED SWEATER) once a proper name token has been established.
  - *grok-4:* Add an explicit rule that when a single V.O. passage exceeds three sentences over one static action line, it must be broken with a one-line visual or sound micro-beat.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 21 (Avg `27` words/turn, Max `56` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 5 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Hits iconic set-pieces (club, Spitz death, Thornton rescue, Yeehats, pack) with decent book lines, but the middle is gutted and many scenes are summary-heavy. Sound cues are sparse outside peaks. Not greenlight-ready without substantial expansion.
  - *grok-4:* Solid coverage but some paraphrased dialogue and less consistent visual locking for Buck; sound cues present but pacing occasionally rushed.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add an explicit minimum-beat checklist for novel-length sources: the adaptation must dramatize (not merely VO-summarize) the protagonist's three deepest losses or turning points before the finale.
  - *grok-4:* Add an explicit rule to prefer exact book wording for iconic lines and limit paraphrasing to whole-beat cuts only.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 10
- **Dialogue Blocks:** 21 (Avg `24.7` words/turn, Max `57` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 5 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *grok-4.5:* Reads like a highlight reel rather than a screenplay: Curly, Spitz fight, Hal arc, Thornton, and ending are present but so telescoped that AI clip breakdown becomes impossible. Heavy reliance on NARRATOR summarizing action violates show-don't-tell and directibility.
  - *grok-4:* Excessive summarization drops key beats and reduces visual specificity; character descriptions lack consistent locking across scenes.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Cap NARRATOR VO at two short sentences per action beat and require every major plot turn (fight, death, rescue) to be carried by concrete visible action lines, not summary narration.
  - *grok-4:* Add an explicit rule that every scene must contain at least one concrete camera-observable action line before any dialogue or V.O., with no more than two distinct actions per scene heading.

