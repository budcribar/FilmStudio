# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-07-31 01:32:31 UTC*  
*Source Story File: `The_Velveteen_Rabbit.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **88.5** | 91.5% | 86.0% | 43 pts | 1.9 |
| 🥈  | **claude-sonnet-5** | **85.7** | 89.5% | 83.0% | 40 pts | 2.3 |
| 🥉  | **gemini-2.5-flash** | **85.2** | 89.5% | 82.0% | 29 pts | 3.9 |
| 4.  | **grok-4** | **83.3** | 91.2% | 78.0% | 24 pts | 4.6 |
| 5.  | **o3-mini** | **79.5** | 91.5% | 71.0% | 27 pts | 4.1 |
| 6.  | **gpt-4o-mini** | **77.3** | 95.5% | 65.0% | 21 pts | 5.0 |
| 7.  | **gpt-4o** | **74.0** | 90.2% | 63.0% | 12 pts | 6.3 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 80% | 9.3/10 | 9.1/10 | 8.4/10 | 8.6/10 | 9.0/10 | 7.6/10 |
| **claude-sonnet-5** | 100% | 100% | 70% | 8.2/10 | 8.9/10 | 8.2/10 | 8.3/10 | 8.7/10 | 7.5/10 |
| **gemini-2.5-flash** | 100% | 100% | 70% | 9.0/10 | 8.7/10 | 7.9/10 | 8.1/10 | 8.6/10 | 7.2/10 |
| **grok-4** | 95% | 100% | 85% | 8.3/10 | 8.1/10 | 7.8/10 | 7.6/10 | 8.2/10 | 6.9/10 |
| **o3-mini** | 100% | 100% | 80% | 7.4/10 | 7.6/10 | 7.1/10 | 6.9/10 | 7.4/10 | 6.6/10 |
| **gpt-4o-mini** | 100% | 100% | 100% | 6.2/10 | 6.9/10 | 6.7/10 | 6.1/10 | 6.9/10 | 6.2/10 |
| **gpt-4o** | 95% | 100% | 80% | 6.3/10 | 6.6/10 | 6.2/10 | 6.2/10 | 6.8/10 | 5.9/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **o3-mini**:
  - grok-4.5: Chronological inventions and misplaced beats (wild rabbits appear in spring garden; Real declaration mistimed; altered ending recognition)
  - grok-4.5: Broken scene flow and location errors that destroy clip sequencing
  - grok-4: Invented dialogue and scene compression that alters key emotional beats; inconsistent character descriptions.
- **gpt-4o-mini**:
  - grok-4.5: Severe abridgment dropping major source beats (full Skin Horse monologue, illness sequence details, doctor burn order, transformation mechanics, shabbiness arc)
  - grok-4.5: Insufficient concrete action lines and character visuals for clip-by-clip AI direction
  - grok-4: Severe source truncation and missing major beats (full Skin Horse exchange, wild rabbit confrontation, fairy mechanics).
  - claude-sonnet-5: Drops the entire illness/scarlet-fever/doctor's-burn-order sequence — the story jumps straight from the wild rabbits' rejection to the Rabbit crying alone in the garden with no established cause, breaking the causal and emotional logic of the central turn.
- **gpt-4o**:
  - grok-4.5: Major dropped beats and severe compression of Real dialogue, garden/illness/fairy sequences
  - grok-4.5: Sparse, non-specific action lines that fail short-clip directibility
  - grok-4: Major dropped beats (Skin Horse full dialogue, fairy transformation mechanics, final Boy recognition scene); overly summarized scenes unsuitable for clip breakdown.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | claude-sonnet-5 | gemini-2.5-flash | grok-4 | o3-mini | gpt-4o-mini | gpt-4o |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-4o** | 7.8 | 8.2 | 7.8 | 7.6 | 8.2 | 8.4 | **7.2** *(self)* |
| **gpt-4o-mini** | 8.8 | 8.4 | 7.7 | 7.7 | 7.9 | **8.2** *(self)* | 7.6 |
| **o3-mini** | 8.4 | 8.5 | 8.2 | 8.3 | **9.0** *(self)* | 8.3 | 8.4 |
| **grok-4.5** | **9.1** *(self)* | 8.1 | 8.8 | 7.9 | 3.9 | 3.3 | 3.8 |
| **grok-4** | 9.0 | 8.7 | 8.3 | **7.8** *(self)* | 5.8 | 4.7 | 4.3 |
| **claude-sonnet-5** | 8.0 | **7.0** *(self)* | 7.8 | 6.7 | 6.8 | 4.3 | 5.0 |
| **gemini-2.5-flash** | 9.5 | 9.2 | **9.2** *(self)* | 8.6 | 8.4 | 8.2 | 7.8 |

### 🧐 Self-Bias Analysis
- ⚠️ gpt-4o rated its own screenplay 7.2/10 vs. a 6.2/10 average from 6 other judge(s) (+1.1) — possible self-preference bias.
- ⚠️ gpt-4o-mini rated its own screenplay 8.2/10 vs. a 6.2/10 average from 6 other judge(s) (+2.0) — possible self-preference bias.
- ⚠️ o3-mini rated its own screenplay 9.0/10 vs. a 6.8/10 average from 6 other judge(s) (+2.2) — possible self-preference bias.
- ℹ️ claude-sonnet-5 rated its own screenplay 7.0/10 vs. a 8.5/10 average from 6 other judge(s) (-1.5) — notably harsher on itself than peers were.
- ⚠️ gemini-2.5-flash rated its own screenplay 9.2/10 vs. a 8.1/10 average from 6 other judge(s) (+1.1) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-4o:** Strongest: Screenplay B because it excels in adaptation fidelity and is highly directible with clear, concise action lines. Weakest: Screenplay F because it occasionally deviates from key narrative beats and has inconsistent character descriptions affecting visual continuity.
- **gpt-4o-mini:** Strongest: Screenplay B because it excels in adaptation fidelity and pacing, providing a strong visual narrative. Weakest: Screenplay F because it lacks the depth and engagement found in higher-rated candidates.
- **o3-mini:** Strongest: Screenplay E because it delivers a beautifully concise, visually direct, and cinematically rich adaptation ideal for short clips; Weakest: Screenplay D because its dense narrative detail and extended beat descriptions risk overloading the limited clip format.
- **grok-4.5:** Strongest: Screenplay G because it delivers near-perfect fidelity, locked visual character continuity, and densely clip-ready action lines that fully honor the 5-10s AI video constraint while preserving every emotional beat. Weakest: Screenplay B because it catastrophically abridges the source into a skeletal outline lacking essential plot pillars, character detail, and any usable directorial specificity.
- **grok-4:** Strongest: Screenplay E because it delivers the highest fidelity, clearest visual action lines, and consistent character anchors optimized for short AI clips. Weakest: Screenplay B because it drops essential narrative beats and provides generic descriptions that break both adaptation accuracy and directibility.
- **claude-sonnet-5:** Strongest: Screenplay C, because it combines the fullest, most faithful beat-for-beat coverage of the source with concrete camera-ready direction (CLOSE ON cues) and the most consistent character descriptions across scenes. Weakest: Screenplay G, because it entirely drops the illness/doctor/burning sequence that motivates the Rabbit's abandonment and tear-shedding, breaking the story's causal chain and leaving the emotional climax unearned.
- **gemini-2.5-flash:** Strongest: Screenplay G because it demonstrates exceptional fidelity to the source material, highly detailed and consistent character descriptions, and perfectly paced, directible beats for short video clips. Weakest: Screenplay D because it frequently relies on internal monologues or mood descriptions that are difficult to translate into concrete visual actions for AI video generation, and its fidelity is slightly less precise.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 13
- **Dialogue Blocks:** 45 (Avg `15.4` words/turn, Max `87` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 4 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay E maintains good adaptation fidelity but occasionally lacks clarity in character descriptions, affecting visual consistency. The screenplay is generally directible, though some scenes are overly descriptive, hindering the short-clip format. Pacing is adequate but could be more dynamic. Dialogue is mostly authentic but sometimes lacks depth. Sound design is functional but could be more engaging to better support the scenes.
  - *gpt-4o-mini:* Screenplay B excels in adaptation fidelity and pacing, providing a strong visual narrative that aligns closely with the source material.
  - *o3-mini:* Screenplay A is highly faithful and detailed in its adaptation, providing clear character and scene descriptions, though some scenes might require tighter condensation for 5–10 second video clips.
  - *grok-4.5 (self):* Most complete and precise translation of every major and minor beat into richly concrete, single-clip visuals with locked character descriptions; exemplary show-don't-tell pacing and atmospheric writing that directly enables both AI direction and complementary sound beds.
  - *grok-4:* Best balance of fidelity, visual specificity, and clip-friendly pacing with consistent character anchors.
  - *claude-sonnet-5:* The most complete and disciplined adaptation: full toy-hierarchy scene, all major dialogue beats verbatim, explicit 'CLOSE ON' camera directions, and a top-of-script style note ensuring visual/tonal consistency across toy and animal characters. Still no explicit sound-design or music cues, which caps the scoring dimension, but atmospheric prose (frosted silver, gaslight gold) gives a composer more to work with than most peers.
  - *gemini-2.5-flash:* Screenplay G is an exceptional adaptation, demonstrating perfect fidelity to the source material, highly detailed and consistent character descriptions, and perfectly paced, directible beats for short video clips. Dialogue is verbatim and authentic, and the narrative flow is seamless. The 'Notes' section also indicates a strong visual approach. The only minor area for improvement is the absence of explicit sound/music cues, which is a common omission across all candidates.

### 🎬 claude-sonnet-5
- **Scene Headings Count:** 10
- **Dialogue Blocks:** 39 (Avg `20.9` words/turn, Max `87` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 8 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay D offers a faithful adaptation with clear character descriptions that support visual continuity. The screenplay is effectively directible, with well-defined actions suitable for short clips. Pacing is solid, maintaining engagement throughout. Dialogue is authentic and captures the characters' voices well. Sound design and music are well-integrated, enhancing the emotional resonance of the scenes.
  - *gpt-4o-mini:* Screenplay E offers a strong adaptation with good pacing and character clarity, though it could improve in directibility.
  - *o3-mini:* Screenplay F offers a richly detailed adaptation with strong narrative continuity, although its complex layering might require careful editing to fit the clip constraints.
  - *grok-4.5:* Strong near-complete beat coverage and book-faithful dialogue with clear visual anchors for Rabbit and Skin Horse; minor invented name (Bramble) and heavy narrator VO slightly soften pure show-don't-tell clip readiness, but structure and consistency remain production-viable for short AI segments.
  - *grok-4:* Highly faithful with rich visual beats and consistent character descriptions; minor pacing compression in illness arc.
  - *claude-sonnet-5 (self):* Otherwise the most cinematically polished script — concrete camera language ('Close on the Rabbit's button eyes'), rich atmosphere, and a strong original final beat — but it invents a named character ('Bramble') for the wild rabbit that does not exist in the source text, which per the fidelity rubric must be treated as a severe, standalone penalty on that dimension even though it doesn't break the overall story.
  - *gemini-2.5-flash:* Screenplay C is an exceptional adaptation, demonstrating high fidelity to the source material, including specific minor characters and details. Character disambiguation is outstanding with vivid, consistent descriptions. The directibility is very strong, with mostly concrete action lines, and the pacing is excellent, breaking the story into distinct, impactful beats. Dialogue is authentic and well-structured. The only minor area for improvement is the absence of explicit sound/music cues.

### 🎬 gemini-2.5-flash
- **Scene Headings Count:** 13
- **Dialogue Blocks:** 42 (Avg `19` words/turn, Max `65` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY`
- **Diagnostics & Warnings:**
  - ⚠️ 8 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay G maintains good adaptation fidelity but occasionally lacks clarity in character descriptions, affecting visual consistency. The screenplay is generally directible, though some scenes are overly descriptive, hindering the short-clip format. Pacing is adequate but could be more dynamic. Dialogue is mostly authentic but sometimes lacks depth. Sound design is functional but could be more engaging to better support the scenes.
  - *gpt-4o-mini:* Screenplay D maintains fidelity to the source but struggles with pacing and directibility in certain scenes.
  - *o3-mini:* Screenplay D is very rich in detail and faithful to the source, but its dense scenes and extended dialogue sequences could challenge the short duration clip format.
  - *grok-4.5:* Near-exhaustive source coverage with precise, consistent character locks and highly observable actions; dense but breakable into clean 5-10s beats, strong emotional arcs that support sound design even without explicit cues.
  - *grok-4:* Excellent coverage and directible beats; minor wardrobe consistency notes across time jumps.
  - *claude-sonnet-5:* Near-complete coverage of the book including Timothy, the model boat, Nana's tidying, the seaside/'hurrah' beat, and the full illness arc; dialogue is largely verbatim. Weakest link is directibility — several scene blocks (e.g., 'WEEKS LATER') compress multiple time-jumps and actions into a single heading, and Narrator V.O. carries much of the storytelling rather than discrete filmable images. No explicit sound/music direction anywhere.
  - *gemini-2.5-flash (self):* Screenplay E provides an excellent, highly faithful adaptation, incorporating many specific details from the book. Character disambiguation and visual consistency are outstanding. The pacing is superb, with granular scene breakdowns ideal for short video clips, and the narrative momentum is strong. Dialogue is authentic and well-preserved. While generally directible, a few action lines still contain internal thoughts ('Rabbit endures, missing...', 'Joy breaks through him'), which are minor points for refinement.

