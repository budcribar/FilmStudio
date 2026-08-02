# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 05:37:36 UTC*  
*Source Story File: `The_Velveteen_Rabbit.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-terra** | **89.9** | 100.0% | 83.0% | 4 pts | 1.0 |
| 🥈  | **grok-4.5** | **89.3** | 100.0% | 82.0% | 2 pts | 2.0 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-terra** | 100% | 100% | 100% | 8.4/10 | 7.8/10 | 8.5/10 | 8.5/10 | 8.3/10 | 8.3/10 |
| **grok-4.5** | 100% | 100% | 100% | 9.0/10 | 7.9/10 | 7.0/10 | 7.6/10 | 8.9/10 | 8.8/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-terra**:
  - gpt-5.6-terra: Missing the required ---VISION_META--- sidecar, which the shared prompt designates as mandatory downstream source material for portrait generation and clip production.
- **grok-4.5**:
  - gpt-5.6-terra: Missing the required ---VISION_META--- sidecar, which the shared prompt designates as mandatory downstream source material for portrait generation and clip production.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-terra | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | **8.1** *(self)* | 8.1 |
| **grok-4.5** | 8.5 | **8.3** *(self)* |

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay B because its scene segmentation, escalation, and clip-scale directibility are more disciplined while retaining nearly the entire source arc. Weakest: Screenplay A because, despite superior fidelity and sound design, its denser action blocks more often overload a single short AI-video beat; both drafts are not production-ready because neither includes the mandated VISION_META sidecar.
- **grok-4.5:** Strongest: Screenplay A because its single-purpose scene cuts and consistently camera-observable micro-beats map cleanly onto 5–10s AI clips without cramming. Weakest: Screenplay B because denser multi-beat scenes, parenthetical entrances, and dissolve summaries undercut short-clip directibility despite slightly higher book-line fidelity.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 14
- **Dialogue Blocks:** 59 (Avg `13.2` words/turn, Max `28` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. BOY).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* B delivers the cleanest short-film structure of the two drafts. It separates key dramatic turns into more manageable, clip-friendly scenes, maintains clear escalation from abandonment to illness and loss, and lands the transformation and epilogue efficiently. Its Rabbit, Skin Horse, Nana, Fairy, and final transformed-rabbit descriptions are usable visual anchors, though the Boy remains under-specified as a recurring human lead and is not formally disambiguated as an older version beyond a changed coat and cap. Fidelity is strong but slightly below A because it trims more of the toy-social-world material and invents the Rabbit's spoken 'No' to the Fairy, where the source gives him a silent look. Some action still compresses multiple beats, and isolated internal labels such as 'ashamed' should be externalized. Sound coverage is consistently competent, but its music plan is less developed and emotionally varied than A's. The mandatory vision sidecar is absent.
  - *grok-4.5:* Faithful spine covering Christmas arrival, Skin Horse lesson, bedtime adoption, garden REAL declaration, wild-rabbit rejection, scarlet-fever crisis, sack/tear/fairy transformation, and spring reunion glance. Dialogue stays close to the book with only minor compression (Skin Horse loses the “can’t be ugly” beat) and one small invented spoken “No.” Scenes are cleanly segmented by single location+purpose with concrete, clip-length observable actions ideal for 5–10s AI shots. Character looks lock well for Rabbit (degradation tracked), Skin Horse, Nana, and Fairy, but DOCTOR never gets a visual description on entrance. Sound cues appear in every scene with good peak density. No deal-breakers.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a final machine-checkable compliance rule: 'Reject the output as incomplete unless it ends with the exact required VISION_META delimiters and valid JSON sidecar; this sidecar is mandatory even when the screenplay body is otherwise valid Fountain.'
  - *grok-4.5:* Require that every speaking character, including one-scene supporting roles, receive 2–3 locked visual traits (build, wardrobe, defining prop) in the first Action line of their first entrance, restated on any later re-entry after a time jump.

### 🎬 grok-4.5
- **Scene Headings Count:** 14
- **Dialogue Blocks:** 54 (Avg `14.8` words/turn, Max `35` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `THE BOY`
- **Diagnostics & Warnings:**
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. THE BOY).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra:* A is highly faithful and covers the complete emotional spine: Christmas abandonment, the Skin Horse's definition of Real, bed companionship, garden recognition, rejection by wild rabbits, scarlet fever separation, Fairy transformation, and the final reunion image. It preserves most iconic dialogue closely and supplies especially strong, emotionally graduated sound and music cues. However, several action paragraphs contain too many sequential events for a single 5–10 second clip, particularly the bedroom companionship, illness, and woodland sequences. The Boy is not sufficiently visually locked beyond broad child descriptors, and his later appearance is only minimally refreshed. A few action lines state internal conditions rather than exclusively observable behavior, and the Rabbit's ellipsis response to the Fairy is an awkward invented dialogue turn. The mandatory vision sidecar is absent.
  - *grok-4.5 (self):* Slightly fuller book coverage than A: Skin Horse monologue is more complete, wild-rabbit exchange retains more iconic lines, and the tear-to-fairy arc is richly staged. Dialogue authenticity is excellent. Casting locks are generally strong (Doctor gets “serious man with black bag”; Rabbit degradation is clear). Weaknesses cluster on short-clip directibility: Nana’s dusk rescue is jammed into a parenthetical instead of standalone Action; illness/recovery and several wood beats cram multiple distinct micro-events into one heading; “time passes in soft dissolves” and an empty “…” dialogue cue are unfilmable or non-performable as discrete 5–10s clips. Sound/Music design is the best of the two (lullaby, chime, leaping theme, closing theme). Still greenlightable, but needs scene splits before shoot.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a final machine-checkable compliance rule: 'Reject the output as incomplete unless it ends with the exact required VISION_META delimiters and valid JSON sidecar; this sidecar is mandatory even when the screenplay body is otherwise valid Fountain.'
  - *grok-4.5 (self):* Prohibit conveying entrances, time jumps, or essential story actions only inside parentheticals or summary phrases like ‘time passes in dissolves’; each such beat must be a concrete standalone Action line under a proper scene heading so it can be filmed as its own short clip.

