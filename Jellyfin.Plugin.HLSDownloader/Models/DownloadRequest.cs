using System;

namespace Jellyfin.Plugin.HLSDownloader.Models
{
    /// <summary>
    /// Request payload for starting a download.
    /// </summary>
    public sealed class DownloadRequest
    {
        /// <summary>
        /// Gets or sets the HLS playlist URL.
        /// </summary>
        public Uri StartUrl { get; set; } = new("https://example.invalid/playlist.m3u8", UriKind.Absolute);

        /// <summary>
        /// Gets or sets the required absolute output file path (.mkv).
        /// </summary>
        public string OutputPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an optional external reference that is stored with the job.
        /// </summary>
        public string? Ref { get; set; }
    }
}
