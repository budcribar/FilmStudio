using FilmStudio.Core.Options;
using FilmStudio.Engine.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FilmStudio.Fakes;

public static class ServiceCollectionExtensions
{
    /// <summary>Register fake Grok clients (video/image/chat/vision).</summary>
    public static IServiceCollection AddFilmStudioFakes(this IServiceCollection services)
    {
        services.AddSingleton<IGrokVideoClient, FakeGrokVideoClient>();
        services.AddSingleton<IGrokImageClient, FakeGrokImageClient>();
        services.AddSingleton<IGrokChatClient, FakeGrokChatClient>();
        services.AddSingleton<IGrokVisionClient, FakeGrokVisionClient>();
        return services;
    }
}
