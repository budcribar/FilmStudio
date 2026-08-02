# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 01:23:55 UTC*  
*Source Story File: `The_Tell-Tale_Heart.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **claude-sonnet-5** | **88.9** | 94.5% | 85.0% | 37 pts | 2.7 |
| 🥈  | **grok-4.5** | **86.6** | 85.5% | 87.0% | 43 pts | 1.9 |
| 🥉  | **gemini-2.5-flash** | **85.8** | 85.5% | 86.0% | 40 pts | 2.3 |
| 4.  | **gpt-4o-mini** | **78.4** | 95.5% | 67.0% | 21 pts | 5.0 |
| 5.  | **o3-mini** | **76.1** | 90.2% | 67.0% | 18 pts | 5.4 |
| 6.  | **gpt-4o** | **75.5** | 90.2% | 66.0% | 17 pts | 5.6 |
| 7.  | **grok-4** | **75.0** | 85.5% | 68.0% | 20 pts | 5.1 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **claude-sonnet-5** | 100% | 100% | 95% | 8.7/10 | 8.9/10 | 8.3/10 | 8.4/10 | 8.7/10 | 8.1/10 |
| **grok-4.5** | 100% | 100% | 50% | 8.9/10 | 8.6/10 | 8.8/10 | 8.6/10 | 8.9/10 | 8.4/10 |
| **gemini-2.5-flash** | 100% | 100% | 50% | 8.9/10 | 8.9/10 | 8.4/10 | 8.3/10 | 8.6/10 | 8.4/10 |
| **gpt-4o-mini** | 100% | 100% | 100% | 7.4/10 | 6.2/10 | 6.6/10 | 6.6/10 | 6.9/10 | 6.4/10 |
| **o3-mini** | 95% | 100% | 80% | 7.1/10 | 6.2/10 | 6.6/10 | 6.6/10 | 6.9/10 | 6.5/10 |
| **gpt-4o** | 95% | 100% | 80% | 7.5/10 | 5.9/10 | 6.8/10 | 6.6/10 | 6.6/10 | 6.1/10 |
| **grok-4** | 100% | 60% | 90% | 7.6/10 | 6.4/10 | 6.6/10 | 6.8/10 | 6.9/10 | 6.5/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-4o-mini**:
  - grok-4.5: Near-total absence of consistent visual character descriptions
  - grok-4.5: Heavy compression drops seven-night buildup and morning kindness beats
  - grok-4.5: Monologue delivered as on-camera dialogue while performing actions creates unfilmable crammed beats
  - grok-4: Excessive repeated identical scene headings; multiple actions crammed per scene
- **o3-mini**:
  - grok-4.5: Significant dropped beats and timeline muddle (Who's there appears without clear eighth-night setup)
  - grok-4.5: Heavy paraphrase plus invented dawn timing and officer business
  - grok-4.5: Weak inconsistent character visuals
  - grok-4: Sloppy metadata (Author: Author); vague action lines and internal monologue not filmable in clips
- **gpt-4o**:
  - gpt-4o: Inconsistent pacing and character descriptions
  - gpt-4o-mini: Pacing issues lead to a lack of tension.
  - grok-4.5: Narrator speaks stage-direction lines aloud (e.g. 'With a loud yell I threw open...') rendering dialogue unusable
  - grok-4.5: Virtually no consistent physical descriptions for casting lock
  - grok-4: Repeated identical scene headings violate single-clip constraint; minimal character visual specificity
- **grok-4**:
  - grok-4: Over-condensed beats cram multiple actions into single headings; insufficient character descriptions
  - gemini-2.5-flash: Action lines too condensed, combining multiple distinct beats into single descriptions, making it impossible to break into 5-10 second clips per beat as required by the production constraint.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | claude-sonnet-5 | grok-4.5 | gemini-2.5-flash | gpt-4o-mini | o3-mini | gpt-4o | grok-4 |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-4o** | 7.8 | 8.8 | 8.0 | 7.7 | 7.7 | **6.5** *(self)* | 8.7 |
| **gpt-4o-mini** | 7.7 | 8.7 | 7.8 | **8.2** *(self)* | 7.8 | 7.2 | 7.7 |
| **o3-mini** | 8.9 | 8.2 | 8.8 | 7.8 | **7.7** *(self)* | 7.7 | 7.2 |
| **grok-4.5** | 8.2 | **8.8** *(self)* | 9.2 | 4.4 | 4.8 | 5.8 | 7.2 |
| **grok-4** | 8.9 | 8.8 | 8.6 | 5.9 | 5.2 | 6.0 | **5.7** *(self)* |
| **claude-sonnet-5** | **8.7** *(self)* | 7.8 | 8.4 | 5.1 | 6.0 | 5.0 | 5.4 |
| **gemini-2.5-flash** | 9.3 | 10.0 | **9.5** *(self)* | 7.8 | 7.5 | 7.9 | 5.7 |

### 🧐 Self-Bias Analysis
- ⚠️ gpt-4o-mini rated its own screenplay 8.2/10 vs. a 6.4/10 average from 6 other judge(s) (+1.8) — possible self-preference bias.
- ⚠️ o3-mini rated its own screenplay 7.7/10 vs. a 6.5/10 average from 6 other judge(s) (+1.2) — possible self-preference bias.
- ℹ️ grok-4 rated its own screenplay 5.7/10 vs. a 7.0/10 average from 6 other judge(s) (-1.3) — notably harsher on itself than peers were.
- ⚠️ gemini-2.5-flash rated its own screenplay 9.5/10 vs. a 8.5/10 average from 6 other judge(s) (+1.0) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-4o:** Strongest: Screenplay E because it offers exceptional fidelity, directibility, and sound design, creating a compelling and immersive experience. Weakest: Screenplay F because it struggles with pacing and character consistency, impacting its overall effectiveness and production readiness.
- **gpt-4o-mini:** Strongest: Screenplay B because it excels in adaptation fidelity and dialogue authenticity, creating a compelling narrative. Weakest: Screenplay F because it has significant pacing issues that hinder its overall effectiveness and production readiness.
- **o3-mini:** Strongest: Screenplay F because it provides exceptional visual direction, sound design, and pacing perfectly suited for short AI-generated video clips. Weakest: Screenplay G because its minimal character details and rushed, overly condensed pacing undermine its cinematic potential.
- **grok-4.5:** Strongest: Screenplay F because it delivers the fullest source coverage, richest consistent character locks, and most clip-ready observable actions with superior sound beds. Weakest: Screenplay B because it lacks usable character descriptions, drops key buildup beats, and forces unfilmable monologue-as-dialogue inside crammed scenes.
- **grok-4:** Strongest: Screenplay A because it delivers the highest fidelity, consistent character visuals, and clip-ready action lines without any disqualifiers. Weakest: Screenplay G because repetitive headings and crammed beats make it unusable for the 5-10s clip constraint.
- **claude-sonnet-5:** Strongest: Screenplay E, because it combines the most consistent, castable character descriptions, the most escalation-driven sound design, and the cleanest single-action-per-clip breakdown while adding only tasteful, low-risk dialogue embellishments. Weakest: Screenplay D, because it reduces nearly every beat to a flat, repeated slugline with no character description, no named/distinguished officers, and no escalation architecture, making it unusable for consistent AI casting or paced clip generation despite technically touching all plot points.
- **gemini-2.5-flash:** Strongest: Screenplay G because it demonstrates exceptional fidelity, meticulous directibility for short clips, and comprehensive character and sound detail. Weakest: Screenplay F because its extreme condensation of action lines makes it unusable for the required 5-10 second clip per beat constraint.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 claude-sonnet-5
- **Scene Headings Count:** 9
- **Dialogue Blocks:** 36 (Avg `20.6` words/turn, Max `39` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ 1 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay D provides clear character disambiguation and maintains a strong narrative flow, though the directibility could be improved for better visual storytelling.
  - *gpt-4o-mini:* Screenplay E is a competent adaptation but lacks the intensity and clarity found in stronger candidates.
  - *o3-mini:* Screenplay F excels with precise visual directions, rich character details, and dynamic sound cues perfectly tailored for 5-10 second video segments, making it highly production-ready.
  - *grok-4.5:* Strong fidelity to all major beats with only minor framing invention (confession room) and named officers; consistent visual locks for Narrator and Old Man; mostly clip-friendly actions though some denser sequences; authentic Poe dialogue; solid heartbeat/creak beds but thin explicit music direction.
  - *grok-4:* Highly faithful with precise visual beats, consistent narrator and old man descriptions, excellent short-clip action lines and tension build.
  - *claude-sonnet-5 (self):* The most cinematically confident adaptation: vivid, consistent character descriptions (gaunt Narrator in buttoned dark coat, frail white-haired Old Man, individually distinct officers Moran/Duffy/Kelly) that hold throughout. Bookend 'Confession Room' framing gives strong structure and closure. Action lines are consistently concrete, single-beat, and camera-observable (hand trembling on lantern, palm pressing floor seams smooth), ideal for short-clip generation. Added minor officer small talk ('It has stood longer than I have') is a tasteful, low-risk embellishment that enhances subtext without inventing plot. Sound design is the most developed of all candidates, with heartbeat, chair-scrape, and candle-guttering cues building a clear emotional arc.
  - *gemini-2.5-flash:* Screenplay C offers an exceptionally faithful adaptation, using a 'Confession Room' framing device effectively. Character descriptions are highly detailed and consistent, aiding casting clarity. Directibility is strong with concise, visual action lines. Pacing is well-managed, and sound design cues are integrated to enhance the narrative tension and atmosphere.

### 🎬 grok-4.5
- **Scene Headings Count:** 11
- **Dialogue Blocks:** 18 (Avg `34.7` words/turn, Max `70` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ High average dialogue length (34.7 words/turn); speech beats risk clip overrun.
  - ⚠️ 9 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay E stands out with exceptional adaptation fidelity, directibility, and sound design, creating a compelling and immersive experience.
  - *gpt-4o-mini:* Screenplay B excels in adaptation fidelity and dialogue authenticity, creating a compelling and directable narrative.
  - *o3-mini:* Screenplay A adheres closely to Poe's text with detailed scene settings and clear character design, though a few sequences might need tighter pacing to suit 5-10 second video clips.
  - *grok-4.5 (self):* Extremely faithful with only minor illustrative day beat; strong locked visuals for all principals; highly directible concrete actions and clear tension ladder; authentic dialogue; good period sound beds though slightly less granular than F.
  - *grok-4:* Excellent directibility and pacing with tight visual actions and consistent gothic tone.
  - *claude-sonnet-5:* Strong bookend structure (candlelit confession room) and generally faithful adaptation, but adds an invented daytime tea-pouring scene and invented Old Man dialogue not present in the source — a minor but real embellishment that slightly dings fidelity. Narrator description is good (lean, pale, threadbare waistcoat) but the parenthetical note '(younger coat, same man)' introduces ambiguity about visual continuity across time. Directibility is strong with clear, single-action beats. Pacing benefits from the added quiet scene as contrast before escalation. Sound cues (bell, hinges, heartbeat) are present but not tightly escalated compared to B/E.
  - *gemini-2.5-flash:* Screenplay G is an exceptional adaptation, achieving perfect scores across all dimensions. It demonstrates unparalleled fidelity to the source, provides meticulous character and sound detail, and its action lines are precisely crafted for AI video directibility, ensuring each beat can be rendered as a distinct 5-10 second clip. The pacing and structure are masterful.

### 🎬 gemini-2.5-flash
- **Scene Headings Count:** 9
- **Dialogue Blocks:** 37 (Avg `40` words/turn, Max `70` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ High average dialogue length (40 words/turn); speech beats risk clip overrun.
  - ⚠️ 22 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay G delivers a balanced adaptation with consistent character descriptions and effective pacing, making it suitable for production.
  - *gpt-4o-mini:* Screenplay D captures the essence of the story but struggles with pacing and directibility in certain sections.
  - *o3-mini:* Screenplay D offers rich visual staging, detailed character cues, and excellent sound design, though its complexity may require careful division into short, discrete clips.
  - *grok-4.5:* Near-complete source coverage with precise visual actions per beat; richest consistent casting descriptions (age, build, clothing, officer distinctions); excellent show-don't-tell and separable 5-10s clips; pure Poe dialogue; densest usable SFX beds (creaks, THUMP-THUMP, chimes) supporting arc.
  - *grok-4:* Strong fidelity and character consistency with detailed visuals, though slightly longer than ideal for 5-10s clips.
  - *claude-sonnet-5:* Extremely thorough coverage of the text with almost every original beat dramatized (seven-night ritual, morning cheerfulness, chair-grating climax). Strong, consistent physical descriptions for Narrator, Old Man, and each named officer (stern/older, young/observant, burly/quiet) that hold up on reappearance. Scene breakdown is granular and mostly filmable in short clips, though some blocks still lean on dense V.O. paragraphs. Sound design explicitly escalates the heartbeat (THUMP-THUMP-THUMP) and chair-screech, giving strong cues for a sound designer, though no distinct musical/score direction is offered.
  - *gemini-2.5-flash (self):* Screenplay E is an outstanding adaptation, excelling in fidelity, character clarity, and directibility. Its action lines are meticulously detailed for short clips, and the pacing builds tension effectively. Character descriptions are precise and consistent, and sound design cues are expertly woven into the narrative, creating a rich sensory experience.

### 🎬 gpt-4o-mini
- **Scene Headings Count:** 6
- **Dialogue Blocks:** 32 (Avg `12.8` words/turn, Max `26` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay B offers a solid adaptation with good pacing and directibility, though character descriptions could be more distinct. The sound design effectively complements the narrative.
  - *gpt-4o-mini (self):* Screenplay A effectively captures the essence of Poe's narrative while maintaining a strong visual direction and character clarity.
  - *o3-mini:* Screenplay B remains faithful and maintains tension well, but its sparser visual cues and generic character descriptions may complicate segmentation into short clips.
  - *grok-4.5:* Major fidelity losses from rushed structure and omitted source beats; characters lack any reusable physical locks; directibility fails because internal states and multi-actions share headings; dialogue feels forced; minimal usable sound beds.
  - *grok-4:* Repetitive headings and poor clip segmentation break the short-video requirement.
  - *claude-sonnet-5:* Adds an invented shriek line for the Old Man ('Ahhh!') and repeatedly has the Narrator's dialogue restate the action just shown on screen (double-telling), which weakens the show-don't-tell requirement. No physical description is given for the Narrator beyond 'eyes wide with fear and excitement,' and the Old Man and officers (only one named, Reynolds) are essentially undifferentiated, making consistent casting difficult. Scene headers repeat generically ('NIGHT - LATER', 'MOMENTS LATER') without strong escalation, and sound design/heartbeat cues are present but thin and unscored for musical arc.
  - *gemini-2.5-flash:* Screenplay A provides a solid adaptation with good fidelity to the source text, utilizing V.O. effectively to convey the narrator's internal thoughts. Action lines are generally directable, though some scenes combine multiple distinct actions that might challenge the 5-10 second clip constraint. Character descriptions are somewhat generic, and explicit sound design cues could be more frequent to enhance the atmosphere.

### 🎬 o3-mini
- **Scene Headings Count:** 8
- **Dialogue Blocks:** 0 (Avg `0` words/turn, Max `0` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay A maintains strong fidelity to the source material, with clear character descriptions and effective dialogue. However, the pacing could be improved to better suit the short clip format.
  - *gpt-4o-mini:* Screenplay G presents a strong adaptation with good pacing and character clarity, though it could use more tension.
  - *o3-mini (self):* Screenplay E remains faithful in narrative beats and mood but suffers slightly from generic character details and a minor credit error, reducing its overall cinematic polish for short clips.
  - *grok-4.5:* Fidelity suffers from omissions, paraphrasing and small inventions; casting locks are thin; several beats remain internal or multi-action; pacing uneven; dialogue loses Poe cadence; sound beds present but generic.
  - *grok-4:* Significant structural and specificity issues make it unusable as-is.
  - *claude-sonnet-5:* Covers the core beats but compresses the seven-night ritual and the morning-visit beat into brief V.O. summary rather than dramatized action, weakening coverage. The odd choice of putting the opening narration in parentheses (as if stage direction rather than spoken dialogue) is a formatting misstep. Neither the Narrator nor the Old Man receive any physical/wardrobe description, and named officers (Reynolds, Merrick, Davis) are never visually distinguished, hurting castability. Actions are mostly filmable but some scenes stack multiple beats (pacing + dialogue + heartbeat) into one heading. Sound design is only lightly sketched (creaks, muffled heart) with no real escalation architecture.
  - *gemini-2.5-flash:* Screenplay B maintains good adaptation fidelity, but its directibility is hampered by some action lines that are too dense, combining several distinct visual beats into one. The formatting for V.O. is also non-standard, and character descriptions lack the specificity needed for consistent AI casting. Pacing is adequate, but could be smoother.

### 🎬 gpt-4o
- **Scene Headings Count:** 20
- **Dialogue Blocks:** 0 (Avg `0` words/turn, Max `0` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o (self):* Screenplay F struggles with pacing and character consistency, impacting its overall effectiveness and production readiness.
  - *gpt-4o-mini:* Screenplay F has significant pacing issues that hinder its overall effectiveness and production readiness.
  - *o3-mini:* Screenplay C is streamlined and faithful to the source, yet its minimal character visuals and somewhat bland dialogue reduce its cinematic impact for rapid video sequencing.
  - *grok-4.5:* Beat coverage is high and short scenes suit clip length, but character visuals are nearly nonexistent and several lines turn action description into spoken dialogue, breaking authenticity and performability.
  - *grok-4:* Covers plot but lacks detail, inconsistent formatting, and weak visual/action specificity for AI clips.
  - *claude-sonnet-5:* Technically hits most plot beats but does so with almost no dramatization — nearly every scene is a repeated 'INT. OLD MAN'S BEDROOM - NIGHT' slug with a single flat description, and the morning-visit beat is dropped. No physical descriptions are given for Narrator or Old Man, and officers are entirely generic/unnamed, making casting reference images impossible. Directibility is workable purely because beats are short, but the lack of visual specificity leaves too much to interpretation. Pacing feels flat and rushed with no escalation architecture, and sound design is essentially unaddressed beyond naming the heartbeat sound.
  - *gemini-2.5-flash:* Screenplay D demonstrates good adaptation fidelity and strong directibility due to its very short, single-action scenes, which align well with the clip constraint. However, character disambiguation is weak, with generic descriptions for most characters. Sound design is implied through V.O. but lacks explicit prompting in action lines.

### 🎬 grok-4
- **Scene Headings Count:** 4
- **Dialogue Blocks:** 11 (Avg `22.4` words/turn, Max `57` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `OLD MAN`
- **Diagnostics & Warnings:**
  - ⚠️ Too few scene headings (4); story lacks visual scene progression.
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. OLD MAN).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay C excels in adaptation fidelity and directibility, with strong pacing and sound design that enhance the story's tension and atmosphere.
  - *gpt-4o-mini:* Screenplay C presents a solid adaptation but lacks the dramatic tension and pacing of the top candidates.
  - *o3-mini:* Screenplay G, while faithful to the source, is overly condensed with minimal character detailing and rushed pacing, limiting its cinematic expressiveness in short clip format.
  - *grok-4.5:* Efficient compression of seven nights works for short clips without inventing plot; basic but consistent Narrator look; clean observable actions and tight escalation; dialogue stays close to source; sound limited mainly to heartbeat.
  - *grok-4 (self):* Too summarized for short-clip format, drops nuance and visual clarity.
  - *claude-sonnet-5:* Covers the major plot points but relies on explicit montage language ('Seven nights pass in quick succession') which is fundamentally at odds with the one-beat-per-clip constraint, since it describes repeated action rather than a single filmable moment. Minimal descriptions for Narrator ('lean man in dark coat') and none for the Old Man beyond the eye; officers are unnamed and undifferentiated. Dialogue is sparse, mostly compressed V.O., limiting performability. Sound cues are present but underdeveloped compared to top-tier candidates.
  - *gemini-2.5-flash:* Screenplay F suffers from excessive condensation, sacrificing fidelity and directibility. Many action lines combine multiple events or summarize long periods, making it unusable for the short-clip production constraint. Character descriptions are minimal, and the rapid pacing undermines the story's psychological tension.

