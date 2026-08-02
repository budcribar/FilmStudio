# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 04:35:40 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **grok-4.5** | **81.1** | 99.5% | 69.0% | 4 pts | 1.0 |
| 🥈  | **gpt-5.6-terra** | **76.4** | 98.5% | 62.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **grok-4.5** | 100% | 100% | 90% | 7.5/10 | 7.0/10 | 5.2/10 | 6.8/10 | 7.0/10 | 7.8/10 |
| **gpt-5.6-terra** | 95% | 100% | 100% | 2.0/10 | 7.0/10 | 7.8/10 | 4.5/10 | 7.5/10 | 8.2/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **grok-4.5**:
  - gpt-5.6-terra: Missing the required ---VISION_META--- sidecar, despite it being mandated for downstream portrait and clip production.
  - gpt-5.6-terra: Prematurely names the first-person narrator PETER from the opening, although the source withholds his name until the closing line.
  - gpt-5.6-terra: Uses prohibited multi-location/slash scene headings and montage containers, including "EXT. PARK / MONTAGE," "INT. NICK'S APARTMENT / JOE'S BAR," "INT. FUNERAL HOME / CEMETERY," "INT. HOSPITAL / MONTAGE," and "EXT. WISCONSIN HIGHWAY / AIRPORT," which are not schedulable single-location scenes.
  - gpt-5.6-terra: The final prison speech is crammed into one oversized dialogue turn and is not viable as a single short AI-video beat.
  - grok-4.5: Illegal multi-location scene headings using '/' (NICK’S APARTMENT / JOE’S BAR, FUNERAL HOME / CEMETERY, HOSPITAL / MONTAGE, HIGHWAY / AIRPORT) which violate the one-concrete-place rule and prevent clean clip breakdown
  - grok-4.5: Several montage scenes cram disparate actions and places into single headings, unusable for 5-10s AI clips
