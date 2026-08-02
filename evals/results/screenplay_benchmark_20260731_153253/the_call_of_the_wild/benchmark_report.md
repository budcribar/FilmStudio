# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 22:05:01 UTC*  
*Source Story File: `The_Call_of_the_Wild.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-sol** | **86.7** | 99.8% | 78.0% | 4 pts | 1.0 |
| 🥈  | **grok-4.5** | **82.0** | 99.5% | 70.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-sol** | 100% | 100% | 95% | 5.2/10 | 8.8/10 | 8.0/10 | 7.8/10 | 8.8/10 | 8.2/10 |
| **grok-4.5** | 100% | 100% | 90% | 8.5/10 | 6.0/10 | 5.0/10 | 6.5/10 | 8.5/10 | 7.8/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-sol**:
  - gpt-5.6-sol: The source's unnamed man in the red sweater is assigned the invented personal name SMITH; under the stated fidelity rule, an invented named character is a disqualifying source alteration for this draft.
- **grok-4.5**:
  - gpt-5.6-sol: The same source character is keyed first as STRANGER and later as KIDNAPPER, creating two cast identities for one person.
  - gpt-5.6-sol: Several headings combine incompatible locations, times, or multi-day sequences, including BAGGAGE CAR / SAN FRANCISCO SHED, SKAGUAY STREET / TRAIL SOUTH, WILDERNESS TRAIL AND SPRUCE CAMP - MONTHS - DAY/NIGHT, and SUMMER DAY / WINTER NIGHT; this is unusable as-is for location-locked, beat-level clip generation.
  - grok-4.5: Multiple compound multi-location/multi-time scene headings (e.g. INT. BAGGAGE CAR / SAN FRANCISCO SHED; EXT. TRAIL AND CAMP - NORTHLAND - DAY/NIGHT; EXT. SKAGUAY STREET / TRAIL SOUTH; EXT. WILDERNESS TRAIL AND SPRUCE CAMP - MONTHS - DAY/NIGHT; EXT. OPEN SPACE AMONG TREES - SUMMER DAY / WINTER NIGHT) that violate single-place/single-purpose scheduling and break short-clip directibility
  - grok-4.5: Unstable supporting identity: STRANGER vs KIDNAPPER for the same kidnapping arc creates a closed-cast split

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-sol | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-sol** | **7.2** *(self)* | 6.6 |
| **grok-4.5** | 8.3 | **7.5** *(self)* |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-sol rated its own screenplay 7.2/10 vs. a 8.3/10 average from 1 other judge(s) (-1.1) — notably harsher on itself than peers were.

### 🗣️ Judge Summary Notes
- **gpt-5.6-sol:** Strongest: Screenplay B because its cast continuity, clip-level staging, dialogue, and dramatic construction are substantially cleaner despite its disqualifying invented name and reduced source coverage. Weakest: Screenplay A because its broad fidelity is undermined by unstable cast tokens and repeated multi-location, multi-time scene packages that are not usable as-is for beat-level AI video production.
- **grok-4.5:** Strongest: Screenplay A because it is the only draft that is consistently single-location, cast-locked, and short-clip-directible while still carrying the full emotional spine. Weakest: Screenplay B because compound multi-place/multi-time headings and the STRANGER/KIDNAPPER split make it unusable for the AI clip pipeline despite slightly fuller book coverage.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-sol
- **Scene Headings Count:** 18
- **Dialogue Blocks:** 42 (Avg `16.1` words/turn, Max `38` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *gpt-5.6-sol (self):* This is the cleaner cinematic package. Buck's markings, weight changes, and physical recovery are repeatedly restated, while Thornton, Perrault, Francois, Spitz, Mercedes, Pete, Hans, Skeet, Nig, and the Wild Brother receive distinct and generally consistent visual identifiers. The action is mostly divided into short observable units, with especially effective construction of the Spitz fight, White River rescue, thousand-pound wager, massacre discovery, and final wolf confrontation. Dialogue is concise, performable, and overwhelmingly drawn from the book. Pacing is coherent, although removing the Lake Le Barge raid, Dolly's madness, Dave's death, and Thornton's Burton and river-rescue episodes leaves the Buck-Spitz rivalry and Buck-Thornton devotion less fully earned. The Forest Creek scene also crosses from night through dawn and traverses several landscapes under one heading, while the four-day moose hunt is reduced to a summary beat. Tailored sound beds are consistently strong and well interleaved at major peaks, but there are no genuine background-music prompts. Most importantly, naming the previously unnamed red-sweater dog breaker SMITH triggers the rubric's mandatory severe fidelity penalty.
  - *grok-4.5:* Strong closed-cast locking and consistent visual re-entry for Buck (sable coat, brown brows, white blaze restated across time jumps) and humans. Scenes are almost all single concrete locations with camera-observable beats sized for 5–10s clips. Book dialogue and dialect are preserved. Sound cues are concrete and present in every scene, with multi-cue density at peaks (Spitz kill, ice collapse, Yeehat attack). Fidelity is solid on the spine but compresses out Dave’s death, Dolly’s madness, and the Lake Le Barge husky raid—noticeable losses for a 10-minute cut, though not invented-plot failures. Pacing is tight and filmable end-to-end.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol (self):* Replace the current supporting-role naming rule with this exception: when the source leaves a person unnamed, use one stable, source-grounded descriptive token such as RED-SWEATER MAN and never fabricate a personal name. Proper personal names may be used only when supplied by the source.
  - *grok-4.5:* Add an explicit compression rule: when cutting a novel to ~10 minutes, preserve at least one secondary emotional death/loyalty beat per major act (not only protagonist peaks), and forbid dropping any beat the source treats as a full scene of grief or mastery unless runtime is otherwise impossible.

### 🎬 grok-4.5
- **Scene Headings Count:** 20
- **Dialogue Blocks:** 32 (Avg `15.6` words/turn, Max `46` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *gpt-5.6-sol:* This is the broader and more faithful condensation, preserving Curly's death, the starving-husky attack, Dolly's madness, Dave's death, Buck's leadership victory, the incompetent owners, Thornton's rescue, the wager, the wild brother, the moose hunt, Thornton's death, and Buck's final return to the pack. Dialogue and narration remain close to London's diction, and every scene has a purposeful sound cue. Its production form is substantially weaker: supporting dogs frequently appear without locked visual traits, one recurring human changes cast token, and numerous montage headings collapse different locations, seasons, and major events into single scenes. The wilderness and massacre passage is particularly overpacked, moving from a months-long journey to a four-day hunt and then the climactic camp attack without schedulable separation. The result has strong narrative momentum but often reads as a compressed synopsis rather than a sequence of discrete 5-10-second visual beats. Sound design is evocative, though background music is effectively absent until the final image.
  - *grok-4.5 (self):* Best source coverage of the two: keeps Dave’s death, Dolly, Lake Le Barge raid, and more trail texture while still hitting Spitz, Thornton, the pull, Yeehats, and Ghost Dog. Dialogue stays close to London. Weaknesses are structural for this pipeline: several headings smash two places or day/night/months into one slug, so beats cannot cleanly become 5–10s clips; character looks are locked once in Notes but rarely re-stated on re-entry; STRANGER/KIDNAPPER split is a cast-key defect. Sound design is generally good, with a strong closing MUSIC cue, but mid-film montage scenes under-specify interleaved audio. Not greenlightable as-is without a heading/cast pass.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-sol:* Add a final clip-granularity validation pass: each Action element may contain only one 5-10-second observable beat, and any location change, time-of-day change, or multi-day event must trigger a new concrete heading or an explicitly bounded montage. Reject slash-combined locations and DAY/NIGHT or seasonal combinations in a single slug.
  - *grok-4.5 (self):* Harden the scene-heading rule with an explicit ban on slash-combined places and DAY/NIGHT or multi-month time spans in a single heading; require one place + one time band per slug, and force any travel/montage into 2–3 separate single-location scenes or progressive Action under one stable heading.

