# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-01 03:05:10 UTC*  
*Source Story File: `The_Tell-Tale_Heart.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **90.2** | 100.0% | 84.0% | 4 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **86.4** | 100.0% | 77.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 100% | 7.5/10 | 9.0/10 | 8.8/10 | 8.5/10 | 7.5/10 | 9.0/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 8.5/10 | 7.5/10 | 6.8/10 | 7.0/10 | 8.8/10 | 8.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - grok-4: Invented spoken dialogue for the Old Man not present in the source.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gpt-5.6-terra |
| :--- | :---: | :---: |
| **grok-4.5** | **8.5** *(self)* | 7.4 |
| **grok-4** | 8.2 | 8.1 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay A because it best balances fidelity, locked cast looks, clip-scalable action, and peak sound/visual density while remaining production-ready. Weakest: Screenplay B because stacked V.O., late/thin character locks, and unnamed group officers materially hurt AI directibility and casting clarity despite cleaner dialogue purity.
- **grok-4:** Strongest: Screenplay B because its action lines and sound cues are far more filmable in short clips despite one fidelity flaw. Weakest: Screenplay A because multiple unbroken V.O. paragraphs violate the short-clip constraint and make it less directible.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 14
- **Dialogue Blocks:** 49 (Avg `19.7` words/turn, Max `34` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
- **Judge Rationale:**
  - *grok-4.5 (self):* Strong, near-complete coverage of Poe’s beats (motive, seven nights, eighth-night standoff, kill, concealment, police, confession) with iconic diction preserved in V.O. and on-camera lines. Narrator and Old Man are locked with consistent, filmable traits; officers receive stable named tokens and brief looks. Action is concrete and clip-scalable, with excellent interleaving of micro-beats and heartbeat sound at the peak. Minor deductions for a few invented spoken lines (Old Man’s ‘Well enough,’ brief officer banter) that the source only summarizes, and for a couple of denser scenes that pack many clip-units under one heading. Sound/music cues are specific, diegetic, and present in every scene, with peak density at the murder and confession.
  - *grok-4:* Strong visual action lines, consistent character descriptions, and well-interleaved sound cues make it highly directible, but invented Old Man lines violate fidelity rules.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5 (self):* Add an explicit hard rule that any exchange the book only summarizes without quoted speech must be carried by Action alone (zero invented spoken lines by default), allowing at most one brief generic line only when a mute gap would break the scene.
  - *grok-4:* Add an explicit rule that any summarized exchange or non-quoted speaker may receive at most one brief generic period-appropriate line, defaulting to zero when Action alone can carry the beat.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 50 (Avg `13.8` words/turn, Max `25` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
- **Judge Rationale:**
  - *grok-4.5:* Covers the full arc and keeps book wording tightly in V.O., with almost no invented dialogue—stronger purity on that axis than A. Weaknesses: Narrator’s full visual lock arrives late and thin; police remain an undifferentiated group (‘three POLICE OFFICERS’) with no individual tokens or looks, hurting cast-reference stability; heavy stacked V.O. over sparse Action and a ‘week passes in brief repetitions’ montage reduce clip-level directibility; peak kill/confession has fewer interleaved visual micro-beats than the source density warrants. Sound cues are present and the heartbeat bed works, but overall show-don’t-tell and casting clarity lag A enough to lower the average.
  - *grok-4:* Stays extremely close to source text with almost no invented dialogue or events; however, long unbroken V.O. blocks and repeated identical scene headings reduce directibility and pacing for short-clip format.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate that every character’s first on-screen appearance include a full locked visual description (build, hair/face, wardrobe) in that scene’s opening Action, and require a concrete visual or diegetic-sound micro-beat after every two consecutive V.O. sentences so peaks cannot run as long narration over static images.
  - *grok-4:* Add an explicit rule that no more than two consecutive V.O. lines may appear without an intervening concrete visual or sound micro-beat action line.

