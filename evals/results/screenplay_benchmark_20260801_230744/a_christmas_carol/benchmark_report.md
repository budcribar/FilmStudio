# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 05:12:58 UTC*  
*Source Story File: `A_Christmas_Carol.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **85.8** | 98.5% | 77.0% | 4 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **80.7** | 100.0% | 68.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 70% | 8.8/10 | 7.8/10 | 5.8/10 | 7.2/10 | 8.0/10 | 8.8/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 3.5/10 | 7.2/10 | 7.8/10 | 7.0/10 | 7.2/10 | 8.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - gpt-5.6-terra: The draft does not follow the required Fountain blank-line separation rules, so its scene, action, and dialogue elements are not reliably parseable by the stated downstream pipeline.
  - gpt-5.6-terra: The required VISION_META sidecar is absent.
  - gpt-5.6-terra: Several dialogue turns substantially exceed the prompt's 35-word maximum and cannot fit the intended 5-10 second clip format without further subdivision.
  - gpt-5.6-terra: The heading "EXT. BLEAK MOOR / LIGHTHOUSE / SHIP - NIGHT" combines three distinct locations in direct violation of the one-location-per-heading rule.
  - grok-4.5: Invalid multi-location heading 'EXT. BLEAK MOOR / LIGHTHOUSE / SHIP - NIGHT' joins places with slashes
  - grok-4.5: Unstable supporting tokens CHARWOMAN, FAT MAN, UNDERTAKER'S MAN instead of proper independent names
- **gpt-5.6-terra**:
  - gpt-5.6-terra: The draft does not follow the required Fountain blank-line separation rules, so its screenplay elements will not reliably parse in the specified production pipeline.
  - gpt-5.6-terra: The required VISION_META sidecar is absent.
  - gpt-5.6-terra: It drops major source beats, including the Ghost of Christmas Present's revelation of Ignorance and Want and the debtor family's relieved response to Scrooge's death; under the stated rubric, these omissions are a severe adaptation-fidelity failure.
  - gpt-5.6-terra: Some scenes combine distinct locations and narrative turns under one heading, notably the Old Joe shop sequence shifting directly into the dead man's bedroom.
  - grok-4.5: Major source beats dropped (Marley knocker transformation, Ignorance and Want, debtors' family relief at the death)
  - grok-4.5: Relational/possessive character cues FRED'S WIFE and BELLE'S HUSBAND violate stable independent token rule

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gpt-5.6-terra |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | 7.2 | **6.4** *(self)* |
| **grok-4.5** | **8.2** *(self)* | 7.2 |

### 🧐 Self-Bias Analysis
- ⚠️ grok-4.5 rated its own screenplay 8.2/10 vs. a 7.2/10 average from 1 other judge(s) (+1.0) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay A because it retains substantially more of Dickens's essential plot, character, and thematic architecture while providing exceptionally consistent scene-level sound cues. Weakest: Screenplay B because, despite its cleaner clip-oriented economy, it omits major source beats that are indispensable to the Present and Future visions, triggering the rubric's severe fidelity penalty.
- **grok-4.5:** Strongest: Screenplay B because it delivers near-complete source coverage, iconic dialogue, and superior emotional arc while still hitting runtime. Weakest: Screenplay A because severe omissions of major beats collapse fidelity and relational cast cues create downstream identity failures.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 27
- **Dialogue Blocks:** 118 (Avg `21` words/turn, Max `97` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG SCROOGE, YOUNG MAN SCROOGE, TINY TIM, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 24 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 4 age-qualified character header(s) (e.g. YOUNG SCROOGE, YOUNG MAN SCROOGE, TINY TIM).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the stronger adaptation on story fidelity. It preserves nearly the entire dramatic spine: Scrooge's cruelty, Fred and the charity collectors, Marley, all three Spirits, Fan, Fezziwig, Belle, the Cratchits, Ignorance and Want, Old Joe, Caroline's debtor-family relief, Tiny Tim's death vision, the grave reveal, and the redemption. Character visual introductions are generally specific and useful, especially Scrooge, Marley, the three Spirits, Bob, Tiny Tim, and the young/adult Scrooge split. However, it is not operationally built for short AI clips: many scenes contain several locations, time shifts, reveals, and action units; the Fezziwig, Belle, Cratchit, and ending sequences are especially overpacked. Long blocks of Dickens dialogue are faithful but routinely unplayable in one short clip. Its sound cues are consistently concrete, emotionally apt, and stronger than B's, but formal Fountain noncompliance and the missing sidecar prevent greenlight as-is.
  - *grok-4.5 (self):* Highest fidelity: nearly every essential beat and quotable line is present (knocker, full Marley, Fezziwig, Belle, Cratchits, Ignorance/Want, Old Joe, debtors, grave, turkey, salary raise) with only light runtime compression. Character introductions supply strong visual locks and clear YOUNG / YOUNG MAN SCROOGE splits, though a few supporting cues remain generic. Directibility suffers from the slash-joined heading and 'quick visions' summary that cram multiple locations and actions into non-clip-sized units. Pacing and dialogue are excellent—book cadence preserved, short enough for clips, tension builds cleanly. Sound/music cues are dense and diegetic at peaks. Structure and cast-token defects prevent an immediate greenlight.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a mandatory runtime-budget audit before output: cap the entire draft at a defined total dialogue/action word count for the target runtime, and require every dialogue turn and visual beat to be independently playable within one 5-10 second clip.
  - *grok-4.5 (self):* Strengthen the single-place heading rule with a positive example: for brief multi-setting montages issue separate concrete headings per micro-location or confine Action to one continuous observable shot; never allow slash/AND joins or 'quick visions' summary language under one slug.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 19
- **Dialogue Blocks:** 130 (Avg `10` words/turn, Max `24` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG SCROOGE, TINY TIM, OLD JOE`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 3 age-qualified character header(s) (e.g. YOUNG SCROOGE, TINY TIM, OLD JOE).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* B is more concise and generally more shootable than A. It provides clear location grounding, concrete visual actions, stronger scene-to-scene economy, and dialogue that is usually short enough to stage in a single generated clip. Its casting descriptions are solid and mostly consistent, with clear age distinctions for Young Scrooge and recurring physical markers for Bob, Fred, Tiny Tim, and the Spirits. Sound beds are present in every scene and are functional, although several music cues remain generic rather than being developed as a deliberate thematic score. The central flaw is fidelity: it omits major moral and plot turns from the source, most notably Ignorance and Want and the debtor household's joy at the miser's death, which weakens the Present Spirit's social indictment and the Future Spirit's accumulating evidence of Scrooge's legacy. It also compresses some consequential transitions so aggressively that the final revelation has less investigative dread and emotional inevitability.
  - *grok-4.5:* Fidelity is severely damaged by omission of multiple iconic beats required for a recognizable Carol (knocker face, Present's children, Caroline scene), even though the three-spirit spine remains; character looks are mostly locked on first appearance and Young Scrooge is visually aged, but relational cues and thin re-entry restatements weaken casting clarity. Directibility is strong—single concrete locations, observable actions, short clip-friendly beats—while pacing is tight for runtime yet feels rushed where cuts land. Dialogue keeps many book phrases but shortens speeches and routes some spoken lines through NARRATOR V.O. Sound cues are consistently present and useful. Not greenlight-ready due to the fidelity holes and cast-token violations.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a mandatory source-coverage checklist step requiring the model to identify every major plot turn, thematic revelation, and final-act proof sequence before drafting, then verify that each is represented by at least one scene or explicit visual beat in the completed screenplay.
  - *grok-4.5:* Add an explicit retention rule: iconic set-pieces and thematic reveals (prop transformations, ghost arrivals, pair-of-children or equivalent moral emblems) must appear as distinct filmed beats even under the 10-minute cap; compression may drop only pure connective or travel paragraphs.

