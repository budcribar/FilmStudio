# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 01:50:31 UTC*  
*Source Story File: `A_Christmas_Carol.txt`*

> ⚠️ **GENERATION FALLBACK DETECTED:** The following models' live API generation failed, and the tool silently substituted a non-AI, book-text-only draft (identical for every failing model). Their rows below do NOT reflect that model's real output and are excluded from multi-book history:
> - **gpt-4o**: Chat HTTP 429: {
    "error": {
        "message": "Rate limit reached for gpt-4o in organization org-Lrf5VruXklblpVn0yFliJuPl on tokens per min (TPM): Limit 30000, Used 21909, Requested 10629. Please try again in 5.076s. Visit https://platform.openai.com/account/rate-limits to learn more.",
        "type": "tokens",
        "param": null,
        "code": "rate_limit_exceeded"
    }
}


## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **88.5** | 91.0% | 87.0% | 34 pts | 1.2 |
| 🥈  | **claude-sonnet-5** | **85.6** | 89.5% | 83.0% | 29 pts | 2.2 |
| 🥉  | **gemini-2.5-flash** | **84.8** | 91.0% | 81.0% | 24 pts | 3.2 |
| 4.  | **grok-4** | **79.3** | 89.5% | 72.0% | 17 pts | 4.6 |
| 5.  | **o3-mini** | **71.4** | 95.5% | 55.0% | 18 pts | 4.4 |
| 6.  | **gpt-4o-mini** | **67.1** | 93.5% | 49.0% | 13 pts | 5.4 |
| 7.  | **gpt-4o ⚠️ *(fallback draft, not real output)*** | **29.7** | 74.2% | 0.0% | 0 pts | 3.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 70% | 9.4/10 | 9.2/10 | 7.7/10 | 8.1/10 | 9.2/10 | 8.5/10 |
| **claude-sonnet-5** | 100% | 100% | 70% | 9.0/10 | 8.9/10 | 7.1/10 | 7.6/10 | 8.7/10 | 8.0/10 |
| **gemini-2.5-flash** | 100% | 100% | 70% | 9.1/10 | 8.9/10 | 6.6/10 | 6.9/10 | 8.9/10 | 8.1/10 |
| **grok-4** | 100% | 100% | 70% | 7.8/10 | 6.5/10 | 7.6/10 | 7.2/10 | 7.6/10 | 7.0/10 |
| **o3-mini** | 100% | 100% | 100% | 4.9/10 | 5.5/10 | 6.0/10 | 5.7/10 | 5.4/10 | 6.0/10 |
| **gpt-4o-mini** | 100% | 100% | 100% | 4.3/10 | 4.5/10 | 6.4/10 | 4.8/10 | 4.7/10 | 4.8/10 |
| **gpt-4o** | 95% | 50% | 50% | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - gemini-2.5-flash: Extensive NARRATOR V.O. and unfilmable internal monologues/emotions, violating 'show, don't tell' for AI video clips.
  - gemini-2.5-flash: Some action lines are too dense for single short clips without further breakdown.
- **claude-sonnet-5**:
  - gemini-2.5-flash: Extensive NARRATOR V.O. and unfilmable internal monologues/emotions, violating 'show, don't tell' for AI video clips.
  - gemini-2.5-flash: Many action lines are too dense, describing multiple visual elements or complex states that would exceed a 5-10 second clip without further breakdown.
- **gemini-2.5-flash**:
  - gemini-2.5-flash: Extensive NARRATOR V.O. and unfilmable internal monologues/emotions, violating 'show, don't tell' for AI video clips.
  - gemini-2.5-flash: Many action lines are too dense, describing multiple visual elements or complex states that would exceed a 5-10 second clip without further breakdown.
- **grok-4**:
  - gpt-4o-mini: Inconsistent character descriptions
  - gpt-4o-mini: Pacing issues
  - gemini-2.5-flash: Severe lack of character disambiguation and casting clarity for almost all characters, making it impossible to consistently cast and visualize them (closed-cast violation).
  - gemini-2.5-flash: Heavy condensation of plot points reduces emotional impact and source coverage.
