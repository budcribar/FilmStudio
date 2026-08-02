# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 21:50:45 UTC*  
*Source Story File: `The_Velveteen_Rabbit.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **88.9** | 99.0% | 82.0% | 3 pts | 1.5 |
| 🥈  | **grok-4.5** | **87.2** | 98.5% | 80.0% | 3 pts | 1.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 100% | 100% | 80% | 5.8/10 | 9.0/10 | 8.5/10 | 8.0/10 | 8.8/10 | 9.2/10 |
| **grok-4.5** | 100% | 100% | 70% | 8.8/10 | 6.8/10 | 8.0/10 | 7.5/10 | 8.2/10 | 8.5/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-sol**:
  - gpt-5.6-sol: Invents the personal identities DOCTOR LEWIS, HAZEL, and CLOVER for source characters who are unnamed, with HAZEL and CLOVER also receiving unsupported recurring identities and characterization.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-sol** | **7.8** *(self)* | 7.9 |
| **grok-4.5** | 8.7 | **8.0** *(self)* |

### 🗣️ Judge Summary Notes
- **gpt-5.6-sol:** Strongest: Screenplay A because it preserves the complete source arc without unsupported named identities and remains highly filmable despite weaker cast-lock detail and oversized dialogue turns. Weakest: Screenplay B because its otherwise superior casting, visual construction, pacing, and audio design are undercut by the severe fidelity violation of inventing DOCTOR LEWIS, HAZEL, and CLOVER.
- **grok-4.5:** Strongest: Screenplay A because it pairs near-book fidelity with the clearest per-character visual locks, densest usable sound/music direction, and the most consistently clip-sized action. Weakest: Screenplay B because shared WILD RABBIT cueing and thinner re-entry descriptions undermine casting stability even though the adaptation itself is faithful.

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
  - *gpt-5.6-sol (self):* Apart from its naming changes, this is a comprehensive and emotionally accurate adaptation that includes even the replacement-bunny contrast omitted by Screenplay A. Under the stated calibration, however, the invented personal identities DOCTOR LEWIS, HAZEL, and CLOVER require a severe fidelity penalty; making CLOVER the returning brown rabbit also asserts continuity the book never establishes. Visually, it is the stronger cast-lock draft: the Boy, Nana, Rabbit, Skin Horse, Doctor, Fairy, Hazel, and Clover receive distinctive traits that are generally repeated with excellent consistency. Its compact twelve-scene structure, observable actions, and animation-friendly transformations are highly directible, though montage paragraphs and several long speeches still exceed a single short clip. Dialogue largely retains the book's wording, while the recurring music-box theme, strategic musical cutoffs, layered ambience, and transformation soundscape form the most sophisticated audio plan of the two drafts.
  - *grok-4.5:* Excellent full-arc fidelity with near-book dialogue and all major beats (Skin Horse lesson, Real declaration, wild-rabbit rejection, scarlet-fever discard, fairy transformation, spring reunion). Minor fidelity ding only for assigning invented proper names (HAZEL, CLOVER, DOCTOR LEWIS) to unnamed book roles. Character looks are locked and restated on re-entry (Boy’s chestnut bowl-cut, Nana’s iron-gray bun/black dress/white apron, Rabbit’s progressive shabbiness, distinct wild rabbits). Action lines are concrete and clip-sized; sound/music cues are dense and scene-specific, including dual cues at emotional peaks. Pacing is strong but a few scenes compress multi-night/week spans into summary blocks that blur 5–10s clip boundaries.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol (self):* Change the supporting-speaker rule so that when the source supplies no personal name, the screenplay must use a stable source-grounded role or descriptive cue rather than inventing one; distinguish multiple unnamed characters through source-supported visual traits and non-ordinal cue tokens.
  - *grok-4.5:* Require that any intra-scene time-lapse (nights passing, weeks later, montage of play) be rendered as 2–3 discrete, single-moment Action micro-beats each filmable as one 5–10s clip, separated by an explicit visual or (SOUND:) marker, rather than one summary sentence covering a long duration.

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
  - *gpt-5.6-sol:* This draft faithfully preserves nearly the complete narrative and thematic spine: Christmas abandonment, the Skin Horse's lesson, the Boy's love, the garden declaration, the wild-rabbit encounter, illness, threatened burning, the Fairy's intervention, transformation, and final reunion. Its omissions, chiefly the replacement bunny and Nana's observation of the Rabbit's knowing expression, are minor, although two brief invented lines slightly dilute dialogue fidelity. The central casting weakness is that the two wild rabbits are visually indistinguishable while only one generic WILD RABBIT cue speaks; the Boy, Nana, and Doctor also receive less consistent re-entry description than the Rabbit. Most action is concrete and filmable, but several montage-like paragraphs span multiple actions or times of day, and the Skin Horse and Fairy have dialogue turns far too long for one 5–10-second clip. The emotional progression remains clear, and the detailed ambient sound, peak-specific effects, and recurring musical idea provide a strong production-ready audio plan.
  - *grok-4.5 (self):* Slightly fuller book coverage than A on the Skin Horse monologue and inclusion of Timothy by name; dialogue stays close to source with only light connective invention. Directibility is solid and scenes are grounded, but several locations pack many story beats with thinner visual micro-breaks. Character disambiguation is the clear weak spot: both wild rabbits share the single cue WILD RABBIT, Nana/Doctor/Boy re-entry traits are under-specified, and wardrobe locks are thinner than A’s, risking reference-image drift. Sound cues are present in every scene and generally useful but less layered at peaks than A. Pacing is workable yet a bit front-loaded and compressed through the illness-to-bonfire stretch.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol:* Add a hard maximum of roughly 20–25 spoken words per dialogue or V.O. turn; preserve longer source speeches by splitting them with concrete visual or sound micro-beats and repeated character cues rather than paraphrasing them.
  - *grok-4.5 (self):* Mandate unique ALL-CAPS tokens for every distinct speaking individual of the same type (never one shared cue for two rabbits/guards/etc.) and require restating 2–3 locked visual traits in the first Action line whenever a character reappears after a time jump or location change.

