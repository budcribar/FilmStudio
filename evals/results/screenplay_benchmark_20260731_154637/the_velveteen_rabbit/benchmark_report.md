# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 21:50:52 UTC*  
*Source Story File: `The_Velveteen_Rabbit.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **92.1** | 99.0% | 88.0% | 4 pts | 1.0 |
| 🥈  | **grok-4.5** | **85.9** | 98.5% | 78.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 100% | 100% | 80% | 8.7/10 | 9.3/10 | 8.4/10 | 8.4/10 | 8.4/10 | 9.4/10 |
| **grok-4.5** | 100% | 100% | 70% | 8.6/10 | 6.2/10 | 7.4/10 | 7.7/10 | 8.0/10 | 8.6/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - gpt-5.6-sol: Closed-cast/reference-lock failure: two on-screen wild rabbits are not individually identified, yet dialogue is assigned to one ambiguous WILD RABBIT token; THE DOCTOR also remains a generic speaking token rather than a uniquely named cast key.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-sol** | **8.9** *(self)* | 7.7 |
| **grok-4.5** | 8.6 | **7.8** *(self)* |

### 🗣️ Judge Summary Notes
- **gpt-5.6-sol:** Strongest: Screenplay B because it combines near-complete source coverage with exceptional cast locking, cleaner escalation, and a coherent musical strategy. Weakest: Screenplay A because its otherwise faithful adaptation contains an unresolved wild-rabbit identity collision and generic speaking-role tokens that prevent reliable reference-image assignment.
- **grok-4.5:** Strongest: Screenplay A because it delivers the tightest cast visual locks, re-entry restatements, clip-ready micro-beats, and densest sound/music scoring while staying faithful to the book. Weakest: Screenplay B because character appearance drifts across scenes and the wild rabbits lack distinct tokens/descriptions, which directly undermines AI reference-image consistency.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 12
- **Dialogue Blocks:** 52 (Avg `17.2` words/turn, Max `52` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 4 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 3 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-sol (self):* This version covers virtually the complete emotional and narrative spine, including the replacement bunny and the Boy's temporary removal to another bedroom. Hazel, Clover, Doctor Lewis, Nana, the Boy, the Fairy, and the Rabbit receive unusually strong, stable casting descriptions, with effective re-entry reminders and clearly tracked wear or recovery. The assigned names identify source-present roles rather than adding new characters, although minor fidelity slips include rainwater replacing garden dew and a golden flower apparently relocating to the wood glade. The action is concrete and animation-friendly, with especially strong transformation and memory imagery, but a few montage paragraphs still contain several clip-sized actions, and some source-faithful speeches exceed the 5–10-second delivery window. The structure escalates cleanly from neglect through love, rejection, threatened destruction, and liberation, while the recurring music-box motif, strategic musical cutoff, and detailed diegetic beds provide the strongest sound arc of the two drafts.
  - *grok-4.5:* High-fidelity adaptation that hits every major book beat (Christmas arrival, Skin Horse lesson, Nana bedtime handoff, Boy declaring him Real, wild-rabbit rejection, scarlet-fever vigil, sack/bonfire crisis, Fairy transformation, spring reunion) with book-close dialogue and only light closed-cast naming (Hazel/Clover, Doctor Lewis). Character looks are locked and restated on re-entry (Boy’s chestnut bowl-cut, Nana’s iron-gray bun/black dress/white apron, Rabbit’s progressive shabbiness, Fairy’s pearl-and-dewdrop dress). Action is concrete and clip-sized; every scene carries usable (SOUND)/(MUSIC) cues, denser at peaks. Minor deductions: multi-night compressions inside single headings slightly blur 5–10s clip boundaries; invented wild-rabbit names are small fidelity nits, not plot invention.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol (self):* Add a hard beat-granularity rule that every action paragraph and dialogue or V.O. turn must fit one 5–10-second clip; cap spoken turns at roughly 20 words and split longer source passages or serial montage actions with separate observable micro-beats.
  - *grok-4.5:* Require that when elapsed time is compressed inside one scene heading, each temporal jump be marked by an explicit one-line visual time-shift beat (light change, prop state, weather, or wardrobe wear) so downstream 5–10s clips never span multiple indistinct days.

### 🎬 grok-4.5
- **Scene Headings Count:** 15
- **Dialogue Blocks:** 48 (Avg `18.2` words/turn, Max `87` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 7 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-sol:* This is a highly faithful adaptation that preserves the Christmas neglect, Skin Horse lesson, the Boy's declaration, the wild-rabbit encounter, scarlet-fever crisis, threatened bonfire, Fairy transformation, and final recognition. It does omit the replacement-bunny bedroom beat and invents a brief Fairy-scene response for the Rabbit, but neither change damages the central arc. Visual progression for the Velveteen Rabbit is clear, while casting clarity is substantially weaker: the two wild rabbits are indistinguishable, the speaking rabbit lacks a unique identity, the Doctor is generic, and the Boy and Nana are not consistently re-grounded after elapsed-time transitions. Most action is filmable, but several montage paragraphs combine multiple actions or time changes, and the longest Skin Horse and Fairy speeches cannot fit a single 5–10-second clip without subdivision. Pacing remains emotionally coherent, and the layered ambient cues, peak sound beats, and recurring musical themes are exceptionally effective.
  - *grok-4.5 (self):* Equally faithful to the book’s spine and iconic lines, with slightly less invented naming than A and solid Skin Horse / Fairy exchanges. Weaknesses concentrate in cast lock and directibility: Boy is only “fair hair, about seven” then later “a little taller” without restating traits; Nana’s look is thin on re-entry; two wild rabbits share a single WILD RABBIT cue with no distinct builds/markings; several Action lines summarize spans (“Months of soft light and dust motes”) rather than giving one observable moment per beat. Sound cues are present and adequate but thinner at emotional peaks than A. Still greenlightable, but needs a cast-consistency pass before AI clip production.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol:* Require a silent preflight cast audit before output: every speaking role must have one unique cast token, every simultaneously present same-species or same-role character must be individually distinguishable, and all required re-entry traits must be verified before the Fountain draft is emitted.
  - *grok-4.5 (self):* Mandate that every on-screen speaking individual (including animals in a group) receive a unique ALL-CAPS token plus 2–3 locked visual traits at first appearance, and that those same traits be restated in the first Action line of every later scene after any heading break.

