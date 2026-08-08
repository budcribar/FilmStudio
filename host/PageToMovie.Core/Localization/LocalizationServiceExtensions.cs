using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PageToMovie.Core.Localization;

public static class LocalizationServiceExtensions
{
    /// <summary>
    /// Adds PageToMovie JSON localization services (<see cref="IAppLocalizer"/>) to the service collection.
    /// </summary>
    public static IServiceCollection AddAppLocalization(this IServiceCollection services)
    {
        services.TryAddSingleton<IAppLocalizer, JsonAppLocalizer>();
        return services;
    }
}
