# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 03:08:24 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **91.0** | 95.0% | 88.0% | 18 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **89.2** | 95.5% | 85.0% | 15 pts | 2.5 |
| 🥉  | **claude-sonnet-5** | **89.2** | 94.2% | 86.0% | 15 pts | 2.5 |
| 4.  | **grok-4.5** | **86.7** | 95.5% | 81.0% | 12 pts | 4.0 |
| 5.  | **gemini-2.5-flash** | **83.8** | 94.0% | 77.0% | 10 pts | 5.0 |
| 6.  | **gemini-3.6-flash** | **77.4** | 93.5% | 67.0% | 8 pts | 6.0 |
| 7.  | **grok-4** | **67.1** | 81.5% | 58.0% | 6 pts | 7.0 |
| 8.  | **gpt-4o-mini** | **56.9** | 89.0% | 35.0% | 4 pts | 8.0 |
| 9.  | **o3-mini** | **56.0** | 97.0% | 29.0% | 2 pts | 9.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 100% | 100% | 90% | 9.5/10 | 9.5/10 | 9.0/10 | 8.8/10 | 8.8/10 | 7.5/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 9.0/10 | 9.2/10 | 8.5/10 | 8.2/10 | 8.8/10 | 7.2/10 |
| **claude-sonnet-5** | 100% | 100% | 75% | 9.2/10 | 9.0/10 | 8.8/10 | 8.5/10 | 8.8/10 | 7.2/10 |
| **grok-4.5** | 100% | 100% | 100% | 8.8/10 | 8.5/10 | 8.2/10 | 8.0/10 | 8.2/10 | 6.8/10 |
| **gemini-2.5-flash** | 100% | 100% | 70% | 8.5/10 | 8.0/10 | 7.8/10 | 7.5/10 | 8.0/10 | 6.5/10 |
| **gemini-3.6-flash** | 95% | 100% | 90% | 6.5/10 | 7.2/10 | 7.0/10 | 6.2/10 | 7.2/10 | 5.8/10 |
| **grok-4** | 100% | 50% | 70% | 5.0/10 | 6.2/10 | 5.8/10 | 5.0/10 | 7.0/10 | 5.5/10 |
| **gpt-4o-mini** | 95% | 80% | 100% | 3.2/10 | 4.0/10 | 3.5/10 | 3.2/10 | 4.2/10 | 3.0/10 |
| **o3-mini** | 100% | 100% | 100% | 2.8/10 | 2.8/10 | 2.5/10 | 3.0/10 | 3.5/10 | 2.8/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gemini-3.6-flash**:
  - grok-4.5: Dropped major beats (full yoga/Buddhism arc, detailed college, Bob reunion depth, sage dream)
  - grok-4.5: Over-condensed structure skips source coverage
- **grok-4**:
  - grok-4.5: Invented protagonist name JAKE (book reveals Peter only at end)
  - grok-4.5: Excess length and novelistic internals unsuited to 5-10s clips
  - grok-4.5: Some invented expansions
  - grok-4: Heavy narration and invented scenes; inconsistent character visuals
- **gpt-4o-mini**:
  - grok-4.5: Major invented confrontations and incomplete/dropped beats (yoga details, full college arc, Bob scenes, sage dream specifics)
  - grok-4.5: Inconsistent unnamed YOUNG MAN / NARRATOR with no age disambiguation or locked visual refs
  - grok-4.5: Fragmented non-clip structure with multi-location crams and heavy unfilmable V.O.
  - grok-4: Heavy narration violates directibility; invented plot events and dropped beats; no consistent visual descriptions
- **o3-mini**:
  - grok-4.5: Massive invented plot (montages, headlines, altered confrontations, happy Sionna reunion)
  - grok-4.5: Vague unnamed characters with no visual consistency or age tags
  - grok-4.5: Montage structure unusable for single 5-10s beats
  - grok-4: Heavy narration; invented plot events; no consistent visual descriptions

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | gpt-5.6-terra | claude-sonnet-5 | grok-4.5 | gemini-2.5-flash | gemini-3.6-flash | grok-4 | gpt-4o-mini | o3-mini |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 8.8 | 8.4 | 8.4 | **7.9** *(self)* | 7.3 | 5.6 | 4.8 | 3.9 | 2.2 |
| **grok-4** | 8.8 | 8.6 | 8.8 | 8.2 | 8.1 | 7.8 | **6.8** *(self)* | 3.2 | 3.6 |

