# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 05:07:41 UTC*  
*Source Story File: `A_Christmas_Carol.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-terra** | **83.1** | 100.0% | 72.0% | 3 pts | 1.5 |
| 🥈  | **grok-4.5** | **80.9** | 98.5% | 69.0% | 3 pts | 1.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-terra** | 100% | 100% | 100% | 5.2/10 | 8.4/10 | 7.8/10 | 7.4/10 | 8.0/10 | 8.7/10 |
| **grok-4.5** | 100% | 100% | 70% | 6.0/10 | 6.5/10 | 5.5/10 | 6.2/10 | 8.7/10 | 8.6/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-terra**:
  - gpt-5.6-terra: Invented proper-name characters are pervasive, including MR. HARTLEY, CLARA, MR. POOLE, MR. SNIPE, MRS. RIGGS, and TOM BAXTER; these replace unnamed source roles and trigger the severe fidelity penalty.
  - gpt-5.6-terra: The major future vision in which a debtor family learns that Scrooge's death frees them from their merciless creditor is omitted.
  - gpt-5.6-terra: The Ghost of Christmas Present dialogue incorrectly has Scrooge say, "I am the Ghost of Christmas Present?" rather than identifying the Spirit, creating a clear character/dialogue error.
  - gpt-5.6-terra: The required VISION_META sidecar is absent.
- **grok-4.5**:
  - gpt-5.6-terra: Invented proper-name supporting characters (including MR. CHAPMAN, MR. PELL, MERCHANT HALES, and MERCHANT GRAY) violate source fidelity; unnamed source roles should remain stable neutral role tokens.
  - gpt-5.6-terra: Multiple scene headings combine distinct locations, directly defeating scheduling and clip generation requirements: for example COUNTRY ROAD AND SCHOOL, CITY STREET / INT. BELLE'S PARTING PLACE, BLEAK MOOR / LIGHTHOUSE / SHIP, and CITY / EXCHANGE.
  - gpt-5.6-terra: Several scenes and action blocks contain an unfilmably dense sequence of separate beats, locations, and dialogue exchanges for short 5–10 second clips.
  - gpt-5.6-terra: The required VISION_META sidecar is absent.
  - grok-4.5: Multiple scene headings join distinct places with / (e.g. EXT. CITY STREET / INT. BELLE'S..., EXT. BLEAK MOOR / LIGHTHOUSE / SHIP) violating the single-place HARD rule
  - grok-4.5: Supporting speakers use unstable generic role cues (PORTLY GENTLEMAN, FAT MERCHANT, CHARWOMAN, UNDERTAKER'S MAN, STREET BOY) instead of proper-name tokens required for closed cast

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-terra | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | **5.8** *(self)* | 5.9 |
| **grok-4.5** | 8.6 | **7.9** *(self)* |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-terra rated its own screenplay 5.8/10 vs. a 8.6/10 average from 1 other judge(s) (-2.8) — notably harsher on itself than peers were.
- ⚠️ grok-4.5 rated its own screenplay 7.9/10 vs. a 5.9/10 average from 1 other judge(s) (+2.0) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay A because it preserves the broadest and most emotionally complete version of the book's plot, themes, set pieces, and Dickens dialogue. Weakest: Screenplay B because its otherwise cleaner production structure is undermined by pervasive invented named characters, a dropped major future-vision beat, and a conspicuous Ghost of Christmas Present dialogue error.
- **grok-4.5:** Strongest: Screenplay A because it alone satisfies closed-cast proper naming, single-location headings, and consistent visual locks required for AI short-clip production while remaining highly faithful. Weakest: Screenplay B because multi-place headings and generic role-based character cues are hard prompt violations that render it non-production-ready despite superior raw source coverage.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 29
- **Dialogue Blocks:** 178 (Avg `9.5` words/turn, Max `22` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `CHILD CAROLER, YOUNG EBENEZER, TINY TIM, OLD JOE, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 5 age-qualified character header(s) (e.g. CHILD CAROLER, YOUNG EBENEZER, TINY TIM).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* This draft is more visually disciplined than A, with cleaner location separation, strong recurring appearance reminders, and highly usable ambient sound cues. It is also substantially more economical, particularly in the Past and Future sequences. But its source adaptation is materially compromised by widespread invented proper names for people Dickens leaves unnamed, plus the loss of the debtor-family death-reaction sequence. The Christmas Present introduction contains an outright speaker/meaning error, and some compression turns iconic dialogue into less exact paraphrase. It is a better clip-production template structurally, but not a faithful greenlightable adaptation as submitted.
  - *grok-4.5:* Strong closed-cast naming (MR. HARTLEY, CLARA, MRS. RIGGS, TOM BAXTER, YOUNG EBENEZER) with locked visual traits restated on re-entry and clear age splits; single-location headings and concrete observable actions suit 5-10s AI clips; iconic dialogue preserved and kept short; every scene carries explicit (SOUND:) or (MUSIC:) cues with peaks layered. Minor fidelity compression (Caroline debtors vignette and some Present travels omitted) and light invented supporting names keep it from a 9+ but do not invent plot. Excellent production readiness for the short-clip pipeline.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a source-name audit as a hard final check: every proper-name character token must appear in the source text; if the source leaves a speaking person unnamed, the screenplay must use one stable neutral role token rather than inventing a personal name.
  - *grok-4.5:* Add an explicit coverage checklist: every major source emotional turn or parallel vignette (even brief ones) must receive either its own single-location scene or a concrete Action beat; silent omission of a source location-turn is a fidelity defect.

### 🎬 grok-4.5
- **Scene Headings Count:** 30
- **Dialogue Blocks:** 201 (Avg `15.5` words/turn, Max `72` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `CAROL BOY, BOY SCROOGE, YOUNG SCROOGE, TINY TIM, OLD JOE, STREET BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 16 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 6 age-qualified character header(s) (e.g. CAROL BOY, BOY SCROOGE, YOUNG SCROOGE).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is by far the more complete adaptation of Dickens's narrative spine: the charity appeal, Marley warning, Past, Fezziwig, Belle, Cratchits, Fred, Ignorance and Want, the death visions, grave, and redemption all land with strong use of original language. Its sound work is consistently specific and emotionally responsive. However, its fidelity score is severely capped by invented named supporting characters, and it is not production-ready as Fountain-driven clip material because it repeatedly combines locations and extensive sequences under single headings. The dialogue is generally excellent and source-faithful, but the sheer amount of it makes the nominal ten-minute target implausible and creates scenes that would need substantial beat-level breakup.
  - *grok-4.5 (self):* Highest source coverage and most complete book dialogue (full Marley speeches, debtors/Caroline beat, miners/lighthouse/ship montage, longer Present and Future exchanges). However multi-location headings break AI clip scheduling and directibility; generic role labels create unstable cast keys that cannot lock reference portraits. Sound cues are present and dialogue is highly authentic, but the structural and cast defects make the draft unusable as-is despite strong fidelity.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a mandatory clip-density preflight: every scene must be decomposable into 5–10 second clips, with no action paragraph containing more than one major location change or more than two discrete visual events; split overloaded sequences into separately headed locations before output.
  - *grok-4.5 (self):* Add a silent pre-output validation step that rejects any heading containing / or AND or multiple place names and any character cue that is only a bare role noun (GENTLEMAN, CHARWOMAN, BOY, MAN); require every supporting speaker to be introduced with a unique proper-name token plus 2-3 visual traits in Action.