- **gpt-5.6-terra**:
  - gpt-5.6-terra: Drops the source's central Nick-versus-Sionna stabbing scene entirely, then presents Sionna later with an unexplained healed scar and trauma-driven breakup.
  - gpt-5.6-terra: Introduces several invented named characters not named in the source, including LIBRARIAN ADA BELL, BOUNCER RAY, DAVID, INSTRUCTOR MAYA, and GUARD HARRIS.
  - gpt-5.6-terra: Missing the required ---VISION_META--- sidecar.
  - gpt-5.6-terra: Uses unstable protagonist identity tokens: THE NARRATOR through nearly the entire script, then PETER OLSEN in the closing scene. The source's final name is Peter Olson, creating both a late cast-token split and a surname inconsistency.
  - gpt-5.6-terra: The omitted stabbing creates a major causal hole between Nick's hospitalization and Sionna's PTSD, separation, and Peter's later resentment.
  - grok-4.5: Dropped the major climax of Nick stabbing Sionna (book's central violent peak); only a later scar is shown without the attack scene
  - grok-4.5: Narrative incomplete: separation, hatred, and Seattle arc lack causal depiction of the assault

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | grok-4.5 | gpt-5.6-terra |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | 6.3 | **5.8** *(self)* |
| **grok-4.5** | **7.4** *(self)* | 6.5 |

### 🧐 Self-Bias Analysis
- ⚠️ grok-4.5 rated its own screenplay 7.4/10 vs. a 6.3/10 average from 1 other judge(s) (+1.1) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay A because it retains the complete emotional and causal spine, especially Nick's attack on Sionna and the eventual prison reconciliation, despite severe clip-structure and compliance problems. Weakest: Screenplay B because it omits the stabbing of Sionna—a central source event—and compounds that omission with invented named characters and a broken narrator/Peter identity token.
- **grok-4.5:** Strongest: Screenplay B because it alone dramatizes the full narrative spine including the stabbing climax that motivates everything after. Weakest: Screenplay A because it entirely omits Nick's attack on Sionna, leaving the scar, breakup, and hatred causally ungrounded.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 grok-4.5
- **Scene Headings Count:** 30
- **Dialogue Blocks:** 59 (Avg `12.5` words/turn, Max `42` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the stronger adaptation because it preserves the full central spine: Peter's dependence on Nick, Sionna's intellectual awakening, Ma's deterioration and death, Nick's assault on Sionna, the breakup, Peter's Seattle success, and the prison forgiveness scene. It also includes several useful source-specific motifs, including Wuthering Heights, yoga, Buddhism, the medication organizer, the hospital dream, and Sionna's confrontation with Nick. However, it compresses too aggressively in several places and treats montage as a substitute for playable scenes. The slash headings create multiple location changes inside nominally single scenes, which is especially damaging for a 5-10 second clip workflow. Peter, Nick, Ma, and Sionna receive solid initial looks, but re-entry descriptions are inconsistent and the childhood versions are not cleanly tokenized as YOUNG PETER/YOUNG NICK. Dialogue often retains the source's voice, but the climactic forgiveness speech is substantially overpacked and more literary than performable. Sound coverage is consistently present and generally useful, though some music directions remain broad rather than beat-specific.
  - *grok-4.5 (self):* Covers the full book arc including the critical stabbing, Ma's death, breakup, Seattle success, and prison forgiveness with solid character locks (PETER consistent across ages, Young NICK flash, Sionna traits restated) and mostly authentic short dialogue. Sound cues are present and useful. Directibility and structure are badly damaged by repeated '/' joined headings and montage slugs that pack multiple locations/actions into one unit, violating HARD rules and making short-clip scheduling impossible. Early naming of PETER is a minor fidelity slip but not fatal.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a clip-beat rule: every scene must contain no more than three filmable micro-beats and must be playable in one 5-10 second clip; prohibit montages and any scene heading containing a slash, with a required final lint pass that splits them into separate concrete locations.
  - *grok-4.5 (self):* Explicitly prohibit any scene heading containing '/', 'AND', or 'MONTAGE' and mandate that multi-beat sequences be split into consecutive single-location scenes with time bridges in Action only.

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 30
- **Dialogue Blocks:** 145 (Avg `7.9` words/turn, Max `21` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Missing 'FADE OUT.' or 'THE END' closing transition.
  - ⚠️ Detected 4 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* The screenplay is better formatted for individual clips than A: it uses more concrete single locations, grounds scenes before dialogue, gives clean sound beds, and splits the prison confrontation into short playable exchanges. Its initial visual descriptions are also generally strong. However, it fails adaptation fidelity at a fundamental level by omitting the stabbing of Sionna, the story's defining violent escalation and the direct cause of her withdrawal from Peter. The later scar and dissociation do not repair that omission; they make the narrative feel abruptly incomplete. It also severely violates the no-invented-named-character requirement through multiple newly named supporting roles. The script retains a reasonable amount of source dialogue and captures Nick's and Peter's contrasting voices, but the invented dialogue and compressed causal logic reduce the emotional credibility of the middle and late acts. Sound design is its strongest category: nearly every scene has an apt, concrete sound environment, and major emotional scenes receive layered audio support.
  - *grok-4.5:* Strong concrete action lines, consistent locked looks for Nick/Sionna/Ma, excellent per-scene sound/music cues, and clean single-location headings make individual clips highly directable. However, the complete omission of Nick's home-invasion stabbing of Sionna (the book's pivotal Part-3 crisis that motivates the scar, breakup, and lasting hatred) is a catastrophic fidelity failure that leaves later scenes unmotivated and the arc broken. Pacing collapses across the missing peak; name reveal as PETER only at the end is faithful but cannot rescue the dropped beat.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a mandatory source-turn coverage audit before output: silently identify the story's setup, inciting event, central escalation, irreversible crisis, aftermath, and resolution, and require that each receives an explicit dramatized scene rather than being implied later through dialogue, scars, or narration.
  - *grok-4.5:* Add a hard rule requiring that every major source crisis (death, assault, confession, reunion) must be rendered as its own dramatized scene with observable action, never elided to a later scar, mention, or time-jump summary.

