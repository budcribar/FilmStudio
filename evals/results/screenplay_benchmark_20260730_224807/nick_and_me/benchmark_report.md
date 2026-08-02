# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 05:32:54 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **89.0** | 92.8% | 86.0% | 14 pts | 1.0 |
| 🥈  | **gpt-5.6-luna** | **86.7** | 95.0% | 81.0% | 12 pts | 2.0 |
| 🥉  | **gpt-5.6-terra** | **84.3** | 95.2% | 77.0% | 8 pts | 4.0 |
| 4.  | **gemini-3.6-flash** | **82.4** | 94.5% | 74.0% | 8 pts | 4.0 |
| 5.  | **gemini-3.1-pro-preview** | **79.8** | 94.0% | 70.0% | 6 pts | 5.0 |
| 6.  | **grok-4.20-reasoning** | **74.2** | 95.0% | 60.0% | 3 pts | 6.5 |
| 7.  | **claude-opus-5** | **71.7** | 88.5% | 60.0% | 5 pts | 5.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 95% | 100% | 75% | 9.0/10 | 9.2/10 | 9.0/10 | 8.5/10 | 9.0/10 | 7.2/10 |
| **gpt-5.6-luna** | 100% | 100% | 90% | 8.5/10 | 8.2/10 | 8.5/10 | 8.0/10 | 8.5/10 | 6.8/10 |
| **gpt-5.6-terra** | 100% | 100% | 95% | 7.8/10 | 8.0/10 | 8.0/10 | 8.0/10 | 8.2/10 | 6.2/10 |
| **gemini-3.6-flash** | 100% | 100% | 80% | 6.5/10 | 8.0/10 | 8.0/10 | 7.5/10 | 8.0/10 | 6.5/10 |
| **gemini-3.1-pro-preview** | 100% | 100% | 70% | 6.0/10 | 7.5/10 | 7.8/10 | 7.0/10 | 8.0/10 | 6.0/10 |
| **grok-4.20-reasoning** | 100% | 100% | 90% | 4.8/10 | 5.2/10 | 7.0/10 | 6.8/10 | 7.0/10 | 5.5/10 |
| **claude-opus-5** | 100% | 78% | 70% | 6.0/10 | 6.0/10 | 6.0/10 | 4.5/10 | 7.5/10 | 6.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gemini-3.6-flash**:
  - grok-4: Early naming of narrator as PETER OLSON violates source withholding of name until final beat.
- **gemini-3.1-pro-preview**:
  - grok-4: Early naming of narrator as PETER and multiple invented dream/plot details violate source fidelity.
- **grok-4.20-reasoning**:
  - grok-4.5: Unstable character token spelling (SIONA vs SIONNA) across cues and headings
  - grok-4.5: Insufficient repeated visual locks on reappearances
  - grok-4: Early naming of narrator as PETER violates source withholding of name until final beat; multiple invented plot details and character actions not in book.
