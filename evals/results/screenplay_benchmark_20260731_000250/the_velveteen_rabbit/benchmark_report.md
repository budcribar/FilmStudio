# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 06:06:41 UTC*  
*Source Story File: `The_Velveteen_Rabbit.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **90.4** | 94.8% | 87.0% | 14 pts | 1.0 |
| 🥈  | **gpt-5.6-luna** | **87.8** | 94.5% | 83.0% | 11 pts | 2.5 |
| 🥉  | **gpt-5.6-terra** | **87.2** | 94.8% | 82.0% | 11 pts | 2.5 |
| 4.  | **gemini-3.1-pro-preview** | **81.1** | 94.8% | 72.0% | 8 pts | 4.0 |
| 5.  | **gemini-3.6-flash** | **77.7** | 93.2% | 67.0% | 4 pts | 6.0 |
| 6.  | **claude-opus-5** | **77.2** | 93.0% | 67.0% | 4 pts | 6.0 |
| 7.  | **grok-4.20-reasoning** | **75.6** | 94.8% | 63.0% | 4 pts | 6.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 100% | 100% | 85% | 9.0/10 | 9.5/10 | 9.0/10 | 8.5/10 | 9.0/10 | 7.5/10 |
| **gpt-5.6-luna** | 100% | 100% | 80% | 9.0/10 | 8.5/10 | 8.5/10 | 8.2/10 | 9.0/10 | 6.8/10 |
| **gpt-5.6-terra** | 100% | 100% | 85% | 8.2/10 | 9.0/10 | 8.5/10 | 8.0/10 | 8.8/10 | 6.8/10 |
| **gemini-3.1-pro-preview** | 100% | 100% | 85% | 7.0/10 | 7.8/10 | 7.8/10 | 7.8/10 | 6.8/10 | 6.2/10 |
| **gemini-3.6-flash** | 95% | 100% | 85% | 6.0/10 | 7.0/10 | 7.2/10 | 6.5/10 | 7.8/10 | 6.0/10 |
| **claude-opus-5** | 95% | 100% | 80% | 3.0/10 | 8.8/10 | 8.0/10 | 5.0/10 | 8.5/10 | 6.8/10 |
| **grok-4.20-reasoning** | 100% | 100% | 85% | 5.2/10 | 7.5/10 | 7.5/10 | 7.2/10 | 4.0/10 | 6.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gemini-3.6-flash**:
  - grok-4.5: Drops essential opening beats (Christmas stocking love-then-forget; mechanical-toy snubbing and Skin Horse setup as lived scenes).
  - grok-4.5: Misstates defining prop detail (Rabbit given glass eyes instead of boot-button eyes).
- **claude-opus-5**:
  - grok-4.5: Screenplay truncates mid-sentence during the recovery beat and omits the entire third act (sack/fowl-house, tear/flower, Fairy transformation, Real Rabbit dance, final spring recognition).
  - grok-4: Screenplay is truncated mid-sentence and does not complete the story arc.
- **grok-4.20-reasoning**:
  - grok-4.5: Critical dialogue misattribution at the climax: RABBIT speaks the Fairy's self-introduction monologue ('I am the nursery magic Fairy...').
  - grok-4: Dialogue lines swapped between Fairy and Rabbit in the transformation scene, violating book wording fidelity.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | gpt-5.6-luna | gpt-5.6-terra | gemini-3.1-pro-preview | gemini-3.6-flash | claude-opus-5 | grok-4.20-reasoning |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 8.6 | 8.2 | 7.9 | 6.5 | 5.7 | 6.2 | 6.2 |
| **grok-4** | 8.9 | 8.5 | 8.5 | 7.9 | 7.8 | 7.1 | 6.4 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay F because it uniquely combines full-arc fidelity, locked recurring cast looks, and the most clip-ready concrete action writing without deal-breaking errors. Weakest: Screenplay D because it gutted the Christmas/cupboard insignificance setup and misstated the Rabbit's eyes, leaving a rushed hollow spine rather than the book's earned transformation.
- **grok-4:** Strongest: Screenplay F because it achieves the highest fidelity, consistent character visuals, and directible short-clip actions with minimal invention. Weakest: Screenplay C because it is truncated mid-sentence and fails to complete the required story arc.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 13
- **Dialogue Blocks:** 46 (Avg `15.4` words/turn, Max `52` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Strongest package: full book arc, near-verbatim iconic dialogue, and the best locked cast bible in Action (Rabbit markings, Nana iron-gray hair/charcoal dress/white apron, Boy chestnut hair, Bracken/Fern, Dr. Harper silver whiskers/frock coat, Fairy scale). Scenes are single-purpose and break cleanly into 5–10s clips with concrete observable actions (tear to flower, hind-toe scratch reveal, etc.). Slight naming of wild rabbits/doctor is cast-hygiene, not plot invention. Richer ambient writing (rain, breeze, night-light) lifts sound above peers though still light on explicit score cues.
  - *grok-4:* Exceptional fidelity, consistent visuals, filmable actions, and natural pacing across short clips with accurate book lines.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require a recurring optional MUSIC cue line (or parenthetical sound bed) at major emotional turns—setup, crisis, transformation, coda—so scoring is directed consistently across all drafts, not left to ambient verbs alone.
  - *grok-4:* Add a rule requiring at least one concrete sound or music direction in every scene heading or first action line.

### 🎬 gpt-5.6-luna
- **Scene Headings Count:** 16
- **Dialogue Blocks:** 0 (Avg `0` words/turn, Max `0` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Near-complete beat coverage with book-faithful dialogue (Skin Horse sermon, Boy's REAL declaration, doctor burn order, Fairy lines, final recognition). Visual identity for the Rabbit and Skin Horse is locked early; Boy/Nana/Doctor/Fairy are thinner on recurring wardrobe. Scenes are clip-sized and filmable with clear single-location purpose. Ambient sound exists (tissue, dew, footsteps) but there are no explicit music beds or scored emotional cues, capping the sound dimension.
  - *grok-4:* Strong source accuracy and dialogue fidelity; slightly longer V.O. blocks and fewer explicit sound cues than top performer.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Require every scene to end with a one-line audio direction (ambient bed + optional music cue tied to the scene's emotional turn) so clip-level sound design is explicit, not merely implied by action verbs.
  - *grok-4:* Add a rule that any V.O. must be paired with a distinct camera-observable action on the same or adjacent line.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 49 (Avg `16.4` words/turn, Max `40` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Full arc with strong closed-cast naming (Edward, Moss, Lark, Dr. Harris, Hobbs) and excellent locked looks (blue wool suit, charcoal dress/white cap, distinct wild-rabbit colors). Dialogue stays close to the book. Minor fidelity nicks: Boy given a proper name the source withholds, gardener staged as a named presence, and the final recognition is oddly split as EDWARD (V.O.) plus Narrator. Sound remains ambient-only. Fountain glitch on '> FADE OUT.' is cosmetic.
  - *grok-4:* Strong source coverage with accurate beats and book dialogue; consistent character descriptions and visual actions; minor long V.O. blocks reduce clip pacing slightly.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a rule that internal thoughts quoted from the book must stay on NARRATOR (V.O.) or silent Action reaction—never reassigned as the thinking character's own V.O.—unless the book marks them as spoken aloud.
  - *grok-4:* Add an explicit rule that every V.O. passage longer than two sentences must be interleaved with at least one distinct visual or sound micro-beat.

### 🎬 gemini-3.1-pro-preview
- **Scene Headings Count:** 13
- **Dialogue Blocks:** 52 (Avg `15.5` words/turn, Max `58` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Complete and schedulable, with most spine beats present and many book lines intact. Fidelity and dialogue suffer from invented on-camera speeches for MODEL BOAT and TIMOTHY (book only narrates their airs) plus Nana's extra plea 'But he loves it so.' Character re-description is thinner than A/B/F; wild rabbits split as FURRY RABBIT/WILD RABBIT without stable visual locks. Usable draft that needs dialogue pruning and look pass, not a rewrite of structure.
  - *grok-4:* Solid coverage and visual actions; minor invented dialogue line in doctor scene slightly lowers fidelity.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Strengthen the summarized-exchange rule with an explicit ban: characters the book only describes as boasting/pretending/referring, without quoted speech, get zero invented dialogue lines—carry that beat in Action or NARRATOR only.
  - *grok-4:* Add an explicit instruction that no new dialogue may be invented for summarized exchanges unless Action alone cannot carry the beat.

### 🎬 gemini-3.6-flash
- **Scene Headings Count:** 9
- **Dialogue Blocks:** 38 (Avg `15.1` words/turn, Max `43` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Delivers a skeletal spine (REAL talk, bedtime, garden, wild rabbits, fever/burn, Fairy, transformation, coda) but gutting the Christmas/cupboard insignificance setup removes the thematic engine. Opens already at the fender conversation. Character looks are thin and drift; eyes error breaks continuity locks. Pacing feels rushed and montage-heavy; almost no scored sound thinking.
  - *grok-4:* Good fidelity and book dialogue; some scenes cram multiple actions, and character names vary slightly in consistency.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Mandate an explicit opening-coverage rule: the first 2–4 scenes must dramatize the source's establishing status quo (introduction of protagonist in their starting world and the social order that rejects or defines them) before jumping to the first major thematic dialogue.
  - *grok-4:* Add a rule requiring every recurring character to use the exact same ALL-CAPS token in every appearance without variation.

### 🎬 claude-opus-5
- **Scene Headings Count:** 10
- **Dialogue Blocks:** 47 (Avg `17.8` words/turn, Max `52` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 4 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* What exists is richly directed—medium note, practical-toy rules, precise wardrobe, Thistle/Bramble, Doctor Hallam—with excellent book dialogue and observable micro-beats. Catastrophic failure is incompleteness: output dies at 'The Boy sits up against the bol' and never reaches the story's climax or resolution, so fidelity and pacing collapse regardless of craft quality in acts one–two.
  - *grok-4:* Incomplete draft prevents full evaluation; strong visual style and fidelity in provided sections but unusable as-is.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a hard completion check: if the source contains a full arc, the Fountain must end with the final resolution beat plus FADE OUT/THE END; if nearing length limits, compress earlier connective scenes rather than stopping mid-scene or omitting the ending.
  - *grok-4:* Add a rule that the final scene must always end with a complete closing beat or FADE OUT before any output cutoff.

### 🎬 grok-4.20-reasoning
- **Scene Headings Count:** 9
- **Dialogue Blocks:** 38 (Avg `16.5` words/turn, Max `87` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *grok-4.5:* Otherwise a competent full-arc cut with recognizable book lines and workable clip structure. The Fairy beat is unusable as written—the Rabbit answers 'don't you know who I am?' by claiming to be the Fairy—destroying the emotional peak and any downstream shot that depends on correct speaker identity. Character tokens (RABBIT vs fuller names) and looks are only moderately locked on reentry.
  - *grok-4:* Major attribution error breaks fidelity; otherwise covers arc but paraphrases some lines and lacks consistent sound prompts.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Add a speaker-fidelity check: after drafting, verify each dialogue block's speaker against the source speaker for that line; iconic identity-reveal or theme speeches must not be reassigned to another character under any compression.
  - *grok-4:* Add a rule requiring every dialogue block to be immediately preceded by its correct ALL-CAPS character cue with no speaker misattribution allowed.

