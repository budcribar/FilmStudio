# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 02:49:05 UTC*  
*Source Story File: `Nick_and_Me.txt`*

> ⚠️ **GENERATION FALLBACK DETECTED:** The following models' live API generation failed, and the tool silently substituted a non-AI, book-text-only draft (identical for every failing model). Their rows below do NOT reflect that model's real output and are excluded from multi-book history:
> - **gpt-4o**: Chat HTTP 429: {
    "error": {
        "message": "Rate limit reached for gpt-4o in organization org-Lrf5VruXklblpVn0yFliJuPl on tokens per min (TPM): Limit 30000, Used 20546, Requested 11109. Please try again in 3.31s. Visit https://platform.openai.com/account/rate-limits to learn more.",
        "type": "tokens",
        "param": null,
        "code": "rate_limit_exceeded"
    }
}


## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **claude-sonnet-5** | **89.9** | 94.2% | 87.0% | 15 pts | 1.5 |
| 🥈  | **grok-4.5** | **89.7** | 95.5% | 86.0% | 15 pts | 1.5 |
| 🥉  | **gemini-2.5-flash** | **81.1** | 94.0% | 72.0% | 9 pts | 4.5 |
| 4.  | **gemini-3.6-flash** | **78.6** | 93.5% | 69.0% | 10 pts | 4.0 |
| 5.  | **grok-4** | **74.8** | 81.5% | 70.0% | 10 pts | 4.0 |
| 6.  | **o3-mini** | **66.8** | 97.0% | 47.0% | 7 pts | 5.5 |
| 7.  | **gpt-4o-mini** | **53.9** | 89.0% | 30.0% | 4 pts | 7.0 |
| 8.  | **gpt-4o ⚠️ *(fallback draft, not real output)*** | **31.6** | 79.0% | 0.0% | 0 pts | 4.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **claude-sonnet-5** | 100% | 100% | 75% | 9.5/10 | 9.2/10 | 9.0/10 | 8.5/10 | 8.8/10 | 7.2/10 |
| **grok-4.5** | 100% | 100% | 100% | 9.2/10 | 9.0/10 | 8.8/10 | 8.5/10 | 8.8/10 | 7.2/10 |
| **gemini-2.5-flash** | 100% | 100% | 70% | 8.5/10 | 7.5/10 | 6.5/10 | 7.0/10 | 8.0/10 | 6.0/10 |
| **gemini-3.6-flash** | 95% | 100% | 90% | 6.5/10 | 7.8/10 | 7.5/10 | 6.8/10 | 7.5/10 | 5.2/10 |
| **grok-4** | 100% | 50% | 70% | 5.5/10 | 7.8/10 | 7.8/10 | 7.0/10 | 8.2/10 | 6.0/10 |
| **o3-mini** | 100% | 100% | 100% | 3.8/10 | 5.0/10 | 4.2/10 | 4.8/10 | 5.2/10 | 5.0/10 |
| **gpt-4o-mini** | 95% | 80% | 100% | 2.5/10 | 3.5/10 | 3.2/10 | 2.8/10 | 3.8/10 | 2.5/10 |
| **gpt-4o** | 95% | 50% | 50% | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gemini-2.5-flash**:
  - grok-4: Excessive unfilmable internal monologue and V.O.
  - grok-4: Minimal recurring visual descriptions
- **gemini-3.6-flash**:
  - grok-4: Early use of 'Peter' name violates late-reveal source
  - grok-4: Invented violent door-kick scene
- **grok-4**:
  - grok-4.5: Invented named protagonist (Jake) throughout
  - grok-4.5: Excess length and scene density incompatible with strict short-clip mandate
- **o3-mini**:
  - grok-4.5: Major invented plot (Sionna remains/reconciles, sanitized confrontation, happy ending)
  - grok-4.5: Dropped core beats (detailed stabbing, full dreams, prison specificity, Ma funeral texture)
  - grok-4.5: Unfilmable V.O.-montage structure
  - grok-4: Heavy invented plot events and montage structure
  - grok-4: Excessive unfilmable V.O. narration
  - grok-4: Vague recurring character descriptions
- **gpt-4o-mini**:
  - grok-4.5: Broken fragmented structure
  - grok-4.5: Major invented scenes and incomplete arc
  - grok-4.5: Unusable multi-cut non-clip format
  - grok-4: Severely condensed and summarized beats
  - grok-4: Heavy unfilmable V.O.
  - grok-4: Invented and altered plot points

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | claude-sonnet-5 | grok-4.5 | gemini-2.5-flash | gemini-3.6-flash | grok-4 | o3-mini | gpt-4o-mini | gpt-4o |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 8.8 | **8.3** *(self)* | 8.2 | 6.9 | 6.0 | 3.7 | 2.4 | N/A |
| **grok-4** | 8.6 | 8.8 | 6.2 | 6.8 | **8.1** *(self)* | 5.7 | 3.7 | N/A |

