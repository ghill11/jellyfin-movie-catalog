using System;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.MovieCatalog;

/// <summary>
/// Auto-discovered by Jellyfin's plugin loader on startup. Registers the
/// debouncer, catalog builder, GitHub pusher, named HttpClient, and the
/// hosted LibraryEventListener.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<MovieCatalogBuilder>();
        serviceCollection.AddSingleton<GitHubPusher>();
        serviceCollection.AddSingleton<Debouncer>();

        serviceCollection.AddHttpClient("github", client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.Add("User-Agent", "jellyfin-movie-catalog/0.1.0");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        serviceCollection.AddHostedService<LibraryEventListener>();
    }
}
