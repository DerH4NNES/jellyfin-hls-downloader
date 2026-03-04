using Jellyfin.Plugin.HLSDownloader.Models;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Plugin.HLSDownloader.Data
{
    /// <summary>
    /// EF Core database context for persisted download jobs.
    /// </summary>
    public sealed class DownloadJobDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadJobDbContext"/> class.
        /// </summary>
        /// <param name="options">Database context options.</param>
        public DownloadJobDbContext(DbContextOptions<DownloadJobDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Gets or sets the download jobs table.
        /// </summary>
        public DbSet<DownloadJobEntity> Jobs { get; set; }
    }
}
