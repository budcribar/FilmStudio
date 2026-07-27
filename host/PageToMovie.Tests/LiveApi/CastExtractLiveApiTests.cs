using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests.LiveApi;

/// <summary>
/// PAID: real Grok cast extract against gold corpus cases.
/// Not run in default CI — see LiveApi/README.md.
/// </summary>
[Trait("Category", LiveApiGate.Category)]
public class CastExtractLiveApiTests
{
    private static string GoldRoot
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CastExtractGold");
            if (Directory.Exists(dir)) return dir;
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "CastExtractGold"));
        }
    }

    private static string PackageFountainDir
    {
        get
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "BookToFountainPackage", "fountain_adaptations");
            if (Directory.Exists(dir)) return dir;
            return Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "BookToFountainPackage", "fountain_adaptations"));
        }
    }

    public static IEnumerable<object[]> GoldCaseIds()
    {
        if (!Directory.Exists(GoldRoot))
            yield break;
        foreach (var dir in Directory.GetDirectories(GoldRoot)
                     .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var id = Path.GetFileName(dir);
            if (File.Exists(Path.Combine(dir, "expected_keys.json")))
                yield return new object[] { id };
        }
    }

    /// <summary>
    /// Full ExtractAsync with live chat — costs tokens per case.
    /// Prefer <see cref="Live_buster_cast_includes_buster"/> when iterating cheaply.
    /// </summary>
    [LiveApiTheory]
    [MemberData(nameof(GoldCaseIds))]
    public async Task Live_cast_extract_covers_required_keys(string caseId)
    {
        await RunLiveExtractAssertRequiredAsync(caseId);
    }

    /// <summary>Single paid smoke call (Buster silent-hero regression).</summary>
    [LiveApiFact]
    public async Task Live_buster_cast_includes_buster()
    {
        await RunLiveExtractAssertRequiredAsync("buster");
    }

    private static async Task RunLiveExtractAssertRequiredAsync(string caseId)
    {
        var (fountain, book, required, forbidden) = LoadCase(caseId);
        var workspace = Path.Combine(Path.GetTempPath(), "ptm-live-cast-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workspace);
        try
        {
            var opts = Options.Create(new PageToMovieOptions
            {
                WorkspaceRoot = workspace,
                UseFakes = false,
            });

            var promptsSrc = FindRepoPromptsDir();
            Assert.True(promptsSrc is not null, "Could not locate prompts/fountain_to_cast.txt");
            var promptsDst = Path.Combine(workspace, "prompts");
            Directory.CreateDirectory(promptsDst);
            foreach (var f in Directory.GetFiles(promptsSrc!))
                File.Copy(f, Path.Combine(promptsDst, Path.GetFileName(f)), overwrite: true);

            var store = new ProjectStore(opts);
            var project = await store.CreateProjectAsync(caseId);
            var dir = store.GetProjectDir(project.Id!);
            Directory.CreateDirectory(Path.Combine(dir, "source"));
            await File.WriteAllTextAsync(Path.Combine(dir, "source", "screenplay.fountain"), fountain);
            if (!string.IsNullOrWhiteSpace(book))
                await File.WriteAllTextAsync(Path.Combine(dir, "source", "book_full.txt"), book);

            // Ensure env key is visible to GrokChatClient.ResolveApiKey
            var key = LiveApiGate.ResolveXaiApiKey();
            Assert.False(string.IsNullOrWhiteSpace(key), "XAI_API_KEY required for live tests");
            Environment.SetEnvironmentVariable("XAI_API_KEY", key);

            using var http = new HttpClient { BaseAddress = new Uri(GrokChatClient.ApiBase + "/") };
            http.Timeout = TimeSpan.FromMinutes(4);
            var telemetry = new ProjectTelemetryService(store, NullLogger<ProjectTelemetryService>.Instance);
            var chat = new GrokChatClient(
                http, opts, telemetry, NullLogger<GrokChatClient>.Instance);
            Assert.True(chat.IsConfigured, "GrokChatClient not configured — check XAI_API_KEY");

            var literalize = new CastVisualLiteralizeService(
                store, chat, NullLogger<CastVisualLiteralizeService>.Instance);
            var learning = new ReviewEventStore(store, NullLogger<ReviewEventStore>.Instance);
            var rules = new ProjectRulesService(
                store, learning, NullLogger<ProjectRulesService>.Instance);
            var cast = new CastFromScreenplayService(
                store, chat, literalize, rules, NullLogger<CastFromScreenplayService>.Instance);

            var result = await cast.ExtractAsync(project.Id!, force: true, model: "grok-4.5");
            Assert.True(result.Ok, $"[{caseId}] extract failed: {result.Error}");
            Assert.NotEmpty(result.CharacterKeys);

            var have = result.CharacterKeys;
            var missing = required.Where(req => !HaveCovers(have, req)).ToList();
            Assert.True(
                missing.Count == 0,
                $"[{caseId}] live extract missing keys: {string.Join(", ", missing)}. " +
                $"Got: {string.Join(", ", have)}");

            // Forbidden fragments (slugline places, title junk) must not appear as cast keys
            foreach (var bad in forbidden)
            {
                var hit = have.FirstOrDefault(k =>
                    k.Contains(bad, StringComparison.OrdinalIgnoreCase));
                Assert.True(
                    hit is null,
                    $"[{caseId}] live extract produced forbidden cast key '{hit}' (fragment '{bad}'). " +
                    $"Got: {string.Join(", ", have)}");
            }
        }
        finally
        {
            try { Directory.Delete(workspace, true); } catch { /* ignore */ }
        }
    }

    private static (string Fountain, string Book, List<string> Required, List<string> Forbidden) LoadCase(string caseId)
    {
        var dir = Path.Combine(GoldRoot, caseId);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "expected_keys.json")));
        var root = doc.RootElement;
        var required = root.GetProperty("required_keys").EnumerateArray()
            .Select(e => e.GetString() ?? "")
            .Where(s => s.Length > 0)
            .ToList();

        var forbidden = new List<string>();
        if (root.TryGetProperty("forbidden_key_substrings", out var forb) &&
            forb.ValueKind == JsonValueKind.Array)
        {
            forbidden = forb.EnumerateArray()
                .Select(e => e.GetString() ?? "")
                .Where(s => s.Length > 0)
                .ToList();
        }

        string fountain;
        if (root.TryGetProperty("fountain_from_package", out var pkg) &&
            pkg.GetString() is { Length: > 0 } name)
            fountain = File.ReadAllText(Path.Combine(PackageFountainDir, name));
        else
            fountain = File.ReadAllText(Path.Combine(dir, "screenplay.fountain"));

        var book = File.ReadAllText(Path.Combine(dir, "book.txt"));
        return (fountain, book, required, forbidden);
    }

    private static bool HaveCovers(IReadOnlyList<string> have, string required)
    {
        if (have.Any(h => string.Equals(h, required, StringComparison.OrdinalIgnoreCase)))
            return true;
        var want = required.Replace("Character_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
        return have.Any(h =>
        {
            var n = h.Replace("Character_", "", StringComparison.OrdinalIgnoreCase)
                .Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
            return n.Contains(want, StringComparison.Ordinal) || want.Contains(n, StringComparison.Ordinal);
        });
    }

    private static string? FindRepoPromptsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 10 && dir is not null; i++, dir = dir.Parent)
        {
            var p = Path.Combine(dir.FullName, "prompts", "fountain_to_cast.txt");
            if (File.Exists(p))
                return Path.GetDirectoryName(p);
        }
        return null;
    }
}
