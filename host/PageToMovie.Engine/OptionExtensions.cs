using Microsoft.Extensions.Options;

namespace PageToMovie.Engine;

/// <summary>
/// Generic extension template for IOptions&lt;T&gt; unpacking across DI services.
/// </summary>
public static class OptionExtensions
{
    public static T GetOrDefault<T>(this IOptions<T>? options) where T : class, new() =>
        options?.Value ?? new T();
}