- **claude-opus-5**:
  - grok-4.5: Structurally incomplete/truncated ending that drops the prison-forgiveness climax
  - grok-4.5: Inconsistent physical descriptions of THE NARRATOR (thick vs thin shoulders, shifting ages)
  - grok-4.5: Vastly exceeds target runtime with dozens of padded scenes and unfinished headings

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | gpt-5.6-luna | gpt-5.6-terra | gemini-3.6-flash | gemini-3.1-pro-preview | grok-4.20-reasoning | claude-opus-5 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 8.5 | 7.9 | 7.4 | 7.8 | 7.7 | 6.2 | 4.0 |
| **grok-4** | 8.8 | 8.3 | 8.0 | 7.1 | 6.4 | 5.9 | 8.1 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay F because it delivers the fullest book-faithful arc with the most consistent character visual locks and the cleanest short-clip Action lines. Weakest: Screenplay C because it is structurally truncated, contradicts its own character descriptions, and massively overruns the 10-minute target.
- **grok-4:** Strongest: Screenplay F because it uses LITTLE BROTHER for consistent disambiguation, maintains highest fidelity without early name invention, and delivers tight visual action lines ideal for short clips. Weakest: Screenplay B because it reveals the narrator's name as PETER early, violating source withholding, and includes multiple invented plot events that break adaptation fidelity.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 89 (Avg `15.7` words/turn, Max `40` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 5 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Highest fidelity to the book’s essential spine and iconic lines while remaining tightly clip-directible; LITTLE BROTHER / YOUNG NICK / Adult NICK tokens plus repeated visual anchors (scars, Trans Am, fish pendant, blue eyes) stay rock-solid; short observable Action lines and natural book dialogue make it the most production-ready package. Sound beds are present but still sparse.
  - *grok-4:* Highest fidelity with LITTLE BROTHER disambiguation, tight visual action lines, and book-accurate dialogue; excellent short-clip structure.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a rule that every major emotional peak (crash, stabbing, prison goodbye) must interleave at least two distinct diegetic sound cues with the Action so the sound-design score cannot lag behind the visual beats.
  - *grok-4:* Add an explicit rule requiring every scene to contain only one primary location and time to prevent multi-location headings.

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 33
- **Dialogue Blocks:** 112 (Avg `12.9` words/turn, Max `45` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Strong full-arc coverage from childhood kickball through dreams, crash, stabbing, breakup and prison forgiveness with mostly book-faithful dialogue and short visual scenes suited to 5-10s clips. Minor deductions for occasional multi-beat compression and sparse explicit sound/music beds; character looks are locked early but young/adult re-specs are not always restated on reappearance.
  - *grok-4:* Strong fidelity and visual action with late name reveal; minor compression needed on some longer scenes.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require every scene that reintroduces a character after a time jump or age change to restate 2-3 locked visual traits (build/hair/scar/wardrobe) in the first Action line so reference images cannot drift.
  - *grok-4:* Add an explicit rule requiring every scene heading to be followed by at least one concrete, single-location visual action line before any dialogue or V.O.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 17
- **Dialogue Blocks:** 78 (Avg `11.6` words/turn, Max `38` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Tight 10-minute spine that hits meeting, fights, crash, stabbing and prison payoff with clean short scenes and usable THE NARRATOR token. Loses points for dropping several childhood/dream beats and thinner visual re-locking of supporting cast on reappearances; almost no music or layered sound beds.
  - *grok-4:* Strong coverage of core beats with consistent THE NARRATOR frame and visual action lines; minor pacing issues from longer scenes and limited sound cues.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate at least one concrete diegetic sound or background-music direction (e.g. ‘distant siren swells’, ‘low tense drone’) inside every scene’s Action so the sound-design dimension is never left empty.
  - *grok-4:* Add an explicit rule requiring every scene heading to be followed by at least one concrete, single-location visual action line before any dialogue or V.O.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 16
- **Dialogue Blocks:** 67 (Avg `14` words/turn, Max `54` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 4 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Clean frame-story structure, consistent age-disambiguated looks for Peter/Nick/Sionna, and clip-sized visual beats that track the crash–stabbing–prison arc without major invention. Dialogue stays close to source; sound design is only lightly indicated.
  - *grok-4:* Fidelity penalty for early name; otherwise strong visual action and dialogue but production-blocked.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require the closing frame return (when a frame is established) to contain one fresh observable micro-action plus a short sound bed rather than pure V.O. so the button remains filmable in a single 5-10s clip.
  - *grok-4:* Add an explicit rule that the narrator's name must never be used until the source text first reveals it, and no new named characters may be invented.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 16
- **Dialogue Blocks:** 81 (Avg `18.7` words/turn, Max `69` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 11 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Solid frame-to-flashback structure, book-accurate dialogue, and clear visual beats for the core confrontations and prison closer. Character looks are established early and mostly held; minor compression of middle act and almost no explicit music/sound direction keep it just below the top tier.
  - *grok-4:* Fidelity broken by early name reveal and added events; otherwise solid visuals but unusable as-is.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require an explicit one-line music or ambient-bed note (genre/intensity) immediately after every scene heading so scorers and downstream audio tools always have a usable cue.
  - *grok-4:* Add an explicit rule that the narrator's name must never be used until the source text first reveals it, and no new named characters may be invented.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 14
- **Dialogue Blocks:** 54 (Avg `13.4` words/turn, Max `40` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Hits the major plot turns in a compact package but the name-token drift breaks closed-cast parsing and visual descriptions are too thin for consistent AI reference images; several connective beats feel summarized rather than shown.
  - *grok-4:* Severe fidelity penalty for early invented name and added events; workable visuals but broken structure.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Enforce a single canonical ALL-CAPS token per character that must be identical in every cue, heading and Action reference; any spelling variant is a hard validation failure.
  - *grok-4:* Add an explicit rule that the narrator's name must never be used until the source text first reveals it, and no new named characters may be invented.

### 🎬 claude-opus-5
- **Scene Headings Count:** 56
- **Dialogue Blocks:** 390 (Avg `19.7` words/turn, Max `101` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG NARRATOR, YOUNG NICK`
- **Diagnostics & Warnings:**
  - ⚠️ Excessive scene count (56 scenes); high micro-scene density inflates video gen budget.
  - ⚠️ 58 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 2 age-qualified character header(s) (e.g. YOUNG NARRATOR, YOUNG NICK).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Contains rich book-faithful dialogue and many secondary beats but collapses under its own length, ends mid-scene without resolution, and repeatedly contradicts the protagonist’s locked look—rendering it unusable for reference-image pipelines or a 10-minute cut.
  - *grok-4:* Excellent on-camera frame with THE NARRATOR, consistent descriptions, and visual action; minor pacing compression needed for clip length.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add an explicit hard cap of 25–35 scene headings for novel-length sources and a final validation rule that the last scene before FADE OUT must complete the emotional/plot spine with no truncated Action or headings.
  - *grok-4:* Add an explicit rule capping any single scene at no more than four distinct visual beats to ensure short-clip compatibility.

