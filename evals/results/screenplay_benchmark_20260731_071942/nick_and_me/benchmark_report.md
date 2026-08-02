# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 13:19:42 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **90.9** | 93.5% | 89.0% | 14 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **90.9** | 99.8% | 85.0% | 11 pts | 2.5 |
| 🥉  | **gpt-5.6-luna** | **89.4** | 99.0% | 83.0% | 11 pts | 2.5 |
| 4.  | **gemini-3.1-pro-preview** | **86.9** | 98.5% | 79.0% | 7 pts | 4.5 |
| 5.  | **gemini-3.6-flash** | **84.0** | 100.0% | 73.0% | 5 pts | 5.5 |
| 6.  | **claude-opus-5** | **74.1** | 91.5% | 62.0% | 5 pts | 5.5 |
| 7.  | **grok-4.20-reasoning** | **73.4** | 99.2% | 56.0% | 3 pts | 6.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 95% | 100% | 90% | 9.2/10 | 9.2/10 | 9.0/10 | 8.5/10 | 9.0/10 | 8.5/10 |
| **gpt-5.6-terra** | 100% | 100% | 95% | 8.8/10 | 8.8/10 | 8.5/10 | 8.2/10 | 8.2/10 | 8.5/10 |
| **gpt-5.6-luna** | 100% | 100% | 80% | 8.5/10 | 8.0/10 | 8.2/10 | 8.0/10 | 8.2/10 | 8.8/10 |
| **gemini-3.1-pro-preview** | 100% | 100% | 70% | 7.8/10 | 8.0/10 | 7.8/10 | 8.0/10 | 8.0/10 | 8.0/10 |
| **gemini-3.6-flash** | 100% | 100% | 100% | 5.5/10 | 8.0/10 | 8.0/10 | 7.5/10 | 7.2/10 | 7.8/10 |
| **claude-opus-5** | 100% | 72% | 70% | 5.5/10 | 6.2/10 | 6.2/10 | 4.5/10 | 7.5/10 | 7.5/10 |
| **grok-4.20-reasoning** | 100% | 100% | 85% | 3.8/10 | 6.5/10 | 6.5/10 | 5.2/10 | 5.5/10 | 6.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gemini-3.6-flash**:
  - grok-4.5: Protagonist locked as PETER from the opening scene, violating the source's late name reveal and the NARRATOR-token rule
- **claude-opus-5**:
  - grok-4.5: Ma repeatedly mis-cued/renamed as MRS. ENGEL, breaking closed cast and source identity
  - grok-4.5: Non-Fountain garbage text and model metadata embedded near the end, rendering the file unparsable
  - grok-4.5: Massively exceeds 10-minute target with dozens of secondary scenes
- **grok-4.20-reasoning**:
  - grok-4.5: Protagonist named PETER from the first scene onward
  - grok-4.5: Invented non-source closing line 'God bless us, every one'
  - grok-4.5: Heavy omission of major middle beats (full library/yoga/philosophy arc) and choppy CUT-TO structure
  - grok-4: Invented ending line 'God bless us, every one' not present in source; frame narrative adds non-book structure and invented closing beat.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | gpt-5.6-terra | gpt-5.6-luna | gemini-3.1-pro-preview | gemini-3.6-flash | claude-opus-5 | grok-4.20-reasoning |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 8.8 | 8.4 | 8.4 | 8.4 | 7.0 | 4.7 | 5.6 |
