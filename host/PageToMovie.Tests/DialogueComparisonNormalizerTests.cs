using PageToMovie.Engine.Deterministic;
using Xunit;

namespace PageToMovie.Tests;

public sealed class DialogueComparisonNormalizerTests
{
    [Fact]
    public void Comparison_normalization_records_regional_spelling_without_mutating_source()
    {
        const string original = "Her favourite colour filled the theatre.";
        var result = DialogueComparisonNormalizer.Normalize(original);

        Assert.Equal(original, result.Original);
        Assert.Contains("favorite", result.Tokens);
        Assert.Contains("color", result.Tokens);
        Assert.Contains("theater", result.Tokens);
        Assert.Equal(3, result.Changes.Count);
    }

    [Fact]
    public void Historical_forms_are_not_modernized_by_default()
    {
        var result = DialogueComparisonNormalizer.Normalize("Thou hast my word.");

        Assert.Equal(new[] { "thou", "hast", "my", "word" }, result.Tokens);
        Assert.Empty(result.Changes);
    }

    [Fact]
    public void Historical_comparison_is_explicit_and_auditable()
    {
        var result = DialogueComparisonNormalizer.Normalize(
            "Thou hast my word.",
            new DialogueNormalizationOptions { NormalizeHistoricalForms = true });

        Assert.Equal(new[] { "you", "have", "my", "word" }, result.Tokens);
        Assert.Equal(2, result.Changes.Count(change => change.Rule == "historical_comparison_form"));
    }

    [Fact]
    public void Fidelity_validator_rejects_modernized_emitted_dialogue()
    {
        var issues = DialogueComparisonNormalizer.ValidateDialogueUnchanged(
            "Thou hast my word.",
            "You have my word.");

        Assert.Contains(issues, issue => issue.Code == "dialogue_mutated");
    }
}
