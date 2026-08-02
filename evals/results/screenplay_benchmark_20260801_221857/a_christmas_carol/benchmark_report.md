# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 04:25:12 UTC*  
*Source Story File: `A_Christmas_Carol.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-terra** | **82.2** | 100.0% | 70.0% | 4 pts | 1.0 |
| 🥈  | **grok-4.5** | **79.4** | 98.5% | 67.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-terra** | 100% | 100% | 100% | 4.0/10 | 7.2/10 | 7.8/10 | 7.2/10 | 7.8/10 | 8.2/10 |
| **grok-4.5** | 100% | 100% | 70% | 5.8/10 | 6.0/10 | 5.5/10 | 6.8/10 | 7.8/10 | 8.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-terra**:
  - gpt-5.6-terra: Invents named characters absent from the source, including MR. HARKER, LUCY, TOM, and CHARWOMAN NELL; this is a severe source-fidelity violation.
  - gpt-5.6-terra: Uses MARLEY for the supernatural figure after establishing the source character as Jacob Marley, weakening recurring character-token consistency for the portrait/cast pipeline.
  - gpt-5.6-terra: Omits the required VISION_META sidecar.
  - gpt-5.6-terra: Contains fidelity errors or distortions in dialogue allocation, most notably assigning Tiny Tim the source line "As good as gold, and better," which Bob says about him.
  - gpt-5.6-terra: Omits the debtor-family future vision, a significant source beat that demonstrates relief at Scrooge's death and motivates Scrooge's request to see genuine grief.
- **grok-4.5**:
  - gpt-5.6-terra: Invents numerous named source characters, including MR. GRANTHAM, MR. BENTLEY, MR. CROSS, MR. WORTLEY, and MR. PEEL, rather than using stable neutral role tokens for unnamed source figures; this is a severe source-fidelity violation.
  - gpt-5.6-terra: Uses unstable or prohibited relational/generic dialogue cast cues, including NIECE and HUSBAND, rather than independently stable character tokens.
  - gpt-5.6-terra: Contains forbidden multi-location scene headings, including "EXT. CITY STREET / INT. BELLE'S PARLOUR" and headings joining distinct rooms such as "INT. SCROOGE'S BEDROOM AND SITTING-ROOM."
  - gpt-5.6-terra: Omits the required VISION_META sidecar.
  - gpt-5.6-terra: Several dialogue turns materially exceed the prompt's 35-word maximum, undermining short-clip performance timing.
  - grok-4.5: Multiple scene headings illegally join two places with AND or slash (e.g. EXT. CITY STREET / INT. BELLE'S PARLOUR, INT. SCROOGE'S BEDROOM AND SITTING-ROOM, EXT. COUNTRY ROAD AND SCHOOL, HALL AND STAIRS), breaking one-place-per-clip scheduling
  - grok-4.5: Closing EXT. LONDON - MONTAGE - DAY is a non-concrete multi-place slug unusable for single 5–10s clips

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-terra | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | **6.3** *(self)* | 6.0 |
| **grok-4.5** | 7.8 | **7.3** *(self)* |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-terra rated its own screenplay 6.3/10 vs. a 7.8/10 average from 1 other judge(s) (-1.4) — notably harsher on itself than peers were.
- ⚠️ grok-4.5 rated its own screenplay 7.3/10 vs. a 6.0/10 average from 1 other judge(s) (+1.3) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay B because its scene construction, visual grounding, clip-level directibility, and narrative compression are cleaner and more production-oriented. Weakest: Screenplay A because, despite broader source coverage and excellent Dickens phrasing, it has more severe structural violations, more invented named minor characters, and more overloaded multi-location scenes.
- **grok-4.5:** Strongest: Screenplay A because it is the only production-ready draft with strictly single-location, clip-schedulable scenes, locked cast looks, and full sound coverage despite missing the knocker set-piece. Weakest: Screenplay B because illegal multi-location headings and a montage slug make the otherwise fuller adaptation unusable for the 5–10s AI-clip pipeline.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 22
- **Dialogue Blocks:** 181 (Avg `9.5` words/turn, Max `23` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG EBENEZER, YOUNG CRATCHIT, TINY TIM, OLD JOE`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 4 age-qualified character header(s) (e.g. YOUNG EBENEZER, YOUNG CRATCHIT, TINY TIM).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* This is the stronger production draft structurally. Its scenes are more discrete, visually grounded, and generally easier to break into five-to-ten-second clips; it also maintains a clear escalation from office cruelty through supernatural instruction, future terror, and active redemption. Sound beds are present in every scene and are usually concrete and editorially useful. Character looks are more consistently supplied for Scrooge, Bob, Tiny Tim, Fan, Belle, Fezziwig, and the spirits than in Screenplay A. But it remains disqualified on fidelity: it invents several personal names, drops the debtor-family death-reaction sequence, and occasionally reallocates or compresses Dickens dialogue in ways that alter characterization. The script is more disciplined than A, but still needs a fidelity pass and final machine-sidecar compliance before production.
  - *grok-4.5:* Cleanest AI-clip package: single-location headings, concrete observable action, locked looks (Scrooge traits, Young Ebenezer age splits, Tiny Tim brace/crutch, Present giant), and consistent (SOUND:) beds on every scene. Iconic dialogue is mostly preserved and split into clip-length turns. Severe fidelity hit for dropping the Marley-knocker transformation entirely (office jumps straight to bedchamber/gruel/bells) and omitting the Future debtors/Caroline tenderness contrast; Present also skips miners/lighthouse/ship. Still covers the redemption spine without invented plot, so greenlightable after beat restores.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a mandatory source-beat checklist before drafting and a final audit after drafting: identify every essential causal turn, require each to appear in the screenplay, and verify that each retained spoken line is assigned to the same source character unless the source explicitly permits narration.
  - *grok-4.5:* Add a hard pre-output checklist step: extract the source’s 8–12 most iconic visual set-pieces and require each either to appear as its own scene or to be explicitly marked as a deliberate runtime cut—submission fails if an unmarked iconic set-piece is missing.

