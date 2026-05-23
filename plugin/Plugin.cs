using System;
using System.Collections.Generic;
using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.MovieCatalog;

/// <summary>
/// Plugin entrypoint. Identity (Name, Id, Description) and the embedded
/// configPage.html resource registration.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Movie Catalog";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("7476643a-a7aa-47eb-8116-5856ce955bb2");

    /// <inheritdoc />
    public override string Description => "Mirrors the movie library to a GitHub-hosted JSON for off-network browsing.";

    /// <summary>
    /// Static accessor so non-DI sites (the scheduled task entry, the
    /// debouncer, the pusher) can read the latest configuration without
    /// having to capture it at construction time.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages() => new[]
    {
        new PluginPageInfo
        {
            Name = Name,
            EmbeddedResourcePath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.Web.configPage.html",
                GetType().Namespace),
        },
    };
}
