using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MovieCatalog;

/// <summary>
/// Plugin configuration; backed by Jellyfin's XmlSerializer-based plugin
/// config store. Values are surfaced through the embedded configPage.html
/// admin form. Read via Plugin.Instance.Configuration on every push, so
/// rotation takes effect immediately without a service restart.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>GitHub username or organization that owns the target repo.</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>Target repo name.</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>Branch to commit catalog updates to.</summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Path inside the repo where movies.json is written. Default works
    /// with GitHub Pages serving from main /docs.
    /// </summary>
    public string JsonPath { get; set; } = "docs/movies.json";

    /// <summary>
    /// Fine-grained Personal Access Token with Contents: read+write on
    /// the target repo. Stored unencrypted on disk by Jellyfin's plugin
    /// config; document narrow scoping on the settings page.
    /// </summary>
    public string PatToken { get; set; } = string.Empty;

    /// <summary>
    /// Quiet window after the last library change before pushing.
    /// Coalesces burst events during a library scan.
    /// </summary>
    public int DebounceSeconds { get; set; } = 30;
}
