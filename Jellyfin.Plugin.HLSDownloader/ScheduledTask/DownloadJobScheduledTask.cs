using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private const string TaskKey = "HLSDownloadJobScheduledTask";
        private readonly ILogger<DownloadJobScheduledTask> _logger;
        private readonly DownloadJobQueueService _queueService;
        private readonly ITaskManager _taskManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadJobScheduledTask"/> class.
        /// </summary>
        /// <param name="logger">Logger dependency.</param>
        /// <param name="taskManager">Task manager dependency.</param>
        public DownloadJobScheduledTask(ILogger<DownloadJobScheduledTask> logger, ITaskManager taskManager)
        {
            _logger = logger;
            _queueService = new DownloadJobQueueService(new DownloadEngine(NullLogger<DownloadEngine>.Instance));
            _taskManager = taskManager;
        }

        /// <inheritdoc />
        public string Name => "HLS Download Job Processor";

        /// <inheritdoc />
        public string Description => "Processes queued HLS download jobs.";

        /// <inheritdoc />
        public string Category => "HLS Downloader";

        /// <inheritdoc />
        public string Key => TaskKey;

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

            var job = await DownloadJobRepository
                .GetNewestQueuedJobAsync(cancellationToken)
                .ConfigureAwait(false);

            if (job is null)
            {
                progress.Report(1d);
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                progress.Report(1d);
                return;
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
                progress.Report(1d);
                return;
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

            progress.Report(1d);
            await TriggerNextRunIfNeededAsync().ConfigureAwait(false);
        }

        private async Task TriggerNextRunIfNeededAsync()
        {
            var nextJob = await DownloadJobRepository.GetNewestQueuedJobAsync(CancellationToken.None).ConfigureAwait(false);
            if (nextJob is null)
            {
                _logger.LogDebug("No queued follow-up job found after task run.");
                return;
            }

            _logger.LogInformation("Queued follow-up job detected ({JobId}). Scheduling next task run.", nextJob.Id);

            _ = Task.Run(async () =>
            {
                const int maxAttempts = 6;
                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    var worker = GetScheduledTaskWorkers().FirstOrDefault(t =>
                        string.Equals(t.ScheduledTask.Key, TaskKey, StringComparison.OrdinalIgnoreCase));

                    if (worker is null)
                    {
                        _logger.LogWarning("Could not locate scheduled task worker with key {TaskKey}.", TaskKey);
                        return;
                    }

                    if (worker.State != TaskState.Running)
                    {
                        _logger.LogInformation("Triggering follow-up run for scheduled task {TaskKey}.", TaskKey);
                        if (!TryExecuteTask(worker))
                        {
                            _logger.LogWarning("Failed to execute follow-up run for task {TaskKey}: no compatible Execute overload found.", TaskKey);
                        }

                        return;
                    }

                    _logger.LogDebug(
                        "Scheduled task {TaskKey} still running while scheduling follow-up run (attempt {Attempt}/{MaxAttempts}).",
                        TaskKey,
                        attempt + 1,
                        maxAttempts);

                    await Task.Delay(250).ConfigureAwait(false);
                }

                _logger.LogWarning(
                    "Skipped follow-up run for task {TaskKey} because it stayed in Running state after {MaxAttempts} attempts.",
                    TaskKey,
                    maxAttempts);
            });
        }

        private bool TryExecuteTask(IScheduledTaskWorker worker)
        {
            var executeMethod = _taskManager.GetType().GetMethod(
                "Execute",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                [typeof(IScheduledTaskWorker), typeof(TaskOptions)],
                null);

            if (executeMethod is null)
            {
                return false;
            }

            _ = executeMethod.Invoke(_taskManager, [worker, new TaskOptions()]);
            return true;
        }

        private IScheduledTaskWorker[] GetScheduledTaskWorkers()
        {
            var scheduledTasksProperty = _taskManager.GetType().GetProperty(
                "ScheduledTasks",
                BindingFlags.Instance | BindingFlags.Public);

            if (scheduledTasksProperty?.GetValue(_taskManager) is not IEnumerable enumerable)
            {
                return [];
            }

            return enumerable.OfType<IScheduledTaskWorker>().ToArray();
        }
    }
}
