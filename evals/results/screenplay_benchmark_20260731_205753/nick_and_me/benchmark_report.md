# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-01 03:01:44 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-terra** | **91.5** | 100.0% | 86.0% | 4 pts | 1.0 |
| 🥈  | **grok-4.5** | **85.2** | 99.8% | 75.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-terra** | 100% | 100% | 100% | 9.0/10 | 8.2/10 | 8.5/10 | 8.2/10 | 8.8/10 | 8.8/10 |
| **grok-4.5** | 100% | 100% | 95% | 7.2/10 | 7.8/10 | 7.8/10 | 7.5/10 | 7.8/10 | 7.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - grok-4.5: Protagonist cued as PETER from the opening (and throughout) despite the book withholding the name until the final line; must remain NARRATOR until the source reveal
  - grok-4.5: Speaking supporting role cued as bare BOYFRIEND (relational/unstable label forbidden by closed-cast rules)
  - grok-4: Missing explicit sound cue in multiple scenes
  - grok-4: Inconsistent character visual restatement across time jumps
  - grok-4: Some action lines contain unfilmable internal states

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-terra | grok-4.5 |
| :--- | :---: | :---: |
| **grok-4.5** | 8.4 | **8.2** *(self)* |
| **grok-4** | 8.8 | 6.8 |

### 🧐 Self-Bias Analysis
- ⚠️ grok-4.5 rated its own screenplay 8.2/10 vs. a 6.8/10 average from 1 other judge(s) (+1.4) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay B because it alone obeys the narrator-name reveal rule, covers more essential source confrontations and the crash without major drops, and stays production-ready. Weakest: Screenplay A because the early PETER naming and illegal BOYFRIEND cue are hard closed-cast/fidelity violations that block greenlight despite superior visual locking.
- **grok-4:** Strongest: Screenplay A because it maintains source fidelity, consistent visuals, and required audio cues across every scene. Weakest: Screenplay B because it omits required sound cues and has inconsistent character descriptions across time periods.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 31
- **Dialogue Blocks:** 184 (Avg `7.7` words/turn, Max `18` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 7 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Highest fidelity: correctly keeps NARRATOR until the final 'President Peter Olson' line, covers more source beats (doctor visit, battlefield dream, early slap fight, Nick apartment mess, Trans Am crash visual, full stabbing aftermath), and invents only light stable names (DAVID, ELLEN, HARRIS) for unnamed speakers. Directibility is concrete and clip-friendly; pacing escalates cleanly through the accident/stabbing/prison arc with good Seattle coda. Dialogue preserves book wording and subtext in short turns. Sound cues are present and specific in every scene. Character looks are described on first entry and some re-entries but lack a locked upfront bible and occasional restatement after time jumps, the only real softness.
  - *grok-4:* Strong fidelity to source beats and dialogue, consistent character visuals across time jumps, concrete filmable actions, and explicit sound cues in every scene.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate a title-page Notes: cast bible that locks 2-3 filmable visual traits (build/hair/scar/wardrobe colors) for every recurring character on first appearance, and require those exact traits to be restated in the opening Action line of any scene after a time jump or age shift.
  - *grok-4:* Add an explicit rule requiring every scene to end with a single-line visual or sound micro-beat that can serve as a natural clip cut point.

### 🎬 grok-4.5
- **Scene Headings Count:** 32
- **Dialogue Blocks:** 91 (Avg `12.1` words/turn, Max `55` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 4 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5 (self):* Strong visual locking via title-page Notes and consistent re-entry traits for the core four; solid short-clip action and near-complete sound coverage. Fidelity suffers from premature naming of the narrator, compression that drops several mid-book confrontations (early slap, full doctor/ER setup, Nick's wrecked apartment), and the illegal BOYFRIEND cue. Pacing is tight with effective Seattle bookends but loses some escalation density around the crash and stabbing. Dialogue stays close to book cadence when present. Production-blocked by the two hard cast/reveal violations.
  - *grok-4:* Covers core beats but drops several source details, has weaker character consistency, and omits required audio cues in several scenes.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5 (self):* Add an explicit enforcement example under closed-cast and name-reveal rules: 'If the source withholds the protagonist's given name until late or final pages, the cue must remain NARRATOR (or equivalent) until that exact reveal; never promote an end-name into early Action or cues, and never allow bare relational tokens (BOYFRIEND, GIRLFRIEND, HUSBAND) as dialogue cues—assign a stable proper name or ROLE SURNAME instead.'
  - *grok-4:* Add an explicit rule requiring every scene to contain at least one standalone (SOUND: concrete diegetic description) or (MUSIC: specific description) cue placed on its own line after the action.

