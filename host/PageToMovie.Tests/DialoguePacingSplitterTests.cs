using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public sealed class DialoguePacingSplitterTests
{
    [Fact]
    public void SplitDialogue_ShortDialogue_ReturnsSingleBeat()
    {
        var input = "Gentlemen — welcome. The shriek was my own, in a dream.";
        var beats = DialoguePacingSplitter.SplitDialogue(input);

        Assert.Single(beats);
        Assert.Equal(input, beats[0].DialogueText);
    }

    [Fact]
    public void SplitDialogue_TellTaleHeartOpening_SplitsAtPunctuationWithoutOrphans()
    {
        var monologue = "True!—nervous—very, very dreadfully nervous I had been and am; but why will you say that I am mad? The disease had sharpened my senses—not destroyed—not dulled them. Above all was the sense of hearing acute. I heard all things in the heaven and in the earth. I heard many things in hell. How, then, am I mad? Hearken! and observe how healthily—how calmly I can tell you the whole story.";

        var beats = DialoguePacingSplitter.SplitDialogue(monologue);

        Assert.True(beats.Count >= 3 && beats.Count <= 4);
        foreach (var b in beats)
        {
            Assert.True(b.WordCount >= 8, $"Beat too short ({b.WordCount} words): '{b.DialogueText}'");
            Assert.True(b.EstimatedDurationSeconds <= ClipDurationEstimator.AbsMaxSeconds + 0.05,
                $"Beat exceeded the model's absolute max ({b.EstimatedDurationSeconds}s): '{b.DialogueText}'");
        }
    }

    [Fact]
    public void SplitDialogue_NickAndMeSampleDialogue_SplitsCleanlyAtNaturalPauses()
    {
        var input = "My dad used to say that every dog has his own secret story, but Buster wasn't just any dog—he was the sort of dog who looked at you like he knew what you were thinking before you even said a word.";

        var beats = DialoguePacingSplitter.SplitDialogue(input);

        Assert.True(beats.Count >= 2);
        foreach (var b in beats)
        {
            Assert.True(b.WordCount >= 5);
            Assert.True(b.EstimatedDurationSeconds <= ClipDurationEstimator.AbsMaxSeconds + 0.05,
                $"Beat exceeded the model's absolute max ({b.EstimatedDurationSeconds}s): '{b.DialogueText}'");
        }
    }

    [Fact]
    public void SplitDialogue_OrphanWordProtection_MergesTrailingShortWords()
    {
        var input = "He had the eye of a vulture — a pale blue eye, with a film over it.";
        var beats = DialoguePacingSplitter.SplitDialogue(input);

        Assert.Single(beats);
        Assert.EndsWith("over it.", beats[0].DialogueText);
    }

    [Fact]
    public void SplitDialogue_LongClauseWithNoInternalPunctuation_StillRespectsHardMax()
    {
        // 36 words, a single trailing period only — no comma/semicolon/colon/em-dash anywhere,
        // so no punctuation tier can find a split point. Previously the "hard fallback" just
        // returned this as one unsplit 36-word chunk despite the hard cap.
        var input = "The old man moved through the darkened house so quietly that even the floorboards seemed to hold their breath while the candle flickered against the wall and the shadows stretched impossibly long across the empty room.";

        var beats = DialoguePacingSplitter.SplitDialogue(input);

        Assert.True(beats.Count >= 2, $"Expected the 36-word clause to split into multiple beats, got {beats.Count}");
        Assert.All(beats, b => Assert.True(b.EstimatedDurationSeconds <= ClipDurationEstimator.AbsMaxSeconds + 0.05,
            $"Beat exceeded the model's absolute max ({b.EstimatedDurationSeconds}s): '{b.DialogueText}'"));
    }

    [Fact]
    public void SplitDialogue_DerivesWordCapsFromModelSecondsBounds_NotHardcodedLiterals()
    {
        // A model with a much tighter cap than the generic default should force smaller beats —
        // proves targetMaxSeconds/hardMaxSeconds actually drive the split, not a fixed word count.
        var input = "True! Nervous, very, very dreadfully nervous I had been and am, but why will you say that I am mad?";

        var loose = DialoguePacingSplitter.SplitDialogue(input, targetMaxSeconds: 20, hardMaxSeconds: 20);
        var tight = DialoguePacingSplitter.SplitDialogue(input, targetMaxSeconds: 5, hardMaxSeconds: 6);

        Assert.True(tight.Count > loose.Count,
            $"Expected a tighter model duration bound to force more beats (loose={loose.Count}, tight={tight.Count})");
        Assert.All(tight, b => Assert.True(b.EstimatedDurationSeconds <= 6.05,
            $"Beat exceeded the requested 6s hard cap ({b.EstimatedDurationSeconds}s): '{b.DialogueText}'"));
    }
}