### 🧐 Self-Bias Analysis
- ⚠️ grok-4 rated its own screenplay 6.8/10 vs. a 4.8/10 average from 1 other judge(s) (+2.0) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay H because it delivers near-perfect fidelity (including the book's late name reveal), locked visual character consistency across ages, and purely clip-directable single-action scenes with authentic dialogue. Weakest: Screenplay G because it replaces the source with invented montages and plot, uses vague uncastable characters, and is structurally unusable for 5-10s AI beats.
- **grok-4:** Strongest: Screenplay I because it achieves near-perfect fidelity, consistent visual character descriptions, and highly filmable short-clip scenes. Weakest: Screenplay C because heavy narration, invented plot events, and absent character visuals make it unusable for AI video production.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 23
- **Dialogue Blocks:** 107 (Avg `12.9` words/turn, Max `43` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Near-perfect fidelity including late Peter name reveal and all key beats/dreams; locked consistent visuals and explicit young/adult tags; exemplary single-action clip scenes; precise escalation and transitions; book-true performable dialogue; strong atmospheric beds (bar, rain, hospital, prison) implied.
  - *grok-4:* Highest fidelity with precise visual action lines and consistent character references.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 32
- **Dialogue Blocks:** 152 (Avg `11.3` words/turn, Max `34` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* High fidelity to all major beats and themes with minimal invention; excellent consistent visuals (Nick scars/hair, Sionna eyes/necklace/clothes, explicit young Nick/Peter ages); strong single-action clip beats and dream visuals; tight escalating structure; book-authentic short dialogue; atmospheric potential (bar, rain, hospital) but sparse explicit beds.
  - *grok-4:* High fidelity and excellent character consistency; scenes are filmable and pacing is tight for short clips.

### 🎬 claude-sonnet-5
- **Scene Headings Count:** 32
- **Dialogue Blocks:** 151 (Avg `13.1` words/turn, Max `98` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 5 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Excellent source coverage with Narrator-to-Peter reveal and full arc; consistent detailed casting refs and age handling; highly directable visual beats and dreams; clean short-clip pacing; authentic dialogue; good but not explicit sound support.
  - *grok-4:* Exceptional source coverage and visual action lines; consistent character references throughout.

### 🎬 grok-4.5
- **Scene Headings Count:** 41
- **Dialogue Blocks:** 90 (Avg `11.2` words/turn, Max `30` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5 (self):* Strong source coverage with Seattle frame and closed-cast note; solid recurring visuals and age shifts; mostly clip-directable actions though some V.O. density; good rhythm through accident/stabbing/prison; natural dialogue; limited explicit sound prompts.
  - *grok-4:* Tight, visual adaptation with strong pacing; minor sound-design gaps.

### 🎬 gemini-2.5-flash
- **Scene Headings Count:** 23
- **Dialogue Blocks:** 124 (Avg `21.2` words/turn, Max `77` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG MARK, YOUNG NICK`
- **Diagnostics & Warnings:**
  - ⚠️ 18 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 2 age-qualified character header(s) (e.g. YOUNG MARK, YOUNG NICK).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Good overall coverage of core arc and themes; decent recurring descriptions though less rigorous age/wardrobe locks; mostly filmable actions with some V.O. overload; solid momentum; authentic lines; minimal music/sound cues.
  - *grok-4:* Strong fidelity and visuals; minor pacing compression issues.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 60 (Avg `13.7` words/turn, Max `53` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Fidelity hit hard by compression and omissions; basic but inconsistent character locks; some clean visual actions but many summary scenes; pacing rushes key escalations; dialogue serviceable but thinned; almost no sound design support.
  - *grok-4:* Solid fidelity with minor invented framing; consistent visuals but some narration-heavy beats reduce directibility.

### 🎬 grok-4
- **Scene Headings Count:** 71
- **Dialogue Blocks:** 279 (Avg `15.4` words/turn, Max `114` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Excessive scene count (71 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ 16 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Severe fidelity penalty for renaming lead and sprawl; visuals present but name break and inconsistent refs; many multi-beat or internal scenes; pacing drags then jumps; dialogue often book-true; weak sound integration.
  - *grok-4 (self):* Workable but flawed; production issues from narration and pacing.

### 🎬 gpt-4o-mini
- **Scene Headings Count:** 55
- **Dialogue Blocks:** 213 (Avg `7` words/turn, Max `20` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Excessive scene count (55 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. YOUNG MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Severely penalized on fidelity for inventions and drops; characters lack consistent visual locks or young/adult tags; scenes often tell via V.O. rather than single observable actions; pacing collapses into summary jumps; dialogue generic; zero music/sound beds.
  - *grok-4:* Broken on fidelity and directibility; unusable for AI video clips.

### 🎬 o3-mini
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 33 (Avg `15.8` words/turn, Max `30` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG WOMAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. YOUNG WOMAN).
- **Judge Rationale:**
  - *grok-4.5:* Broken fidelity via wholesale invention and summary; characters indistinct; almost nothing camera-concrete or single-beat; no dramatic build; generic dialogue; zero sound design.
  - *grok-4:* Broken on fidelity and directibility; unusable.

