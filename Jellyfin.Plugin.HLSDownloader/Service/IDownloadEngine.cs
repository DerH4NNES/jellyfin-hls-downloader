using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.HLSDownloader.Service
{
    /// <summary>
    /// Contract for HLS media download orchestration.
    /// </summary>
    public interface IDownloadEngine
    {
        /// <summary>
        /// Downloads the given HLS URL and creates an MKV file.
        /// </summary>
        /// <param name="startUrl">HLS playlist URL.</param>
        /// <param name="outputDir">Directory where the output file is written.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Full path to the created MKV file.</returns>
        Task<string> OrchestrateDownloadAsync(Uri startUrl, string outputDir, CancellationToken cancellationToken = default);
    }
}
