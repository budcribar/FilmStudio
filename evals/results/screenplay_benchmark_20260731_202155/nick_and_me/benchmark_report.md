# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-01 02:25:45 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **91.4** | 99.0% | 86.0% | 4 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **87.0** | 100.0% | 78.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 80% | 8.8/10 | 8.5/10 | 8.5/10 | 8.2/10 | 8.8/10 | 9.0/10 |
| **gpt-5.6-terra** | 100% | 100% | 100% | 7.5/10 | 7.2/10 | 8.2/10 | 7.5/10 | 8.2/10 | 8.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-terra**:
  - grok-4.5: Protagonist identity split across unstable tokens (NARRATOR in early Action/V.O. then PETER for all dialogue and later Action), violating closed-cast single-token rule and early name reveal before book ending
  - grok-4.5: Invented named supporting character MARTIN (book leaves boyfriend unnamed)

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gpt-5.6-terra |
| :--- | :---: | :---: |
| **grok-4.5** | **8.2** *(self)* | 7.6 |
| **grok-4** | 9.0 | 8.1 |

### 🗣️ Judge Summary Notes
- **grok-4.5:** Strongest: Screenplay A because it alone keeps a single stable NARRATOR token until the book’s final self-naming, delivers high source coverage with dense usable sound cues, and has no disqualifying cast or invention defects. Weakest: Screenplay B because the protagonist is fractured across NARRATOR and PETER tokens plus an invented named boyfriend, rendering the closed cast unusable for locked reference images despite strong clip-level directing.
- **grok-4:** Strongest: Screenplay B because its tighter scene rhythm and precise micro-beat structure best serve the short-clip constraint while preserving fidelity. Weakest: Screenplay A because its expanded scene count occasionally dilutes momentum under the short-clip constraint.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 28
- **Dialogue Blocks:** 75 (Avg `14.6` words/turn, Max `58` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG NICK`
- **Diagnostics & Warnings:**
  - ⚠️ 4 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. YOUNG NICK).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5 (self):* Strong fidelity to the book's first-person arc, late name reveal, key beats (Nick intro/Viking framing, Sionna meet, Ma decline, bar confrontation, crash, stabbing, Seattle aftermath, prison forgiveness) without major invented plot. NARRATOR token stays consistent until the final self-naming line. Visual locks for Nick (scars, reddish-brown hair, bulk), Sionna (pale skin, light-blue eyes, fish necklace, specific wardrobe), and Ma are mostly stable, with YOUNG NICK disambiguated; occasional re-entry after time jumps under-describes Narrator. Action is largely camera-observable and clip-sized, though several V.O.-heavy montages and multi-beat scenes (bar fight into run, stabbing aftermath) slightly strain single-clip directibility. Pacing compresses the long source effectively but rushes the post-stabbing recovery and Seattle transition. Dialogue stays close to book diction and short enough for clips. Every scene carries concrete (SOUND:) or (MUSIC:) cues, denser at peaks.
  - *grok-4:* Tight, filmable scenes with precise action lines, consistent audio cues, and faithful compression of the source arc.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5 (self):* Add an explicit hard cap: after any time-jump or multi-year cut, the first Action line of the new scene must restate 2-3 locked visual traits for every returning principal before any dialogue or V.O.
  - *grok-4:* Add an explicit rule requiring every scene to contain at most three distinct visual micro-beats separated by a single sound cue before the next heading.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 27
- **Dialogue Blocks:** 128 (Avg `8` words/turn, Max `19` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *grok-4.5:* Covers the same essential spine and many book phrases, but early and consistent use of PETER (plus Action calling the same figure both NARRATOR and PETER) directly contradicts the source's late name reveal and the single-token rule; inventing MARTIN is a further named-character addition. Visual descriptions are otherwise clear and age-split (twelve-year-old NICK, older leaner PETER, Sionna traits locked), yet the dual token makes casting reference images unusable. Directibility is strong—short observable actions, clean single-location headings, good micro-beats at the stabbing and prison. Pacing and scene rhythm feel tighter and less montage-dependent than A. Dialogue is performable and book-adjacent. Sound cues are present and specific in nearly every scene. The token fracture is a hard production blocker despite otherwise solid craft.
  - *grok-4:* Strong visual descriptions and consistent cast but expanded scene count and occasional dense beats reduce clip-friendly pacing.
- **Judge Prompt-Improvement Suggestions:**
  - *grok-4.5:* Strengthen the closed-cast rule with: 'The protagonist’s on-screen and cue token must be exactly one string for the entire draft; if the source withholds the given name until late or never, that token remains NARRATOR (or THE NARRATOR) in every Action mention and cue until the source itself utters the name—no mid-draft switch to a proper name.'
  - *grok-4:* Add an explicit rule requiring every scene to contain at most three distinct visual micro-beats separated by a single sound cue before the next heading.

