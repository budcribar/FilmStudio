# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 22:05:37 UTC*  
*Source Story File: `The_Call_of_the_Wild.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **86.2** | 99.8% | 77.0% | 4 pts | 1.0 |
| 🥈  | **grok-4.5** | **80.2** | 99.2% | 68.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 100% | 100% | 95% | 5.5/10 | 8.2/10 | 8.0/10 | 7.8/10 | 8.5/10 | 8.2/10 |
| **grok-4.5** | 100% | 100% | 85% | 8.8/10 | 5.8/10 | 4.2/10 | 5.5/10 | 8.2/10 | 8.0/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-sol**:
  - gpt-5.6-sol: The source's unnamed man in the red sweater is assigned the invented proper name SMITH, triggering the required severe fidelity penalty for an invented named character.
  - gpt-5.6-sol: The character is introduced as JOHN THORNTON in Action but uses THORNTON as the dialogue and recurrence token, violating the prompt's exact-token identity requirement.
- **grok-4.5**:
  - gpt-5.6-sol: Repeated multi-location and multi-time headings, including BAGGAGE CAR AND SAN FRANCISCO WATERFRONT SHED, CLIFF CREST AND FORTY-MILE RAPIDS, and EASTERN WILDERNESS AND SPRUCE-BOUGH CAMP, make the draft structurally unsuitable for one short clip per beat.
  - gpt-5.6-sol: Omnibus action paragraphs compress raids, deaths, journeys, and time jumps into single units that cannot be rendered as coherent 5–10-second clips.
  - gpt-5.6-sol: Several recurring characters, including John Thornton, Dave, Sol-leks, Mercedes, Pete, and Hans, lack sufficiently locked visual descriptions or re-entry traits for reliable reference-image continuity.
  - grok-4.5: Multiple compound/multi-place scene headings (e.g. BAGGAGE CAR AND SAN FRANCISCO WATERFRONT SHED; ORCHARD AND COLLEGE PARK FLAG STATION; NORTHLAND TRAIL AND CAMPS; CLIFF CREST AND FORTY-MILE RAPIDS; EASTERN WILDERNESS AND SPRUCE-BOUGH CAMP) violate the hard one-concrete-place rule and break short-clip scheduling
  - grok-4.5: Weak closed-cast tokens for speaking roles (STRANGER, SALOON-KEEPER, MAN IN RED SWEATER) without stable proper-name keys
  - grok-4.5: Several beats written as summary montage paragraphs rather than ordered filmable actions, unusable as discrete 5–10s clips

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-sol** | **7.0** *(self)* | 6.2 |
| **grok-4.5** | 8.4 | **7.3** *(self)* |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-sol rated its own screenplay 7.0/10 vs. a 8.4/10 average from 1 other judge(s) (-1.4) — notably harsher on itself than peers were.
- ⚠️ grok-4.5 rated its own screenplay 7.3/10 vs. a 6.2/10 average from 1 other judge(s) (+1.2) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-sol:** Strongest: Screenplay B because its disciplined scene construction, clearer visual identity locks, concise dialogue, and granular sound-supported action make it the more viable short-clip production draft despite its severe fidelity defect. Weakest: Screenplay A because its excellent source coverage is undermined by multi-location mega-scenes and overloaded action units that cannot be reliably segmented into coherent 5–10-second clips.
- **grok-4.5:** Strongest: Screenplay A because it alone delivers single-location, clip-ready scenes with consistently re-locked character looks and no format deal-breakers while still hitting the emotional spine. Weakest: Screenplay B because compound multi-place headings and summary-montage blocks make it unusable for the short-clip pipeline despite superior beat coverage.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 42 (Avg `16.1` words/turn, Max `38` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *gpt-5.6-sol (self):* This is the cleaner and more directable package: most scenes have one clear dramatic purpose, action is divided into usable visual increments, recurring appearances—especially Buck's changing physical condition—are described with strong continuity, and the sound beds are specific and well interleaved at major peaks. Dialogue is concise, performable, and generally preserves the book's wording. Fidelity is nevertheless capped at 3 under the stated rule because SMITH is an invented proper name, while the omission of Dave's death, the starving-dog raid, Dolly's madness, and Thornton's cliff and rapids episodes substantially reduces thematic and character coverage. A few scenes still summarize several days or cross terrain and time within one heading, but these are localized rather than systemic directibility problems.
  - *grok-4.5:* Strong closed-cast adaptation with locked, repeatedly restated visual identity for Buck (sable coat, brown muzzle/brows, white blaze, weight changes) and clear first-looks for Thornton, Hal, Charles, Mercedes, Perrault, Francois, Skeet, and Nig. Scene headings are single concrete places suitable for 5–10s clips; action is camera-observable; book dialect and iconic lines are preserved; every scene carries usable (SOUND:) beds and peaks get multiple cues. Fidelity is high on the spine (kidnap → club → Curly → Spitz death → lead → Hal ordeal → Thornton → pull → Yeehats → pack) but compresses away Dave’s harness death, the cliff jump, and the Forty-Mile rapids rescue, which costs a point on coverage without inventing plot. Pacing fits a ~10-minute cut without montage dumps. Production-ready as-is for the AI clip pipeline.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol (self):* Change the closed-cast rule so that an unnamed source figure must receive a stable source-grounded descriptive role token, never an invented proper personal name; the need for a cast key must not override source naming fidelity.
  - *grok-4.5:* Add an explicit compression rule: when cutting a long novel to short-film runtime, retain at least one concrete visual micro-beat for every major secondary death or iconic set-piece the source treats as a turning point; cut connective travel and repeated trail montage before cutting those peaks.

### 🎬 grok-4.5
- **Scene Headings Count:** 15
- **Dialogue Blocks:** 37 (Avg `14.8` words/turn, Max `56` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *gpt-5.6-sol:* This is the more comprehensive adaptation, covering nearly every essential stage of Buck's transformation, including Dave's death, the cliff and rapids incidents, the wager, the moose hunt, Thornton's death, and the final joining of the pack. Its retained dialogue and narration preserve London's diction well, although several voice-over passages are too long for a short clip. The principal failure is structural: multiple headings combine distinct locations, days, and major sequences, while individual action paragraphs frequently contain enough material for several clips. Explicit, atmospheric sound cues appear throughout, but their usefulness is reduced when a single scene encompasses an entire montage or multiple set pieces.
  - *grok-4.5 (self):* Highest source coverage of the two: includes baggage-car struggle, Dave dying in harness, cliff whim-jump, rapids rope rescue, hairy-man visions, and full Yeehat/pack close, with strong book-voice dialogue and solid (SOUND:)/(MUSIC:) design including a strong aurora pack-song button. However, directibility collapses under compound location headings and DAY AND NIGHT slugs that cram multiple places and times into one unit, plus prose-summary stretches (“Mail train. Heavy toil. Dave…”) that are not shot lists. Character looks are locked once in Notes for Buck but rarely restated on re-entry; supporting speakers stay epithet-tokens rather than cast keys; Pete/Hans arrive thin. Excellent fidelity raw material that is not production-schedulable without a structural rewrite of headings and montage blocks.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol:* Add a HARD clip-atomization rule: each action paragraph must represent one continuous, camera-observable action that fits a single 5–10-second clip in one place and time; any second location, time jump, montage phase, or separate action must become a new action beat or scene heading.
  - *grok-4.5 (self):* Make the single-place heading rule fail-closed with an explicit ban on compound headings that join two places with AND, slash, or comma, and require any multi-place sequence to be split into separate scene headings each followed by its own grounding Action line before dialogue or V.O.

