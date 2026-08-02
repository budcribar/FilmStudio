# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 04:57:45 UTC*  
*Source Story File: `The_Velveteen_Rabbit.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-terra** | **87.5** | 100.0% | 79.0% | 4 pts | 1.0 |
| 🥈  | **grok-4.5** | **82.8** | 100.0% | 71.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-terra** | 100% | 100% | 100% | 8.5/10 | 6.0/10 | 8.2/10 | 8.0/10 | 8.5/10 | 8.2/10 |
| **grok-4.5** | 100% | 100% | 100% | 5.0/10 | 6.0/10 | 7.5/10 | 7.8/10 | 7.8/10 | 8.8/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-terra**:
  - gpt-5.6-terra: The protagonist is introduced and described as THE BOY but speaks as BOY; this violates the required single stable character token.
  - gpt-5.6-terra: The NURSERY MAGIC FAIRY later speaks as FAIRY, creating a second cast identity for the same recurring character.
  - gpt-5.6-terra: Two separately acting wild rabbits share the singular WILD RABBIT cue, which is insufficiently disambiguated for cast locking and clip generation.
- **grok-4.5**:
  - gpt-5.6-terra: Invents the named wild rabbit THISTLE, a source-absent character; under the stated fidelity rule, this is a major invention.
  - gpt-5.6-terra: Uses WILD RABBIT as a shared dialogue token for distinct rabbits, then introduces THISTLE for one of them, creating unstable cast identity for downstream portrait and clip generation.
  - grok-4.5: Unintroduced character token THISTLE speaks mid-scene with no prior visual establishment or cast lock
  - grok-4.5: Location heading drift breaks place stability (NURSERY / BOY'S NURSERY / BOY'S BEDROOM / BOY'S BED / DIFFERENT BEDROOM)

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-terra | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | **7.4** *(self)* | 6.8 |
| **grok-4.5** | 8.4 | **7.5** *(self)* |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-terra rated its own screenplay 7.4/10 vs. a 8.4/10 average from 1 other judge(s) (-1.0) — notably harsher on itself than peers were.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay B because it is the most faithful, efficiently structured, and clip-directable adaptation without inventing source events or characters, though it needs cast-token normalization. Weakest: Screenplay A because its otherwise excellent coverage and atmosphere are disqualified by the invented named character THISTLE and unstable wild-rabbit identity handling.
- **grok-4.5:** Strongest: Screenplay A because it preserves book dialogue and the full beat spine without invented cast tokens and stays production-clean for short-clip filming. Weakest: Screenplay B because the unexplained THISTLE cue and drifting location headings are closed-cast/place-stability failures that block greenlight despite stronger music cues.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 52 (Avg `14.1` words/turn, Max `27` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. BOY).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* This is the stronger adaptation overall because it stays faithful to the source without adding plot or named characters, cleanly compresses the story into a manageable sequence of major turns, and keeps most action lines short, visible, and clip-oriented. Its treatment of the illness, disposal threat, magical intervention, and closing recognition is especially efficient. Sound cues are present in every scene and usually combine credible ambience with appropriately restrained musical escalation. The principal weakness is not story but production identity control: THE BOY/BOY and NURSERY MAGIC FAIRY/FAIRY are explicit token inconsistencies that can generate duplicate portrait references, while the two wild rabbits cannot reliably be separated. It also omits or condenses some useful source texture, including the broader toy-cupboard social dynamic and portions of the Boy’s prolonged attachment, but these are reasonable runtime cuts rather than major fidelity failures.
  - *grok-4.5:* High-fidelity compression of the full arc with book-true dialogue and no invented named characters or plot. Scenes are clean single-location units with concrete, clip-length actions and consistent (SOUND)/(MUSIC) coverage. Weakest area is casting lock: the Boy enters with almost no build/face/wardrobe traits, the Doctor is undescribed, and re-entries rest on thin reminders, which will drift portrait consistency. Pacing is tight and filmable for a ~10-minute cut without padding.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a final cast-token lint rule requiring the writer to compare every ALL-CAPS dialogue cue and character reference against a single first-appearance token list, automatically replacing aliases, abbreviations, and collective cues used by separately acting speakers before output.
  - *grok-4.5:* Require that every speaking character’s first Action appearance lock 2–3 stable visual traits (age/build/hair/wardrobe/colors) and that any later re-entry after a time jump restate at least two of those same traits verbatim—apply this to children and one-scene professionals as well as leads.

### 🎬 grok-4.5
- **Scene Headings Count:** 16
- **Dialogue Blocks:** 55 (Avg `13.9` words/turn, Max `35` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the fuller and more emotionally textured adaptation, preserving nearly every essential turn: Christmas introduction, Skin Horse’s philosophy, bedtime attachment, garden declaration of reality, wild-rabbit rejection, scarlet-fever separation, Fairy transformation, and final reunion image. Its dialogue is exceptionally faithful to the book’s cadence, and its soundscape is consistently specific, varied, and emotionally supportive. The visual writing is generally concrete and highly filmable. However, the invented name THISTLE is an explicit source-fidelity failure under the brief, not a harmless embellishment. Several scenes also contain enough sequential actions, dialogue, and time passage to require substantial further subdivision into 5–10 second clips. The wild rabbits are not cleanly individualized as cast entities, and the final Boy re-entry does not fully restate his locked visual traits.
  - *grok-4.5 (self):* Richer emotional micro-beats and the strongest sound/music bed of the two, with warmer peak interleaving at the tear/fairy/transformation. Fidelity is undercut by invented or paraphrased lines (Skin Horse opener, Boy’s china-dog and burrow lines, Rabbit voicing internal monologue) and by minting the proper name THISTLE for a wild rabbit. Character clarity is mixed: Boy and Nana get good first-look detail, but THISTLE appears from nowhere, and nursery/bed headings are not stable return strings—unusable for locked location reuse. Directibility is mostly solid but diluted by denser multi-action passages and the vague INT. DIFFERENT BEDROOM / INT. BOY'S BED headings.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a mandatory source-to-cast preflight audit: every named character token and every individually speaking member of a same-species group must be traceable to the source or use a neutral, consistently differentiated role token; prohibit adding proper names merely to distinguish unnamed source characters.
  - *grok-4.5 (self):* Add a hard rule that any stable token assigned to an unnamed book speaker must be introduced with a full visual lock on the same Action line as their first cue, and that returning places must reuse one exact heading string with no synonyms, room/bed swaps, or DIFFERENT/OTHER qualifiers.

