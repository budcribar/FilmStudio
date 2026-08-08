using System.Globalization;

namespace PageToMovie.Core.Localization;

/// <summary>
/// Centralized localizer interface for PageToMovie supporting JSON-based multi-language translation.
/// </summary>
public interface IAppLocalizer
{
    /// <summary>
    /// Gets the localized string for the specified key in the current culture.
    /// </summary>
    string this[string key] { get; }

    /// <summary>
    /// Gets the localized string for the specified key formatted with the provided arguments.
    /// </summary>
    string Format(string key, params object[] args);

    /// <summary>
    /// Currently active UI culture.
    /// </summary>
    CultureInfo CurrentCulture { get; }

    /// <summary>
    /// Event triggered when the current UI culture changes.
    /// </summary>
    event Action<CultureInfo>? CultureChanged;

    /// <summary>
    /// Changes the active UI culture.
    /// </summary>
    void SetCulture(string cultureCode);

    /// <summary>
    /// List of all supported UI cultures in PageToMovie.
    /// </summary>
    IReadOnlyList<CultureInfo> SupportedCultures { get; }
}