- **o3-mini**:
  - gpt-4o-mini: Inconsistent character descriptions
  - gpt-4o-mini: Pacing issues
  - grok-4.5: Severe drops of major source beats (full Marley dialogue, Fezziwig party, Belle arc, Ignorance/Want, Old Joe thieves, debtor relief)
  - grok-4.5: Heavily invented simplified plot events and non-Dickensian dialogue
  - grok-4.5: Vague inconsistent character descriptions lacking age disambiguation
  - grok-4: Major invented plot events and dropped beats; invented named characters; insufficient source coverage
  - gemini-2.5-flash: Severe lack of source coverage, dropping major plot points and characters (e.g., Ignorance and Want, Old Joe's scene, Caroline's scene, much of Fezziwig and Fred's party).
  - gemini-2.5-flash: Extensive use of NARRATOR V.O. and unfilmable internal monologues.
  - gemini-2.5-flash: Poor character disambiguation and casting clarity for several key characters.
- **gpt-4o-mini**:
  - grok-4.5: Extreme omission of nearly all major source beats, arcs and themes
  - grok-4.5: Invented bare-bones plot with inauthentic simplified dialogue
  - grok-4.5: Virtually no visual character descriptions or disambiguation
  - grok-4: Major invented plot events and dropped beats; invented named characters; insufficient source coverage; broken structure
  - gemini-2.5-flash: Severe lack of source coverage, dropping most major plot points and characters, rendering it an unusable outline.
  - gemini-2.5-flash: Extremely poor character disambiguation, making consistent casting impossible.
  - gemini-2.5-flash: Dialogue is heavily truncated and often invented, losing all authenticity and subtext.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | claude-sonnet-5 | gemini-2.5-flash | grok-4 | o3-mini | gpt-4o-mini | gpt-4o |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-4o** | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | N/A |
| **gpt-4o-mini** | 8.7 | 8.3 | 7.7 | 7.3 | 7.3 | **8.0** *(self)* | N/A |
| **o3-mini** | 9.1 | 8.3 | 8.2 | 8.0 | **8.7** *(self)* | 7.8 | N/A |
| **grok-4.5** | **9.1** *(self)* | 8.2 | 8.2 | 7.3 | 3.1 | 3.1 | N/A |
| **grok-4** | 8.7 | 8.6 | 8.3 | **8.1** *(self)* | 4.0 | 3.2 | N/A |
| **claude-sonnet-5** | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | N/A |
| **gemini-2.5-flash** | 7.9 | 8.1 | **7.9** *(self)* | 5.5 | 4.6 | 2.7 | N/A |

### 🧐 Self-Bias Analysis
- ⚠️ gpt-4o-mini rated its own screenplay 8.0/10 vs. a 4.2/10 average from 4 other judge(s) (+3.8) — possible self-preference bias.
- ⚠️ o3-mini rated its own screenplay 8.7/10 vs. a 4.7/10 average from 4 other judge(s) (+3.9) — possible self-preference bias.
- ⚠️ grok-4 rated its own screenplay 8.1/10 vs. a 7.0/10 average from 4 other judge(s) (+1.0) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-4o:** ⚠️ Mock judge evaluation returned for gpt-4o.
- **gpt-4o-mini:** Strongest: Screenplay A because it excels in adaptation fidelity and dialogue authenticity. Weakest: Screenplay D because it has inconsistent character descriptions and pacing issues that hinder its effectiveness.
- **o3-mini:** Strongest: Screenplay D because it offers vivid visual details, consistent character disambiguation, and excellent cinematic pacing; Weakest: Screenplay E because its overly simplified character descriptions and reduced sound design cues diminish its overall cinematic impact.
- **grok-4.5:** Strongest: Screenplay C because it delivers near-perfect source fidelity, locked consistent character visuals, and single-beat camera-ready action ideally matched to 5-10s AI clips. Weakest: Screenplay F because it obliterates the narrative with extreme omissions and invented shorthand that retains almost none of the book's characters, beats or themes.
- **grok-4:** Strongest: Screenplay B because it balances near-perfect fidelity, clear character visuals, and clip-friendly pacing without excess length. Weakest: Screenplay E because it contains multiple invented plot events, dropped major beats, and vague descriptions that violate core adaptation and directibility criteria.
- **claude-sonnet-5:** ⚠️ Mock judge evaluation returned for claude-sonnet-5.
- **gemini-2.5-flash:** Strongest: Screenplay C because it offers the most comprehensive adaptation with strong character clarity and dialogue, while having slightly better directibility and pacing for short clips than other faithful adaptations. Weakest: Screenplay F because it is an extremely condensed outline that sacrifices nearly all fidelity, character detail, and dramatic pacing, rendering it unusable.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 24
- **Dialogue Blocks:** 204 (Avg `18.4` words/turn, Max `121` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY SCROOGE, YOUNG MAN SCROOGE, TINY TIM, OLD JOE, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 35 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 5 age-qualified character header(s) (e.g. BOY SCROOGE, YOUNG MAN SCROOGE, TINY TIM).
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay A'.
  - *gpt-4o-mini:* Screenplay A maintains high fidelity to the source material, with strong character introductions and engaging dialogue, making it suitable for production.
  - *o3-mini:* Screenplay D stands out with richly descriptive, consistent character visuals, structured scene transitions, and strong sound and lighting cues that make it exceptionally filmable.
  - *grok-4.5 (self):* Near-exhaustive source fidelity with precise beat coverage, richly consistent visual character locks (BOY SCROOGE / YOUNG MAN SCROOGE, frosty rime details) and single-action camera-ready staging ideal for 5-10s clips; authentic dialogue and strong implied sound beds (chains, bells, carols) elevate every dimension.
  - *grok-4:* Near-verbatim fidelity with consistent visual descriptions and filmable beats; minor pacing compression for clip length.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay B'.
  - *gemini-2.5-flash:* This screenplay offers very high adaptation fidelity and strong character clarity, preserving much of the original dialogue and emotional beats. It is slightly better than B and D in AI directibility due to marginally less dense action lines and a slightly more cinematic pacing. However, the pervasive use of NARRATOR V.O. and internal monologues remains a significant barrier for direct AI video generation, preventing it from being production-ready.

### 🎬 claude-sonnet-5
- **Scene Headings Count:** 25
- **Dialogue Blocks:** 152 (Avg `14.9` words/turn, Max `74` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG SCROOGE, YOUNG MAN SCROOGE, TINY TIM, OLD JOE, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 10 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 5 age-qualified character header(s) (e.g. YOUNG SCROOGE, YOUNG MAN SCROOGE, TINY TIM).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay C'.
  - *gpt-4o-mini:* Screenplay B effectively captures the essence of the original text, though it has minor pacing issues that could be improved.
  - *o3-mini:* Screenplay F provides a well-structured, faithful rendition with clear narrative transitions and detailed descriptive cues, although its narration occasionally feels dense for short clip directibility.
  - *grok-4.5:* High fidelity covers nearly all major beats and themes with largely authentic Dickens dialogue; solid age-disambiguated intros (YOUNG SCROOGE) and concrete visual actions support short clips, though secondary named inventions (Ashby, Hardwick) and occasional multi-beat scenes slightly dilute purity and clip isolation.
  - *grok-4:* Slightly tighter pacing than A while retaining source accuracy and clear character visuals.
  - *claude-sonnet-5 (self):* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay C'.
  - *gemini-2.5-flash:* Screenplay D provides an exceptionally faithful adaptation, closely mirroring the source text's dialogue and narrative. Character descriptions are detailed and consistent. However, its extensive use of NARRATOR V.O. and internal character thoughts, combined with action lines that frequently cram too much visual information into a single beat, makes it highly problematic for AI video directibility and prevents it from being production-ready.

### 🎬 gemini-2.5-flash
- **Scene Headings Count:** 36
- **Dialogue Blocks:** 433 (Avg `18.4` words/turn, Max `166` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG SINGER, YOUNG SCROOGE, YOUNG CRATCHITS, TINY TIM, OLD JOE`
- **Diagnostics & Warnings:**
  - ⚠️ 68 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 5 age-qualified character header(s) (e.g. YOUNG SINGER, YOUNG SCROOGE, YOUNG CRATCHITS).
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay D'.
  - *gpt-4o-mini:* Screenplay C is a solid adaptation but lacks some clarity in character introductions and pacing, which affects its overall impact.
  - *o3-mini:* Screenplay A is highly faithful to the source with detailed narrative and dialogue, though its pacing can be a bit overloaded for short video clips and some scenes merge multiple actions.
  - *grok-4.5:* Most complete beat-for-beat fidelity and authentic dialogue with strong age variants, yet dense multi-action scene headings and exhaustive novelistic detail hinder clean 5-10s clip isolation and momentum despite rich implied sound opportunities.
  - *grok-4:* Extremely faithful but overly long for short-clip constraint; pacing suffers from verbatim density.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay E'.
  - *gemini-2.5-flash (self):* Screenplay B demonstrates exceptional fidelity to the source text, including almost verbatim dialogue and comprehensive character descriptions with excellent disambiguation across time periods. However, its heavy reliance on NARRATOR V.O. and internal monologues, coupled with very dense action lines, makes it poorly suited for direct AI video generation under the specified constraints, as these elements are not camera-observable actions.

### 🎬 grok-4
- **Scene Headings Count:** 13
- **Dialogue Blocks:** 113 (Avg `14.4` words/turn, Max `114` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD FEZZIWIG, TINY TIM, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 12 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 3 age-qualified character header(s) (e.g. OLD FEZZIWIG, TINY TIM, BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay E'.
  - *gpt-4o-mini:* Screenplay F presents a decent adaptation but has issues with character clarity and pacing that make it unsuitable for production.
  - *o3-mini:* Screenplay B is succinct and covers the major narrative beats well, but its overall simplicity sometimes leaves out nuances needed for strong character visuals and dramatic effect.
  - *grok-4.5:* Condensed but recognizable coverage of core staves with decent book-derived dialogue and tighter pacing suited to clips; weaker on consistent visual re-descriptions and age locks, plus abbreviated Past/Present details lower fidelity and atmospheric sound potential.
  - *grok-4 (self):* Faithful condensation of source with solid visuals, though less exhaustive than A/B.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay A'.
  - *gemini-2.5-flash:* This screenplay suffers from a severe lack of character disambiguation, with most characters lacking distinct visual descriptions, which is a critical flaw for AI casting. While it avoids NARRATOR V.O., its heavy condensation of the plot sacrifices significant detail and emotional depth, leading to rushed pacing and truncated dialogue, making it a superficial adaptation.

### 🎬 o3-mini
- **Scene Headings Count:** 10
- **Dialogue Blocks:** 20 (Avg `14.4` words/turn, Max `28` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG SCROOGE, TINY TIM, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 3 age-qualified character header(s) (e.g. YOUNG SCROOGE, TINY TIM, BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay B'.
  - *gpt-4o-mini:* Screenplay D has some adaptation strengths but suffers from inconsistent character descriptions and pacing issues that hinder its readiness for production.
  - *o3-mini (self):* Screenplay C offers a cinematic and modern reimagining with clear, filmable visuals and effective pacing, though it sacrifices a bit of meticulous character detail for broader cinematic appeal.
  - *grok-4.5:* Broken adaptation that invents a skeletal modernized outline while omitting most narrative, thematic and character substance; unfilmable internal V.O., rushed jumps and inauthentic lines make it unusable for clip-based production.
  - *grok-4:* Severe fidelity violations with invented content and missing major beats; descriptions too vague for consistent casting.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay D'.
  - *gemini-2.5-flash:* This screenplay is a highly condensed summary rather than a faithful adaptation, omitting numerous critical plot points and characters, which severely impacts fidelity. Character descriptions are inconsistent and often sparse, making casting clarity difficult. While action lines are generally concise, the frequent use of V.O. and internal thoughts detracts from directibility for AI video. The extreme condensation results in rushed pacing and a loss of dramatic depth and subtext in the dialogue.

### 🎬 gpt-4o-mini
- **Scene Headings Count:** 9
- **Dialogue Blocks:** 30 (Avg `7` words/turn, Max `15` words)
- **Generic Numbered Speakers:** `1` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG SCROOGE, TINY TIM, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 1 generic numbered speaker(s) found (e.g. MAN 1, OFFICER 2); replace with proper names.
  - ⚠️ Detected 3 age-qualified character header(s) (e.g. YOUNG SCROOGE, TINY TIM, BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay F'.
  - *gpt-4o-mini (self):* Screenplay E effectively captures the narrative and character essence, though it could benefit from tighter pacing.
  - *o3-mini:* Screenplay E is a more streamlined adaptation and remains fairly faithful, but its simpler character descriptions and less detailed sound cues reduce its effectiveness for a richly cinematic production.
  - *grok-4.5:* Skeletal summary that discards the book's structure, ghosts' full visitations, Cratchit depth and redemption texture; minimal visuals and generic lines fail every production criterion for short-clip adaptation.
  - *grok-4:* Severely condensed with invented dialogue and events; fails basic fidelity and directibility tests.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay F'.
  - *gemini-2.5-flash:* Screenplay F is an extremely condensed outline that fails to capture the essence or detail of the source material, resulting in very poor fidelity and dramatic pacing. Character descriptions are almost non-existent, making it impossible for consistent AI casting. Dialogue is minimal and often invented, completely stripping away authenticity and subtext, making it unusable as an adaptation.

### 🎬 gpt-4o
- **Scene Headings Count:** 714
- **Dialogue Blocks:** 714 (Avg `32.1` words/turn, Max `86` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Excessive scene count (714 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ High average dialogue length (32.1 words/turn); speech beats risk clip overrun.
  - ⚠️ 273 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.

