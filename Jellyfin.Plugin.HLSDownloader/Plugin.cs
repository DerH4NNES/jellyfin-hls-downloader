using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Jellyfin.Plugin.HLSDownloader.Configuration;
using Jellyfin.Plugin.HLSDownloader.Data;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Plugin.HLSDownloader;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly string _dbPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        ArgumentNullException.ThrowIfNull(applicationPaths);
        Instance = this;

        var pluginDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "jellyfin-hls-downloader");
        Directory.CreateDirectory(pluginDir);
        _dbPath = Path.Combine(pluginDir, "jobs.db");

        try
        {
            _ = DownloadJobRepository.ResetErroredJobsToQueuedAsync(CancellationToken.None).GetAwaiter().GetResult();
            _ = DownloadJobRepository.ResetRunningJobsToQueuedAsync(CancellationToken.None).GetAwaiter().GetResult();
            _ = DownloadJobRepository.DeleteJobsByStatusAsync("DOWNLOADING", CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
        }
    }

    /// <inheritdoc />
    public override string Name => "HLS Downloader";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("d0e54984-2989-45bc-b81e-c71838bdba50");

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the full path to the plugin database file.
    /// </summary>
    public string DbPath => _dbPath;

    /// <summary>
    /// Creates a new <see cref="DownloadJobDbContext"/> instance configured for the plugin database.
    /// </summary>
    /// <returns>A new <see cref="DownloadJobDbContext"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the plugin has not been initialized.</exception>
    public static DownloadJobDbContext CreateDbContext()
    {
        ArgumentNullException.ThrowIfNull(Instance);

        var options = new DbContextOptionsBuilder<DownloadJobDbContext>()
            .UseSqlite($"Data Source={Instance.DbPath}")
            .Options;

        return new DownloadJobDbContext(options);
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name + " Configuration",
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            },
            new PluginPageInfo
            {
                Name = Name + " Jobs",
                EnableInMainMenu = true,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.jobsPage.html", GetType().Namespace)
            },
        ];
    }
}
