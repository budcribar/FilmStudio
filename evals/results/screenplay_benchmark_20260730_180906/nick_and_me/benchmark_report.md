# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 00:09:25 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **claude-sonnet-5** | **70.1** | 86.8% | 59.0% | 1 pts | 1.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **claude-sonnet-5** | 85% | 100% | 75% | 8.0/10 | 6.0/10 | 5.5/10 | 6.5/10 | 7.5/10 | 2.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **claude-sonnet-5**:
  - claude-sonnet-5: No sound design or music cues included anywhere in the script (zero score-worthy content for that dimension) — completely unaddressed for an AI video pipeline that needs audio direction.
  - claude-sonnet-5: Narrator is unnamed for nearly the entire script (credited as THE NARRATOR), which will cause severe reference-image/casting confusion in a system that needs a consistent character identity from the first clip; the late reveal works in prose but is a serious usability problem for per-character casting in this pipeline.
  - claude-sonnet-5: Several scene headings/action blocks combine multiple distinct actions or time-jumps (e.g., the bar brawl scene, the stabbing scene) that pack too much into a single filmable beat, violating the one-clip-per-beat constraint in places.
  - claude-sonnet-5: Nick's transformation from violent to sobbing/apologetic in the prison scene is compressed into a single short exchange with little intermediate beat coverage, undercutting the emotional arc of Part 4.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | claude-sonnet-5 |
| :--- | :---: |
| **claude-sonnet-5** | **5.9** *(self)* |

### 🗣️ Judge Summary Notes
- **claude-sonnet-5:** Strongest: Screenplay A by default, as it is the only candidate submitted and demonstrates strong narrative fidelity and closed-cast discipline. Weakest: Screenplay A also, since it is the only candidate and suffers from a complete absence of sound/music direction, an unnamed protagonist for nearly the entire runtime causing casting-clarity problems, and several overstuffed action beats that violate the single-clip-per-beat directive.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 claude-sonnet-5
- **Scene Headings Count:** 32
- **Dialogue Blocks:** 151 (Avg `13.1` words/turn, Max `98` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Non-Fountain colon dialogue format (6 instances like 'Character:') detected.
  - ⚠️ 5 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *claude-sonnet-5 (self):* Screenplay A is a fairly faithful, well-condensed adaptation that hits nearly all major book beats: Nick's introduction, Sionna's meet-cute, the Wuthering Heights parallel, the medieval dream sequences, Ma's decline, the DUI accident, the stabbing, the estrangement, and the prison reconciliation. It preserves the closed cast (Narrator, Nick, Ma, Sionna, Bob, Dr. Carson, Officer Dupuis, Lindsey/Ray, Sage) without inventing new named characters, which is a real strength for fidelity. However, character disambiguation suffers because the narrator is credited as 'THE NARRATOR' for the entire runtime and only gets a name in the final seconds — for a system that locks reference images per named character, this is a structural liability. Visual descriptions for Nick, Sionna, and Ma are given once at introduction but are not consistently restated at each reappearance, and aging/timeskip (Nick in prison eight years later) is only marked by adjective changes in action lines rather than clear age-labeled sluglines. Directibility is hurt by several scenes that mash together dialogue-heavy exchanges with multiple physical actions (the Joe's Bar brawl, the stabbing scene) that would need to be split into many more clips than the writing implies. Pacing is brisk and generally follows a clear escalation, though Part Two's college/growth material is compressed to the point of feeling rushed. Dialogue is largely lifted verbatim from the book, which keeps Nick and the narrator's voices authentic and distinct, though it occasionally retains run-on interior-monologue-style lines translated awkwardly into spoken dialogue. The complete absence of any sound design, score, or ambient audio cues across the entire script is the most severe deficiency — there is not a single music or sound prompt anywhere in the document, making this dimension essentially a zero and a disqualifying gap for a production that specifically evaluates audio-visual scoring.

