# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 21:59:25 UTC*  
*Source Story File: `A_Christmas_Carol.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **85.8** | 99.5% | 77.0% | 3 pts | 1.5 |
| 🥈  | **grok-4.5** | **80.6** | 98.5% | 69.0% | 3 pts | 1.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 100% | 100% | 90% | 5.2/10 | 8.5/10 | 8.0/10 | 7.5/10 | 8.0/10 | 8.8/10 |
| **grok-4.5** | 100% | 100% | 70% | 8.8/10 | 6.2/10 | 5.2/10 | 6.0/10 | 7.8/10 | 7.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-sol**:
  - grok-4.5: Dropped major Stave IV beats entirely (merchants on 'Change discussing the death, Old Joe's plunder shop with charwoman/Dilber/undertaker, debtors' family relief)
  - grok-4.5: Incomplete Ghost of Christmas Yet to Come arc collapses the no-one-mourns-Scrooge revelation into a single bare corpse shot
- **grok-4.5**:
  - gpt-5.6-sol: The estimated finished runtime substantially exceeds the roughly 10-minute target, with numerous speeches and compound action passages too long for 5-10-second clips.
  - gpt-5.6-sol: Headings such as "EXT. CITY STREET / INT. BELLE'S ROOM" and "INT. CRATCHIT HOME / EXT. LONDON - MONTAGE" combine distinct locations and interiors/exteriors, breaking the required schedulable single-location structure.
  - gpt-5.6-sol: Collective or ambiguous cues such as ALL and GENTLEMEN/GENTLEMAN violate closed-cast clarity, while the same YOUNG SCROOGE token is used for both a child and an adult apprentice despite reference-image locking.
  - grok-4.5: Multiple illegal multi-location scene headings using slashes or MONTAGE (EXT. CITY STREET / INT. BELLE'S ROOM; INT. CRATCHIT HOME / EXT. LONDON - MONTAGE), violating the single concrete place HARD rule
  - grok-4.5: Initial generic GENTLEMAN cue before later named tokens creates a brief closed-cast instability

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-sol** | **7.9** *(self)* | 5.4 |
| **grok-4.5** | 7.4 | **8.3** *(self)* |

### 🧐 Self-Bias Analysis
- ⚠️ grok-4.5 rated its own screenplay 8.3/10 vs. a 5.4/10 average from 1 other judge(s) (+2.9) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-sol:** Strongest: Screenplay B because it offers the best overall balance of faithful dramatic spine, cast stability, clip-level directibility, pacing, and purposeful audio scoring. Weakest: Screenplay A because its excessive runtime, compound beats, multi-location headings, and unstable cast tokens make the otherwise faithful draft unsuitable for production as written.
- **grok-4.5:** Strongest: Screenplay B because it delivers near-complete stave coverage and book-faithful dialogue including every major Future revelation. Weakest: Screenplay A because it entirely drops the merchants/Old Joe/debtors sequence that proves Scrooge dies unmourned, collapsing Stave IV fidelity.

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
  - *gpt-5.6-sol (self):* This draft efficiently preserves the central transformation, the three Spirits, the key emotional relationships, and the most famous dialogue while remaining close to the intended runtime. Its principal fidelity weakness is aggressive source compression: Christmas Present's broader survey is largely omitted, while the Future sequence loses the businessmen's indifference, Old Joe's sale of the stolen property, and the debtor family's relief, reducing the evidence of Scrooge's unmourned death to one strong visual scene. Character descriptions are distinctive and usually restated on re-entry, although Young Scrooge still spans more than one adult age and Belle changes from young woman to matron without separate age-specific cast tokens. The concrete headings, controlled transitions, and comparatively short exchanges are well suited to clip generation, with only a few compound actions and speeches exceeding the ideal beat length. Its sound design is the strongest package element, using detailed ambience, recurring musical ideas, a coherent funeral pulse, and properly intensified cues at major reveals.
  - *grok-4.5:* Fidelity collapses because the Future stave omits the merchants' indifference, Old Joe's fencing of bed-curtains/shirt, and the debtors' relieved household—core proof that Scrooge dies unmourned—triggering the mandated severe penalty. Character lockups are excellent (gaunt pointed-nose Scrooge, Bob's white comforter and threadbare look restated on re-entry, clear BOY SCROOGE / YOUNG SCROOGE / SCROOGE splits, consistent Belle/Fan). Directibility is strong: single concrete headings, observable micro-actions, clip-friendly beats. Pacing is tight for runtime but hollowed by the missing Future peaks. Dialogue keeps most icons (Bah Humbug, surplus population, God bless us) with only light compression. Sound/music cues are present and concrete in every scene, with good peak layering. Not production-ready solely due to the structural omissions.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol (self):* Add a silent source-coverage audit before output: identify each section's indispensable causal or revelatory beats, then verify that each is dramatized rather than replacing an entire sequence with only its endpoint; cut descriptive texture before cutting evidence that drives the protagonist's change.
  - *grok-4.5:* Add an explicit coverage mandate: every major stave turn and iconic set-piece (including all three spirit climaxes and the unmourned-death sequence) must receive at least one dedicated scene heading; runtime cuts may only drop connective tissue, never entire revelation sequences.

### 🎬 grok-4.5
- **Scene Headings Count:** 26
- **Dialogue Blocks:** 148 (Avg `20.8` words/turn, Max `122` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `CAROL BOY, YOUNG SCROOGE, TINY TIM, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 28 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 4 age-qualified character header(s) (e.g. CAROL BOY, YOUNG SCROOGE, TINY TIM).
- **Judge Rationale:**
  - *gpt-5.6-sol:* This is the more comprehensive adaptation, preserving nearly every essential encounter, most thematic reversals, and a large amount of Dickens's original language. That fidelity comes at the expense of production usability: long monologues, whole celebrations summarized inside single action paragraphs, multiple temporal changes within one beat, and multi-location slash headings make clean clip extraction difficult. Character locking is particularly weak for Young Scrooge's age changes, Belle's age change, the charity gentlemen, the businessmen, and several Cratchit family members; many recurring characters also return without their identifying traits being restated. The dialogue is authentic but frequently far too long for one short clip. Diegetic effects are often vivid, but two scenes lack the mandatory audio cue and the screenplay provides almost no sustained background-music strategy before the final montage.
  - *grok-4.5 (self):* Fidelity is near-complete: all five staves, iconic lines, Fezziwig dance, Belle release + later family, full Cratchit dinner, Ignorance/Want, merchants, Old Joe, debtors, Tim's death future, grave, and redemption morning are present with book diction largely intact. Character work is solid (Scrooge's sharp-nose/blue-chin lock, Bob's comforter, YOUNG SCROOGE) but re-entry restatements are thinner and some supporting looks sparse. Directibility suffers from slash-combined and MONTAGE headings that prevent clean single-location 5-10 s clips and break Fountain scheduling. Pacing maintains momentum across the full arc with clear transitions. Dialogue is highly authentic and performable, preserving long Marley and Fred speeches. Sound design is densest and most useful—diegetic cues plus music on every scene and multi-layered at peaks. Not production-ready because the heading violations are structural deal-breakers under the shared prompt's priority-1 rules.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol:* Add a hard clip-budget preflight: each action paragraph may contain only one continuous 5-10-second visual event, each dialogue turn may contain no more than roughly 18 spoken words, and the draft must be regenerated if its estimated finished runtime exceeds the target by more than 15 percent.
  - *grok-4.5 (self):* Strengthen the scene-heading HARD rule with an explicit ban on slash-combined places or the word MONTAGE inside any heading; require either separate single-place headings for consecutive locations or pure Action-line transitions under one stable heading.