### 🎬 grok-4.5
- **Scene Headings Count:** 26
- **Dialogue Blocks:** 178 (Avg `18.1` words/turn, Max `74` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY SCROOGE, YOUNG MAN SCROOGE, TINY TIM, OLD JOE, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 28 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 5 age-qualified character header(s) (e.g. BOY SCROOGE, YOUNG MAN SCROOGE, TINY TIM).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the more expansive and textually fluent adaptation, preserving nearly every major story turn: Fred and the charity collectors, Marley, the three spirits, Fan, Fezziwig, Belle, the Cratchits, Fred's party, Ignorance and Want, the death visions, and Scrooge's restitution. Its quoted Dickens dialogue is generally strong and its sound cues are specific, varied, and consistently placed. However, fidelity must receive a severe penalty because it invents a substantial roster of personally named minor characters where the source leaves them unnamed. It also violates its own closed-cast rule with relational cues, combines distinct locations in individual headings, and repeatedly overloads scenes with enough dialogue and action for many clips. The descriptions of principal figures are useful, but supporting players and period variants are not consistently locked or reintroduced. It is not greenlightable as submitted despite its rich coverage.
  - *grok-4.5 (self):* Strongest book coverage: knocker-to-Marley face, fuller Marley chain sermon, Caroline debtors beat, Ignorance/Want, and denser Cratchit/Fred material with book-cadence VO. Directibility is the deal-breaker—slash/AND headings and a montage slug cannot be scheduled as single short clips. Character tokens drift (NIECE, CHARWOMAN, BOB vs BOB CRATCHIT, BOY SCROOGE / YOUNG MAN SCROOGE / apprentice SCROOGE without consistent re-entry locks). Several dialogue turns far exceed the 35-word cap (Fred’s Christmas speech, long Marley blocks) despite good authenticity of wording. Sound cues are present but thinner at some peaks than A.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a hard preflight rule: for every source-unnamed speaking minor role, use a stable neutral role token only (for example, CHARITY GENTLEMAN or EXCHANGE MERCHANT) and prohibit inventing personal names or relational labels; reject the draft if any such token appears.
  - *grok-4.5 (self):* Add a mandatory validation pass that rejects any scene heading containing AND, /, OR, or multi-place words (MONTAGE, VARIOUS, STREETS as plural summary) and requires splitting into separate one-place INT./EXT. headings before output is allowed.

