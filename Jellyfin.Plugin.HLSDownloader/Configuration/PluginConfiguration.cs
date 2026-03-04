using MediaBrowser.Model.Plugins;
using System.ComponentModel;

namespace Jellyfin.Plugin.HLSDownloader.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the downloadDistPath (lokaler Pfad auf Jellyfin-System).
    /// </summary>
    [DefaultValue("")]
    public string DownloadDistPath { get; set; } = "";
}
