using System;
using Microsoft.Data.Sqlite;
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
            var context = Plugin.CreateDbContext();
            context.Database.EnsureCreated();
            EnsureRefColumn(context);
            return context;
        }

        private static void EnsureRefColumn(DownloadJobDbContext context)
        {
            try
            {
                _ = context.Database.ExecuteSqlRaw("ALTER TABLE Jobs ADD COLUMN Ref TEXT NULL;");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
            {
            }
        }
    }
}
