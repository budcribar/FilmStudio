using PageToMovie.Fountain;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// Architecture guard for the shared <c>PageToMovie.Fountain</c> module: it is the leaf that both
/// Engine and Adaptation consume, so it must reference NOTHING but the framework — never Engine,
/// Adaptation, Core, Api, Web, or Fakes. If it grew a back-edge to Engine, Adaptation could reach
/// Engine transitively and the module boundary (AGENTS.md rule 10) would silently break.
/// </summary>
public sealed class FountainModuleBoundaryTests
{
    [Fact]
    public void Fountain_assembly_references_no_other_PageToMovie_project()
    {
        var assembly = typeof(FountainParser).Assembly;
        Assert.Equal("PageToMovie.Fountain", assembly.GetName().Name);

        var offenders = assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(n => n.StartsWith("PageToMovie.", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.True(offenders.Length == 0,
            "PageToMovie.Fountain must be a leaf module (no PageToMovie.* references), but found: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void Fountain_lexer_and_parser_live_in_the_Fountain_assembly()
    {
        Assert.Equal("PageToMovie.Fountain", typeof(FountainLexer).Assembly.GetName().Name);
        Assert.Equal("PageToMovie.Fountain", typeof(FountainParser).Assembly.GetName().Name);
    }
}
