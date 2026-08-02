# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 12:06:47 UTC*  
*Source Story File: `A_Christmas_Carol.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **92.8** | 99.5% | 88.0% | 13 pts | 1.5 |
| 🥈  | **gpt-5.6-terra** | **91.1** | 99.0% | 86.0% | 12 pts | 2.0 |
| 🥉  | **gpt-5.6-luna** | **88.8** | 97.0% | 83.0% | 11 pts | 2.5 |
| 4.  | **grok-4.20-reasoning** | **84.4** | 98.5% | 75.0% | 8 pts | 4.0 |
| 5.  | **gemini-3.6-flash** | **82.9** | 98.5% | 72.0% | 5 pts | 5.5 |
| 6.  | **gemini-3.1-pro-preview** | **78.9** | 98.5% | 66.0% | 3 pts | 6.5 |
| 7.  | **claude-opus-5** | **77.6** | 97.0% | 65.0% | 4 pts | 6.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 100% | 100% | 90% | 9.0/10 | 9.2/10 | 8.8/10 | 8.2/10 | 9.0/10 | 8.8/10 |
| **gpt-5.6-terra** | 100% | 100% | 80% | 8.8/10 | 9.0/10 | 8.5/10 | 8.0/10 | 8.8/10 | 8.5/10 |
| **gpt-5.6-luna** | 95% | 100% | 70% | 8.5/10 | 8.5/10 | 8.2/10 | 7.8/10 | 8.8/10 | 8.2/10 |
| **grok-4.20-reasoning** | 100% | 100% | 70% | 7.2/10 | 7.2/10 | 7.2/10 | 7.2/10 | 8.0/10 | 8.0/10 |
| **gemini-3.6-flash** | 100% | 100% | 70% | 6.2/10 | 8.0/10 | 7.8/10 | 7.2/10 | 6.2/10 | 8.0/10 |
| **gemini-3.1-pro-preview** | 100% | 100% | 70% | 6.0/10 | 6.8/10 | 7.0/10 | 6.5/10 | 6.2/10 | 7.0/10 |
| **claude-opus-5** | 95% | 100% | 70% | 3.5/10 | 8.2/10 | 6.8/10 | 4.0/10 | 8.0/10 | 8.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gemini-3.6-flash**:
  - grok-4.5: Major dialogue attribution inversion on the iconic Marley 'You don't believe in me' exchange
  - grok-4.5: Invented park-bench location for the Belle breakup
- **gemini-3.1-pro-preview**:
  - grok-4.5: Major dialogue attribution inversion on Marley belief exchange
  - grok-4.5: Drops core Past beats (solitary schoolboy and Fan rescue almost entirely absent)
- **claude-opus-5**:
  - grok-4.5: Screenplay truncates mid-sentence and mid-climax during the grave reveal, omitting the entire redemption/Stave V resolution
  - grok-4: Incomplete screenplay that cuts off mid-sentence during the churchyard reveal, breaking the required full arc coverage.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | gpt-5.6-terra | gpt-5.6-luna | grok-4.20-reasoning | gemini-3.6-flash | gemini-3.1-pro-preview | claude-opus-5 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 9.0 | 8.5 | 8.5 | 7.3 | 6.8 | 6.1 | 6.8 |
| **grok-4** | 8.7 | 8.7 | 8.2 | 7.7 | 7.7 | 7.1 | 6.2 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay F because it alone combines near-perfect source fidelity, explicit multi-age casting locks restated on every re-entry, and consistently clip-length concrete actions with dense diegetic sound. Weakest: Screenplay C because catastrophic mid-climax truncation omits the entire redemption, rendering an otherwise richly detailed draft unusable.
- **grok-4:** Strongest: Screenplay A because it achieves the highest combined fidelity, character consistency, and audio integration without any invented frames. Weakest: Screenplay C because it is fatally incomplete and cuts off mid-sentence, violating the full-arc requirement.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 19
- **Dialogue Blocks:** 103 (Avg `13.7` words/turn, Max `46` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY SCROOGE, YOUNG SCROOGE, TINY TIM`
- **Diagnostics & Warnings:**
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 3 age-qualified character header(s) (e.g. BOY SCROOGE, YOUNG SCROOGE, TINY TIM).
  - ⚠️ Detected 4 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Highest overall: complete faithful arc, crystal-clear BOY SCROOGE / YOUNG SCROOGE / adult locks restated on re-entry, every action is a single observable 5-10 s beat, iconic dialogue preserved verbatim, and sound/music cues are dense and diegetic at every emotional peak.
  - *grok-4:* Exceptionally close to source text with precise dialogue, consistent age-disambiguated visuals, and dense audio cues in every scene.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a runtime-shape rule that peak emotional scenes (death reveal, confession, reunion) must interleave at least two distinct (SOUND/MUSIC) cues with two visual micro-beats rather than one long VO or one static image.
  - *grok-4:* Add an explicit rule requiring every scene to include at least one concrete diegetic (SOUND: ...) cue before any non-diegetic (MUSIC: ...) element.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 23
