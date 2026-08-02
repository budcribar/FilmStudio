# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 04:51:12 UTC*  
*Source Story File: `The_Tell-Tale_Heart.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **86.5** | 100.0% | 78.0% | 3 pts | 1.5 |
| 🥈  | **gpt-5.6-terra** | **84.5** | 100.0% | 74.0% | 3 pts | 1.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 100% | 5.2/10 | 8.5/10 | 8.5/10 | 8.2/10 | 7.0/10 | 9.0/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 6.0/10 | 6.2/10 | 7.5/10 | 7.5/10 | 8.8/10 | 8.5/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - gpt-5.6-terra: Required VISION_META sidecar is absent, leaving the downstream portrait-and-clip pipeline without its mandated machine-readable style lock.
  - gpt-5.6-terra: The adaptation invents three specifically named police characters and gives them unquoted dialogue beyond the source's reported police visit; under the stated strict fidelity standard, invented named characters require a severe fidelity penalty.
- **gpt-5.6-terra**:
  - gpt-5.6-terra: Required VISION_META sidecar is absent, leaving the downstream portrait-and-clip pipeline without its mandated machine-readable style lock.
  - gpt-5.6-terra: The protagonist is called THE NARRATOR in Action but NARRATOR V.O. in dialogue cues, creating unstable cast-token identity and likely parser/cast-reference ambiguity.
  - gpt-5.6-terra: The adaptation invents three specifically named police characters; under the stated strict fidelity standard, invented named characters require a severe fidelity penalty.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gpt-5.6-terra |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | 7.4 | **6.5** *(self)* |
| **grok-4.5** | **8.1** *(self)* | 8.3 |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-terra rated its own screenplay 6.5/10 vs. a 8.3/10 average from 1 other judge(s) (-1.8) — notably harsher on itself than peers were.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay A because it delivers the most cinematic, clip-directable escalation, with superior visual micro-beats and an excellent sound-driven murder-to-confession arc. Weakest: Screenplay B because its otherwise solid adaptation is undermined by a core THE NARRATOR/NARRATOR token inconsistency that threatens cast locking and Fountain downstream parsing.
- **grok-4.5:** Strongest: Screenplay A because it delivers near-complete source coverage and book-authentic wording with almost no invented speech, which is the highest-priority fidelity bar. Weakest: Screenplay B because multiple invented officer and narrator lines the source never quotes drag dialogue authenticity and adaptation fidelity below an otherwise stronger directorial package.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 34 (Avg `16.1` words/turn, Max `26` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the stronger dramatic adaptation: it covers the full narrative spine, including the motive, seven-night ritual, eighth-night vigil, murder, concealment, police search, auditory collapse, and confession. Its locked physical descriptions are clear and mostly consistent, and the murder and confession sequences are exceptionally well broken into observable visual and sound micro-beats suitable for short clips. The sound plan is specific, escalating, and especially effective at the two major peaks. The chief fidelity defect is substantial under the evaluation rule: REYNOLDS, HALE, and BRIGGS are invented named characters, and their dialogue expands a source passage that largely reports rather than quotes the officers. A few early V.O. passages also carry exposition over visuals rather than using the narrator's behavior to externalize it. The missing required VISION_META block prevents a complete downstream-ready delivery as submitted.
  - *grok-4.5 (self):* Cinematically the more clip-friendly draft: varied concrete locations (chamber, hall, sitting room, street door), dense observable micro-beats (thumb slip on tin, spider-thread ray, bed crash, hand on still chest, chair grate), and excellent interleaved SOUND plus MUSIC at the kill and confession peaks. Cast looks lock cleanly (Narrator thirties lean pale dark waistcoat bright eyes; Old Man seventies thin white hair sunken cheeks filmed eye; three named officers). Fidelity and dialogue are the clear soft spots: the book never quotes the officers, yet OFFICER REYNOLDS delivers an invented search line; Narrator is given several non-source spoken beats (“Gentlemen—welcome…”, “Search—search well…”, “Rest from your fatigues…”, “A fine night for the watch…”) that violate the summarized-exchange / no-invented-officer-speech rules. Iconic confession and opening V.O. remain close to Poe, so the draft is still usable, but dialogue authenticity is only workable-to-flawed rather than excellent.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a mandatory final compliance check that rejects any spoken dialogue assigned to a source-unnamed incidental character unless the source explicitly quotes that speaker; where speech is not quoted, require the interaction to remain Action.
  - *grok-4.5 (self):* Add an explicit hard cap: supporting roles the book does not quote may have zero invented dialogue lines; fold their presence into Action only, and limit any narrator/on-camera paraphrase of unquoted book summary (“they chatted,” “I bade them welcome”) to at most one short generic line total per beat.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 10
- **Dialogue Blocks:** 47 (Avg `14.6` words/turn, Max `26` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* This draft faithfully preserves nearly all major source turns and retains Poe's diction more consistently in its spoken material than A. Its visual action is generally concrete, its scene progression is clear, and its audio cues create a usable gothic atmosphere. However, the character-token inconsistency is a serious production defect: THE NARRATOR, NARRATOR, and NARRATOR V.O. can be interpreted as separate identities by downstream Fountain and portrait workflows. The opening also compresses several distinct setup functions into a largely static bedchamber/V.O. sequence, and the repeated V.O. blocks reduce clip-level visual dynamism compared with A. As with A, the named officers trigger the required severe fidelity penalty despite otherwise strong source coverage. The absent sidecar and cast-token break make the package not greenlightable as-is.
  - *grok-4.5:* Highest-fidelity adaptation: covers every essential beat (motive/eye, seven nights, eighth-night vigil, groan/terror, ray on the open eye, heartbeat escalation, kill under the bed, dismemberment and floorboards, four o'clock knock, police search, seat over the corpse, confession) with almost no invented plot or quoted speech. Officers are named for closed cast but correctly kept silent; Narrator’s spoken lines track the book. Character locks are stable (lean young man, dark uncombed hair, hollow cheeks, plain black waistcoat; frail white-haired Old Man with filmed pale-blue eye) and restated on re-entry. Weakness is AI-clip directibility and pacing: long stretches of stacked NARRATOR V.O. over relatively static bedchamber images, many consecutive INT. OLD MAN'S BEDCHAMBER headings with limited micro-beat variety, and peak scenes that lean on monologue more than interleaved visual/sound cuts. Sound design is strong and present in every scene (diegetic ticks, creaks, heartbeat, shriek, saw, knocks) though peak scenes lack distinct MUSIC beds alongside SOUND.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add an automated final cast-token audit: every character reference in Action and every dialogue cue, after stripping only approved delivery suffixes such as parentheticals, must match one canonical character token exactly; reject the draft on any mismatch.
  - *grok-4.5:* Require that any V.O. passage longer than two sentences be broken by at least one new camera-observable Action micro-beat or distinct (SOUND:/MUSIC:) cue before the next V.O. block, and discourage more than two consecutive scene headings that reuse the exact same location+time pattern without a clear purpose or staging change.

