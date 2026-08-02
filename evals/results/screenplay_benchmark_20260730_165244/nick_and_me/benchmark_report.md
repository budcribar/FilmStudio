# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-30 23:09:53 UTC*  
*Source Story File: `Nick_and_Me.txt`*

> ⚠️ **GENERATION FALLBACK DETECTED:** The following models' live API generation failed, and the tool silently substituted a non-AI, book-text-only draft (identical for every failing model). Their rows below do NOT reflect that model's real output and are excluded from multi-book history:
> - **gpt-4o**: Chat HTTP 429: {
    "error": {
        "message": "Rate limit reached for gpt-4o in organization org-Lrf5VruXklblpVn0yFliJuPl on tokens per min (TPM): Limit 30000, Used 15532, Requested 14655. Please try again in 374ms. Visit https://platform.openai.com/account/rate-limits to learn more.",
        "type": "tokens",
        "param": null,
        "code": "rate_limit_exceeded"
    }
}

> - **o3-mini**: Chat HTTP 400: {
  "error": {
    "message": "Unsupported parameter: 'temperature' is not supported with this model.",
    "type": "invalid_request_error",
    "param": "temperature",
    "code": "unsupported_parameter"
  }
}
> - **claude-sonnet-5**: Anthropic messages HTTP 401: {"type":"error","error":{"type":"authentication_error","message":"API key is invalid."},"request_id":null}

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **85.4** | 91.8% | 81.0% | 21 pts | 1.0 |
| 🥈  | **gemini-2.5-pro** | **75.9** | 90.5% | 66.0% | 16 pts | 2.7 |
| 🥉  | **grok-4** | **68.5** | 75.8% | 64.0% | 17 pts | 2.3 |
| 4.  | **gpt-4o-mini** | **65.8** | 86.5% | 52.0% | 12 pts | 4.0 |
| 5.  | **gpt-4o ⚠️ *(fallback draft, not real output)*** | **39.1** | 70.5% | 18.0% | 5 pts | 6.3 |
| 6.  | **o3-mini ⚠️ *(fallback draft, not real output)*** | **39.1** | 70.5% | 18.0% | 5 pts | 6.3 |
| 7.  | **claude-sonnet-5 ⚠️ *(fallback draft, not real output)*** | **39.1** | 70.5% | 18.0% | 8 pts | 5.3 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 85% | 100% | 100% | 8.5/10 | 8.8/10 | 8.5/10 | 8.3/10 | 8.2/10 | 6.3/10 |
| **gemini-2.5-pro** | 80% | 100% | 100% | 6.0/10 | 7.3/10 | 7.0/10 | 6.7/10 | 7.5/10 | 5.2/10 |
| **grok-4** | 85% | 50% | 70% | 4.8/10 | 6.2/10 | 7.5/10 | 7.3/10 | 7.3/10 | 5.0/10 |
| **gpt-4o-mini** | 80% | 80% | 100% | 3.5/10 | 6.0/10 | 6.0/10 | 4.7/10 | 6.5/10 | 4.5/10 |
| **gpt-4o** | 80% | 50% | 50% | 4.0/10 | 1.7/10 | 1.0/10 | 1.3/10 | 2.0/10 | 1.0/10 |
| **o3-mini** | 80% | 50% | 50% | 4.0/10 | 1.7/10 | 1.0/10 | 1.3/10 | 2.0/10 | 1.0/10 |
| **claude-sonnet-5** | 80% | 50% | 50% | 4.0/10 | 1.7/10 | 1.0/10 | 1.3/10 | 2.0/10 | 1.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - grok-4: Still uses substantial narration
  - grok-4: No explicit music prompts
  - grok-4: Some invented framing elements
