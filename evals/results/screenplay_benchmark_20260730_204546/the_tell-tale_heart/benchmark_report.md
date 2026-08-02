# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 02:51:46 UTC*  
*Source Story File: `The_Tell-Tale_Heart.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **91.2** | 93.0% | 90.0% | 8 pts | 1.0 |
| 🥈  | **gemini-3.6-flash** | **90.6** | 94.0% | 88.0% | 7 pts | 2.0 |
| 🥉  | **claude-sonnet-5** | **89.6** | 95.2% | 86.0% | 6 pts | 3.0 |
| 4.  | **gemini-2.5-flash** | **85.2** | 93.0% | 80.0% | 4 pts | 5.0 |
| 5.  | **gpt-4o** | **83.7** | 93.0% | 78.0% | 5 pts | 4.0 |
| 6.  | **o3-mini** | **79.2** | 93.0% | 70.0% | 3 pts | 6.0 |
| 7.  | **gpt-4o-mini** | **74.7** | 95.5% | 61.0% | 2 pts | 7.0 |
| 8.  | **grok-4** | **69.0** | 85.0% | 58.0% | 1 pts | 8.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 50% | 9.5/10 | 9.5/10 | 9.0/10 | 8.5/10 | 9.0/10 | 8.5/10 |
| **gemini-3.6-flash** | 100% | 100% | 70% | 9.0/10 | 9.0/10 | 9.0/10 | 8.5/10 | 8.5/10 | 9.0/10 |
| **claude-sonnet-5** | 100% | 100% | 95% | 9.0/10 | 9.0/10 | 8.5/10 | 8.0/10 | 8.5/10 | 8.5/10 |
| **gemini-2.5-flash** | 100% | 100% | 50% | 8.5/10 | 8.0/10 | 8.0/10 | 7.5/10 | 8.0/10 | 8.0/10 |
| **gpt-4o** | 95% | 100% | 80% | 8.0/10 | 8.5/10 | 7.5/10 | 7.0/10 | 8.0/10 | 7.5/10 |
| **o3-mini** | 95% | 100% | 80% | 6.5/10 | 7.0/10 | 7.0/10 | 7.5/10 | 7.0/10 | 7.0/10 |
| **gpt-4o-mini** | 100% | 100% | 100% | 5.5/10 | 6.5/10 | 6.0/10 | 6.0/10 | 6.5/10 | 6.0/10 |
| **grok-4** | 100% | 60% | 90% | 3.0/10 | 6.0/10 | 7.0/10 | 6.5/10 | 6.0/10 | 6.5/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **o3-mini**:
  - grok-4: Invented officer names and condensed beats drop source fidelity
- **gpt-4o-mini**:
  - grok-4: Significant condensation drops source beats and mixes narration awkwardly
- **grok-4**:
  - grok-4: Major source beats dropped including week of kindness and full dismemberment sequence

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gemini-3.6-flash | claude-sonnet-5 | gemini-2.5-flash | gpt-4o | o3-mini | gpt-4o-mini | grok-4 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* |
| **grok-4** | 9.0 | 8.8 | 8.6 | 8.0 | 7.8 | 7.0 | 6.1 | **5.8** *(self)* |

### 🗣️ Judge Summary Notes
- **grok-4.5:** ⚠️ Mock judge evaluation returned for grok-4.5.
- **grok-4:** Strongest: Screenplay C because it delivers the highest fidelity, consistent character visuals, and clip-ready concrete actions without invention. Weakest: Screenplay G because it drops multiple major source beats and condenses the story to the point of unusable adaptation fidelity.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 18 (Avg `34.7` words/turn, Max `70` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ High average dialogue length (34.7 words/turn); speech beats risk clip overrun.
  - ⚠️ 9 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5 (self):* [MOCK / FAILED JUDGE] Model 'grok-4.5' failed or was skipped for candidate 'Screenplay F'.
  - *grok-4:* Highly faithful, consistent character visuals, concrete actions suited to short clips.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 9
- **Dialogue Blocks:** 38 (Avg `19.8` words/turn, Max `44` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ 6 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* [MOCK / FAILED JUDGE] Model 'grok-4.5' failed or was skipped for candidate 'Screenplay E'.
  - *grok-4:* Strong fidelity and visual specificity with excellent heartbeat sound integration.

### 🎬 claude-sonnet-5
- **Scene Headings Count:** 9
- **Dialogue Blocks:** 36 (Avg `20.6` words/turn, Max `39` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* [MOCK / FAILED JUDGE] Model 'grok-4.5' failed or was skipped for candidate 'Screenplay D'.
  - *grok-4:* Very close to source with clear visuals but slightly less clip-optimized pacing than C/D.

### 🎬 gemini-2.5-flash
- **Scene Headings Count:** 9
- **Dialogue Blocks:** 37 (Avg `40` words/turn, Max `70` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ High average dialogue length (40 words/turn); speech beats risk clip overrun.
  - ⚠️ 22 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* [MOCK / FAILED JUDGE] Model 'grok-4.5' failed or was skipped for candidate 'Screenplay H'.
  - *grok-4:* Solid fidelity but heavier V.O. and less distinct officer descriptions.

### 🎬 gpt-4o
- **Scene Headings Count:** 20
- **Dialogue Blocks:** 0 (Avg `0` words/turn, Max `0` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* [MOCK / FAILED JUDGE] Model 'grok-4.5' failed or was skipped for candidate 'Screenplay G'.
  - *grok-4:* Faithful narration but repetitive scene headings and heavy V.O. limit clip-friendly pacing.

### 🎬 o3-mini
- **Scene Headings Count:** 8
- **Dialogue Blocks:** 0 (Avg `0` words/turn, Max `0` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* [MOCK / FAILED JUDGE] Model 'grok-4.5' failed or was skipped for candidate 'Screenplay A'.
  - *grok-4:* Some invented content and less consistent visual descriptions.

### 🎬 gpt-4o-mini
- **Scene Headings Count:** 6
- **Dialogue Blocks:** 32 (Avg `12.8` words/turn, Max `26` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* [MOCK / FAILED JUDGE] Model 'grok-4.5' failed or was skipped for candidate 'Screenplay B'.
  - *grok-4:* Too abbreviated with fidelity and structure issues.

### 🎬 grok-4
- **Scene Headings Count:** 4
- **Dialogue Blocks:** 11 (Avg `22.4` words/turn, Max `57` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Too few scene headings (4); story lacks visual scene progression.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* [MOCK / FAILED JUDGE] Model 'grok-4.5' failed or was skipped for candidate 'Screenplay C'.
  - *grok-4 (self):* Severely abbreviated, missing key narrative coverage.

