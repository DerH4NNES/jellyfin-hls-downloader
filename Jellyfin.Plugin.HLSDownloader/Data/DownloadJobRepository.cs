using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.HLSDownloader.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Plugin.HLSDownloader.Data
{
    /// <summary>
    /// Centralized data access for download jobs.
    /// </summary>
    internal static class DownloadJobRepository
    {
        internal static async Task<DownloadJobEntity> CreateJobAsync(Uri startUrl, string outputPath, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(
                async db =>
                {
                    var job = new DownloadJobEntity
                    {
                        Id = Guid.NewGuid(),
                        StartUrl = startUrl.ToString(),
                        OutputPath = outputPath,
                        Status = "QUEUED",
                        CreatedAt = DateTime.UtcNow
                    };

                    db.Jobs.Add(job);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return job;
                },
                cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<List<DownloadJobEntity>> GetAllJobsAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(
                db => db.Jobs
                    .OrderByDescending(j => j.CreatedAt)
                    .ToListAsync(cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<DownloadJobEntity?> GetJobByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(
                db => db.Jobs
                    .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<List<DownloadJobEntity>> GetQueuedJobsAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(
                db => db.Jobs
                    .Where(j => j.Status == "QUEUED")
                    .OrderBy(j => j.CreatedAt)
                    .AsNoTracking()
                    .ToListAsync(cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<DownloadJobEntity?> GetNewestQueuedJobAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(
                db => db.Jobs
                    .Where(j => j.Status == "QUEUED")
                    .OrderByDescending(j => j.CreatedAt)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<bool> TryUpdateJobAsync(Guid jobId, Action<DownloadJobEntity> mutate, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(
                async db =>
                {
                    var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken).ConfigureAwait(false);
                    if (job == null)
                    {
                        return false;
                    }

                    mutate(job);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<bool> DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(
                async db =>
                {
                    var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken).ConfigureAwait(false);
                    if (job == null)
                    {
                        return false;
                    }

                    db.Jobs.Remove(job);
                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return true;
                },
                cancellationToken).ConfigureAwait(false);
        }

        internal static async Task<int> ResetRunningJobsToQueuedAsync(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithRetryAsync(
                async db =>
                {
                    var runningJobs = await db.Jobs
                        .Where(j => j.Status == "RUNNING" || j.Status == "ERROR")
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (runningJobs.Count == 0)
                    {
                        return 0;
                    }

                    foreach (var job in runningJobs)
                    {
                        job.Status = "QUEUED";
                        job.FinishedAt = null;
                        job.ErrorMessage = null;
                    }

                    await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return runningJobs.Count;
                },
                cancellationToken).ConfigureAwait(false);
        }

        private static async Task<T> ExecuteWithRetryAsync<T>(Func<DownloadJobDbContext, Task<T>> operation, CancellationToken cancellationToken)
        {
            const int maxAttempts = 3;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var db = DownloadJobStore.CreateContext();
                    await using (db.ConfigureAwait(false))
                    {
                        return await operation(db).ConfigureAwait(false);
                    }
                }
                catch (SqliteException ex) when ((ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6) && attempt < maxAttempts)
                {
                    await Task.Delay(150 * attempt, cancellationToken).ConfigureAwait(false);
                }
            }

            var fallbackDb = DownloadJobStore.CreateContext();
            await using (fallbackDb.ConfigureAwait(false))
            {
                return await operation(fallbackDb).ConfigureAwait(false);
            }
        }
    }
}