- **gemini-2.5-pro**:
  - grok-4: Heavy narration still dominates many beats
  - grok-4: Limited sound design prompts
  - grok-4: Some scenes cram multiple actions
  - gemini-2.5-pro: Significant omissions of key plot points and character interactions from the source material, such as the detailed schoolyard flashback, the confrontation between Nick and Sionna at Peter's apartment, and Peter's pizza baby dream. The invented scene of Nick attacking Sionna *before* the accident is a major deviation.
- **grok-4**:
  - grok-4.5: Invented protagonist name JAKE (source explicitly ends as Peter Olson)
  - grok-4.5: Name change plus compressions count as severe fidelity violation
  - grok-4: Heavy narration still dominates many beats
  - grok-4: Limited sound design prompts
  - grok-4: Some scenes cram multiple actions
  - gemini-2.5-pro: The main character's name is changed from 'Peter Olson' to 'Jake', which is a significant deviation from the source material and would cause continuity issues if the source is referenced elsewhere.
- **gpt-4o-mini**:
  - grok-4.5: Heavy dropping of major source beats (dreams, yoga/Buddhism arc, full stabbing aftermath, Seattle resolution details)
  - grok-4.5: Vague 'YOUNG MAN' naming and incomplete character lock
  - grok-4.5: Invented compressions and truncated ending
  - grok-4: Heavy narration in many sections
  - grok-4: Limited visual action lines
  - grok-4: No music prompts
  - gemini-2.5-pro: Severe deviation from source material by inventing major plot events (pre-accident confrontation with Nick, Nick attacking Sionna *before* the accident, Peter promising to 'take care of him' at Ma's grave) and completely omitting a critical plot event (Nick stabbing Sionna and Peter intervening). This fundamentally alters the story and character arcs from the source book.
- **gpt-4o**:
  - grok-4.5: Identical pure narrator text-dump structure as A
  - grok-4.5: No filmable action or character visuals
  - grok-4.5: Unusable for AI short-clip production
  - grok-4: Heavy reliance on narrator voice-over instead of visual action
  - grok-4: No consistent character visual descriptions
  - grok-4: Scenes not broken into filmable short clips
  - grok-4: No sound design or music prompts
  - gemini-2.5-pro: Not a screenplay adaptation; it is a raw text dump of the source material, lacking all necessary screenplay formatting (scene headings, action lines, character dialogue) and directibility for AI video generation.
- **o3-mini**:
  - grok-4.5: Pure narrator book dump identical to A/C
  - grok-4.5: Zero camera-observable action or character design
  - grok-4.5: Cannot be broken into AI video clips
  - grok-4: Heavy reliance on narrator voice-over instead of visual action
  - grok-4: No consistent character visual descriptions
  - grok-4: Scenes not broken into filmable short clips
  - grok-4: No sound design or music prompts
  - gemini-2.5-pro: Not a screenplay adaptation; it is a raw text dump of the source material, lacking all necessary screenplay formatting (scene headings, action lines, character dialogue) and directibility for AI video generation.
- **claude-sonnet-5**:
  - grok-4.5: Pure unbroken narrator dump of book text with zero visual action lines or scene breakdowns
  - grok-4.5: Unusable for 5-10s AI video clips
  - grok-4.5: No character visual descriptions or consistency locks
  - grok-4: Heavy reliance on narrator voice-over instead of visual action
  - grok-4: No consistent character visual descriptions
  - grok-4: Scenes not broken into filmable short clips
  - grok-4: No sound design or music prompts
  - gemini-2.5-pro: Not a screenplay adaptation; it is a raw text dump of the source material, lacking all necessary screenplay formatting (scene headings, action lines, character dialogue) and directibility for AI video generation.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gemini-2.5-pro | grok-4 | gpt-4o-mini | gpt-4o | o3-mini | claude-sonnet-5 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-4o** | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* |
| **gpt-4o-mini** | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* |
| **o3-mini** | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* |
| **grok-4.5** | **8.2** *(self)* | 6.9 | 5.9 | 4.7 | 2.7 | 2.7 | 2.7 |
| **grok-4** | 7.0 | 5.5 | **5.5** *(self)* | 4.7 | 1.8 | 1.8 | 1.8 |
| **claude-sonnet-5** | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* | ⚠️ **-1.0** *(Mock/Failed)* |
| **gemini-2.5-pro** | 9.2 | **7.4** *(self)* | 7.7 | 6.2 | 1.0 | 1.0 | 1.0 |

### 🧐 Self-Bias Analysis
- ℹ️ grok-4 rated its own screenplay 5.5/10 vs. a 6.8/10 average from 2 other judge(s) (-1.3) — notably harsher on itself than peers were.
- ⚠️ gemini-2.5-pro rated its own screenplay 7.4/10 vs. a 6.2/10 average from 2 other judge(s) (+1.2) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-4o:** ⚠️ Mock judge evaluation returned for gpt-4o.
- **gpt-4o-mini:** ⚠️ Mock judge evaluation returned for gpt-4o-mini.
- **o3-mini:** ⚠️ Mock judge evaluation returned for o3-mini.
- **grok-4.5:** Strongest: Screenplay G because it delivers the highest fidelity-to-directibility balance with consistent casting locks, short filmable beats, and zero disqualifying inventions while covering the full source arc. Weakest: Screenplay A because it is a non-adapted pure narrator text dump with no visuals, no character design, and zero utility for AI short-clip production (C and E share the identical fatal flaw).
- **grok-4:** Strongest: Screenplay E because it uses a frame narrative with on-camera elements and more dramatized scenes reducing pure narration. Weakest: Screenplay A because it consists almost entirely of unfilmable narrator blocks with zero action lines or visual beats.
- **claude-sonnet-5:** ⚠️ Mock judge evaluation returned for claude-sonnet-5.
- **gemini-2.5-pro:** Strongest: Screenplay G because it delivers exceptional adaptation fidelity, character clarity, and directibility, making it fully production-ready without significant deviations from the source. Weakest: Screenplays B, C, and D are equally weakest because they are not screenplays at all, but raw text dumps of the source material, rendering them completely unusable for video production.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 41
- **Dialogue Blocks:** 90 (Avg `11.2` words/turn, Max `30` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Non-Fountain colon dialogue format (5 instances like 'Character:') detected.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay E'.
  - *gpt-4o-mini:* [MOCK / FAILED JUDGE] Model 'gpt-4o-mini' failed or was skipped for candidate 'Screenplay B'.
  - *o3-mini:* [MOCK / FAILED JUDGE] Model 'o3-mini' failed or was skipped for candidate 'Screenplay A'.
  - *grok-4.5 (self):* Best overall: faithful major-beat coverage with correct Peter Olson, explicit age/visual locks (YOUNG NICK, mid-thirties Peter, Sionna details), short concrete show-don't-tell actions ideal for 5-10s clips, natural dialogue, clean Seattle frame matching source end; only mild gap is sparse explicit music beds.
  - *grok-4:* Best attempt with frame narrative, on-camera elements, and character action but narration remains a flaw.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay C'.
  - *gemini-2.5-pro:* Screenplay G is an outstanding adaptation, demonstrating excellent fidelity to the source material while effectively translating it into a highly directible screenplay format. It provides clear character disambiguation, strong visual action lines, and maintains the dramatic pacing suitable for short video clips. The dialogue is authentic, and sound design cues are well-integrated. Minor omissions do not detract from the overall quality and faithfulness of the adaptation.

### 🎬 gemini-2.5-pro
- **Scene Headings Count:** 24
- **Dialogue Blocks:** 1 (Avg `2` words/turn, Max `2` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG NICK, YOUNG PETER`
- **Diagnostics & Warnings:**
  - ⚠️ Non-Fountain colon dialogue format (4 instances like 'Character:') detected.
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Detected 2 age-qualified character header(s) (e.g. YOUNG NICK, YOUNG PETER).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay G'.
  - *gpt-4o-mini:* [MOCK / FAILED JUDGE] Model 'gpt-4o-mini' failed or was skipped for candidate 'Screenplay D'.
  - *o3-mini:* [MOCK / FAILED JUDGE] Model 'o3-mini' failed or was skipped for candidate 'Screenplay D'.
  - *grok-4.5:* Correct Peter naming, covers full arc including Seattle/prison/forgiveness without major inventions, decent visual actions mixed with V.O.; workable though still narration-heavy and light on repeated casting locks and music cues.
  - *grok-4:* Similar to C with some dramatization but narration-heavy and lacks music cues.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay B'.
  - *gemini-2.5-pro (self):* Screenplay E demonstrates good directibility and character clarity, with strong dialogue and sound cues. However, it suffers from significant fidelity issues, including the omission of several important character development beats and dreams from the source. Crucially, it invents a scene where Nick attacks Sionna *before* the car accident, which fundamentally alters the narrative timeline and character motivations, making it an unfaithful adaptation.

### 🎬 grok-4
- **Scene Headings Count:** 71
- **Dialogue Blocks:** 279 (Avg `15.4` words/turn, Max `114` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Non-Fountain colon dialogue format (4 instances like 'Character:') detected.
  - ⚠️ Excessive scene count (71 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ 16 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay C'.
  - *gpt-4o-mini:* [MOCK / FAILED JUDGE] Model 'gpt-4o-mini' failed or was skipped for candidate 'Screenplay C'.
  - *o3-mini:* [MOCK / FAILED JUDGE] Model 'o3-mini' failed or was skipped for candidate 'Screenplay G'.
  - *grok-4.5:* Strong visual staging and short-clip friendly actions, solid dialogue and some age notes, but inventing 'Jake' instead of Peter is an automatic severe fidelity fail that tanks production readiness.
  - *grok-4 (self):* Attempts dramatization with character names and action lines but still relies heavily on narration.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay F'.
  - *gemini-2.5-pro:* Screenplay F is well-structured and highly directible, effectively breaking down the narrative into short, filmable clips. It maintains strong dramatic pacing and authentic dialogue. The inclusion of the critical Nick attacking Sionna scene is a major strength. However, the unprompted change of the main character's name from 'Peter Olson' to 'Jake' is a severe fidelity issue that makes it not production-ready, despite its other strengths.

### 🎬 gpt-4o-mini
- **Scene Headings Count:** 55
- **Dialogue Blocks:** 213 (Avg `7` words/turn, Max `20` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Non-Fountain colon dialogue format (4 instances like 'Character:') detected.
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Excessive scene count (55 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. YOUNG MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay B'.
  - *gpt-4o-mini (self):* [MOCK / FAILED JUDGE] Model 'gpt-4o-mini' failed or was skipped for candidate 'Screenplay A'.
  - *o3-mini:* [MOCK / FAILED JUDGE] Model 'o3-mini' failed or was skipped for candidate 'Screenplay B'.
  - *grok-4.5:* Some visual attempts and CUT TOs exist but massive omissions and vague casting destroy fidelity; pacing jumps too hard for short-clip rhythm and sound is absent.
  - *grok-4:* Mix of narration and some scenes but still fails on directibility and consistency.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay G'.
  - *gemini-2.5-pro:* Screenplay A attempts a proper screenplay format but introduces significant fidelity issues. It invents a pre-accident confrontation with Nick and omits the crucial scene where Nick stabs Sionna, which is a core event in the source material. This severely impacts the narrative's integrity and Peter's character development. While directibility and dialogue are generally good, the structural changes and omissions make it unusable for a faithful adaptation.

### 🎬 gpt-4o
- **Scene Headings Count:** 566
- **Dialogue Blocks:** 566 (Avg `56.9` words/turn, Max `90` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Non-Fountain colon dialogue format (4 instances like 'Character:') detected.
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Excessive scene count (566 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ High average dialogue length (56.9 words/turn); speech beats risk clip overrun.
  - ⚠️ 415 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o (self):* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay F'.
  - *gpt-4o-mini:* [MOCK / FAILED JUDGE] Model 'gpt-4o-mini' failed or was skipped for candidate 'Screenplay F'.
  - *o3-mini:* [MOCK / FAILED JUDGE] Model 'o3-mini' failed or was skipped for candidate 'Screenplay C'.
  - *grok-4.5:* Exact same non-adaptation as A: full text fidelity wasted on non-directible narrator blocks with no showable beats or casting clarity.
  - *grok-4:* Identical to A; pure narrator blocks with no action lines or visual beats; unusable for short AI clips.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay D'.
  - *gemini-2.5-pro:* Screenplay D is identical to Screenplays B and C. It is a raw text dump of the source material, not a screenplay adaptation. It lacks proper formatting, visual direction, and character disambiguation, rendering it entirely unsuitable for AI video production.

### 🎬 o3-mini
- **Scene Headings Count:** 566
- **Dialogue Blocks:** 566 (Avg `56.9` words/turn, Max `90` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Non-Fountain colon dialogue format (4 instances like 'Character:') detected.
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Excessive scene count (566 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ High average dialogue length (56.9 words/turn); speech beats risk clip overrun.
  - ⚠️ 415 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay A'.
  - *gpt-4o-mini:* [MOCK / FAILED JUDGE] Model 'gpt-4o-mini' failed or was skipped for candidate 'Screenplay G'.
  - *o3-mini (self):* [MOCK / FAILED JUDGE] Model 'o3-mini' failed or was skipped for candidate 'Screenplay E'.
  - *grok-4.5:* Same fatal non-screenplay structure as A and C; fidelity to words is high only because nothing was dramatized or visualized.
  - *grok-4:* Pure narrator blocks identical to A/B; unusable for short AI clips.
  - *claude-sonnet-5:* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay A'.
  - *gemini-2.5-pro:* Screenplay B is not a screenplay. It is a direct copy-paste of the source text, with each paragraph simply labeled 'INT. ROOM - DAY' and attributed to 'NARRATOR'. This format is completely unusable for AI video generation as it provides no visual direction, character descriptions, or properly formatted dialogue.

### 🎬 claude-sonnet-5
- **Scene Headings Count:** 566
- **Dialogue Blocks:** 566 (Avg `56.9` words/turn, Max `90` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Non-Fountain colon dialogue format (4 instances like 'Character:') detected.
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Excessive scene count (566 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ High average dialogue length (56.9 words/turn); speech beats risk clip overrun.
  - ⚠️ 415 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* [MOCK / FAILED JUDGE] Model 'gpt-4o' failed or was skipped for candidate 'Screenplay D'.
  - *gpt-4o-mini:* [MOCK / FAILED JUDGE] Model 'gpt-4o-mini' failed or was skipped for candidate 'Screenplay E'.
  - *o3-mini:* [MOCK / FAILED JUDGE] Model 'o3-mini' failed or was skipped for candidate 'Screenplay F'.
  - *grok-4.5:* Verbatim book coverage gives high fidelity on paper but zero adaptation into filmable beats; endless INT. ROOM + NARRATOR blocks destroy every other dimension and make clip extraction impossible.
  - *grok-4:* Pure narrator blocks with no action lines or visual beats; unusable for short AI clips.
  - *claude-sonnet-5 (self):* [MOCK / FAILED JUDGE] Model 'claude-sonnet-5' failed or was skipped for candidate 'Screenplay E'.
  - *gemini-2.5-pro:* Screenplay C is identical to Screenplay B. It is a raw text dump of the source material, not a screenplay adaptation. It lacks proper formatting, visual direction, and character disambiguation, rendering it entirely unsuitable for AI video production.

