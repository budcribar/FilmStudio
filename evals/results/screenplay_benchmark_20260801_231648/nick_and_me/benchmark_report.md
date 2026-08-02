# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 05:20:28 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **80.5** | 100.0% | 68.0% | 3 pts | 1.5 |
| 🥈  | **gpt-5.6-terra** | **80.2** | 100.0% | 67.0% | 3 pts | 1.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 100% | 7.0/10 | 6.5/10 | 5.0/10 | 6.8/10 | 7.2/10 | 8.0/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 5.5/10 | 5.5/10 | 6.5/10 | 7.0/10 | 7.5/10 | 8.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - gpt-5.6-terra: The Fountain body is not production-parseable as delivered: dialogue cues are separated from their dialogue by blank lines, contrary to the required Fountain format.
  - gpt-5.6-terra: The required VISION_META sidecar is missing.
  - gpt-5.6-terra: The protagonist is inconsistently keyed as both NARRATOR and PETER, and Peter's name is revealed substantially earlier than the source reveals it.
  - gpt-5.6-terra: An unnamed GUARD has dialogue, violating the closed-cast/stable-token requirement.
  - gpt-5.6-terra: Several headings improperly combine separate locations, including CITY STREET / PUBLIC LIBRARY, DOCTOR'S OFFICE / HOSPITAL CORRIDOR, FUNERAL HOME / CEMETERY, and WISCONSIN HIGHWAY / AIRPORT.
  - grok-4.5: Multiple scene headings illegally join distinct locations with / (DOCTOR’S OFFICE / HOSPITAL CORRIDOR, FUNERAL HOME / CEMETERY, CITY STREET / PUBLIC LIBRARY), violating the hard single-place rule and breaking downstream scheduling/clip generation
  - grok-4.5: Narrator is named PETER from the opening scenes, revealing the given name long before the book does so at the close
- **gpt-5.6-terra**:
  - gpt-5.6-terra: The Fountain body is not production-parseable as delivered: dialogue cues are separated from their dialogue by blank lines, contrary to the required Fountain format.
  - gpt-5.6-terra: The required VISION_META sidecar is missing.
  - gpt-5.6-terra: It invents named supporting characters not named in the source, including CARLOS, BOUNCER HARRIS, and INSTRUCTOR MAYA; under the evaluation rubric, invented named characters are a severe fidelity failure.
  - gpt-5.6-terra: The protagonist is inconsistently keyed as NARRATOR and PETER, while MA is initially identified as OLIVIA OLSEN but later keyed only as MA.
  - gpt-5.6-terra: Young Nick and adult Nick use the same NICK token without explicit age disambiguation, despite the childhood flashback.
  - gpt-5.6-terra: DREAM is used as a scene heading without an INT./EXT. location, producing invalid/non-filmable scene structure.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gpt-5.6-terra |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | 6.4 | **5.1** *(self)* |
