# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 23:55:54 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-terra** | **89.3** | 100.0% | 82.0% | 2 pts | 1.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-terra** | 100% | 100% | 100% | 8.5/10 | 8.2/10 | 8.0/10 | 7.5/10 | 8.2/10 | 8.8/10 |

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-terra |
| :--- | :---: |
| **grok-4.5** | 7.8 |
| **grok-4** | 8.7 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay A because it is the only candidate and delivers a complete, filmable Fountain arc with strong diegetic sound coverage and faithful peak beats (crash, stabbing, prison forgiveness). Weakest: Screenplay A because high scene density plus V.O.-heavy connective tissue and one unstable supporting cast token (LINDSEY'S BOYFRIEND) keep it from being exceptional on pacing and casting clarity.
- **grok-4:** Strongest: Screenplay A because it delivers high fidelity, locked character visuals, and required audio cues across all scenes. Weakest: N/A (only one candidate provided).

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 25
- **Dialogue Blocks:** 119 (Avg `10.2` words/turn, Max `25` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Covers the full spine (Nick/Ma setup, Sionna meet-cute, library/Java Hut awakening, bar confrontation, crash/Ma death, stabbing, Seattle aftermath, prison forgiveness) without major invented plot. Compression drops secondary threads (yoga comedy, Buddhism depth, farm trip, extended Nick POV) but keeps essential turns. Character looks are mostly locked (Nick scars/hair/boots; Sionna pale/blue eyes; Ma afghan/pills) and age for Narrator/Nick is restated at the Seattle and prison jumps, but LINDSEY'S BOYFRIEND is an unstable cast token and several re-entries under-specify wardrobe. Action is generally clip-sized and camera-observable, though several scenes lean hard on V.O. restating what we already see and the bar/stab peaks pack multiple micro-beats tightly. Pacing hits the arc but scene count is high for a ~10-minute cut, so momentum thins in the middle montage of caretaking/school beats. Dialogue stays close to book diction and clip-length. Sound design is the strongest dimension: nearly every scene has concrete diegetic (SOUND:) cues, with multi-cue density on crash and stabbing.
  - *grok-4:* Strong fidelity to source beats and themes with consistent character visuals, concrete filmable actions, natural short dialogue, and mandatory audio cues in every scene; minor compression of some internal reflection keeps runtime tight without invention.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add an explicit rule that any supporting speaker must receive a stable personal name token on first cue (e.g. MARCO, not LINDSEY'S BOYFRIEND / THE BOYFRIEND) and that after any multi-year time jump the first Action line must restate 2–3 locked visual traits for every returning principal before dialogue or V.O.
  - *grok-4:* Add an explicit rule requiring every scene to end with a single concrete visual or sonic micro-beat that can stand alone as a 5-10 second clip.

