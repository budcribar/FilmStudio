# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 21:59:53 UTC*  
*Source Story File: `A_Christmas_Carol.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **84.3** | 99.5% | 74.0% | 4 pts | 1.0 |
| 🥈  | **grok-4.5** | **75.4** | 98.5% | 60.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 100% | 100% | 90% | 5.2/10 | 8.5/10 | 7.5/10 | 6.8/10 | 8.0/10 | 8.5/10 |
| **grok-4.5** | 100% | 100% | 70% | 6.0/10 | 6.5/10 | 4.8/10 | 4.5/10 | 7.0/10 | 7.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-sol**:
  - gpt-5.6-sol: MR. DOBSON and TOM are proper names invented for source-anonymous characters, triggering the stated severe fidelity penalty.
  - gpt-5.6-sol: Major Future-stave evidence is omitted, including the indifferent businessmen, Old Joe's sale of the stolen death-room goods, and the relieved debtor family, substantially weakening the source's social indictment and delayed identification of the corpse.
  - gpt-5.6-sol: The approximately 1,500 spoken words already consume about ten minutes without action or transitions, while several long dialogue turns and compound action blocks cannot fit individual 5-10-second clips.
- **grok-4.5**:
  - gpt-5.6-sol: The draft is far beyond a roughly 10-minute film: extensive source transcription and numerous 30-100-word dialogue turns cannot fit 5-10-second clips.
  - gpt-5.6-sol: Forbidden multi-location headings and hidden location changes, including EXT. CITY STREET / INT. BELLE'S ROOM and EXT. BLEAK MOOR / LIGHTHOUSE / SHIP, make the draft unschedulable and clip-ambiguous.
  - gpt-5.6-sol: Closed-cast and dialogue integrity are broken by the plural-to-singular CHARITY GENTLEMEN/CHARITY GENTLEMAN handling, ALL CRATCHITS, uncued passers-by, placeholder-only parentheticals, and the reversed Scrooge/Marley belief exchange.
  - gpt-5.6-sol: MR. ADAMS and MR. BARKER are proper names invented for source-anonymous businessmen.
  - grok-4.5: Compound multi-location scene headings banned by prompt (EXT. BLEAK MOOR / LIGHTHOUSE / SHIP, EXT. CITY STREET / INT. BELLE'S ROOM)
  - grok-4.5: Malformed character/dialogue lines containing placeholder parentheticals ('(what business)', '(a pimple—lead on)') that break Fountain parsing
  - grok-4.5: Unstable supporting tokens (NIECE, CHARITY GENTLEMAN, FAT MAN, MR. ADAMS) violating closed-cast consistency

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-sol** | **6.4** *(self)* | 4.2 |
| **grok-4.5** | 8.4 | **7.8** *(self)* |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-sol rated its own screenplay 6.4/10 vs. a 8.4/10 average from 1 other judge(s) (-2.0) — notably harsher on itself than peers were.
- ⚠️ grok-4.5 rated its own screenplay 7.8/10 vs. a 4.2/10 average from 1 other judge(s) (+3.5) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-sol:** Strongest: Screenplay B because its disciplined selection, stable visual identities, clear transitions, and purposeful audio arc make it substantially more filmable despite fidelity and runtime defects. Weakest: Screenplay A because its excessive length, multi-location scene construction, unstable supporting cast, and malformed or oversized dialogue make it unusable for the required short-clip production workflow as written.
- **grok-4.5:** Strongest: Screenplay A because it delivers clean single-location, clip-ready scenes with locked character visuals, consistent audio direction, and full emotional spine without structural defects. Weakest: Screenplay B because banned compound headings plus broken placeholder dialogue cues render it unparsable and unfilmable as-is despite richer beat coverage.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 19
- **Dialogue Blocks:** 103 (Avg `13.7` words/turn, Max `46` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY SCROOGE, YOUNG SCROOGE, TINY TIM`
- **Diagnostics & Warnings:**
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 3 age-qualified character header(s) (e.g. BOY SCROOGE, YOUNG SCROOGE, TINY TIM).
  - ⚠️ Detected 4 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-sol (self):* This is the more coherent and selective adaptation, preserving the central progression through Marley, childhood isolation, Fezziwig, Belle, the Cratchits, Fred, Ignorance and Want, the future grave, and Scrooge's restitution. Fidelity is nevertheless capped below three because it invents the proper names MR. DOBSON and TOM and drops several major Future-stave beats that establish how Scrooge's death affects society. Casting clarity is strong: Scrooge, Bob, Fred, Tiny Tim, the ghosts, and the age-separated Scrooge identities receive stable and generally consistent visual traits on re-entry. The main identity weakness is retaining BELLE as one token through a substantial age transformation rather than defining a distinct matron version. Most action is concrete and filmable, but the Fezziwig transformation, family meals, temporal dissolves, and several other paragraphs bundle multiple clips into one beat. The structure moves cleanly and makes sensible compression choices, but the spoken-word volume still pushes the finished film well beyond ten minutes, while the abbreviated Future section loses escalation before the grave reveal. Dialogue remains close to Dickens and is character-distinct, though several speeches exceed a single clip's performable length. Sound design is the strongest craft element, with explicit ambience in every scene and a well-shaped musical progression from warmth through dissonance, funeral pulse, and final release.
  - *grok-4.5:* Strong closed-cast adaptation that hits every major spine beat (counting-house setup, Marley, three spirits with school/Fezziwig/Belle, Cratchit dinner + Tim, Fred party, Ignorance/Want, future corpse/grave, redemption turkey/salary/Tim lives) while keeping iconic lines intact. Character looks are locked early and restated on re-entry/age splits (Boy/Young/Elderly Scrooge, Bob’s comforter, Tim’s crutch/braces). Scenes are single-location with concrete camera actions ideal for 5-10s clips; sound/music cues appear in every scene and densify at peaks. Minor compressions (miners/lighthouse montage dropped, Future Joe’s shop abbreviated) keep runtime tight without inventing plot. No deal-breakers.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol (self):* Change the supporting-role rule so that a source-anonymous person receives a stable descriptive token such as CHARITY COLLECTOR or STREET BOY, never a fabricated proper name; proper names may be used only when supplied by the source.
  - *grok-4.5:* Add a hard rule that every spirit-journey sequence must retain at least one dedicated single-location micro-scene for each major book locale visited (no wholesale omission of secondary stops solely for runtime), forcing explicit coverage before compression.

