using System;

namespace Jellyfin.Plugin.HLSDownloader.Models
{
    /// <summary>
    /// Represents a persisted download job.
    /// </summary>
    public sealed class DownloadJobEntity
    {
        /// <summary>
        /// Gets or sets the job identifier.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the source HLS URL.
        /// </summary>
        public string StartUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the output directory or resulting file path.
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current job state.
        /// </summary>
        public string Status { get; set; } = "QUEUED";

        /// <summary>
        /// Gets or sets the creation timestamp in UTC.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the completion timestamp in UTC.
        /// </summary>
        public DateTime? FinishedAt { get; set; }

        /// <summary>
        /// Gets or sets the error message when a job fails.
        /// </summary>
        public string? ErrorMessage { get; set; }
    }
}
