using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PageToMovie.Core.Localization;

/// <summary>
/// Embedded JSON resource-backed implementation of <see cref="IAppLocalizer"/>.
/// </summary>
public sealed class JsonAppLocalizer : IAppLocalizer
{
    private static readonly Assembly CoreAssembly = typeof(JsonAppLocalizer).Assembly;
    private const string ResourceNamespace = "PageToMovie.Core.Localization.Resources";
    private const string DefaultCultureCode = "en-US";

    private static readonly List<CultureInfo> SupportedCulturesList = new()
    {
        new CultureInfo("en-US"),
        new CultureInfo("es"),
        new CultureInfo("fr"),
        new CultureInfo("de"),
    };

    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _dictionaryCache = new(StringComparer.OrdinalIgnoreCase);
    private CultureInfo _currentCulture = CultureInfo.CurrentUICulture;

    public JsonAppLocalizer()
    {
        // Pre-load default culture dictionary
        GetOrLoadDictionary(DefaultCultureCode);
    }

    public CultureInfo CurrentCulture => _currentCulture;

    public event Action<CultureInfo>? CultureChanged;

    public IReadOnlyList<CultureInfo> SupportedCultures => SupportedCulturesList;

    public void SetCulture(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) return;
        try
        {
            var culture = new CultureInfo(cultureCode);
            _currentCulture = culture;
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureChanged?.Invoke(culture);
        }
        catch
        {
            // Ignore invalid culture codes
        }
    }

    public string this[string key] => GetLocalizedString(key);

    public string Format(string key, params object[] args)
    {
        var raw = GetLocalizedString(key);
        if (args is null || args.Length == 0) return raw;
        try
        {
            return string.Format(_currentCulture, raw, args);
        }
        catch
        {
            return raw;
        }
    }

    public string GetLocalizedString(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "";

        // 1. Try current culture (e.g. es-MX or es)
        var dict = GetOrLoadDictionary(_currentCulture.Name);
        if (dict is not null && dict.TryGetValue(key, out var val)) return val;

        // 2. Try two-letter language code if current culture was specific (e.g. es from es-MX)
        if (_currentCulture.Name.Contains('-'))
        {
            var baseDict = GetOrLoadDictionary(_currentCulture.TwoLetterISOLanguageName);
            if (baseDict is not null && baseDict.TryGetValue(key, out val)) return val;
        }

        // 3. Try default culture fallback (en-US)
        var defaultDict = GetOrLoadDictionary(DefaultCultureCode);
        if (defaultDict is not null && defaultDict.TryGetValue(key, out val)) return val;

        // 4. Fallback to raw key
        return key;
    }

    private Dictionary<string, string>? GetOrLoadDictionary(string cultureCode)
    {
        if (string.IsNullOrWhiteSpace(cultureCode)) return null;

        var normalized = NormalizeCultureCode(cultureCode);
        return _dictionaryCache.GetOrAdd(normalized, code =>
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Attempt exact match e.g. en-US.json or es.json
            var resourceName = $"{ResourceNamespace}.{code}.json";
            using var stream = CoreAssembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                // Fallback to two-letter ISO if available e.g. "es" from "es-ES"
                var iso = code.Split('-')[0];
                var fallbackResource = $"{ResourceNamespace}.{iso}.json";
                using var fallbackStream = CoreAssembly.GetManifestResourceStream(fallbackResource);
                if (fallbackStream is null) return dict;
                LoadStreamIntoDictionary(fallbackStream, dict, "");
                return dict;
            }

            LoadStreamIntoDictionary(stream, dict, "");
            return dict;
        });
    }

    private static void LoadStreamIntoDictionary(Stream stream, Dictionary<string, string> dict, string prefix)
    {
        try
        {
            var node = JsonNode.Parse(stream);
            if (node is JsonObject obj)
            {
                FlattenJsonObject(obj, dict, prefix);
            }
        }
        catch
        {
            // Ignore corrupted JSON streams
        }
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

    private static string NormalizeCultureCode(string code) => code.Trim();
}
