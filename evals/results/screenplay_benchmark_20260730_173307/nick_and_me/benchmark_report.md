# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-30 23:43:50 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **claude-sonnet-5** | **63.6** | 88.5% | 47.0% | 1 pts | 1.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **claude-sonnet-5** | 80% | 100% | 90% | 5.5/10 | 5.0/10 | 6.5/10 | 3.0/10 | 7.0/10 | 1.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **claude-sonnet-5**:
  - claude-sonnet-5: Screenplay is severely incomplete — cuts off mid-scene/mid-sentence ('INT. NARRATOR'S APARTMENT - LIV') covering only a small fraction of the book's total narrative (roughly Part 1, chapters 6-9)
  - claude-sonnet-5: No music or sound design cues present anywhere in the provided text, making that dimension unscorable/unusable as delivered
  - claude-sonnet-5: Massive portions of the source material are entirely missing: Nick's death-spiral into alcoholism, the car accident killing Ma, Nick's stabbing of Sionna, the trial, the prison years, and the entire Part 4 Seattle timeline/reconciliation arc
  - claude-sonnet-5: Central named character 'Narrator' is never given an actual name (Peter), which is used at the very end of the book, creating identity/casting ambiguity
  - claude-sonnet-5: No visual character description given for Narrator, Nick, or Ma anywhere in the excerpt — physical descriptions from the book (Nick's scars, red hair, size; Ma's aging appearance) are absent, undermining the reference-image requirement

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | claude-sonnet-5 |
| :--- | :---: |
| **claude-sonnet-5** | **4.7** *(self)* |

### 🗣️ Judge Summary Notes
- **claude-sonnet-5:** Only one candidate was provided for review. Screenplay A is a heavily truncated fragment covering only a small early portion of the book (yoga/library/Buddhism/doctor visit beats) and cuts off mid-sentence without completing even that scene, let alone depicting the accident, the stabbing, the trial, prison, or the entire Seattle-set final act that make up the majority of the source material's dramatic content. It has no music/sound cues at all, no visual character descriptions to anchor casting/reference images, and the protagonist is never named despite the book revealing his name (Peter) in its final line. As the sole candidate it is ranked first by default, but it is not production-ready in its current form.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 claude-sonnet-5
- **Scene Headings Count:** 8
- **Dialogue Blocks:** 31 (Avg `15.8` words/turn, Max `51` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Non-Fountain colon dialogue format (4 instances like 'Character:') detected.
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *claude-sonnet-5 (self):* This fragment only adapts a small slice of Part One/early Part Two material (roughly the yoga/library/Buddhism/Ma's ER visit beats) and stops abruptly mid-scene without completing even that arc, let alone the accident, the stabbing, prison, or the Seattle-set redemption arc that make up more than half the book. Voiceover captures the narrator's introspective voice reasonably well and individual scenes (yoga studio, doctor's office, Ma's confusion) are decently filmable with clear single actions, but the fragment gives no distinct visual descriptions for any character (Nick, Ma, Sionna, the Narrator) to lock a reference image, and the Narrator is never named. There are zero music/sound cues anywhere in the text, making the sound design dimension essentially a failing/empty category. As a production package this is unusable — it's roughly 10% of a screenplay with no ending, act structure, or resolution of the central dramatic engine (Nick's downward spiral, Ma's death, the stabbing, prison, redemption).

