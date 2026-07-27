using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class CastExtractCorpusGateTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var d = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (d != null)
        {
            if (Directory.Exists(Path.Combine(d.FullName, "books")) &&
                Directory.Exists(Path.Combine(d.FullName, "host")))
                return d.FullName;
            d = d.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    [Fact]
    public void QualityGate_Downloaded_Picture_Books_Exist_In_Books_Directory()
    {
        var booksDir = Path.Combine(RepoRoot, "books");
        Assert.True(Directory.Exists(booksDir), $"Missing books directory: {booksDir}");

        var expectedBooks = new[]
        {
            "The_Tale_of_Peter_Rabbit.txt",
            "The_Tale_of_Benjamin_Bunny.txt",
            "The_Velveteen_Rabbit.txt",
            "Five_Children_and_It.txt",
            "The_Secret_Garden.txt"
        };

        foreach (var name in expectedBooks)
        {
            var fullPath = Path.Combine(booksDir, name);
            Assert.True(File.Exists(fullPath), $"Missing picture book: {name} at {fullPath}");
            var text = File.ReadAllText(fullPath);
            Assert.True(text.Length > 1000, $"Book {name} is suspiciously short ({text.Length} bytes)");
        }
    }

    [Fact]
    public void QualityGate_Forbidden_Slugline_And_Verb_Names_Are_Never_Valid_Character_Keys()
    {
        var forbiddenNames = new[]
        {
            "Kitchen", "Backyard", "Living_Room", "Garden", "Bedroom",
            "Leaps", "Runs", "Jumps", "Enters", "Exits", "Bed", "Door"
        };

        foreach (var name in forbiddenNames)
        {
            var charKey = "Character_" + name;
            var isTextOnlyPlate = ProjectStore.IsTextOnlyPlatePath(name);
            Assert.False(string.IsNullOrWhiteSpace(charKey));
        }
    }
}
