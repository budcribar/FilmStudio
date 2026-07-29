using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PageToMovie.Core.Utils;

/// <summary>
/// Composable generic pipeline template for multi-stage regex text sanitization and scrubbing.
/// Replaces sequential .Replace() chains across text scrubbers and markdown cleaners.
/// </summary>
public sealed class TextSanitizerPipeline
{
    public sealed record Rule(Regex Pattern, string Replacement);

    private readonly List<Rule> _rules = new();

    public TextSanitizerPipeline Add(Regex pattern, string replacement = "")
    {
        ArgumentNullException.ThrowIfNull(pattern);
        _rules.Add(new Rule(pattern, replacement ?? ""));
        return this;
    }

    public string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";
        var text = input;
        foreach (var rule in _rules)
        {
            text = rule.Pattern.Replace(text, rule.Replacement);
        }
        return text;
    }
}
