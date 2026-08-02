# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 23:36:16 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **87.8** | 100.0% | 80.0% | 2 pts | 1.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 100% | 100% | 100% | 7.5/10 | 8.5/10 | 7.8/10 | 7.5/10 | 8.2/10 | 8.8/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-sol**:
  - grok-4: Narrator explicitly named Peter Olson at end and in final lines, but source book never reveals the first-person narrator's name

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol |
| :--- | :---: |
| **grok-4.5** | 7.9 |
| **grok-4** | 8.0 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay A because it is the only candidate and delivers a production-ready, full-arc Fountain adaptation with excellent sound cueing and high source fidelity. Weakest: Screenplay A because age-split cast tokens are underspecified and a few peak scenes still cram multiple clip-length actions into one heading.
- **grok-4:** Strongest: Screenplay A because it is the sole candidate and delivers strong visual directibility plus consistent sound cues. Weakest: Screenplay A because it violates the closed-narrator rule by naming the first-person protagonist.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 22
- **Dialogue Blocks:** 125 (Avg `10.9` words/turn, Max `30` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Strong full-arc compression of Nick and Me that hits the essential spine (Ma caretaking, Sionna awakening, bar rupture, crash/Ma death, stabbing, Seattle reinvention, prison forgiveness, Peter Olson button) without inventing major plot. Supporting names (Marco, Paramedic Harris) are closed-cast necessities, not freestanding inventions. Character looks lock well on first entry (Nick scars/suede, Sionna eyes/necklace, Narrator work clothes → later business shirt) and mostly restate on re-entry, though age splits rely on prose ('older NARRATOR') rather than explicit YOUNG/ADULT tokens. Directibility is mostly clip-ready concrete action, weakened by a few dense multi-beat scenes (Joe's brawl, stabbing aftermath) and heavy V.O. over static images. Pacing covers decades via clear time jumps but several 'months later' cuts feel abrupt for short-clip rhythm. Dialogue stays close to book diction and clip length. Sound/music is the standout: nearly every scene has concrete diegetic (SOUND:) or (MUSIC:) cues, with peaks double-cued.
  - *grok-4:* Strong visual action lines and consistent character descriptions with locked wardrobe/age traits; excellent sound cues in every scene; dialogue stays close to book voice. Fidelity penalized for naming the unnamed narrator and for runtime length exceeding the 10-minute target. Pacing solid but some scenes cram multiple micro-beats.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require explicit age-disambiguated cast tokens (e.g. YOUNG X / ADULT X) with 2–3 restated visual traits on every first appearance after a time jump, and cap any single scene to one primary dramatic purpose so multi-phase fights or confrontations split into separate headings for 5–10s clip generation.
  - *grok-4:* Add an explicit rule that the narrator character cue must remain NARRATOR unless the source text explicitly names the narrator.

