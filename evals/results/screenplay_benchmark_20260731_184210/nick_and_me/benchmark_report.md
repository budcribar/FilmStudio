# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-01 00:45:26 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **88.8** | 98.8% | 82.0% | 2 pts | 1.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 75% | 8.5/10 | 8.8/10 | 7.8/10 | 7.2/10 | 8.5/10 | 8.5/10 |

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 |
| :--- | :---: |
| **grok-4.5** | **8.2** *(self)* |
| **grok-4** | 8.2 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay A because it alone is present and delivers a faithful closed-cast full-arc Fountain cut with excellent sound cues and clear character locks. Weakest: Screenplay A because the same draft still over-packs V.O. and multi-beat scenes for short-clip AI direction and rushes mid-to-late structure.
- **grok-4:** Strongest: Screenplay A because it faithfully adapts the source with consistent character descriptions and explicit sound cues. Weakest: N/A as only one candidate evaluated.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 39
- **Dialogue Blocks:** 125 (Avg `13.3` words/turn, Max `54` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG NICK`
- **Diagnostics & Warnings:**
  - ⚠️ 5 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. YOUNG NICK).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5 (self):* Strong full-arc fidelity from Kirk Street setup through Sionna, Ma’s decline, the bar rupture, the crash, the stabbing, Seattle aftermath, and the prison forgiveness/button, with book diction largely preserved in dialogue and V.O. and no major invented plot. Cast looks lock well (Nick’s scars/hair/build, Sionna’s eyes/necklace, YOUNG NICK vs adult, older Seattle/prison variants). Directibility is mostly concrete but several scenes stack multi-location or multi-beat action under heavy V.O., and nonstandard headings like DREAM - BATTLEFIELD weaken clip scheduling. Pacing hits the emotional peaks but rushes connective tissue across a very large scene count for a ~10-minute target. Dialogue is character-distinct and clip-length friendly. Sound design is the standout: nearly every scene carries explicit diegetic (SOUND:) cues, with denser beds at peaks.
  - *grok-4:* Faithfully covers source beats with consistent character visuals and explicit sound cues per scene; minor pacing compression in flashbacks and V.O. density slightly reduces clip rhythm.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5 (self):* Add a hard rule that each scene may contain at most 2–3 distinct camera-observable micro-beats (suitable for 5–10s clips) and that any V.O. block longer than two short sentences must be broken by a new visual or sound micro-beat or a new scene heading—never leave multi-minute internal arc as continuous V.O. over one static image.
  - *grok-4:* Add an explicit rule requiring every scene to end with a single concrete visual or sound micro-beat before any transition or cut to the next heading.

