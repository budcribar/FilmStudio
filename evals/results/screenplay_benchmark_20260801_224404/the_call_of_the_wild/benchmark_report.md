# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 04:48:26 UTC*  
*Source Story File: `The_Call_of_the_Wild.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-terra** | **87.6** | 100.0% | 79.0% | 3 pts | 1.5 |
| 🥈  | **grok-4.5** | **87.1** | 99.8% | 79.0% | 3 pts | 1.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-terra** | 100% | 100% | 100% | 7.4/10 | 8.4/10 | 8.0/10 | 7.6/10 | 8.2/10 | 8.0/10 |
| **grok-4.5** | 100% | 100% | 95% | 8.8/10 | 8.2/10 | 6.3/10 | 7.0/10 | 8.6/10 | 8.4/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-terra**:
  - gpt-5.6-terra: The required VISION_META sidecar is absent, despite the shared prompt designating it HARD output.
  - gpt-5.6-terra: The repeated "NARRATOR V.O." cues are not valid Fountain character cues under the prompt's required NARRATOR (V.O.) format, risking narration being parsed incorrectly downstream.
  - gpt-5.6-terra: The script contains a concrete setting error, "INT. SEATTLE BACK YARD - DAY," for an exterior yard scene.
- **grok-4.5**:
  - gpt-5.6-terra: The required VISION_META sidecar is absent, despite the shared prompt designating it HARD output.
  - gpt-5.6-terra: It violates the one-location-per-heading rule with "EXT. SEATTLE DOCK / DECK OF THE NARWHAL - DAY."
  - gpt-5.6-terra: Several scenes combine too many separate plot turns, locations, and actions to function as discrete 5-10 second AI-video beats, especially the Seattle/Narwhal transition, the White River rescue-and-collapse sequence, and the final Yeehat attack through wolf-pack ending.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-terra | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | **7.6** *(self)* | 7.3 |
| **grok-4.5** | 8.2 | **8.4** *(self)* |

### 🧐 Self-Bias Analysis
- ⚠️ grok-4.5 rated its own screenplay 8.4/10 vs. a 7.3/10 average from 1 other judge(s) (+1.1) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay B because its leaner scene construction, clearer casting locks, and more clip-ready action make it the more practical production draft despite meaningful source omissions. Weakest: Screenplay A because, although it is considerably more faithful, it is overstuffed with multiple major actions per scene and contains a direct one-location-heading violation that undermines short-clip execution.
- **grok-4.5:** Strongest: Screenplay B because it preserves the fullest source arc, richest dialect, and most iconic beats with superior cast locking and sound density. Weakest: Screenplay A because, while cleaner for single-location AI clips, it compresses away several emotionally load-bearing middle beats (Dave, Dolly, cliff test, Francois farewell) and is thinner on secondary-character coverage.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 15
- **Dialogue Blocks:** 36 (Avg `8.8` words/turn, Max `24` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* This is the stronger production draft because it is materially cleaner, more restrained, and more shootable in short AI-video clips. It gives Buck, Spitz, Thornton, Skeet, and Nig useful stable visual identifiers; uses clear, compact action; and maintains a strong visual/sound rhythm through the major set pieces. Its major flaw is source coverage. The draft jumps from initial training to the Lake Le Barge raid and then directly to the Spitz fight, dropping much of Buck's survival adaptation and the escalating team conflict. It also omits the mail-team/Dave material and compresses Hal, Charles, and Mercedes's long deterioration so heavily that Buck's near-death and Thornton's rescue lose cumulative force. The closing also removes Buck's prolonged moose hunt and reduces the wilderness evolution that makes his final choice feel earned. Dialogue remains mostly faithful to London, though some selected narration is functional rather than especially distinctive. The sound beds are concrete and well distributed, but the screenplay is not fully compliant Fountain because its narrator notation is malformed.
  - *grok-4.5:* Tight, filmable spine that hits the core arc (kidnapping, club law, Curly, Spitz death-fight, leadership, Hal/Mercedes collapse, Thornton love, pull wager, Yeehat massacre, pack join) with clean single-location headings and consistent Buck visual locks (brown-black coat, white chest blaze) restated when he returns gaunt. Book dialect and iconic lines are largely preserved. Weakness is compression that drops or elides load-bearing middle beats (Dave’s trace death, Dolly’s madness, the cliff loyalty jump, Francois’s farewell, hairy-man visions), so fidelity is strong on the spine but not complete. Sound cues are present in every scene and double up at peaks; directibility is high because actions stay concrete and clip-sized.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a mandatory preflight source-coverage audit before output: identify the protagonist's required status changes, central rival conflict, lowest-point sequence, and final-choice causality, then ensure each has at least one dedicated on-screen beat rather than being skipped between set pieces.
  - *grok-4.5:* Add an explicit rule that every major character death, permanent exit, or loyalty-test beat present in the source must be retained as at least one concrete visual scene (even a short one), and that runtime cuts must come from connective travel/setup first—not from those spine emotional turns.

### 🎬 grok-4.5
- **Scene Headings Count:** 26
- **Dialogue Blocks:** 35 (Avg `13.6` words/turn, Max `46` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the more faithful adaptation by a substantial margin: it tracks Buck's full transformation from Miller estate king to kidnapped dog, trained sled dog, Spitz's rival and successor, exhausted mail dog, Thornton's devoted companion, and finally wolf-pack leader. It also preserves important intermediate material that Screenplay B omits, including the red-sweater lesson, hunger and adaptation, Dave's death, the failed Hal/Charles/Mercedes expedition, and Buck's moose hunt. However, its compression is often achieved by stacking an entire sequence of separate visual turns into one action block. That produces strong literary coverage but weak clip-level scheduling, uneven emotional breathing room, and a likely runtime above the stated ten-minute target. Buck is visually well locked at the opening and ending, but several recurring humans receive little or no stable physical re-entry description, and broad role labels such as STRANGER and SALOON-KEEPER are only minimally useful portrait keys. The dialogue is generally source-faithful and concise, while its sound work is consistently concrete and especially effective in the Spitz fight, river collapse, and final wolf sequence.
  - *grok-4.5 (self):* Most complete and book-faithful of the two: baggage car, saloon crate, first snow, Curly, nest theft raid, Dolly, Spitz fight with full tactics, leadership revolt, Dave’s revolver exit, Hal/Charles/Mercedes incompetence, Thornton rescue, cliff jump, rapids save, Eldorado wager, moose hunt, Yeehats, and Ghost Dog coda all land with strong dialect fidelity. Cast looks are locked early and reused (Buck’s 140-lb St. Bernard/shepherd build and white chest splash; Hal’s cartridge belt; Charles’s watery eyes/mustache). Sound/music density is excellent, including a MUSIC build on the Spitz kill. Main drag is directibility: multiple HARD heading violations join places with '/' or 'AND' (Seattle dock/Narwhal deck, trail fringe/camp, Chilcoot and lakes, Eldorado saloon/street, etc.) and some Action blocks montage several beats, which fights the 5–10s one-clip-per-beat pipeline even though the narrative itself is stronger.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a hard clip-beat rule: every Action paragraph must be playable as one 5-10 second shot/clip and may contain only one irreversible story action; when a sequence has multiple reversals, injuries, decisions, or location changes, split it into separate ordered Action beats or scenes.
  - *grok-4.5 (self):* Add a pre-output validation rule that any scene heading containing '/', 'AND', or two distinct place names is invalid and must be split into separate single-location headings (with movement folded into Action only when place does not change), and reject the draft until every heading names exactly one filmable place.