### 🎬 grok-4
- **Scene Headings Count:** 9
- **Dialogue Blocks:** 24 (Avg `16.1` words/turn, Max `65` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ 3 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay C maintains good fidelity to the source material but occasionally lacks clarity in character descriptions, which could affect visual consistency. The screenplay is generally directible, though some scenes are overly descriptive, hindering the short-clip format. Pacing could be improved to enhance narrative flow. Dialogue is mostly authentic but sometimes lacks subtext. Sound design is adequate but could be more dynamic to better support the scenes.
  - *gpt-4o-mini:* Screenplay C is a solid adaptation but lacks the same level of visual clarity and pacing as the top candidates.
  - *o3-mini:* Screenplay G is a streamlined and faithful adaptation that cleanly maps the narrative beats, though its overall approach is less distinctive compared to the top contenders.
  - *grok-4.5:* Faithful core dialogue and beat progression with clean, single-action visual lines ideal for short clips; character intros solid for leads though supporting figures thinner, and atmospheric cues present but not richly scored for sound beds.
  - *grok-4 (self):* Solid fidelity and clean action lines; slightly less visual specificity than top entries.
  - *claude-sonnet-5:* Solid, workmanlike coverage including Timothy, the model boat, the doctor scene, and the fairy transformation, with mostly verbatim dialogue. Loses points because several scene headings cram three or four discrete actions (wheelbarrow ride + picnic + fairy hut building) into one slugline, and the illness/rejection climax is rushed without much room to breathe. No music/sound direction provided.
  - *gemini-2.5-flash:* Screenplay F delivers a very good adaptation with strong fidelity and clear character descriptions. The pacing is effective, breaking the story into manageable beats for short clips. Dialogue is authentic and well-integrated. However, similar to other candidates, some action lines describe internal states ('knows the nursery magic has happened', 'He has no hind legs') rather than purely observable actions, slightly impacting its AI video directibility score.

### 🎬 o3-mini
- **Scene Headings Count:** 10
- **Dialogue Blocks:** 0 (Avg `0` words/turn, Max `0` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay A maintains a strong fidelity to the source material, capturing the essence of the story and characters effectively. The character descriptions are consistent and clear, aiding in visual continuity. The screenplay is directible with clear, filmable actions, though some scenes could benefit from tighter pacing. Dialogue is authentic and captures the emotional depth of the characters. Sound design and music complement the scenes well, enhancing the emotional impact.
  - *gpt-4o-mini:* Screenplay G presents a charming adaptation but could enhance its pacing and dialogue authenticity.
  - *o3-mini (self):* Screenplay E is exceptionally polished with precise, filmable visual action lines, clear character cues, and a well-calibrated pacing ideal for AI-generated short video clips.
  - *grok-4.5:* Scrambled structure with invented timing and dialogue deviations fatally undermines fidelity; inconsistent visuals and multi-location crams make reliable AI clip generation impossible.
  - *grok-4:* Noticeable invented content and weaker visual directibility.
  - *claude-sonnet-5:* Hits the major plot beats (Skin Horse dialogue, becoming Real, wild-rabbit rejection, doctor's burn order, fairy transformation, final reveal) but drops the toy-cupboard hierarchy (no Timothy/model boat), the Nana tidying ritual, and the 'hurrah, seaside' beat, and compresses the transformation into vague 'something marvelous occurs' language that tells rather than shows. No music/sound cues at all, only ambient description.
  - *gemini-2.5-flash:* Screenplay B offers a faithful adaptation with good character disambiguation and consistent descriptions. The pacing is well-suited for short clips, breaking down the story into clear beats. While generally directible, some action lines lean slightly into internal states ('quiet satisfaction glowing in his eyes', 'somber mood fills the room'), which could be challenging for AI generation. Dialogue is authentic, and the overall structure is strong.

### 🎬 gpt-4o-mini
- **Scene Headings Count:** 10
- **Dialogue Blocks:** 25 (Avg `10.9` words/turn, Max `27` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. BOY).
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o:* Screenplay B excels in adaptation fidelity, closely following the book's narrative and themes without unnecessary deviations. Character descriptions are mostly consistent, though there are minor lapses in visual continuity. The screenplay is highly directible, with clear, concise action lines that fit the short-clip format. Pacing is well-managed, maintaining narrative momentum. Dialogue is natural and fitting for the characters, and the sound design effectively supports the emotional tone of each scene.
  - *gpt-4o-mini (self):* Screenplay A effectively captures the essence of the original story while maintaining a clear narrative structure and character clarity.
  - *o3-mini:* Screenplay B offers a more streamlined and visually clear version of the story, making it highly directable for AI shorts even if it slightly sacrifices some narrative depth.
  - *grok-4.5:* Extremely thin skeleton that omits core emotional and plot pillars; vague descriptions and tell-heavy narrator leave almost nothing filmable in 5-10s beats, rendering it unusable without total rewrite.
  - *grok-4:* Too abbreviated for faithful adaptation or short-clip production.
  - *claude-sonnet-5:* The condensed toy-cupboard and Skin Horse scenes retain some charm, but the complete omission of the illness/doctor/sack beat is a structural break — the audience has no idea why the Rabbit is suddenly discarded and weeping. Combined with thin, generic character description and heavy reliance on unfilmable internal-state narration, this draft needs a substantial rewrite before it could be greenlit.
  - *gemini-2.5-flash:* Screenplay A provides a solid adaptation, covering key narrative beats without major deviations. Character descriptions are clear and consistent. However, its reliance on narration to summarize events, and occasional internal thoughts in action lines ('feeling loved', 'feels ashamed'), slightly reduces its AI video directibility and pacing for short, distinct clips. The lack of explicit sound/music cues is a minor missed opportunity.

### 🎬 gpt-4o
- **Scene Headings Count:** 10
- **Dialogue Blocks:** 0 (Avg `0` words/turn, Max `0` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ No explicit music/sound design cues detected in screenplay.
- **Judge Rationale:**
  - *gpt-4o (self):* Screenplay F captures the essence of the source material but occasionally deviates from key narrative beats. Character descriptions are somewhat inconsistent, affecting visual continuity. The screenplay is directible, though some scenes are overly descriptive, hindering the short-clip format. Pacing could be improved to enhance narrative flow. Dialogue is mostly authentic but sometimes lacks subtext. Sound design is adequate but could be more dynamic to better support the scenes.
  - *gpt-4o-mini:* Screenplay F is a competent adaptation but lacks the depth and engagement found in higher-rated candidates.
  - *o3-mini:* Screenplay C remains faithful, balancing descriptive narration with clear visual beats, though its pacing is similar to A and might require slight trimming for clip segmentation.
  - *grok-4.5:* Overly summarized adaptation that loses narrative spine and character specificity; remaining scenes lack the observable detail and rhythmic escalation required for AI video segmentation.
  - *grok-4:* Severely truncated source coverage and generic descriptions make it unusable.
  - *claude-sonnet-5:* Touches every major plot beat but in extremely thin, summarized form, losing texture (Skin Horse is 'old and wise', Nana is just 'bustling'). Several lines lean on unfilmable internal states ('listens intently, longing to become Real'; 'grateful for the love that made him Real'), and paraphrased dialogue lacks the book's lyrical specificity. No invented major content, so it remains produceable, just underdeveloped.
  - *gemini-2.5-flash:* Screenplay D covers the main story beats but simplifies some details, leading to slightly lower fidelity. Character descriptions are adequate, though the 'WILD RABBITS' then 'WILD RABBIT' usage is a minor inconsistency. The primary weakness is in AI video directibility, with several action lines describing internal states ('longing to become Real', 'feeling the warmth of love', 'Rabbit's heart sinks') that are difficult to visualize directly. Pacing is quick but sometimes feels summarized by narration.

