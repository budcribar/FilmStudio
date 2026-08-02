# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 05:24:28 UTC*  
*Source Story File: `The_Call_of_the_Wild.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-terra** | **88.7** | 100.0% | 81.0% | 4 pts | 1.0 |
| 🥈  | **grok-4.5** | **82.0** | 100.0% | 70.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-terra** | 100% | 100% | 100% | 8.1/10 | 8.4/10 | 7.9/10 | 8.0/10 | 8.3/10 | 8.0/10 |
| **grok-4.5** | 100% | 100% | 100% | 8.4/10 | 6.2/10 | 4.8/10 | 5.9/10 | 8.2/10 | 8.6/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-terra**:
  - gpt-5.6-terra: The required VISION_META sidecar is absent, so the screenplay does not supply the mandated machine-readable visual-medium and render-style lock.
  - gpt-5.6-terra: There are identity and location-token quality defects, including the malformed "DYE A" location spelling and inconsistent "FRANC OIS" rendering, which risk creating unstable production entities.
  - gpt-5.6-terra: Although much more clip-ready than Screenplay A, several action paragraphs still combine multiple independent beats that require further shot/clip splitting before generation.
- **grok-4.5**:
  - gpt-5.6-terra: The required VISION_META sidecar is absent, leaving no machine-readable final visual-medium/style lock for downstream portrait and clip production.
  - gpt-5.6-terra: Several scene headings combine separate physical locations, including "INT. BAGGAGE CAR / SAN FRANCISCO SHED - NIGHT" and "EXT. SKAGUAY STREET / TRAIL - DAY," directly violating the one-location-per-scene requirement.
  - gpt-5.6-terra: Multiple scenes compress major sequences, location changes, and long spans of time into single action blocks, making them unusable as discrete 5-10 second AI-video beats without substantial breakdown.
  - grok-4.5: Multi-location scene headings joined with slashes (INT. BAGGAGE CAR / SAN FRANCISCO SHED; EXT. SKAGUAY STREET / TRAIL) violate one-place-per-heading and break schedule/clip parsing
  - grok-4.5: Iconic Thornton set pieces (cliff jump, Black Burton throat attack, Forty-Mile rapids rescue) collapsed into unfilmable prose summary inside one camp scene instead of directed, observable beats

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-terra | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | **8.0** *(self)* | 6.8 |
| **grok-4.5** | 8.2 | **7.2** *(self)* |

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay B because it is substantially more directable as short AI-video clips, with clearer visual continuity, tighter scene rhythm, and superior scene-by-scene sound design. Weakest: Screenplay A because, despite stronger source coverage, it repeatedly collapses multiple locations, elapsed-time jumps, and distinct dramatic events into oversized scenes that are not production-ready for the stated clip format.
- **grok-4.5:** Strongest: Screenplay A because it is the only draft with consistent single-location, clip-ready staging, locked cast looks, and a full arc that can be scheduled without structural rewrite. Weakest: Screenplay B because illegal multi-location slash headings and unfilmable montage dumps of the Thornton set pieces make it unusable for short AI-video production despite wider beat name-checks.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 20
- **Dialogue Blocks:** 43 (Avg `8.4` words/turn, Max `19` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* This is the cleaner and more filmable draft. It gives Buck a strong, repeatable physical identity, provides useful recurring descriptions for Thornton and the dogs, uses more disciplined scene progression, and frequently turns source action into concrete visual beats. Its sound design is the best of the two packages: every sequence has a specific sound bed, and the rescue, wager, massacre, and wolf-pack climax have layered sonic escalation. The principal weakness is reduced source coverage. Important dramatic material is either omitted or heavily abbreviated, including much of Perrault and Francois's relationship with Buck, the growing team mutiny before Spitz's death, Buck's defense of Thornton against Black Burton, the cliff-jump demonstration, and the prolonged moose hunt. The screenplay remains faithful in the events it includes, but it narrows the book's evolutionary and emotional texture.
  - *grok-4.5:* Strong closed-cast short-film cut that hits the spine (kidnapping, red-sweater club law, Curly, Spitz death-fight, lead dog, Dave’s mercy shot, Hal/Mercedes collapse, Thornton rescue and thousand-pound pull, Yeehat massacre, pack joining) without inventing plot. Buck’s visual lock (red-brown coat, white chest blaze, dark muzzle, St. Bernard-shepherd build) is restated at key re-entries; supporting dogs and Thornton are distinct and stable. Scenes are single-location and mostly one-purpose, with concrete observable actions suited to 5–10s clips. Book dialect and iconic lines are preserved in short turns. Weakness is audio: every scene has diegetic (SOUND:) but zero (MUSIC:) cues at peaks (Spitz kill, ice collapse, pack howl), so emotional scoring is under-directed. Minor typos (DYE A BEACH, FRANC OIS) are polish, not blockers.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add an explicit adaptation-spine checklist requirement: before drafting, identify and include every major irreversible protagonist turn, relationship-defining set piece, and climactic trial from the full source, even when secondary travel or exposition must be cut to meet runtime.
  - *grok-4.5:* Require at least one explicit (MUSIC: specific cue) in addition to diegetic (SOUND:) at every major emotional peak (kill, death, rescue, reunion, final turn), so scores cannot pass on ambient sound beds alone.

### 🎬 grok-4.5
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 22 (Avg `12.6` words/turn, Max `29` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the more comprehensive adaptation: it retains Buck's privileged California life, kidnapping, club lesson, Curly's death, Spitz conflict, leadership, Dave's death, Hal's collapse, Thornton's rescue, wager, wilderness awakening, Thornton's death, and Buck's final union with the wolves. However, its coverage comes at the expense of production usability. Large sections summarize several source events in one scene or one action paragraph, particularly the mail run, Hal's disastrous expedition, Thornton's later exploits, and Buck's wilderness period. Character descriptions are strongest for Buck but weak or absent for many recurring human roles, and re-entries do not reliably restate locked traits. Dialogue is generally faithful and recognizable, while the sound work is consistently specific and especially effective at major violent and emotional peaks.
  - *grok-4.5 (self):* Broader beat checklist than A (hairy-man dreams, cliff whim, Black Burton, moose hunt, epilogue Ghost Dog) and strong book-flavored dialogue plus useful (MUSIC:) at fight/massacre peaks. But structure fails the short-clip brief: slash-joined headings are illegal multi-place slugs; the entire mid-Thornton hero run is one montage paragraph that cannot be broken into 5–10s directed clips; character tokens drift (MAN IN THE RED SWEATER vs MAN IN RED SWEATER; STRANGER then KIDNAPPER) and re-entry looks are thinner. Fidelity of coverage is high on paper, fidelity of dramatization is not—named set pieces are mentioned, not staged. Not greenlightable as-is despite solid sound design.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a hard clip-density rule: every action paragraph must depict only one camera-observable micro-event lasting no more than roughly 10 seconds; require a new scene or separately spaced action beat whenever time, location, or the primary action changes.
  - *grok-4.5 (self):* Add a hard validation rule: reject any scene heading containing '/' or ' AND '; and forbid collapsing multiple distinct source set-pieces into summary Action—each major turn must be its own single-location scene with ordered concrete beats.

