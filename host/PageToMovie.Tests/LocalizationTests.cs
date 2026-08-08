using System.Globalization;
using System.Text.Json.Nodes;
using PageToMovie.Core.Localization;
using Xunit;

namespace PageToMovie.Tests;

public sealed class LocalizationTests
{
    private readonly JsonAppLocalizer _localizer = new();

    [Fact]
    public void JsonAppLocalizer_LoadsEmbeddedDefaultCulture_ReturnsStringForKey()
    {
        var title = _localizer["Home.Title"];
        Assert.False(string.IsNullOrWhiteSpace(title));
        Assert.Equal("Film Studio Projects", title);
    }

    [Fact]
    public void JsonAppLocalizer_FormatString_FillsPlaceholders()
    {
        var formatted = _localizer.Format("Scenes.ClipCount", 5);
        Assert.Equal("5 clips", formatted);
    }

    [Fact]
    public void JsonAppLocalizer_MissingKey_ReturnsRawKeyWithoutThrowing()
    {
        var missingKey = "NonExistent.Key.Name";
        var result = _localizer[missingKey];
        Assert.Equal(missingKey, result);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("de")]
    public void AllSupportedLocales_HaveCompleteKeyParityWithEnglish(string cultureCode)
    {
        var enDict = GetResourceDictionary("en-US");
        var targetDict = GetResourceDictionary(cultureCode);

        Assert.NotEmpty(enDict);
        Assert.NotEmpty(targetDict);

        var missingKeys = new List<string>();
        foreach (var key in enDict.Keys)
        {
            if (!targetDict.ContainsKey(key))
            {
                missingKeys.Add(key);
            }
        }

        Assert.True(
            missingKeys.Count == 0,
            $"Target locale '{cultureCode}' is missing {missingKeys.Count} keys present in en-US.json:\n{string.Join("\n", missingKeys)}");
    }

    [Theory]
    [InlineData("es")]
    [InlineData("fr")]
    [InlineData("de")]
    public void AllSupportedLocales_PlaceholderCountsMatchEnglish(string cultureCode)
    {
        var enDict = GetResourceDictionary("en-US");
        var targetDict = GetResourceDictionary(cultureCode);

        foreach (var (key, enVal) in enDict)
        {
            if (!targetDict.TryGetValue(key, out var targetVal)) continue;

            var enPlaceholders = CountPlaceholders(enVal);
            var targetPlaceholders = CountPlaceholders(targetVal);

            Assert.True(
                enPlaceholders == targetPlaceholders,
                $"Key '{key}' in locale '{cultureCode}' has {targetPlaceholders} placeholders ({targetVal}), expected {enPlaceholders} from en-US ({enVal}).");
        }
    }

    private static Dictionary<string, string> GetResourceDictionary(string cultureCode)
    {
        var assembly = typeof(JsonAppLocalizer).Assembly;
        var resourceName = $"PageToMovie.Core.Localization.Resources.{cultureCode}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var node = JsonNode.Parse(stream);
        if (node is JsonObject obj)
        {
            FlattenJsonObject(obj, dict, "");
        }
        return dict;
    }

    private static void FlattenJsonObject(JsonObject obj, Dictionary<string, string> dict, string prefix)
    {
        foreach (var (propName, node) in obj)
        {
            var key = string.IsNullOrEmpty(prefix) ? propName : $"{prefix}.{propName}";
            if (node is JsonObject childObj)
            {
                FlattenJsonObject(childObj, dict, key);
            }
            else if (node is not null)
            {
                dict[key] = node.ToString();
            }
        }
    }

    private static int CountPlaceholders(string val)
    {
        if (string.IsNullOrEmpty(val)) return 0;
        var count = 0;
        for (var i = 0; i < 10; i++)
        {
            if (val.Contains($"{{{i}}}")) count++;
        }
        return count;
    }
}
