using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HLSDownloader.Data;
using Jellyfin.Plugin.HLSDownloader.Models;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.HLSDownloader.Controller
{
    /// <summary>
    /// Provides endpoints for creating and mutating persisted download jobs.
    /// </summary>
    [ApiController]
    [Route("api/hlsdownloader/downloads")]
    public class DownloadController : ControllerBase
    {
        /// <summary>
        /// Creates a new queued download job.
        /// </summary>
        /// <param name="request">Download request payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result containing the new job id.</returns>
        [HttpPost]
        public async Task<IActionResult> StartDownload([FromBody] DownloadRequest request, CancellationToken cancellationToken)
        {
            if (request is null || !request.StartUrl.IsAbsoluteUri)
            {
                return BadRequest(new { error = "Missing or invalid startUrl parameter." });
            }

            var configuredOutputPath = Plugin.Instance?.Configuration?.DownloadDistPath;
            var defaultOutputRoot = string.IsNullOrWhiteSpace(configuredOutputPath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hls-downloader", "downloads")
                : configuredOutputPath;

            var resolvedOutputPath = string.IsNullOrWhiteSpace(request.OutputPath)
                ? defaultOutputRoot
                : request.OutputPath;

            var job = await DownloadJobRepository
                .CreateJobAsync(request.StartUrl, resolvedOutputPath, cancellationToken)
                .ConfigureAwait(false);
            return Ok(new { success = true, jobId = job.Id.ToString(), persistence = "db" });
        }

        /// <summary>
        /// Returns current jobs from the DB queue.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of persisted jobs ordered by creation time descending.</returns>
        [HttpGet("jobs")]
        public async Task<IActionResult> GetJobs(CancellationToken cancellationToken)
        {
            var jobs = await DownloadJobRepository
                .GetAllJobsAsync(cancellationToken)
                .ConfigureAwait(false);

            var payload = jobs
                .Select(j => new
                {
                    id = j.Id.ToString(),
                    startUrl = j.StartUrl,
                    outputPath = j.OutputPath,
                    status = j.Status,
                    createdAt = j.CreatedAt,
                    finishedAt = j.FinishedAt,
                    errorMessage = j.ErrorMessage
                })
                .ToList();

            return Ok(new { jobs = payload });
        }

        /// <summary>
        /// Applies an action to an existing job.
        /// </summary>
        /// <param name="id">Job id.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Result payload for the requested action.</returns>
        [HttpPost("jobs/{id}/cancel")]
        public async Task<IActionResult> CancelJob(string id, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return BadRequest(new { error = "Missing jobId." });
            }

            if (!Guid.TryParse(id, out var jobGuid))
            {
                return BadRequest(new { error = "Invalid jobId format." });
            }

            var job = await DownloadJobRepository.GetJobByIdAsync(jobGuid, cancellationToken).ConfigureAwait(false);
            if (job == null)
            {
                return NotFound(new { error = "Job not found or action failed." });
            }

            var result = !string.Equals(job.Status, "DONE", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(job.Status, "ERROR", StringComparison.OrdinalIgnoreCase);

            if (result)
            {
                result = await DownloadJobRepository.DeleteJobAsync(jobGuid, cancellationToken).ConfigureAwait(false);
            }

            if (result)
            {
                return Ok(new { success = true, action = "cancel", persistence = "db", jobId = job.Id.ToString() });
            }

            return NotFound(new { error = "Job not found or action failed." });
        }
    }
}
