using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PageToMovie.Core.Utils;

/// <summary>
/// Safe property access templates for System.Text.Json.JsonElement.
/// Replaces verbose, repeated TryGetProperty + ternary type checks across JSON parsers.
/// </summary>
public static class JsonElementExtensions
{
    public static string GetStringProp(this JsonElement el, string name, string fallback = "") =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? fallback : fallback;

    public static int GetIntProp(this JsonElement el, string name, int fallback = 0) =>
        el.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : fallback;

    public static double GetDoubleProp(this JsonElement el, string name, double fallback = 0.0) =>
        el.TryGetProperty(name, out var v) && v.TryGetDouble(out var d) ? d : fallback;

    public static bool GetBoolProp(this JsonElement el, string name, bool fallback = false) =>
        el.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : fallback;
}
