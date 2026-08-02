# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 06:58:09 UTC*  
*Source Story File: `The_Call_of_the_Wild.txt`*

> ⚠️ **GENERATION FALLBACK DETECTED:** The following models' live API generation failed, and the tool silently substituted a non-AI, book-text-only draft (identical for every failing model). Their rows below do NOT reflect that model's real output and are excluded from multi-book history:
> - **gpt-5.6-luna**: Chat HTTP 429: {
    "error": {
        "message": "You have no credits remaining. Add credits to continue using the API at https://platform.openai.com/settings/organization/billing/.",
        "type": "insufficient_quota",
        "param": null,
        "code": "credit_balance_exhausted"
    }
}

> - **claude-opus-5**: Anthropic messages HTTP 400: {"type":"error","error":{"type":"invalid_request_error","message":"Your credit balance is too low to access the Anthropic API. Please go to Plans & Billing to upgrade or purchase credits."},"request_id":"req_011CdZhFnPrPnva4vxEFxZYn"}

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **89.7** | 93.5% | 87.0% | 13 pts | 1.5 |
| 🥈  | **gpt-5.6-terra** | **89.5** | 96.8% | 85.0% | 13 pts | 1.5 |
| 🥉  | **gemini-3.6-flash** | **87.1** | 94.0% | 82.0% | 10 pts | 3.0 |
| 4.  | **gemini-3.1-pro-preview** | **77.6** | 92.8% | 68.0% | 8 pts | 4.0 |
| 5.  | **grok-4.20-reasoning** | **72.2** | 93.5% | 58.0% | 6 pts | 5.0 |
| 6.  | **gpt-5.6-luna ⚠️ *(fallback draft, not real output)*** | **32.2** | 80.5% | 0.0% | 0 pts | 3.5 |
| 7.  | **claude-opus-5 ⚠️ *(fallback draft, not real output)*** | **32.2** | 80.5% | 0.0% | 0 pts | 3.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 95% | 100% | 90% | 8.8/10 | 9.5/10 | 8.8/10 | 8.5/10 | 9.0/10 | 7.8/10 |
| **gpt-5.6-terra** | 100% | 100% | 95% | 9.0/10 | 9.0/10 | 8.5/10 | 8.2/10 | 8.8/10 | 7.2/10 |
| **gemini-3.6-flash** | 100% | 100% | 70% | 8.2/10 | 8.8/10 | 8.2/10 | 8.0/10 | 8.5/10 | 7.8/10 |
| **gemini-3.1-pro-preview** | 95% | 100% | 75% | 6.8/10 | 7.0/10 | 6.8/10 | 6.5/10 | 7.8/10 | 5.8/10 |
| **grok-4.20-reasoning** | 95% | 100% | 90% | 5.5/10 | 6.0/10 | 5.5/10 | 5.2/10 | 7.2/10 | 5.2/10 |
| **gpt-5.6-luna** | 95% | 50% | 50% | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 |
| **claude-opus-5** | 95% | 50% | 50% | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 | 0.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gemini-3.1-pro-preview**:
  - grok-4.5: Invented recurring snowy-ridge narrator frame substitutes for source texture
  - grok-4.5: Omits major sequences (Dave's death, boat rescue, mail-train suffering, moose hunt)
- **grok-4.20-reasoning**:
  - grok-4.5: Heavy invented cabin-narrator framing device that dominates runtime and is absent from the source
  - grok-4.5: Drops multiple major book beats (Dave's death, Forty-Mile boat rescue, detailed mail-train decline, moose)
  - grok-4.5: Over-reliance on summary V.O. makes scenes unusable for 5-10s clip breakdown
  - grok-4: Heavy reliance on static cabin narrator frame reduces directibility and visual momentum for short clips
  - grok-4: Some scenes compress too many beats into single headings

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | gpt-5.6-terra | gemini-3.6-flash | gemini-3.1-pro-preview | grok-4.20-reasoning | gpt-5.6-luna | claude-opus-5 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 8.7 | 8.7 | 8.3 | 6.2 | 5.2 | N/A | N/A |
| **grok-4** | 8.8 | 8.2 | 8.2 | 7.3 | 6.4 | N/A | N/A |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay B because it alone covers the full spine including Dave's death and the Forty-Mile boat rescue while remaining tightly directable and faithful. Weakest: Screenplay A because its invented cabin-narrator frame and wholesale summary of essential sequences make it unusable for short-clip production.
- **grok-4:** Strongest: Screenplay B because it best balances high fidelity, consistent character visuals, and clip-friendly micro-beats without any structural weaknesses. Weakest: Screenplay C because its static narrator frame and compressed scenes make it least suitable for short AI video clips.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 45 (Avg `13.1` words/turn, Max `44` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Strong fidelity and the best character lock (detailed coat markings, wardrobe, medium note) that never drifts; includes the moose hunt and full Hal decline. Misses the boat rescue and Dave's death, slightly lowering coverage versus B. Action is precise and clip-ready. Pacing is excellent. Dialogue is authentic. Sound beds are well implied by howls, arrows, ice and aurora imagery.
  - *grok-4:* Excellent balance of book fidelity, consistent character visuals, and filmable micro-beats with natural pacing across the arc.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Explicitly require that the adaptation hit every major emotional set-piece of the source (rescue, death of a teammate, climactic wager, final call) as dramatized scenes rather than allowing any one of them to be dropped for runtime.
  - *grok-4:* Add a rule requiring at least one concrete sound or music cue per scene heading to support the sound design dimension.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 38 (Avg `12.6` words/turn, Max `42` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *grok-4.5:* Highest fidelity: covers Curly's death, full Spitz fight, Dave's heartbreaking end, Hal/Charles/Mercedes decline, Thornton rescue plus ice collapse, Forty-Mile boat rescue, thousand-pound pull, wolf-brother run, Yeehat massacre and pack joining with almost no invented plot. Character looks (Buck's blaze, Ryder's red sweater, Thornton's wool/fur, etc.) are locked early and stay consistent. Action is concrete, single-location, camera-ready for short clips. Pacing escalates cleanly. Dialogue keeps book wording and dialect. Sound is implied well via howls, shots, water and clubs but lacks explicit BGM direction.
  - *grok-4:* Solid fidelity and visuals with good micro-beat structure, though slightly less polished transitions than the top entry.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require optional trailing sound-design notes after key action lines (e.g. 'SFX: bone crunch, pack silence' or 'MUSIC: low primordial drone') so every scene carries an explicit audio bed for AI clip generation.
  - *grok-4:* Add a rule requiring every scene to end with a clear visual or sound transition cue to the next heading.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 16
- **Dialogue Blocks:** 35 (Avg `17` words/turn, Max `68` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 6 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Solid coverage of core arc (kidnap, club, Curly, Spitz death, leadership, Hal collapse, Thornton, pull, Yeehats, pack) with only secondary omissions (Dave, boat). Character visuals are locked and stable. Action lines are highly directable with strong onomatopoeia (CRACK, SHRIEK) ideal for short clips. Pacing maintains momentum. Dialogue stays close to book. Richest embedded SFX of the set, though still light on explicit BGM mood.
  - *grok-4:* Strong fidelity and character consistency with vivid visuals, but some action blocks are too dense for 5-10s clips and sound design is minimal.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a requirement that every scene heading be followed by at least one concrete, single-clip visual action before any V.O., and that major emotional peaks interleave at least two distinct micro-beats of sound or image.
  - *grok-4:* Add an explicit rule that every action line must describe at most one primary visual event or micro-beat observable in a single 5-10 second clip.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 16
- **Dialogue Blocks:** 33 (Avg `20.3` words/turn, Max `56` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 5 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Frame device and heavy compression drop essential middle and late beats; character descriptions exist but are thin and not consistently re-asserted; several scenes remain filmable (Spitz fight, pull) yet many collapse into V.O.; pacing is uneven; dialogue preserves key lines; sound design is nearly absent.
  - *grok-4:* Good core story coverage but weaker on consistent character visuals and some action lines that bundle multiple events.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate that the adaptation must dramatize, not narrate, every primary turning point of the source in its own concrete scene heading; limit any framing device to a single opening and single closing beat.
  - *grok-4:* Add an explicit rule that character descriptions must include at least one stable wardrobe or marking detail that is restated on every reappearance.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 19
- **Dialogue Blocks:** 26 (Avg `17.3` words/turn, Max `52` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Fidelity collapses under an invented prospector-cabin frame and wholesale omission of key sequences; character looks are introduced sparsely and rarely re-locked; action lines frequently summarize weeks or multi-location arcs instead of concrete single-clip visuals; pacing feels rushed and interrupted by frame cutaways; dialogue retains a few iconic lines but is thin; almost no usable sound-bed or music cues.
  - *grok-4:* Uses a framing device that limits visual action and pacing, with less consistent character descriptions and weaker clip-friendly structure.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a hard rule that any on-camera narrator frame must be limited to at most three brief cutaways total and cannot replace dramatization of the book's primary turning-point sequence; require every major source location-turn to appear as its own visual scene rather than V.O. summary.
  - *grok-4:* Add an explicit rule that any on-camera narrator frame must be limited to at most three cutbacks total and must include a visual action beat in each return.

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 343
- **Dialogue Blocks:** 343 (Avg `55.3` words/turn, Max `82` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Excessive scene count (343 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ High average dialogue length (55.3 words/turn); speech beats risk clip overrun.
  - ⚠️ 249 monologue turn(s) exceed 35 words without action line splits.

### 🎬 claude-opus-5
- **Scene Headings Count:** 343
- **Dialogue Blocks:** 343 (Avg `55.3` words/turn, Max `82` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Excessive scene count (343 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ High average dialogue length (55.3 words/turn); speech beats risk clip overrun.
  - ⚠️ 249 monologue turn(s) exceed 35 words without action line splits.