| **grok-4.5** | **7.1** *(self)* | 8.3 |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-terra rated its own screenplay 5.1/10 vs. a 8.3/10 average from 1 other judge(s) (-3.2) — notably harsher on itself than peers were.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay A because it covers the source's full emotional and plot arc more faithfully while maintaining a relatively efficient dramatic progression. Weakest: Screenplay B because its invented named characters trigger a severe fidelity penalty, compounded by major age/token ambiguity and invalid scene-heading structure.
- **grok-4.5:** Strongest: Screenplay A because it alone obeys single-location headings, correctly withholds the narrator’s name until the book’s final reveal, and delivers consistently clip-ready observable action. Weakest: Screenplay B because illegal multi-place slash headings render structure unusable for AI video breakdown and the early PETER naming violates source fidelity.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 32
- **Dialogue Blocks:** 88 (Avg `10.7` words/turn, Max `32` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the stronger adaptation because it preserves the core emotional spine: Peter's dependence on intimidating Nick, Sionna's intellectual awakening of Peter, Ma's deterioration and death, Nick's assault on Sionna, the breakup, Seattle success, and the prison forgiveness scene. It is impressively comprehensive for a ten-minute target and generally avoids major invented plot. However, it skips or reduces major source development, especially Peter's schooling progression, Nick's manipulative return to Ma, the childhood material, and the extended psychological aftermath. Character looks are initially useful, especially Nick and Sionna, but the NARRATOR/PETER identity split is a serious portrait-lock defect, and re-entry descriptions are inconsistent. Many action lines are visual, but several scenes are compressed summaries containing multiple beats, locations, and emotional conclusions that cannot cleanly become one 5-10 second clip. Sound coverage is consistently present and often specific, though some music cues remain generic rather than tightly motivated. Dialogue retains portions of the book's cadence but often compresses source exchanges into blunt thematic statements.
  - *grok-4.5 (self):* Covers the same spine and many book lines, but early permanent naming of the first-person narrator as PETER directly contradicts the source-withholding rule and costs fidelity. Character looks are introduced with decent specificity and held reasonably well. Directibility collapses on the multi-location headings, which also hurt pacing clarity and make short-clip extraction unreliable. Dialogue is largely authentic and clip-length. Sound cues are present and functional. The heading violations plus the name reveal are deal-breakers for production readiness even though average scores are middling.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a mandatory preflight output-validation rule: before returning, verify Fountain cue/dialogue adjacency, stable character-token identity, one location per slugline, and the required sidecar; if any check fails, regenerate the full screenplay rather than returning a partially compliant draft.
  - *grok-4.5 (self):* Add a mandatory pre-output scan: reject or auto-split any scene heading that contains '/' or ' AND ' joining places; enforce one concrete filmable location per heading with zero exceptions.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 25
- **Dialogue Blocks:** 19 (Avg `10.8` words/turn, Max `20` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* The draft has a coherent and readable dramatic throughline, strong initial visual descriptions, and excellent scene-level ambient sound coverage. It also gives the crash, ICU confrontation, stabbing, breakup, and prison reunion clear visual treatment. Nevertheless, its fidelity score must be severely capped because it invents several specifically named source-absent characters. It also substantially compresses or drops the book's gradual educational transformation, Peter's sustained caregiver burden, the family-history material, and portions of Nick's escalating possessiveness, making some later turns feel more abrupt. The childhood scene is useful source coverage but creates a major casting ambiguity because the same NICK token covers a twelve-year-old and adult Nick. Directibility is weakened by invalid dream slugs, broad action summaries, and sequences that ask one clip to carry multiple physical actions. Dialogue is generally concise and playable, but several exchanges are modernized or simplified rather than preserving the source's distinctive first-person voice and awkward specificity.
  - *grok-4.5:* Strong fidelity to the full arc (Viking brother setup, Sionna meet-cute and growth, Ma decline/hospital, bar fight, crash death, stabbing, Seattle aftermath, prison forgiveness) while correctly withholding the narrator’s given name until the final beat. Visual locks for Nick (scars, reddish-brown hair, build), Sionna (pale, light-blue eyes, fish necklace, specific wardrobe), and Ma are introduced early and mostly held. Action lines stay concrete and single-location, with clean dream/wake splits and short-clip-friendly beats. Dialogue stays close to book cadence and length. Every scene carries usable (SOUND:) or (MUSIC:) cues, denser at peaks. Minor deductions for occasional thin re-description on late re-entries and some middle compression, but nothing breaks usability.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Change the closed-cast rule to require stable neutral role tokens for source-unnamed people without inventing personal names or added dialogue; for example, use BOUNCER or LINDSEY'S DATE only where essential, rather than fabricating a first name, surname, or new speaking character.
  - *grok-4.5:* Add an explicit CHARACTER RE-ENTRY rule requiring that after any time jump or gap of more than two scenes the first Action line must restate at least two locked visual traits (build/hair/scar/wardrobe) for every returning character.

