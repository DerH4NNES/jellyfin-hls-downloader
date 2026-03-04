using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HLSDownloader.Data;
using Jellyfin.Plugin.HLSDownloader.Service;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.HLSDownloader.ScheduledTask
{
    /// <summary>
    /// Scheduled task that processes queued download jobs.
    /// </summary>
    public class DownloadJobScheduledTask : IScheduledTask
    {
        private readonly ILogger<DownloadJobScheduledTask> _logger;
        private readonly DownloadJobQueueService _queueService;

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadJobScheduledTask"/> class.
        /// </summary>
        /// <param name="logger">Logger dependency.</param>
        public DownloadJobScheduledTask(ILogger<DownloadJobScheduledTask> logger)
        {
            _logger = logger;
            _queueService = new DownloadJobQueueService(new DownloadEngine(NullLogger<DownloadEngine>.Instance));
        }

        /// <inheritdoc />
        public string Name => "HLS Download Job Processor";

        /// <inheritdoc />
        public string Description => "Processes queued HLS download jobs.";

        /// <inheritdoc />
        public string Category => "HLS Downloader";

        /// <inheritdoc />
        public string Key => "HLSDownloadJobScheduledTask";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return
            [
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerInterval,
                    IntervalTicks = TimeSpan.FromMinutes(1).Ticks
                }
            ];
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(progress);

            var jobs = await DownloadJobRepository
                .GetQueuedJobsAsync(cancellationToken)
                .ConfigureAwait(false);

            if (jobs.Count == 0)
            {
                progress.Report(1d);
                return;
            }

            var processed = 0;
            foreach (var job in jobs)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var wasSetRunning = await DownloadJobRepository.TryUpdateJobAsync(
                    job.Id,
                    current =>
                    {
                        current.Status = "RUNNING";
                        current.ErrorMessage = null;
                    },
                    cancellationToken).ConfigureAwait(false);

                if (!wasSetRunning)
                {
                    processed++;
                    progress.Report((double)processed / jobs.Count);
                    continue;
                }

                try
                {
                    _ = await _queueService.ProcessJobAsync(job, cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Job processing failed for job {JobId}.", job.Id);
                    await DownloadJobRepository.TryUpdateJobAsync(
                        job.Id,
                        current =>
                        {
                            current.Status = "ERROR";
                            current.ErrorMessage = ex.Message;
                            current.FinishedAt = DateTime.UtcNow;
                        },
                        cancellationToken).ConfigureAwait(false);
                }
                catch (System.IO.IOException ex)
                {
                    _logger.LogWarning(ex, "Job processing failed for job {JobId}.", job.Id);
                    await DownloadJobRepository.TryUpdateJobAsync(
                        job.Id,
                        current =>
                        {
                            current.Status = "ERROR";
                            current.ErrorMessage = ex.Message;
                            current.FinishedAt = DateTime.UtcNow;
                        },
                        cancellationToken).ConfigureAwait(false);
                }

                processed++;
                progress.Report((double)processed / jobs.Count);
            }
        }
    }
}
