using System.Net.Http;
using Jellyfin.Plugin.ArrProfileSwitcher.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.ArrProfileSwitcher;

/// <summary>
/// Registers the plugin's services into Jellyfin's DI container at server startup.
/// </summary>
/// <remarks>
/// <c>AddHttpClient()</c> + resolving <see cref="IHttpClientFactory"/> to hand
/// <see cref="RadarrClient"/>/<see cref="SonarrClient"/> their own <see cref="HttpClient"/>
/// mirrors the live, working pattern in
/// <c>Jellyfin.Plugin.HomeScreenSections.PluginServiceRegistrator</c> (also installed
/// on this NUC's Jellyfin) for its own <c>ArrApiService</c>.
/// </remarks>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Shared cooldown state — must be a singleton so it's the same instance across
        // every request, not recreated per (transient/scoped) controller instance.
        serviceCollection.AddSingleton<ArrUpgradeThrottleState>();

        serviceCollection.AddHttpClient();

        serviceCollection.AddSingleton(services =>
        {
            var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            return ActivatorUtilities.CreateInstance<RadarrClient>(services, httpClientFactory.CreateClient());
        });

        serviceCollection.AddSingleton(services =>
        {
            var httpClientFactory = services.GetRequiredService<IHttpClientFactory>();
            return ActivatorUtilities.CreateInstance<SonarrClient>(services, httpClientFactory.CreateClient());
        });
    }
}
