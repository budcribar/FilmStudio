using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public sealed class DialoguePacingSplitterTests
{
    [Fact]
    public void SplitDialogue_ShortDialogue_ReturnsSingleBeat()
    {
        var input = "Gentlemen — welcome. The shriek was my own, in a dream.";
        var beats = DialoguePacingSplitter.SplitDialogue(input, targetMaxWords: 22);

        Assert.Single(beats);
        Assert.Equal(input, beats[0].DialogueText);
    }

    [Fact]
    public void SplitDialogue_TellTaleHeartOpening_SplitsAtPunctuationWithoutOrphans()
    {
        var monologue = "True!—nervous—very, very dreadfully nervous I had been and am; but why will you say that I am mad? The disease had sharpened my senses—not destroyed—not dulled them. Above all was the sense of hearing acute. I heard all things in the heaven and in the earth. I heard many things in hell. How, then, am I mad? Hearken! and observe how healthily—how calmly I can tell you the whole story.";

        var beats = DialoguePacingSplitter.SplitDialogue(monologue, targetMaxWords: 22);

        Assert.True(beats.Count >= 3 && beats.Count <= 4);
        foreach (var b in beats)
        {
            Assert.True(b.WordCount >= 8, $"Beat too short ({b.WordCount} words): '{b.DialogueText}'");
            Assert.True(b.WordCount <= 25, $"Beat too long ({b.WordCount} words): '{b.DialogueText}'");
        }
    }

    [Fact]
    public void SplitDialogue_NickAndMeSampleDialogue_SplitsCleanlyAtNaturalPauses()
    {
        var input = "My dad used to say that every dog has his own secret story, but Buster wasn't just any dog—he was the sort of dog who looked at you like he knew what you were thinking before you even said a word.";

        var beats = DialoguePacingSplitter.SplitDialogue(input, targetMaxWords: 22);

        Assert.True(beats.Count >= 2);
        foreach (var b in beats)
        {
            Assert.True(b.WordCount >= 5);
            Assert.True(b.WordCount <= 25);
        }
    }

    [Fact]
    public void SplitDialogue_OrphanWordProtection_MergesTrailingShortWords()
    {
        var input = "He had the eye of a vulture — a pale blue eye, with a film over it.";
        var beats = DialoguePacingSplitter.SplitDialogue(input, targetMaxWords: 22);

        Assert.Single(beats);
        Assert.EndsWith("over it.", beats[0].DialogueText);
    }
}
