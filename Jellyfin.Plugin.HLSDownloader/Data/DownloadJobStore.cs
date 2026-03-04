using System;
using System.IO;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Plugin.HLSDownloader.Data
{
    /// <summary>
    /// Factory and utility helpers for the download jobs SQLite store.
    /// </summary>
    internal static class DownloadJobStore
    {
        /// <summary>
        /// Creates a new <see cref="DownloadJobDbContext"/> instance and ensures schema exists.
        /// </summary>
        /// <returns>Ready-to-use DB context.</returns>
        internal static DownloadJobDbContext CreateContext()
        {
            var dbPath = GetDatabasePath();
            var options = new DbContextOptionsBuilder<DownloadJobDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            var context = new DownloadJobDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        private static string GetDatabasePath()
        {
            var appDataDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var pluginDir = Path.Combine(appDataDir, "jellyfin-hls-downloader");
            Directory.CreateDirectory(pluginDir);
            return Path.Combine(pluginDir, "jobs.db");
        }
    }
}
