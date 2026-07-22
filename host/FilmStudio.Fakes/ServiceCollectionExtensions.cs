using FilmStudio.Core.Options;
using FilmStudio.Engine.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FilmStudio.Fakes;

public static class ServiceCollectionExtensions
{
    /// <summary>Register fake Grok clients (video/image/chat/vision).</summary>
    public static IServiceCollection AddFilmStudioFakes(this IServiceCollection services)
    {
        services.AddSingleton<IVideoClient, FakeGrokVideoClient>();
        services.AddSingleton<IImageClient, FakeGrokImageClient>();
        services.AddSingleton<IChatClient, FakeGrokChatClient>();
        services.AddSingleton<IVisionClient, FakeGrokVisionClient>();
        return services;
    }
}
