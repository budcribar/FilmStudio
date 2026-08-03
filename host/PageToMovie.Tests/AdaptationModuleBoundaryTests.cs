using System.Reflection;
using System.Xml.Linq;
using PageToMovie.Adaptation;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// Architecture guards for <c>PageToMovie.Adaptation</c> (plan A6.1): pure Stage‑1 module must not
/// reference Engine (or other product I/O hosts).
/// </summary>
public sealed class AdaptationModuleBoundaryTests
{
    [Fact]
    public void Adaptation_assembly_does_not_reference_Engine()
    {
        var assembly = typeof(AdaptationVersion).Assembly;
        var refs = assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(n => n.Length > 0)
            .ToArray();

        Assert.DoesNotContain(refs, n =>
            string.Equals(n, "PageToMovie.Engine", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(refs, n =>
            n.StartsWith("PageToMovie.Engine", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Adaptation_csproj_does_not_reference_Engine()
    {
        var csprojPath = FindAdaptationCsproj();
        Assert.True(File.Exists(csprojPath), $"Missing csproj at {csprojPath}");

        var doc = XDocument.Load(csprojPath);
        var refs = doc.Descendants()
            .Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => (string?)e.Attribute("Include") ?? "")
            .Where(s => s.Length > 0)
            .ToArray();

        Assert.DoesNotContain(refs, r =>
            r.Contains("PageToMovie.Engine", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(refs, r =>
            r.Contains("PageToMovie.Api", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(refs, r =>
            r.Contains("PageToMovie.Web", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(refs, r =>
            r.Contains("PageToMovie.Fakes", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(refs, r =>
            r.Contains("PageToMovie.Core", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AdaptationVersion_Current_is_stable_short_hex()
    {
        var id = AdaptationVersion.Current;
        Assert.Equal(12, id.Length);
        Assert.Matches("^[0-9a-f]{12}$", id);
        Assert.Equal(id, AdaptationVersion.ComputeId());
    }

    private static string FindAdaptationCsproj()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "host", "PageToMovie.Adaptation", "PageToMovie.Adaptation.csproj");
            if (File.Exists(candidate))
                return candidate;
            // When tests run from host/PageToMovie.Tests/bin/...
            candidate = Path.Combine(dir.FullName, "PageToMovie.Adaptation", "PageToMovie.Adaptation.csproj");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("PageToMovie.Adaptation.csproj not found from test base directory.");
    }
}