- **Dialogue Blocks:** 117 (Avg `12` words/turn, Max `39` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG SCROOGE, TINY TIM, OLD JOE`
- **Diagnostics & Warnings:**
  - ⚠️ 4 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 3 age-qualified character header(s) (e.g. YOUNG SCROOGE, TINY TIM, OLD JOE).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Excellent closed-cast naming and age-split visuals (Young Scrooge restated cleanly); hits every spine beat and most iconic dialogue with only minor paraphrase; actions are camera-ready and scenes stay single-purpose. Slight compression of Belle family and Fred party is acceptable for runtime.
  - *grok-4:* Highly faithful to source beats and dialogue with consistent character visuals and age disambiguation; strong audio cues and filmable actions throughout.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require that summarized book exchanges (no quoted speech) receive at most one generic period line total, defaulting to pure action description to prevent any invented banter.
  - *grok-4:* Add an explicit rule requiring every scene to include at least one concrete diegetic (SOUND: ...) cue before any non-diegetic (MUSIC: ...) element.

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 23
- **Dialogue Blocks:** 138 (Avg `13.9` words/turn, Max `48` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `TINY TIM, OLD JOE`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 9 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 2 age-qualified character header(s) (e.g. TINY TIM, OLD JOE).
- **Judge Rationale:**
  - *grok-4.5:* Strong full-arc coverage of all major beats and iconic lines with only light compression; consistent Scrooge/Bob/young variants and concrete single-location actions that clip cleanly; minor duplicate Bob cue and occasional multi-beat scenes are fixable. Sound cues are present and useful throughout.
  - *grok-4:* Strong fidelity and character descriptions with good audio, but slightly less dense visual micro-beats than top entries.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a hard rule that every character re-entry after any time jump or age shift must restate 2-3 locked visual traits in the opening action line of that scene.
  - *grok-4:* Add an explicit rule requiring at least two distinct visual or sound micro-beats at every major emotional peak rather than a single summary sentence.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 14
- **Dialogue Blocks:** 62 (Avg `17.5` words/turn, Max `118` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `TINY TIM, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 7 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 2 age-qualified character header(s) (e.g. TINY TIM, BOY).
- **Judge Rationale:**
  - *grok-4.5:* Functional full arc with a useful on-camera narrator frame and good sound, but Past is heavily summarized (school/Fan/Fezziwig reduced to fragments), character looks are thinly restated, and some actions remain summary rather than concrete clip-able beats.
  - *grok-4:* Adds an invented narrator study frame not in the source, slightly reducing fidelity, but maintains good character consistency and audio cues.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require that any on-camera narrator or frame device be established with a stable heading and used for at least three cut-backs (setup, peak, close) rather than a single book-end, and that all major source set-pieces receive at least two concrete visual micro-beats.
  - *grok-4:* Add an explicit rule prohibiting the introduction of any new frame locations, narrator studies, or characters not present in the source text.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 19
- **Dialogue Blocks:** 129 (Avg `15.8` words/turn, Max `59` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG SCROOGE, YOUNG ADULT SCROOGE, TINY TIM, OLD JOE, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 12 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 5 age-qualified character header(s) (e.g. YOUNG SCROOGE, YOUNG ADULT SCROOGE, TINY TIM).
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Solid visual structure and sound, decent age splits, but the reversed Marley/Scrooge belief lines and the non-book Belle location are severe fidelity breaches that break trust in the adaptation; remaining dialogue is otherwise close.
  - *grok-4:* Uses an invented narrator frame similar to B but otherwise faithful with strong audio integration and character consistency.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate that every quoted or near-quoted exchange preserve exact speaker identity from the source text; add a verification step forbidding any swap of who speaks which line.
  - *grok-4:* Add an explicit rule prohibiting the introduction of any new frame locations, narrator studies, or characters not present in the source text.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 86 (Avg `17.8` words/turn, Max `55` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG SCROOGE, TINY TIM, OLD JOE`
- **Diagnostics & Warnings:**
  - ⚠️ 10 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 3 age-qualified character header(s) (e.g. YOUNG SCROOGE, TINY TIM, OLD JOE).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Readable short-clip structure and adequate sound, but inverted Marley lines plus near-total omission of the school/Fan sequence fatally damage fidelity; character looks are thinly described after first appearance and Past feels rushed.
  - *grok-4:* Covers core beats with some paraphrased dialogue and minor invented names, but includes required audio cues and consistent visuals.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Explicitly require that every major emotional set-piece named in the source (childhood isolation, sibling rescue, workplace feast, romantic rupture, family dinner, scavenger scene, grave) appear as its own headed scene with book dialogue intact.
  - *grok-4:* Add an explicit rule mandating that all dialogue must preserve the source's exact wording and cadence unless runtime compression requires dropping entire minor beats.

### 🎬 claude-opus-5
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 199 (Avg `17.3` words/turn, Max `85` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG EBENEZER, TINY TIM, OLD JOE`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 27 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 3 age-qualified character header(s) (e.g. YOUNG EBENEZER, TINY TIM, OLD JOE).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Up to the cut-off the text is the most faithful and richly detailed adaptation with superb multi-age tokens (Young Ebenezer / Apprentice / In His Prime) and dense sound, but the catastrophic truncation makes the package unusable and destroys pacing/fidelity scores.
  - *grok-4:* Strong visual descriptions and audio but fatally incomplete, ending abruptly without resolution or THE END.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add an explicit completeness check: the final Fountain must contain setup, every major turn, and a full resolution ending with FADE OUT; never emit a draft that ends mid-scene or mid-sentence.
  - *grok-4:* Add an explicit rule requiring the final scene to include a complete resolution beat and the FADE OUT / THE END marker before any truncation.

