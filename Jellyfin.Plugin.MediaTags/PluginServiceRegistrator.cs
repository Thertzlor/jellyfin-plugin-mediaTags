using Jellyfin.Plugin.MediaTags.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MediaTags;

/// <summary>
/// Register plugin service.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc/>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Register services
        serviceCollection.AddSingleton<ConfigurationService>();
        serviceCollection.AddSingleton<MediaTagService>();
        serviceCollection.AddSingleton<LibraryQueryService>();

        // Register MediaTagsManager as both singleton and hosted service
        serviceCollection.AddSingleton<MediaTagsManager>();
        serviceCollection.AddHostedService(provider => provider.GetRequiredService<MediaTagsManager>());
    }
}
