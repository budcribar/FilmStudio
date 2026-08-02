# 🏆 Screenplay Benchmark & Peer-Evaluation Report
*Generated at: 2026-08-02 04:39:46 UTC*  
*Source Story File: `Nick_and_Me.txt`*

## 📊 Overall Model Leaderboard

| Rank | Model ID | Composite Score (0-100) | C# Syntax/Budget Score | LLM Peer Consensus | Borda Points | Avg Rank |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: |
| 🥇  | **gpt-5.6-terra** | **81.5** | 100.0% | 69.0% | 3 pts | 1.5 |
| 🥈  | **grok-4.5** | **81.1** | 99.5% | 69.0% | 3 pts | 1.5 |

## 📐 Dimension Breakdown Matrix

| Model ID | Fountain Syntax | Scene Budget | Dialogue Pacing | Fidelity | Character Age Split | Video Directibility | Dramatic Pacing | Dialogue Authenticity | Sound/Music Design |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **gpt-5.6-terra** | 100% | 100% | 100% | 6.8/10 | 5.5/10 | 7.2/10 | 6.8/10 | 7.2/10 | 8.0/10 |
| **grok-4.5** | 100% | 100% | 90% | 6.8/10 | 6.5/10 | 6.8/10 | 6.5/10 | 7.0/10 | 7.8/10 |

## 🚫 Production-Readiness Flags
Deal-breaker issues judges called out independent of the averaged scores above:

- **gpt-5.6-terra**:
  - gpt-5.6-terra: The protagonist identity is unstable and inaccurate: NARRATOR becomes PETER before the source's final-name reveal, and the screenplay uses PETER OLSEN / "President Peter Olsen" even though the source's final name is Peter Olson.
  - gpt-5.6-terra: YOUNG NICK is not given a distinct cast token from adult NICK; the draft instead uses "Twelve-year-old NICK," risking one locked adult reference image across incompatible ages.
  - gpt-5.6-terra: GUARD is an unnamed generic supporting dialogue token, contrary to the required stable named-role convention.
  - gpt-5.6-terra: Mandatory Vision Meta sidecar is absent.
  - gpt-5.6-terra: A dream scene and real-world wake-up are combined under "EXT. DREAM ROAD - DAY," creating an impossible single-location scene for clip production.
- **grok-4.5**:
  - gpt-5.6-terra: The protagonist is called PETER throughout despite the source withholding his name until the final line, and the draft alternates between PETER and NARRATOR for the same character; this is a source-spoiler and unstable cast-identity defect.
  - gpt-5.6-terra: Mandatory Vision Meta sidecar is absent.
  - gpt-5.6-terra: Several slugs combine distinct locations or travel beats, including "INT. FUNERAL HOME / CEMETERY" and "EXT. WISCONSIN HIGHWAY / AIRPORT," which is unusable as a single scheduled, generated clip location.
  - gpt-5.6-terra: The climactic prison speech is a long multi-idea monologue that exceeds the prompt's dialogue-length limit and is not viable as one 5-10 second clip.

## ⚖️ Peer Judge Matrix (Heatmap)
Shows how each judge model evaluated candidate screenplays (scored out of 10):

| Judge Model \ Author | gpt-5.6-terra | grok-4.5 |
| :--- | :---: | :---: |
| **gpt-5.6-terra** | **5.6** *(self)* | 5.7 |
| **grok-4.5** | 8.2 | **8.1** *(self)* |

### 🧐 Self-Bias Analysis
- ℹ️ gpt-5.6-terra rated its own screenplay 5.6/10 vs. a 8.2/10 average from 1 other judge(s) (-2.7) — notably harsher on itself than peers were.
- ⚠️ grok-4.5 rated its own screenplay 8.1/10 vs. a 5.7/10 average from 1 other judge(s) (+2.4) — possible self-preference bias.

### 🗣️ Judge Summary Notes
- **gpt-5.6-terra:** Strongest: Screenplay A because it preserves substantially more of the book's full emotional and plot arc, especially the protagonist's education-driven awakening and the long path to forgiveness. Weakest: Screenplay B because its unstable and incorrect protagonist naming, failure to separate young and adult Nick, and more drastic loss of the source's middle transformation make its ending feel less faithful and less production-safe.
- **grok-4.5:** Strongest: Screenplay A because it best obeys narrator-name withholding, delivers the tightest clip-sized observable beats, and densest usable sound design while still hitting every major book turn. Weakest: Screenplay B because premature PETER naming undercuts fidelity and a few denser scenes reduce pure short-clip directibility.

## 🔍 Character & Music Structural Diagnostics (C# Audit)

