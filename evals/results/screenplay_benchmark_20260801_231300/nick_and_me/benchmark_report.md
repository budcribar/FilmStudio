# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 05:16:45 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **84.1** | 99.8% | 74.0% | 4 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **78.7** | 98.5% | 66.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 95% | 6.8/10 | 7.8/10 | 6.4/10 | 7.2/10 | 7.8/10 | 8.4/10 |
| **gpt-5.6-terra** | 95% | 100% | 100% | 2.8/10 | 7.2/10 | 7.3/10 | 5.9/10 | 7.7/10 | 8.4/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - gpt-5.6-terra: The screenplay reveals the narrator as PETER from the opening despite the source withholding his name until the closing line, violating the source’s reveal structure.
  - gpt-5.6-terra: Several headings combine distinct locations or major time/action changes in one scene, including "DOCTOR’S OFFICE / HOSPITAL CORRIDOR," "MILWAUKEE STREETS / INTERSECTION," and "WISCONSIN HIGHWAY / AIRPORT," making the draft noncompliant with the one-location-per-scene production constraint.
  - gpt-5.6-terra: The draft omits the required VISION_META sidecar, a stated hard delivery requirement for downstream portrait and clip production.
  - grok-4.5: First-person protagonist locked as PETER from FADE IN though the book withholds the name until the final line—violates NARRATOR-until-named rule
  - grok-4.5: Multiple illegal multi-place scene headings using slashes (e.g. DOCTOR'S OFFICE / HOSPITAL CORRIDOR, MILWAUKEE STREET / PUBLIC LIBRARY, WISCONSIN HIGHWAY / AIRPORT)
- **gpt-5.6-terra**:
  - gpt-5.6-terra: The screenplay drops the source’s central irreversible stabbing sequence: Nick’s confrontation with Sionna, Peter’s arrival and intervention, Nick stabbing Sionna, Peter applying pressure to the wound, and the emergency response. It then presents Sionna already recovering from a stabbing, creating an unsupported major consequence.
  - gpt-5.6-terra: The omission breaks the causal arc behind Sionna’s trauma, breakup with the narrator, Nick’s attempted-murder conviction, and Peter’s later resentment and forgiveness.
  - gpt-5.6-terra: The draft omits the required VISION_META sidecar, a stated hard delivery requirement for downstream portrait and clip production.
  - grok-4.5: Drops the source's central violent peak (Nick stabbing Sionna); only shows braced aftermath, which is a major omitted beat
  - grok-4.5: Invented named supporting cast not in source (LIBRARIAN EVELYN, BOUNCER MARTIN, INSTRUCTOR MAYA) without necessity

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gpt-5.6-terra |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | 6.8 | **6.4** *(self)* |
| **grok-4.5** | **7.9** *(self)* | 6.7 |

### 🧐 Self-Bias Analysis
- ⚠️ grok-4.5 rated its own screenplay 7.9/10 vs. a 6.8/10 average from 1 other judge(s) (+1.1) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay A because it retains the book’s complete major arc, including Ma’s death, Sionna’s stabbing, the breakup aftermath, and Peter’s final forgiveness of Nick, despite significant compression and formatting defects. Weakest: Screenplay B because it entirely omits the stabbing that is the source’s central causal climax, yet relies on its aftermath throughout the final act.
- **grok-4.5:** Strongest: Screenplay B because it dramatizes the full spine including crash, stabbing, and prison forgiveness with clear age-split casting. Weakest: Screenplay A because it omits the stabbing climax entirely and only alludes to it in aftermath, which is a deal-breaking fidelity failure for this book.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 27
- **Dialogue Blocks:** 62 (Avg `11.6` words/turn, Max `40` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG NICK`
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. YOUNG NICK).
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the stronger adaptation because it preserves the central narrative spine: Peter’s dependence on intimidating Nick, Ma’s decline, Sionna’s intellectual and spiritual awakening of Peter, the bar rupture, Ma’s death in Nick’s drunk-driving crash, the stabbing, Sionna’s departure, Seattle success, and the prison forgiveness scene. It does, however, compress or omit substantial developmental material, particularly Peter’s college progression, Nick’s manipulative return to Ma’s life, and the longer aftermath that gives Sionna’s departure its full emotional weight. Peter is prematurely named, and visual identity is only partially maintained after long jumps; Sionna and Peter receive little re-entry description in later periods. Many individual beats are filmable, but several scenes contain multiple locations, ellipses, and major actions that should be separated into short clips. Sound coverage is consistently strong and concrete, though the prison confrontation contains an overlong speech that is not optimized for a 5–10 second clip.
  - *grok-4.5 (self):* Strongest source coverage of the two: alley window, gym, Sionna meet, Ma confusion, bar fights, crash, Ma death, hospital, full stabbing with 911 pressure, Seattle aftermath, prison forgiveness. Character looks are locked and restated across ages (YOUNG NICK vs adult Nick; older Peter). Dialogue stays short and character-distinct. Directibility is hurt by slash multi-location headings that break one-place/one-clip scheduling. Sound/music cues are dense and usable. Early PETER naming and heading violations block greenlight despite otherwise solid craft.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a strict clip-beat rule: each scene may contain only one decisive dramatic action or reversal, and any time jump, location change, or separate consequence must begin a new scene heading rather than being folded into "Later" action within the existing scene.
  - *grok-4.5 (self):* Strengthen the heading rule with a parse-time ban example list and require validators to reject any INT./EXT. line containing '/' or ' AND '; also restate that an unnamed first-person narrator must remain the single token NARRATOR in cues and Action until the source text itself reveals the given name.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 25
- **Dialogue Blocks:** 103 (Avg `8` words/turn, Max `21` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* The screenplay is cleaner than A at the scene level, with generally discrete locations, concise dialogue, strong recurring visual identifiers for Nick and Sionna, and excellent scene-by-scene ambient sound design. However, its adaptation fidelity is severely compromised by omitting the pivotal attack on Sionna while retaining every consequence of that attack. Sionna appears in a shoulder brace and later leaves Peter because of trauma, while Nick is said to have received an attempted-murder sentence, but the audience never sees or hears the event that causes those outcomes. This is not a permissible compression; it removes the story’s essential climax and makes the third act feel discontinuous. The draft also skips much of the school, business-growth, and relationship erosion material, making the Seattle resolution feel more asserted than earned. Its dialogue is generally playable and character-specific, but the emotional logic cannot fully land without the missing causal scene.
  - *grok-4.5:* Fidelity collapses because the stabbing climax is never dramatized—only a later brace and breakup—violating the severe-penalty rule for dropped major beats. Cast looks for Nick/Sionna/Olivia are mostly stable and NARRATOR token correctly delays the Peter name, but NARRATOR's own visual lock is thin. Action is generally clip-sized and sound cues are consistent and concrete. Pacing feels like a highlight reel with jarring ellipses (funeral → already-recovered Sionna → Seattle), so tension never fully peaks on-camera.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a mandatory causal-event coverage audit before drafting: identify every irreversible event that directly causes a later injury, death, conviction, breakup, revelation, or ending choice, and require that each event be explicitly dramatized on-screen before any of its consequences appear.
  - *grok-4.5:* Add an explicit rule: every source major violent or emotional peak (kill, assault, death reveal, confession) must be dramatized on-screen as interleaved concrete Action micro-beats plus short dialogue/VO—never only referenced in aftermath or exposition.