| **grok-4** | 9.1 | 8.6 | 8.2 | 7.4 | 7.7 | 7.8 | 5.7 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay F because it delivers the highest fidelity to the book's late name reveal, locked character visuals, book-close dialogue and single-clip directible beats without any disqualifying inventions. Weakest: Screenplay C because Ma is fatally mis-cued as Mrs. Engel, the file contains unparsable garbage text, and the scene count massively exceeds the 10-minute short-clip target.
- **grok-4:** Strongest: Screenplay F because it achieves near-perfect source fidelity, consistent character visuals, and required sound cues without any inventions or structural additions. Weakest: Screenplay B because it introduces an invented closing line from another work and relies on a non-book frame narrative that violates adaptation fidelity.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 23
- **Dialogue Blocks:** 111 (Avg `12.6` words/turn, Max `47` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Highest-fidelity spine that preserves book wording, late name reveal, key dreams, crash, stabbing and prison forgiveness while locking consistent visual traits (lean Narrator, scarred reddish-brown Nick, pale blue-eyed Sionna) on re-entry; actions are single-clip concrete and dialogue is performable. Sound is strong though a few bridge scenes are thinner.
  - *grok-4:* Highest fidelity with precise book wording, locked visual traits restated on re-entry, concrete filmable actions, and sound cues in every scene without inventions.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate that every scene heading is followed by at least one explicit (SOUND:) or (MUSIC:) cue before any dialogue; treat absence as a parse failure.
  - *grok-4:* Add a rule requiring 2-3 locked visual traits to be restated in the first action line whenever a character re-enters after a time jump or long absence.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 19
- **Dialogue Blocks:** 72 (Avg `14.2` words/turn, Max `42` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *grok-4.5:* Tight, faithful compression of the essential spine (Sionna meet-cute, Ma decline, bar confrontation, crash, stabbing, separation, prison release) with clear visual locks (dark-blond Narrator, scarred Nick) and concrete crash/stabbing micro-beats; dialogue is performable though slightly smoothed. Sound beds are present and useful; pacing suits 5-10s clips without multi-location cram.
  - *grok-4:* Strong fidelity with visual action lines, consistent character descriptions, and sound cues in every scene; minor compression of some internal beats but no inventions.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add an explicit cap that no single Action block may contain more than two distinct physical actions; split further actions into new Action lines or micro-scenes.
  - *grok-4:* Add an explicit rule requiring every scene to end with a concrete visual or sound micro-beat before any transition or cut.

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 29
- **Dialogue Blocks:** 0 (Avg `0` words/turn, Max `0` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Strong full-arc coverage from Milwaukee setup through crash, stabbing, Seattle success and prison forgiveness with book-close dialogue and consistent Seattle frame; minor Sionna hair-length drift and light compression of secondary library/yoga beats keep it from perfect. Excellent per-scene sound cues and filmable single-location actions make it highly directible for short clips.
  - *grok-4:* Strong coverage and sound design with consistent narrator voice, but some scenes lack the required grounding action line immediately after the heading.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require that every character re-entry after a time jump or age split must restate 2-3 locked visual traits (build, hair, scar, signature wardrobe) in the opening Action line of that scene.
  - *grok-4:* Add a hard rule that every scene heading must be followed by at least one concrete camera-observable action line before any dialogue or V.O.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 77 (Avg `19.4` words/turn, Max `58` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 10 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Clean, well-paced short-film cut with authentic book dialogue, reliable sound beds on every scene, and clear visual introductions; slightly lighter on middle-act philosophy/yoga texture than the top candidate but still fully filmable and faithful to the emotional arc.
  - *grok-4:* Solid frame narrative and visual descriptions, but some dialogue paraphrasing and minor invented connective tissue reduce fidelity slightly.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require that the central violent/emotional peak (crash, stabbing, confession) interleave at least two distinct visual or sound micro-beats with any VO rather than a single summary Action under a long monologue.
  - *grok-4:* Add an explicit instruction that any summarized exchange may receive at most one brief generic period-appropriate line, and only if action alone cannot carry the beat.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 16
- **Dialogue Blocks:** 80 (Avg `12.3` words/turn, Max `29` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 6 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Clean short-clip structure, solid sound, and filmable actions, but front-loading the real name PETER (revealed only on the final boarding pass in the book) is a hard fidelity and closed-cast violation; some paraphrase and minor invented gate-agent business further lower the score.
  - *grok-4:* Good visual action and consistent looks, but early use of the narrator's full name before the book's reveal and some compression of key emotional beats lower fidelity.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Re-state as a hard rule that a first-person narrator's real name may appear in cues or Action only at or after the exact moment the source text first discloses it; until then the token must remain NARRATOR/THE NARRATOR.
  - *grok-4:* Add a rule that a narrator's real name may not appear in character cues or action until the exact moment the source text first reveals it.

### 🎬 claude-opus-5
- **Scene Headings Count:** 59
- **Dialogue Blocks:** 369 (Avg `19.9` words/turn, Max `104` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG NICK, YOUNG NARRATOR, TEEN NICK`
- **Diagnostics & Warnings:**
  - ⚠️ Excessive scene count (59 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ 64 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 3 age-qualified character header(s) (e.g. YOUNG NICK, YOUNG NARRATOR, TEEN NICK).
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Contains many authentic book passages and strong young/adult splits, but catastrophic Ma/Mrs. Engel identity swap, embedded junk text, and runaway length make it unusable; fidelity and pacing collapse under the errors.
  - *grok-4:* Detailed coverage with strong visual grounding and consistent looks, but some scenes cram multiple actions and the overall length exceeds the 10-minute target.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a hard validation rule: every CHARACTER cue and Action mention must match an explicitly introduced token with identical spelling; any rename or swap is a fatal defect.
  - *grok-4:* Add a rule capping total scene headings at 25 for any adaptation targeting a 10-minute runtime, with mandatory merging of consecutive same-location beats.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 19
- **Dialogue Blocks:** 63 (Avg `12` words/turn, Max `41` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *grok-4.5:* Over-compressed skeleton that drops essential growth beats, front-loads the name, and grafts an alien Christmas-Carol tag; remaining scenes are only moderately directible and sound coverage is uneven.
  - *grok-4:* Major fidelity violation with invented content and non-source dialogue; pacing suffers from frame device and weak character consistency across time jumps.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Prohibit any dialogue or closing button that is not traceable to the source text; require the final image/VO to use only diction present in the book.
  - *grok-4:* Add a hard rule that no dialogue, closing lines, or thematic tags may be invented or borrowed from other works; all spoken words must derive directly from the source text.

