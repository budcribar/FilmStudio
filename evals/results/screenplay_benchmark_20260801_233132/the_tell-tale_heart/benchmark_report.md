# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 05:34:28 UTC*  
*Source Story File: `The_Tell-Tale_Heart.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **88.2** | 100.0% | 80.0% | 4 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **82.4** | 100.0% | 71.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 100% | 5.8/10 | 8.6/10 | 8.6/10 | 8.3/10 | 7.8/10 | 9.0/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 5.2/10 | 6.2/10 | 7.8/10 | 7.7/10 | 7.2/10 | 8.4/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - gpt-5.6-terra: Invents three specifically named police characters—OFFICER REYNOLDS, OFFICER HALE, and OFFICER BRIGGS—where the source provides only three unnamed officers; under the evaluation rule, invented named characters trigger a severe fidelity failure.
  - gpt-5.6-terra: Omits the required VISION_META sidecar, leaving the mandated downstream machine-readable visual lock incomplete.
- **gpt-5.6-terra**:
  - gpt-5.6-terra: Renames the source's unnamed old man as MR. VALE, an invented named principal character; this is a severe source-fidelity violation.
  - gpt-5.6-terra: Invents named police characters and gives officers and the old man dialogue not quoted as spoken in the source.
  - gpt-5.6-terra: Omits the required VISION_META sidecar, leaving the mandated downstream machine-readable visual lock incomplete.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gpt-5.6-terra |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | 7.6 | **6.1** *(self)* |
| **grok-4.5** | **8.5** *(self)* | 8.0 |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-terra rated its own screenplay 6.1/10 vs. a 8.0/10 average from 1 other judge(s) (-1.9) — notably harsher on itself than peers were.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay A because it most effectively translates Poe's escalating visual and sonic horror into concrete, short-clip-ready beats while covering the full narrative. Weakest: Screenplay B because its invented principal name MR. VALE, unstable narrator identity treatment, and additional invented spoken material create the most serious fidelity and casting-clarity failures.
- **grok-4.5:** Strongest: Screenplay B because it locks cast looks hardest, keeps more complete source VO, and densifies peak Action/SOUND/MUSIC for short-clip directing. Weakest: Screenplay A because Narrator and officer visual locks are thinner and some internal old-man lines are incorrectly externalized as spoken dialogue.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 13
- **Dialogue Blocks:** 43 (Avg `19.5` words/turn, Max `34` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the clearly stronger adaptation in narrative execution. It covers the complete source arc, preserves Poe's central language and escalating structure, retains the old man as the unnamed OLD MAN, and gives the murder, concealment, inspection, and confession substantial playable detail. Its visual beats are highly clip-directable: lantern, latch, eye ray, heartbeat, bed, floorboards, chair scrape, and confession are concrete actions with strong shot potential. The layered sound cues are particularly effective and create an increasingly oppressive acoustic arc. The principal material flaw is strict-source compliance: the three police visitors are given invented personal names and some extra dialogue, despite the source not naming or quoting them. There are also occasional action units containing several actions that would need to be divided into separate clips, but the staging is generally clear and producible after revision.
  - *grok-4.5 (self):* Strongest overall package: fuller retention of iconic opening and peak VO, clearer micro-beats for AI clip splitting, and the best cast locks (Narrator age/build/hair/wardrobe; Old Man eye; three officers each given distinct physical traits). Kill and confession peaks interleave Action, SOUND, and MUSIC at the density the short-clip format needs. Main deduction is dialogue authenticity in the officers beat—invented small talk (“More tea?—the weather…”, “Rest from your fatigues. You see? All is well.”) violates the summarized-exchange cap when the book only says they chatted of familiar things. Still no major invented plot and fully closed cast; greenlight-ready after trimming those invented lines.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Change the closed-cast rule to explicitly distinguish stable production tokens from invented canonical names: unnamed source characters must use reusable neutral role tokens without fabricated surnames or biographical identity, and may not receive invented dialogue unless the source audibly quotes it.
  - *grok-4.5 (self):* Tighten the summarized-exchange rule to an explicit default of zero invented spoken lines when the source only reports that people talked or chatted: carry the beat with Action and book-voice V.O. only, and allow at most one generic period line total if Action alone cannot signal the social beat.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 52 (Avg `13.3` words/turn, Max `26` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* The draft follows the major plot spine and provides a coherent, economical horror progression, with good use of lantern, heartbeat, floorboards, weather, and silence. However, MR. VALE is a substantial unnecessary invention: the source deliberately leaves the old man unnamed, and repeatedly replacing him with a fabricated proper name changes the adaptation's fidelity and production identity lock. Character specification is also materially weaker than A: the NARRATOR has no durable age, build, hair, or facial description, while the cue form "NARRATOR V.O." is inconsistent with the action token NARRATOR and risks being parsed as a separate cast identity. The old man's self-reassuring thoughts are converted into spoken dialogue, and “Well enough” plus officer lines are invented. Its pacing is functional but rushes several peak-stage actions together, and the final confession receives less layered visual escalation than A.
  - *grok-4.5:* Solid, faithful short-film cut that hits the stalking week, eighth-night kill, concealment, and confession without inventing major plot. Book VO is largely preserved and iconic confession lines land cleanly. Weaknesses: Narrator’s visual lock is thin (mostly “dark-clad”/black coat) and not restated on re-entry; officers lack distinct individual looks; the old man’s self-comfort lines (“wind in the chimney” / “mouse”) are staged as spoken dialogue rather than internal, a mild fidelity stretch; opening monologue is slightly compressed versus the source. Sound cues are consistent and the heartbeat arc is well staged for short clips. Structure and closed cast are production-usable as-is.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a hard identity-preservation rule: when the source withholds a character's name, the screenplay must retain a neutral stable token and may never assign a fabricated personal name, surname, or identity; enforce exact reuse of that token across action and dialogue, including V.O. extensions.
  - *grok-4.5:* Require that every speaking character’s first on-screen Action line lock at least age/build/hair/wardrobe in one concrete phrase, and that any re-entry after a time skip or cutaway restates two of those locked traits in the opening Action of that scene.

