# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 13:19:43 UTC*  
*Source Story File: `The_Tell-Tale_Heart.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **claude-opus-5** | **93.7** | 98.5% | 90.0% | 14 pts | 1.0 |
| 🥈  | **gpt-5.6-sol** | **90.8** | 98.0% | 86.0% | 12 pts | 2.0 |
| 🥉  | **gpt-5.6-luna** | **87.9** | 97.5% | 82.0% | 7 pts | 4.5 |
| 4.  | **gpt-5.6-terra** | **87.0** | 97.5% | 80.0% | 7 pts | 4.5 |
| 5.  | **gemini-3.6-flash** | **82.9** | 97.5% | 73.0% | 7 pts | 4.5 |
| 6.  | **grok-4.20-reasoning** | **79.5** | 96.0% | 68.0% | 7 pts | 4.5 |
| 7.  | **gemini-3.1-pro-preview** | **75.7** | 97.0% | 62.0% | 2 pts | 7.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **claude-opus-5** | 100% | 100% | 70% | 9.2/10 | 9.2/10 | 9.2/10 | 8.5/10 | 9.0/10 | 9.2/10 |
| **gpt-5.6-sol** | 95% | 100% | 90% | 8.5/10 | 9.0/10 | 8.5/10 | 8.2/10 | 8.5/10 | 8.8/10 |
| **gpt-5.6-luna** | 100% | 100% | 50% | 8.5/10 | 7.8/10 | 8.0/10 | 7.5/10 | 8.8/10 | 8.5/10 |
| **gpt-5.6-terra** | 95% | 100% | 80% | 7.2/10 | 9.0/10 | 7.5/10 | 7.8/10 | 8.5/10 | 8.0/10 |
| **gemini-3.6-flash** | 100% | 100% | 50% | 7.0/10 | 7.5/10 | 7.2/10 | 7.0/10 | 7.0/10 | 8.2/10 |
| **grok-4.20-reasoning** | 95% | 100% | 50% | 5.8/10 | 7.2/10 | 7.0/10 | 6.8/10 | 6.5/10 | 8.0/10 |
| **gemini-3.1-pro-preview** | 95% | 100% | 70% | 5.5/10 | 5.5/10 | 6.2/10 | 6.0/10 | 6.0/10 | 7.5/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gemini-3.6-flash**:
  - grok-4.5: Multiple invented multi-line exchanges for officers and narrator that exceed the summarized-chat cap
  - grok-4.5: Under-specified and potentially anachronistic officer visuals (e.g. CHEN) without consistent re-locking
- **grok-4.20-reasoning**:
  - grok-4.5: Major invented plot event: officers seize the narrator and tear up the planks to reveal the body—absent from the source’s ending
  - grok-4.5: Extensive invented officer and narrator dialogue
- **gemini-3.1-pro-preview**:
  - grok-4.5: Inconsistent character-token casing (Narrator vs NARRATOR) creating unstable closed-cast identity
  - grok-4.5: Extensive invented multi-line dialogue for both narrator and officers far beyond source
  - grok-4.5: Supporting cast given only minimal non-restated visuals
  - grok-4: Inconsistent capitalization of character names (Narrator vs NARRATOR) violating ALL CAPS closed-cast rule

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | claude-opus-5 | gpt-5.6-sol | gpt-5.6-luna | gpt-5.6-terra | gemini-3.6-flash | grok-4.20-reasoning | gemini-3.1-pro-preview |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 9.3 | 8.5 | 8.3 | 8.0 | 6.4 | 5.4 | 5.1 |
| **grok-4** | 8.8 | 8.7 | 8.0 | 8.0 | 8.2 | 8.3 | 7.2 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay C because it alone combines near-perfect source fidelity, zero invented plot, rigorously locked and restated character visuals, and fully atomic 5-10s-clip directibility with dense diegetic sound. Weakest: Screenplay E because it fabricates a major post-confession arrest-and-body-reveal sequence absent from Poe, a disqualifying fidelity violation that renders the draft unusable.
- **grok-4:** Strongest: Screenplay C because its detailed, concrete action lines and consistent use of THE NARRATOR and THE OLD MAN make it highly directible for short AI clips. Weakest: Screenplay D because inconsistent character name capitalization breaks the closed cast requirement and reduces casting clarity.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 claude-opus-5
- **Scene Headings Count:** 13
- **Dialogue Blocks:** 44 (Avg `28.8` words/turn, Max `53` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ 12 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE OLD MAN).
- **Judge Rationale:**
  - *grok-4.5:* Highest-fidelity adaptation: exact iconic lines, no invented plot events or improper names, closed cast with richly distinct and consistently restated looks for THE NARRATOR, THE OLD MAN and each officer. Every action line is a single camera-observable micro-beat ideal for 5-10s AI clips; prison-cell frame is used exactly as the shared prompt prefers for turns and button. Sound design is exemplary—diegetic, multi-layered on peaks, present in every scene. Minor dramatization of the old man’s self-comfort lines is the only quibble and stays within book spirit.
  - *grok-4:* Highly concrete action lines, consistent THE NARRATOR and THE OLD MAN descriptions, and precise sound cues make it exceptionally filmable in short clips.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a rule that any internal monologue the book attributes to a non-narrating character may be externalized as at most one whispered self-address line, clearly parentheticalized, to avoid accidental dialogue invention.
  - *grok-4:* Add an explicit instruction to restate two to three locked visual traits for every recurring character on their first re-entry after any time gap or location change.

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 35 (Avg `23.5` words/turn, Max `42` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
- **Judge Rationale:**
  - *grok-4.5:* Strong frame (interrogation room + pocket-watch motif) that visualizes the heartbeat without inventing plot. Character looks are distinct, period-accurate and restated on re-entry for NARRATOR, OLD MAN and all three officers. Actions stay concrete and clip-friendly; pacing uses frame cutaways at major turns per prompt. Minor officer intro lines are the only invented dialogue. Sound design is excellent with the watch as recurring diegetic bed. Fully production-ready.
  - *grok-4:* Excellent fidelity, consistent NARRATOR and OLD MAN visuals, and precise sound cues with strong subtext in dialogue.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Cap any single officer or searcher speaking turn at one short sentence that closely paraphrases the book’s report of their purpose; forbid multi-speaker invented exchanges.
  - *grok-4:* Add an instruction requiring at least one explicit diegetic sound cue in every scene, placed on its own line immediately after the relevant action.

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 7
- **Dialogue Blocks:** 43 (Avg `33.8` words/turn, Max `59` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ High average dialogue length (33.8 words/turn); speech beats risk clip overrun.
  - ⚠️ 19 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE OLD MAN).
- **Judge Rationale:**
  - *grok-4.5:* Near-exact book VO and beats with no invented plot or improper character names; officers receive only generic coat descriptions without individual build/hair locks or re-entry restatements, slightly weakening casting clarity. Actions are mostly concrete and clip-scalable though some seven-night compression and long VO blocks reduce atomicity. Sound cues are dense and progressive on the heartbeat peaks. Strong overall fidelity and audio make it greenlightable with minor polish.
  - *grok-4:* Good source fidelity and frame usage, but some scene headings vary slightly and pacing has minor repetition in night sequences.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require every supporting speaker (officers etc.) to receive 2-3 distinct, restatable visual traits (build, hair/facial hair, age markers, signature prop) at first entrance and on every re-entry after a gap.
  - *grok-4:* Add a rule that every recurring location must use the exact same scene heading wording on every return, with no additions or omissions.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 39 (Avg `25.6` words/turn, Max `43` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 4 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *grok-4.5:* Solid frame structure and visual locking for NARRATOR and officers, but inventing the proper name MR. VALE for the unnamed old man is an unnecessary fidelity hit; otherwise covers all major beats with good book diction. Directibility is strong via concrete lantern/eye micro-beats and chamber cutaways. Pacing benefits from deliberate frame returns. Minor invented connective tissue keeps it from the top tier but still production-viable.
  - *grok-4:* Strong source coverage and consistent character visuals for NARRATOR and MR. VALE, but dissolves and multiple short scenes reduce directibility for 5-10s clips.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Clarify that characters given no proper name in the source must use stable descriptive tokens (THE OLD MAN, THE OFFICER) rather than newly invented proper names unless the book itself supplies one.
  - *grok-4:* Add an explicit rule forbidding dissolves or montages within a single scene heading and requiring every scene to contain at most three distinct visual beats.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 8
- **Dialogue Blocks:** 21 (Avg `32.6` words/turn, Max `111` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ High average dialogue length (32.6 words/turn); speech beats risk clip overrun.
  - ⚠️ 8 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Covers the spine but freely invents officer banter and morning dialogue far beyond the book’s summarized ‘chatted of familiar things,’ triggering a fidelity penalty. Character looks are present at intro yet inconsistently restated and thin for supporting cast. Several action blocks cram the kill and concealment into non-atomic paragraphs hard to slice into 5-10s clips. Sound/music is a relative strength with clear heartbeat escalation. Not greenlightable without dialogue purge.
  - *grok-4:* Solid coverage and natural pacing with good officer naming, but occasional long action paragraphs limit short-clip suitability.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Strengthen the summarized-exchange rule to an absolute default of zero invented spoken lines for searchers/police when the book only narrates that they conversed; force pure Action coverage.
  - *grok-4:* Add a rule capping every action line at two sentences maximum to enforce single-clip filmability.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 7
- **Dialogue Blocks:** 23 (Avg `47` words/turn, Max `120` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ High average dialogue length (47 words/turn); speech beats risk clip overrun.
  - ⚠️ 13 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Disqualifying fidelity breach by extending past Poe’s final confession into an unstated arrest-and-reveal sequence. Officers lack visual descriptions entirely, weakening cast locks. Directibility suffers from crammed peak actions and long unbroken VO. Dialogue mixes faithful opening with modernized inventions. Sound is adequate but cannot rescue the structural invention. Unusable as-is.
  - *grok-4:* Excellent fidelity and sound integration with clear frame room, though some V.O. blocks are lengthy for short clips.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate that the screenplay’s closing narrative beat must be identical to the source’s final event; explicitly forbid appending unstated aftermath (arrests, trials, body reveals) after the book’s last line.
  - *grok-4:* Add a rule that any V.O. passage exceeding two sentences must be interleaved with at least one concrete visual or sound micro-beat before continuing.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 26 (Avg `24.8` words/turn, Max `43` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 8 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Heavy invention of police-scene banter and narrator responses destroys dialogue authenticity and fidelity. Action lines frequently use lowercase ‘Narrator,’ breaking the ALL-CAPS closed-cast rule. Officer descriptions are thin and never restated. Peak kill is somewhat visual but many beats remain summary-like and hard to clip. Sound/music provides partial salvage yet cannot overcome cast and invention defects. Unusable without rewrite.
  - *grok-4:* Format violations in character cues reduce clarity; some actions cram multiple beats into one heading.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Make identical ALL-CAPS spelling of every character token in cues, Action lines and headings a hard validation failure; any lowercase or variant form must be rejected before output.
  - *grok-4:* Add a hard rule that every character cue must appear in ALL CAPS on its own line with no exceptions or lowercase variants anywhere in the screenplay.

