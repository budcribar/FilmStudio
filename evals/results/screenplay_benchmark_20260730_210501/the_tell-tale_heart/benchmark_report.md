# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 03:12:03 UTC*  
*Source Story File: `The_Tell-Tale_Heart.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **92.4** | 93.5% | 92.0% | 18 pts | 1.0 |
| 🥈  | **gemini-3.6-flash** | **87.1** | 94.0% | 82.0% | 12 pts | 4.0 |
| 🥉  | **gpt-5.6-terra** | **87.0** | 95.5% | 81.0% | 13 pts | 3.5 |
| 4.  | **claude-sonnet-5** | **86.8** | 95.2% | 81.0% | 13 pts | 3.5 |
| 5.  | **grok-4.5** | **86.7** | 93.0% | 82.0% | 13 pts | 3.5 |
| 6.  | **gemini-2.5-flash** | **83.9** | 93.0% | 78.0% | 9 pts | 5.5 |
| 7.  | **gpt-4o-mini** | **62.0** | 95.5% | 40.0% | 5 pts | 7.5 |
| 8.  | **o3-mini** | **58.0** | 93.0% | 35.0% | 4 pts | 8.0 |
| 9.  | **grok-4** | **53.0** | 85.0% | 32.0% | 3 pts | 8.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 95% | 100% | 90% | 9.5/10 | 9.5/10 | 9.2/10 | 9.0/10 | 9.0/10 | 8.8/10 |
| **gemini-3.6-flash** | 100% | 100% | 70% | 7.8/10 | 9.0/10 | 8.2/10 | 8.0/10 | 7.8/10 | 8.8/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 8.5/10 | 8.5/10 | 8.2/10 | 8.2/10 | 8.0/10 | 7.2/10 |
| **claude-sonnet-5** | 100% | 100% | 95% | 8.2/10 | 8.2/10 | 8.2/10 | 8.0/10 | 8.2/10 | 7.8/10 |
| **grok-4.5** | 100% | 100% | 50% | 9.0/10 | 8.0/10 | 8.2/10 | 8.2/10 | 8.8/10 | 7.2/10 |
| **gemini-2.5-flash** | 100% | 100% | 50% | 8.2/10 | 8.0/10 | 7.5/10 | 7.5/10 | 7.8/10 | 7.8/10 |
| **gpt-4o-mini** | 100% | 100% | 100% | 4.0/10 | 3.8/10 | 4.2/10 | 4.0/10 | 4.0/10 | 3.8/10 |
| **o3-mini** | 95% | 100% | 80% | 3.5/10 | 3.5/10 | 3.5/10 | 3.2/10 | 3.5/10 | 3.5/10 |
| **grok-4** | 100% | 60% | 90% | 2.8/10 | 3.0/10 | 3.5/10 | 2.8/10 | 3.8/10 | 3.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-4o-mini**:
  - grok-4.5: Virtually no consistent visual character descriptions for reference-image lock
  - grok-4.5: Internal monologue dumped as spoken dialogue unfilmable in short clips
  - grok-4.5: Major source beats rushed or collapsed; invented officer name without payoff
  - grok-4.5: Scene structure crams multiple actions and fails 5-10s beat constraint
  - grok-4: Major beats heavily condensed or dropped; insufficient visual specificity for short clips
- **o3-mini**:
  - grok-4.5: Major beats omitted or paraphrased into incoherence
  - grok-4.5: No character physical descriptions whatsoever
  - grok-4.5: Vague, non-concrete action lines ('chaos collides') unfilmable
  - grok-4.5: Broken, incomplete structure that cannot support clip breakdown
  - grok-4: Invented framing and condensed beats violate fidelity rules
- **grok-4**:
  - grok-4.5: Major source beats dropped or reduced to single-sentence summaries
  - grok-4.5: No usable character visual descriptions
  - grok-4.5: Multi-action scenes impossible to split into 5-10s clips
  - grok-4.5: Zero tension escalation or dramatic structure
  - grok-4: Severe dropped beats and overly condensed structure; unusable for required coverage

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | gemini-3.6-flash | gpt-5.6-terra | claude-sonnet-5 | grok-4.5 | gemini-2.5-flash | gpt-4o-mini | o3-mini | grok-4 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 9.3 | 7.8 | 8.3 | 8.2 | **8.0** *(self)* | 7.8 | 3.2 | 2.0 | 2.6 |
| **grok-4** | 9.0 | 8.7 | 7.9 | 8.0 | 8.5 | 7.8 | 4.7 | 4.9 | **3.8** *(self)* |

### 🧐 Self-Bias Analysis
- ⚠️ grok-4 rated its own screenplay 3.8/10 vs. a 2.6/10 average from 1 other judge(s) (+1.2) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay H because it alone combines near-perfect source fidelity, locked character visuals, and every action written as a discrete 5-10s filmable beat with fully designed sound escalation. Weakest: Screenplay G because it omits or mangles core beats, supplies zero casting descriptions, and writes vague undirectable abstractions that cannot be broken into AI clips.
- **grok-4:** Strongest: Screenplay I because it delivers near-perfect fidelity, consistent character visuals, and precisely filmable short-clip beats. Weakest: Screenplay F because it drops major source beats and lacks any usable visual or pacing structure for the required format.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 41 (Avg `19` words/turn, Max `38` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* H is the gold standard: exhaustive fidelity, locked visual portraits for every character (including three distinct officers), and every beat written as a single observable 5-10s action. Tension escalates with surgical precision; SFX (deathwatch, THUMP, chair grate, candle) form a complete sound design. Dialogue is performable and authentic. Immediate greenlight.
  - *grok-4:* Highest fidelity, clearest visuals, and best clip pacing.

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
  - *grok-4.5:* D adds an asylum-cell frame (mild invention) yet covers the core plot with strong visual casting (Narrator 30s hollow-eyed Victorian; distinct officers). Actions and THUMP progression are highly clip-directible and the sound bed is the best of the set. Some added dialogue softens authenticity. Solid and shootable.
  - *grok-4:* Highly faithful with consistent visuals and strong clip-friendly actions.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 8
- **Dialogue Blocks:** 42 (Avg `16.2` words/turn, Max `35` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* B delivers near-complete beat coverage with clear visual locks (lean pale Narrator in dark waistcoat; frail white-haired Old Man; three distinct officers). Actions are concrete and clip-sized; seven nights and eighth-night hour are shown, not told. Dialogue stays sparse and performable. Sound relies on heartbeat escalation but lacks explicit underscore prompts. Strong, green-lightable draft.
  - *grok-4:* Solid fidelity but slightly less consistent character details across scenes.

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
  - *grok-4.5:* I is a polished, high-fidelity adaptation with strong confession framing and clear visual anchors for Narrator and Old Man (officers slightly thinner). Beats are cleanly separable for short clips; pacing and dialogue stay true to Poe. Sound design is functional but less layered than H or D. Fully production-ready.
  - *grok-4:* Faithful with good visuals but confession framing slightly disrupts flow.

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
  - *grok-4.5 (self):* C is nearly verbatim in spirit and structure, using a candlelit confession frame that serves the short-clip form well. Narrator and Old Man have solid visual anchors; officers remain under-described. Directibility is high via clear physical beats and VO. Pacing and dialogue are excellent; sound design is only lightly indicated. Production-ready with minor casting polish needed.
  - *grok-4:* Excellent fidelity and consistent character descriptions.

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
  - *grok-4.5:* F covers the story thoroughly with a confession-room bookend and clear (if slightly generic) character looks. Most actions are filmable, though some continuous blocks run long for 5-10s cuts. Heartbeat and clock SFX are well integrated. Minor invented business (hands over ears) does not break fidelity. Ready with light trimming.
  - *grok-4:* Strong overall but pacing occasionally overloaded for clip constraint.

### 🎬 gpt-4o-mini
- **Scene Headings Count:** 6
- **Dialogue Blocks:** 32 (Avg `12.8` words/turn, Max `26` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* A is a thin, dialogue-heavy condensation that turns Poe's interiority into on-camera speech, supplies almost zero casting visuals, and collapses the seven-night ritual, hour-long wait, and police slow-burn into unusable lumps. Heartbeat is mentioned but never designed as a progressive sound bed. Unusable for AI clip production.
  - *grok-4:* Too summarized, loses source coverage and directibility.

### 🎬 o3-mini
- **Scene Headings Count:** 8
- **Dialogue Blocks:** 0 (Avg `0` words/turn, Max `0` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* G is the weakest package: it skips the seven-night campaign, mangles the police sequence, supplies zero casting information, and writes undirectable abstractions. Completely unusable for AI video production.
  - *grok-4:* Too many alterations and insufficient visual action lines.

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
  - *grok-4.5:* E is a skeletal synopsis, not a screenplay. Seven nights, the hour wait, dismemberment detail, and police agony are all collapsed; characters lack any locking description; scenes pack entire sequences into one heading. Unusable for the required short-clip pipeline.
  - *grok-4 (self):* Broken fidelity and pacing; major source elements omitted.