### 🎬 grok-4.5
- **Scene Headings Count:** 32
- **Dialogue Blocks:** 179 (Avg `18` words/turn, Max `84` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `CAROL BOY, BOY SCROOGE, YOUNG SCROOGE, TINY TIM, OLD JOE, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 28 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 6 age-qualified character header(s) (e.g. CAROL BOY, BOY SCROOGE, YOUNG SCROOGE).
- **Judge Rationale:**
  - *gpt-5.6-sol:* This version covers an unusually broad selection of the source, including the carol boy, Fan, Fezziwig, Belle, the remote Christmas celebrations, Old Joe, the debtor family, Tiny Tim's projected death, and the complete redemption. However, the required severe penalty applies because MR. ADAMS and MR. BARKER are invented names, while several lines are also misattributed or altered in meaning, most notably the reversed exchange about believing in Marley. Principal ghosts receive vivid visual descriptions, but BOY SCROOGE and YOUNG SCROOGE lack sufficiently locked physical traits, Belle is not given a separate matron identity, and several supporting tokens are ambiguous or generic. Directibility is undermined by compound action paragraphs, literary summaries, multiple locations inside single headings, and dialogue far too long for individual clips. The dramatic progression remains recognizable, but near-transcription produces a film substantially longer than the target and leaves the final stave rushed despite the overall excess. Much of the dialogue preserves Dickens's diction, yet malformed parentheticals and oversized speeches require substantial rewriting. Diegetic sound is often specific and atmospheric, but at least two scenes lack the mandated parenthetical cue and the background score is largely undeveloped outside Fezziwig and the ending.
  - *grok-4.5 (self):* Highest source coverage—retains nearly every book beat, long Marley speeches, Ali Baba fantasy, full Fezziwig dance, Belle double scene, Cratchit Martha/pudding details, miners/lighthouse/ship, Joe’s rag shop, Caroline debtors, and all iconic dialogue verbatim. Sound cues are frequent. However, directibility collapses on multi-place headings and crammed montages that cannot be scheduled as single clips; several cues are literally unparsable placeholders; supporting names drift. Pacing bloats well past a tight 10-minute cut. These structural defects make the draft unusable without rewrite despite superior fidelity.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol:* Add a quantitative preflight budget: no more than 80 clip-beats, approximately 1,300 spoken words, and 25 spoken words per 5-10-second beat; if the draft exceeds any cap, cut whole secondary sequences rather than compressing them into compound montage actions.
  - *grok-4.5 (self):* Strengthen the existing multi-place ban with an explicit parse-time rule: any heading containing a slash, 'VARIOUS', or more than one concrete place is invalid and must be split into sequential single-location scenes each opening with a grounding Action line; also forbid any parenthetical text inside or immediately after a character cue except standard delivery notes.

