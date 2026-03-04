using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HLSDownloader.Data;
using Jellyfin.Plugin.HLSDownloader.Models;

namespace Jellyfin.Plugin.HLSDownloader.Service
{
    /// <summary>
    /// Queue facade for persisted download jobs.
    /// </summary>
    public sealed class DownloadJobQueueService
    {
        private readonly IDownloadEngine _downloadEngine;

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadJobQueueService"/> class.
        /// </summary>
        /// <param name="downloadEngine">Download engine instance.</param>
        public DownloadJobQueueService(IDownloadEngine downloadEngine)
        {
            _downloadEngine = downloadEngine;
        }

        /// <summary>
        /// Processes one job if possible.
        /// </summary>
        /// <param name="job">Job to process.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if completed successfully; otherwise false.</returns>
        public async Task<bool> ProcessJobAsync(DownloadJobEntity job, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(job.StartUrl, UriKind.Absolute, out var startUri))
            {
                await DownloadJobRepository.TryUpdateJobAsync(
                    job.Id,
                    current =>
                    {
                        current.Status = "ERROR";
                        current.ErrorMessage = "Job start URL is not a valid absolute URI.";
                        current.FinishedAt = DateTime.UtcNow;
                    },
                    cancellationToken).ConfigureAwait(false);
                return false;
            }

            _ = await _downloadEngine.OrchestrateDownloadAsync(startUri, job.OutputPath, cancellationToken).ConfigureAwait(false);
            await DownloadJobRepository.DeleteJobAsync(job.Id, cancellationToken).ConfigureAwait(false);
            return true;
        }
    }
}