### 🎬 gpt-5.6-terra
- **Scene Headings Count:** 31
- **Dialogue Blocks:** 20 (Avg `12.2` words/turn, Max `22` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Diagnostics & Warnings:**
  - ⚠️ Detected 2 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra (self):* The draft is comparatively clean in scene-level construction and frequently offers concise, performable dialogue and concrete sound beds. Its hospital, bar confrontation, stabbing, breakup, and prison scenes are readable as short-form visual units. But it drops too much of the source's essential middle progression: Peter's sustained college growth, the escalating caregiver burden, Nick's withdrawal and tactical return to Ma, Sionna's family/farm material, and the work-to-business trajectory are substantially reduced or bypassed. This makes the final successful Seattle businessman feel under-earned. The script also contains severe continuity problems for downstream casting: the anonymous narrator is renamed early, the surname is misspelled, and young/adult Nick are not properly disambiguated. Sound design is a relative strength, with concrete environmental cues in nearly every scene and layered audio at the violent peaks, but it cannot compensate for the identity and fidelity failures.
  - *grok-4.5:* Strong fidelity to the full arc (Nick intro/Viking lore, Sionna meet, Ma decline/hospital, dreams, yoga, bar fight, crash/death, stabbing, breakup, Seattle success, prison forgiveness) with proper NARRATOR withholding until late reveal; minor spelling slip (Olsen/Olson) and light compression of secondary beats. Character looks are locked early (Nick scars/suede/reddish hair; Sionna pale/blue eyes/fish necklace) and mostly restated, though NARRATOR-to-PETER handoff and young/adult splits could be sharper. Action is highly clip-friendly (single observable beats, short scenes). Pacing holds momentum across ~40 tight scenes without padding. Dialogue stays close to book voice and clip-length. Sound/Music cues are dense and diegetic on every scene plus peaks.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra (self):* Add a final automated-style canonical-entity audit requiring every named character, age variant, surname, and supporting speaking role to match a single approved cast-token list exactly; require explicit separate tokens for any character shown at materially different ages.
  - *grok-4.5:* Add an explicit rule: on every character re-entry after a time jump, dream, or age split, the first Action line MUST restate at least three locked visual traits (build/hair/scar/wardrobe) using the exact same tokens; also require source-exact spelling of all proper names.

### 🎬 grok-4.5
- **Scene Headings Count:** 40
- **Dialogue Blocks:** 83 (Avg `11.4` words/turn, Max `47` words)
- **Generic Numbered Speakers:** `0` (e.g. MAN 1, OFFICER 2)
- **Age-Disambiguated Character Headers:** `YOUNG NICK`
- **Diagnostics & Warnings:**
  - ⚠️ 2 monologue turn(s) exceed 35 words without action line splits.
  - ⚠️ Detected 1 age-qualified character header(s) (e.g. YOUNG NICK).
  - ⚠️ Detected 1 descriptive instrumental music/sound cue(s).
- **Judge Rationale:**
  - *gpt-5.6-terra:* This is the stronger adaptation because it covers more of the book's emotional spine: Nick's early protection and intimidation, Sionna's awakening effect, college conflict, bar rupture, Ma's death, the stabbing, separation, Seattle reinvention, and prison forgiveness. However, it compresses several major turns into summary-like transitions, omits much of the gradual school/work transformation, and uses broad VO to bridge story rather than playable visual beats. Character locking is weak because the lead's identity switches from NARRATOR to PETER long before the book permits the reveal, while recurring appearances do not consistently restate locked traits. The sound cue coverage is consistently present and often concrete, with particularly effective crash, stabbing, hospital, and prison sound beds, though some scenes rely on only one generic ambient cue. Dialogue retains the book's rough voice in places, but the final confrontation is overpacked and several lines are more expositional than performable.
  - *grok-4.5 (self):* Covers the same essential spine cleanly but violates the source-name rule by locking PETER from the opening (book withholds until final line), slightly lowering fidelity; otherwise no major inventions or dropped peaks. Excellent consistent casting (PETER lean ordinary clothes; NICK tall scarred reddish hair; YOUNG NICK flashback; Sionna traits restated). Directibility is solid and filmable yet a few scenes pack denser multi-beat action that strains pure 5-10s clips. Pacing is smooth but less granular than ideal for the short-clip constraint. Dialogue is natural, character-distinct, and book-true. Sound cues present on every scene though slightly less varied at emotional peaks than A.
- **Judge Prompt-Improvement Suggestions:**
  - *gpt-5.6-terra:* Add a mandatory identity-reveal audit: until a source text explicitly reveals a first-person protagonist's name, require the exact same anonymous character token in every action line, dialogue cue, title reference, and visual-description reference; prohibit early-name substitutions even when the name appears later in the source.
  - *grok-4.5 (self):* Strengthen the narrator-name rule with: 'If the source withholds the first-person protagonist’s given name until late (or never), the cue MUST remain NARRATOR/THE NARRATOR for all pre-reveal scenes; never front-load the revealed name even for casting convenience.'

