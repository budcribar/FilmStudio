# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 04:31:15 UTC*  
*Source Story File: `A_Christmas_Carol.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-terra** | **85.2** | 100.0% | 75.0% | 4 pts | 1.0 |
| 🥈  | **grok-4.5** | **79.2** | 98.5% | 66.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-terra** | 100% | 100% | 100% | 5.2/10 | 8.2/10 | 8.0/10 | 7.8/10 | 7.8/10 | 8.2/10 |
| **grok-4.5** | 100% | 100% | 70% | 6.0/10 | 7.2/10 | 5.2/10 | 6.2/10 | 6.8/10 | 8.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-terra**:
  - gpt-5.6-terra: Invents several named source characters, including MR. HARKER, MR. WELLS, MR. BLAKE, MR. PIPER, and MR. GRANT, triggering the rubric's severe fidelity penalty.
  - gpt-5.6-terra: Drops the future debtors/Caroline sequence, a major source beat that demonstrates the relief caused by Scrooge's death.
  - gpt-5.6-terra: Uses unstable or noncompliant dialogue identities, including BELLE'S HUSBAND and the collective cue THE CRATCHITS, rather than independent stable cast tokens.
  - gpt-5.6-terra: The screenplay does not follow the mandated Fountain element-spacing syntax, making downstream parsing unreliable.
  - gpt-5.6-terra: The required VISION_META sidecar is absent.
- **grok-4.5**:
  - gpt-5.6-terra: Invents named merchant characters not present in the source (MR. HALES, MR. CHISWICK, MR. LUTTRIDGE), triggering the rubric's severe fidelity penalty.
  - gpt-5.6-terra: The screenplay does not follow the mandated Fountain element-spacing syntax, making downstream parsing unreliable.
  - gpt-5.6-terra: The required VISION_META sidecar is absent.
  - gpt-5.6-terra: Several headings and beats are not clip-directable as written, especially the combined "EXT. BLEAK MOOR / LIGHTHOUSE / SHIP - NIGHT" sequence, which contains multiple locations and events in one scene.
  - gpt-5.6-terra: Multiple dialogue turns substantially exceed the shared prompt's 35-word maximum.
  - grok-4.5: Scene headings that join multiple locations with slashes (EXT. BLEAK MOOR / LIGHTHOUSE / SHIP) and vague placeholders (INT. OPEN PLACE) violate the single-concrete-place HARD rule and make clip scheduling impossible
  - grok-4.5: Multiple dialogue turns exceed the 35-word hard limit without intervening action/sound micro-beats, breaking the short-clip constraint

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-terra | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | **6.5** *(self)* | 5.9 |
| **grok-4.5** | 8.6 | **7.3** *(self)* |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-terra rated its own screenplay 6.5/10 vs. a 8.6/10 average from 1 other judge(s) (-2.1) — notably harsher on itself than peers were.
- ⚠️ grok-4.5 rated its own screenplay 7.3/10 vs. a 5.9/10 average from 1 other judge(s) (+1.4) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay B because its scene segmentation, concise visual beats, character look-locking, and sound integration are materially more production-oriented for 5–10 second clips, despite its severe fidelity failures. Weakest: Screenplay A because, while it covers more of Dickens's plot, its overloaded scenes, multi-location heading, excessive dialogue lengths, and invalid downstream formatting make it less directly producible as an AI-clip screenplay.
- **grok-4.5:** Strongest: Screenplay A because it alone delivers single-location, clip-length action and dialogue while remaining production-clean and highly faithful. Weakest: Screenplay B because multi-location headings and over-length dialogue turns render it unusable for the short-clip AI pipeline without a full rewrite.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 25
- **Dialogue Blocks:** 157 (Avg `9.6` words/turn, Max `22` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG EBENEZER, YOUNG SCROOGE, TINY TIM, OLD JOE, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 5 age-qualified character header(s) (e.g. YOUNG EBENEZER, YOUNG SCROOGE, TINY TIM).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* B is more disciplined in scene construction and is generally better suited to short AI-video clips. It gives most principal recurring characters concrete, stable visual anchors, divides the story into clearer physical locations, and uses concise visual action with consistently useful sound beds. The pacing is stronger than A's because it reduces sprawling montage material and gives the major ghosts, Cratchits, grave reveal, and redemption clean turns. Its central adaptation problem is serious: it omits the Caroline/debtors sequence, which is a distinct and important proof that Scrooge's death brings relief rather than grief. It also invents multiple proper names for otherwise unnamed source figures, which the rubric expressly identifies as a severe fidelity failure. Dialogue is more clip-manageable than A's but occasionally paraphrases or adds material, and the collective CRATCHITS cue is not cast-safe. The sound work is consistently concrete and emotionally appropriate, though a few major scenes rely on a single general ambience rather than a more developed progression of sound and music beats.
  - *grok-4.5:* Strong, tight adaptation that hits every spine beat (counting-house, Marley, three spirits, Cratchit dinner, grave, redemption) with locked visual descriptions and YOUNG EBENEZER / FAN age splits restated on re-entry. Action lines are single-location, camera-concrete micro-beats ideal for 5-10 s clips; iconic dialogue is preserved but split to clip length; every scene carries explicit (SOUND:) or (MUSIC:) cues with peaks doubled. Minor compression drops the debtors/Caroline reaction and the miners-lighthouse ship tour, slightly lowering pure coverage, yet nothing invented breaks the plot and the result remains fully greenlightable.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Change the closed-cast naming rule so unnamed source speakers must use stable neutral role tokens rather than invented proper names, and explicitly prohibit collective dialogue cues; require each individual speaker to have one reusable non-possessive token.
  - *grok-4.5:* Add an explicit retention rule: when compressing for runtime, never wholly omit a secondary emotional-reaction beat that the source attaches to a death or fate reveal (list and keep at least one such beat if present) before cutting travel or montage material.

### 🎬 grok-4.5
- **Scene Headings Count:** 25
- **Dialogue Blocks:** 140 (Avg `19.9` words/turn, Max `74` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY SCROOGE, YOUNG SCROOGE, TINY TIM, BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 23 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 4 age-qualified character header(s) (e.g. BOY SCROOGE, YOUNG SCROOGE, TINY TIM).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the more complete adaptation of the two: it preserves nearly the entire emotional and narrative spine, including the charitable collectors, Marley, all three spirits, Fan, Fezziwig, Belle, the Cratchit celebration and bereavement, the debtors' relief, Old Joe, the grave, and Scrooge's redemption. However, invented named merchants impose a severe fidelity score cap under the stated rubric. Its visual descriptions are generally vivid and useful, with clear age separation for BOY SCROOGE and YOUNG SCROOGE, but recurring supporting characters are unevenly reintroduced and some collective or generic character identities are weak reference-image keys. The script is often highly cinematic, yet several enormous scenes compress too many beats for 5–10 second clips, notably the Cratchit dinner, the Past bedroom-to-memory progression, and the moor/lighthouse/ship/Fred montage. Dialogue is largely faithful Dickens, but long uninterrupted speeches are impractical at the required clip duration. Sound coverage is the strongest category: cues are specific, atmospheric, and particularly effective in the ghost scenes and emotional peaks.
  - *grok-4.5 (self):* Highest source coverage of the two—retains Caroline debtors, miners/lighthouse/ship, fuller Marley and Fezziwig text, and more Present travel—yet fatally undermines AI-video usability. Multi-location and vague headings cram distinct places into one slug; long unbroken speeches and summary Action paragraphs cannot be filmed as discrete 5-10 s clips; character tokens are mostly stable but supporting roles receive thinner re-entry descriptions. Sound cues are present and often strong, but the structural violations are deal-breakers.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a hard per-scene clip-density rule: each scene may contain no more than three distinct visual beats or one location-specific dramatic turn; split any montage, travel sequence, meal progression, or revelation that crosses locations into separate headed scenes.
  - *grok-4.5 (self):* Strengthen the compliance check to auto-reject any scene heading containing '/', 'AND', or multiple place names and to enforce splitting of every dialogue turn over 35 words with an intervening concrete Action or (SOUND:) line before output is accepted.

