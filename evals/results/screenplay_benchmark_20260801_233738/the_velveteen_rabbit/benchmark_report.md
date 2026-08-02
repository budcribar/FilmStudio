# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 05:40:43 UTC*  
*Source Story File: `The_Velveteen_Rabbit.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **91.6** | 100.0% | 86.0% | 4 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **86.4** | 100.0% | 77.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 100% | 9.0/10 | 8.5/10 | 8.0/10 | 8.2/10 | 8.8/10 | 9.0/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 8.0/10 | 7.8/10 | 7.5/10 | 7.5/10 | 8.0/10 | 7.8/10 |

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gpt-5.6-terra |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | 8.5 | **7.6** *(self)* |
| **grok-4.5** | **8.7** *(self)* | 7.9 |

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay A because it delivers the fullest, most faithful emotional arc with superior visual identity locking and exceptionally purposeful sound-and-music coverage. Weakest: Screenplay B because its heavier compression, inconsistent BOY/THE BOY tokenization, and misattributed Doctor dialogue create more fidelity and production-continuity risk.
- **grok-4.5:** Strongest: Screenplay B because it delivers the most complete, correctly attributed book spine with cleaner single-purpose scenes and richer peak music/sound interleaving. Weakest: Screenplay A because it reassigns Doctor dialogue to Nana and packs multi-phase time jumps into single headings that hurt clip-level directibility.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 17
- **Dialogue Blocks:** 61 (Avg `14.6` words/turn, Max `33` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra:* A is the more complete and emotionally faithful adaptation, preserving every major turn: Christmas abandonment, the Skin Horse's explanation of Real, bedtime attachment, garden recognition, wild-rabbit rejection, illness and disposal, the Fairy's intervention, transformation, and spring reunion. Its character descriptions are unusually useful for image locking, especially the Rabbit's progressive wear, the Skin Horse, Nana, Fairy, and the Boy's final age progression. The principal limitation is clip granularity: several action blocks contain mini-montages or time shifts that need to become separate 5-10 second beats, notably the spring garden sequence, the fever-to-recovery passage, and portions of the final transformation. Dialogue is highly faithful and performable, while the recurring, specific sound and music motifs strongly support the emotional arc.
  - *grok-4.5 (self):* Strongest overall package: fullest book coverage (Timothy, complete Skin Horse monologue including shabbiness/ugliness lines, Nana’s “knowing expression,” correct Doctor burn orders, wild-rabbit clockwork/hind-legs/smell beats, tear-flower-fairy, spring spotted-rabbit coda) with almost no invented plot. Scene breaks are cleaner for AI clips (separate bed-later shabbiness, nursery day/night illness, fowl-house evening). Dialogue stays close to Williams; only soft spot is the Fairy beat’s ellipsis-only Rabbit line. Cast locks are consistent (fair-haired Boy restated taller in spring; Skin Horse bald patches/seams; Fairy pearl and dew-drops). Sound/music is the best of the two—diegetic beds plus distinct MUSIC cues at Real declaration, loneliness, tear, and transformation peaks.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a hard beat-level rule: each Action paragraph must describe only one continuous 5-10 second camera-observable event, with any time jump, montage progression, or distinct action separated into a new Action beat or scene.
  - *grok-4.5 (self):* Prohibit ellipsis-only or blank dialogue lines; if the book gives no spoken words for that beat, carry reaction in Action alone or quote the nearest source line rather than emitting a non-performable placeholder cue.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 53 (Avg `12.8` words/turn, Max `26` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. BOY).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* B retains the central plot and ending, but it is materially more compressed than A and loses texture from the toy-cupboard social world, the Skin Horse's fuller explanation, the Rabbit's post-recognition happiness, and the Fairy's full Rabbit-land instruction. It also incorrectly assigns the Doctor's "Nonsense! Get him a new one" line to Nana, weakening source fidelity and character logic. Visual action is generally concrete, but dense time-lapse passages still need subdivision for clip production. Character continuity is less reliable because the protagonist is referred to as both THE BOY and BOY, which risks separate cast identities in an automated pipeline. Sound beds are consistently present and serviceable, though they are less musically shaped and emotionally varied than A's.
  - *grok-4.5:* Solid, faithful short-film cut that hits the Christmas intro, Skin Horse lesson, Boy’s REAL declaration, wild-rabbit rejection, scarlet-fever crisis, sack/tear/fairy transformation, and spring reunion. Medium lock and cast looks (spotted velveteen, bald Skin Horse, Nana apron, Fairy dew-drops) are clear and mostly restated on re-entry. Weaknesses: Doctor’s “Nonsense! Get him a new one” is wrongly given to Nana; Skin Horse speech is trimmed (loses the “can’t be ugly” beat and Timothy); several headings pack multi-phase action (garden play + dusk abandonment + Nana candle search; full fever arc in one bedroom night) that fight 5–10s clip slicing; sound is present every scene but thinner at peaks than B.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a final source-dialogue attribution audit: for every retained or compressed quoted line, verify that the speaking character matches the source, and convert uncertain attribution into action or narration rather than assigning it to another character.
  - *grok-4.5:* Add a hard rule that a single scene heading may cover only one continuous time-of-day and dramatic purpose; any elapsed-time jump (night after night, dusk then later bedroom, fever then recovery) must become a new heading or an explicit Action time-card, never a multi-phase montage under one slug.

