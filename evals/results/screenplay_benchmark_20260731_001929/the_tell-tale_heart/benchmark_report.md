# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 06:19:30 UTC*  
*Source Story File: `The_Tell-Tale_Heart.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **claude-opus-5** | **87.1** | 94.0% | 82.0% | 10 pts | 3.0 |
| 🥈  | **gpt-5.6-sol** | **86.0** | 93.8% | 81.0% | 11 pts | 2.5 |
| 🥉  | **grok-4.20-reasoning** | **82.0** | 93.0% | 75.0% | 10 pts | 3.0 |
| 4.  | **gpt-5.6-luna** | **81.6** | 91.5% | 75.0% | 10 pts | 3.0 |
| 5.  | **gpt-5.6-terra** | **80.8** | 95.0% | 71.0% | 6 pts | 5.0 |
| 6.  | **gemini-3.6-flash** | **77.1** | 94.0% | 66.0% | 5 pts | 5.5 |
| 7.  | **gemini-3.1-pro-preview** | **73.8** | 94.0% | 60.0% | 4 pts | 6.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **claude-opus-5** | 100% | 100% | 70% | 6.5/10 | 9.2/10 | 8.5/10 | 8.2/10 | 8.5/10 | 8.5/10 |
| **gpt-5.6-sol** | 95% | 100% | 95% | 6.2/10 | 9.0/10 | 8.2/10 | 8.2/10 | 8.2/10 | 8.5/10 |
| **grok-4.20-reasoning** | 100% | 100% | 50% | 8.5/10 | 6.8/10 | 7.0/10 | 7.0/10 | 8.5/10 | 7.0/10 |
| **gpt-5.6-luna** | 95% | 100% | 50% | 8.8/10 | 4.5/10 | 7.0/10 | 7.8/10 | 9.0/10 | 8.0/10 |
| **gpt-5.6-terra** | 100% | 100% | 90% | 5.2/10 | 8.5/10 | 7.2/10 | 7.0/10 | 7.5/10 | 7.2/10 |
| **gemini-3.6-flash** | 100% | 100% | 70% | 4.2/10 | 8.0/10 | 7.2/10 | 6.8/10 | 5.5/10 | 7.8/10 |
| **gemini-3.1-pro-preview** | 100% | 100% | 70% | 4.2/10 | 6.5/10 | 6.5/10 | 6.0/10 | 6.0/10 | 7.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **claude-opus-5**:
  - grok-4: Invented asylum cell frame not present in source
- **gpt-5.6-sol**:
  - grok-4: Invented magistrate's chamber frame
- **grok-4.20-reasoning**:
  - grok-4.5: Multi-night rituals and multi-step concealment crammed into single Action paragraphs unsuited to 5-10s clip breakdown
  - grok-4.5: Recurring characters lack consistent re-stated wardrobe/build locks on reappearance
- **gpt-5.6-luna**:
  - grok-4.5: Narrator is deliberately never fully seen (sleeve/hands only) with no lockable full-body/face wardrobe description, so AI reference images cannot be stabilized
  - grok-4.5: Heavy stacked V.O. over minimal observable action makes many beats unusable as 5-10s clips
- **gpt-5.6-terra**:
  - grok-4: Invented character name MR. VALE for the unnamed old man
- **gemini-3.6-flash**:
  - grok-4.5: Severe invented dialogue throughout (Old Man breakfast banter; multi-line officer small talk; modernized chat) violating dialogue-fidelity hard rules
  - grok-4.5: Source summarized ‘they chatted’ beats expanded into multiple non-book spoken lines
  - grok-4: Invented asylum cell frame not in source
- **gemini-3.1-pro-preview**:
  - grok-4.5: Incomplete/malformed Fountain title page (missing proper Credit/Source pattern)
  - grok-4.5: Substantial invented officer and narrator chit-chat beyond the source
  - grok-4.5: Thin unstable visual descriptions for recurring supporting cast
  - grok-4: Invented asylum cell frame
  - grok-4: Inconsistent use of NARRATOR vs THE NARRATOR token

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | claude-opus-5 | gpt-5.6-sol | grok-4.20-reasoning | gpt-5.6-luna | gpt-5.6-terra | gemini-3.6-flash | gemini-3.1-pro-preview |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 9.2 | 8.6 | 6.5 | 7.3 | 7.5 | 6.0 | 5.8 |
| **grok-4** | 7.3 | 7.6 | 8.4 | 7.7 | 6.8 | 7.2 | 6.2 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay C because it best unites book-faithful diction, a stable on-camera confession frame with proper returns, lockable cast looks, and short-clip-directable micro-beats at the murder and police climax. Weakest: Screenplay D because extensive invented Old Man and officer dialogue is a severe fidelity breach that makes the draft unusable despite competent visuals and sound.
- **grok-4:** Strongest: Screenplay B because it achieves the highest fidelity without any invented names, frames, or locations while delivering consistent character descriptions and clip-friendly visual actions. Weakest: Screenplay A because the invention of MR. VALE constitutes a severe source fidelity violation that immediately disqualifies the draft.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 claude-opus-5
- **Scene Headings Count:** 13
- **Dialogue Blocks:** 45 (Avg `25.3` words/turn, Max `68` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ 8 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Best overall package: stable confession-frame (bare stone cell) with mandated returns, dense peak interleaving of book voice and filmable micro-beats, locked looks for Narrator/Old Man/officers, and near-quotation dialogue. One brief invented officer pleasantry is the only notable blemish. Heartbeat, deathwatch, lantern, and board sounds are staged so short AI clips can carry tension without internal monologue dumps.
  - *grok-4:* Strong character descriptions and book dialogue but major fidelity hit from adding an unmentioned cell frame and multiple on-camera narration scenes.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require each scene to include at least one explicit diegetic sound cue written inside Action (not only implied by V.O.) plus an optional one-line music-bed mood so clip-level sound design does not rely on inference.
  - *grok-4:* Add an explicit rule that no on-camera frame or location may be invented if it does not appear in the source text; all narration must use existing book locations or V.O. only.

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 13
- **Dialogue Blocks:** 43 (Avg `21.4` words/turn, Max `42` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Strong second place: magistrate-chamber frame, precise cast looks, excellent diegetic sound writing (creak, deathwatch, thud, grate), and disciplined peak density. Slight fidelity ding where the old man’s private self-soothing becomes on-camera dialogue. Minor end-marker formatting noise. Highly directable for short AI clips and greenlight-ready with light polish.
  - *grok-4:* Excellent consistent descriptions and directible actions but fidelity penalty from invented frame location used for narration.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Clarify that when the source reports what a character ‘had been saying to himself’ or only attributes inner supposition, those lines stay NARRATOR V.O. or Action—not on-camera dialogue—unless the book presents them as audible speech.
  - *grok-4:* Add a rule prohibiting any new recurring location for narration unless the source text explicitly describes such a setting.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 5
- **Dialogue Blocks:** 18 (Avg `57.2` words/turn, Max `117` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ High average dialogue length (57.2 words/turn); speech beats risk clip overrun.
  - ⚠️ 12 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Wording stays close to Poe and the plot spine is complete, but craft is montage-heavy and description-thin. Seven nights, kill, and burial are summarized in blocks a director cannot schedule as discrete short clips. Officers are named yet barely visualized; sound beds are mostly left to V.O. rather than staged Action.
  - *grok-4:* Highest fidelity with no invented names or frames, consistent OLD MAN and NARRATOR descriptions, book-accurate dialogue, and concrete visual actions suitable for clips.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Forbid multi-day or multi-step montages inside one Action paragraph; each 5–10 second filmable unit must be its own Action line, and any clear time/location purpose change must get a new scene heading.
  - *grok-4:* Add a rule requiring every V.O. passage longer than two sentences to be interleaved with at least one distinct visual or sound micro-beat action line.

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 7
- **Dialogue Blocks:** 58 (Avg `30.4` words/turn, Max `64` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ High average dialogue length (30.4 words/turn); speech beats risk clip overrun.
  - ⚠️ 20 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Extremely faithful to Poe’s wording and spine beats, with strong confession climax and clear heartbeat escalation. Catastrophically weak on cast locking: the protagonist is defined as unseen, which breaks the AI clip pipeline’s need for a stable character reference. Directibility suffers from long V.O. blocks riding thin visuals (hands, ray, door crack) without enough discrete filmable micro-actions per clip.
  - *grok-4:* Strong fidelity and dialogue with no invented names or frames but low disambiguation score because NARRATOR is never fully seen, violating filmable identity requirement.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require that the first on-screen appearance of every named speaking character include a complete lockable visual description (age/build/hair/face/wardrobe colors) and forbid ‘never fully seen’ protagonists unless the entire piece is strictly POV with no reverse angle on that character.
  - *grok-4:* Add a rule that every named character must receive at least one concrete visual description of build, clothing, and defining features that remains consistent on every reappearance.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 7
- **Dialogue Blocks:** 38 (Avg `21.4` words/turn, Max `38` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Solid, filmable closed-cast adaptation with clear looks for Narrator, Mr. Vale, and three officers. Fidelity is good but not elite: period name for the old man is allowed, yet several connective spoken lines and compressions thin iconic texture. Action is generally clip-sized; some V.O. stacks still run long. Ending formatting glitch is minor. Production-ready as a workable draft with revision, not a showcase.
  - *grok-4:* Severe fidelity penalty for inventing MR. VALE; otherwise solid descriptions and book dialogue but long V.O. blocks hurt directibility and pacing for short clips.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate a concrete visual or diegetic-sound micro-beat (one short Action line) between every two consecutive V.O. blocks and cap unbroken V.O. runs at roughly two short cues before cutting back to picture.
  - *grok-4:* Add an explicit rule that no names may be invented for any character not named in the source text; unnamed protagonists must use stable tokens like THE NARRATOR or THE OLD MAN.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 37 (Avg `19.3` words/turn, Max `70` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ 7 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Visually competent and cast-clear, with usable heartbeat SFX writing, but fidelity collapses under invented conversational padding the book never speaks. Iconic lines are diluted by movie-ized exchanges and on-camera whispers that replace narration. That single dimension failure is disqualifying regardless of mid-pack averages.
  - *grok-4:* Good use of book dialogue and visual actions but fidelity lowered by added cell frame; pacing works for clips but invented element disqualifies.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a hard rule that any dialogue not a near-quotation of words the source presents as spoken or clearly recited must be deleted or folded into Action; summarized ‘they chatted/pleasantries’ beats default to zero invented lines (Action only).
  - *grok-4:* Add a rule that any frame or recurring narration location must be justified by explicit source text or else default to V.O. without visual presence.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 32 (Avg `23.4` words/turn, Max `59` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ 7 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Covers the spine but with weaker craft: broken title page, invented multi-line police banter, thinner identity locks, and less disciplined clip-sized staging than C/F. Heartbeat sound is present but structure and fidelity are not production-grade.
  - *grok-4:* Multiple invented elements including cell frame plus inconsistent character tokens and some paraphrased dialogue reduce scores across fidelity and disambiguation.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Hard-require a complete Fountain title page (Title, Credit, Author, Source, Draft date) before any scene, and auto-flag drafts that give supporting roles multi-line invented conversations when the source only summarizes that people talked.
  - *grok-4:* Add a rule requiring every recurring speaker to use the exact same ALL-CAPS token on every appearance with no variation.