### 🧐 Self-Bias Analysis
- ⚠️ grok-4 rated its own screenplay 8.1/10 vs. a 6.0/10 average from 1 other judge(s) (+2.1) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay C because it delivers the highest source fidelity with consistently locked character visuals and purely observable single-clip action lines ideal for AI video. Weakest: Screenplay F because its fragmented invented structure and near-total absence of filmable beats render it unusable.
- **grok-4:** Strongest: Screenplay G because it delivers the highest fidelity, locked character visuals, and perfectly clip-sized directable action while preserving the book's late name reveal. Weakest: Screenplay F because extreme condensation, heavy V.O., and invented beats render it unusable for the short-clip constraint.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 claude-sonnet-5
- **Scene Headings Count:** 32
- **Dialogue Blocks:** 151 (Avg `13.1` words/turn, Max `98` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 5 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Near-complete source coverage including Nick interior, dreams, pill-box fight, bar brawl, stabbing, breakup, sage dream and prison closer with late Peter reveal; excellent age/wardrobe locks and single-action beats ideal for 5-10s clips; authentic short dialogue; strong atmospheric sound beds.
  - *grok-4:* Excellent fidelity with on-camera frame narrator, precise age/time disambiguation, and highly filmable single-beat scenes.

### 🎬 grok-4.5
- **Scene Headings Count:** 41
- **Dialogue Blocks:** 90 (Avg `11.2` words/turn, Max `30` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5 (self):* High-fidelity compression of nearly all major beats, dreams, Nick POV elements, stabbing, and Seattle frame with consistent visual locks for Peter/Nick/Sionna/Ma across ages; action lines are mostly single-clip concrete; dialogue stays performable and book-true; minor V.O. density and light music cues keep it production-viable for short AI clips.
  - *grok-4:* Tightest fidelity with on-camera frame, locked visual descriptions, and single-action clip-ready scenes throughout.

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
  - *grok-4.5:* Faithful beat-for-beat translation with Young/Adult disambiguation and concrete visuals; dialogue lifts cleanly from source; solid clip rhythm though slightly denser V.O. and thinner explicit music prompts than top tier.
  - *grok-4:* High source coverage but heavy telling and lack of locked character visuals make it unsuitable for short AI clips.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 60 (Avg `13.7` words/turn, Max `53` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Serviceable main-arc compression with clear visual intros and clip-sized actions, but drops substantial middle material (yoga practice, full school arc, extended Nick interior) lowering fidelity; dialogue authentic where present; music cues thin.
  - *grok-4:* Solid visuals but name reveal and invented action break fidelity; some scenes cram multiple beats.

### 🎬 grok-4
- **Scene Headings Count:** 71
- **Dialogue Blocks:** 279 (Avg `15.4` words/turn, Max `114` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Excessive scene count (71 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ 16 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Invented proper name is an automatic severe fidelity penalty despite otherwise detailed coverage and decent action lines; overlong structure and residual internal passages reduce clip readiness.
  - *grok-4 (self):* Close adherence to source beats with concrete visual action lines and consistent physical descriptions; short scenes suit clip format.

### 🎬 o3-mini
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 33 (Avg `15.8` words/turn, Max `30` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG WOMAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. YOUNG WOMAN).
- **Judge Rationale:**
  - *grok-4.5:* Severe fidelity collapse via inventions and omissions; vague character visuals; heavy internal monologue and multi-location montages violate short-clip directibility; generic dialogue and weak escalation make it unusable.
  - *grok-4:* Significant invented content and reliance on voice-over reduce fidelity and directibility; character visuals lack consistency for AI reference locking.

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
  - *grok-4.5:* Collapses into disconnected vignettes with invented confrontations and missing core payoffs; no consistent casting locks or filmable single beats; dialogue and sound are negligible.
  - *grok-4:* Too abbreviated with major fidelity loss and unusable structure for clip-per-beat format.

### 🎬 gpt-4o
- **Scene Headings Count:** 566
- **Dialogue Blocks:** 566 (Avg `56.9` words/turn, Max `90` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Excessive scene count (566 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ High average dialogue length (56.9 words/turn); speech beats risk clip overrun.
  - ⚠️ 415 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.

