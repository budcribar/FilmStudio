using PageToMovie.Engine.Deterministic.Pronunciation;
using Xunit;

namespace PageToMovie.Tests;

public sealed class PronunciationResolverTests
{
    [Theory]
    [InlineData("Please tear up the old boards.", "tear.rip")]
    [InlineData("A tear rolled down her cheek.", "tear.eye")]
    [InlineData("Lead us through the doorway.", "lead.guide")]
    [InlineData("The lead pipe felt heavy.", "lead.metal")]
    [InlineData("Wind the clock before midnight.", "wind.turn")]
    [InlineData("The wind howled outside.", "wind.air")]
    [InlineData("Please present the evidence.", "present.show")]
    [InlineData("The present moment was enough.", "present.current")]
    public void Resolves_heteronyms_from_general_context(string dialogue, string senseId)
    {
        var result = PronunciationResolver.Default.Resolve(dialogue);

        var annotation = Assert.Single(result.Annotations);
        Assert.Equal(senseId, annotation.SenseId);
        Assert.Empty(result.Unresolved);
    }

    [Theory]
    [InlineData("Pronounce 'wind' as /waɪnd/ (turn or coil)", "Wind the clock!", true)]   // word present
    [InlineData("Pronounce 'wind' as /waɪnd/ (turn or coil)", "Hello there, friend!", false)] // word absent
    [InlineData("Pronounce 'wind' as /waɪnd/ (turn or coil)", "", false)]                  // no dialogue
    [InlineData("Pronounce 'wind' as /waɪnd/ (turn or coil)", null, false)]                // no dialogue
    [InlineData("free-form hint with no quoted word", "Any spoken line.", true)]           // no target → only needs dialogue
    [InlineData("free-form hint with no quoted word", "", false)]
    [InlineData("", "Wind the clock!", false)]                                             // no hint
    public void HintAppliesToDialogue_gates_on_word_presence(string hint, string? dialogue, bool expected)
    {
        Assert.Equal(expected, PronunciationResolver.HintAppliesToDialogue(hint, dialogue));
    }

    [Fact]
    public void Ambiguous_context_is_reported_instead_of_guessed()
    {
        var result = PronunciationResolver.Default.Resolve("I noticed the bass.");

        Assert.Empty(result.Annotations);
        var unresolved = Assert.Single(result.Unresolved);
        Assert.Equal("bass", unresolved.Token, ignoreCase: true);
        Assert.Equal(2, unresolved.CandidateSenseIds.Count);
    }

    [Fact]
    public void Annotation_offsets_reference_immutable_dialogue()
    {
        const string dialogue = "They will produce another film.";
        var annotation = Assert.Single(PronunciationResolver.Default.Resolve(dialogue).Annotations);

        Assert.Equal(annotation.Token, dialogue.Substring(annotation.Start, annotation.Length));
        Assert.Equal("produce.create", annotation.SenseId);
    }
}
