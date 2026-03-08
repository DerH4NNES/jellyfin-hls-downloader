using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HLSDownloader.Service
{
    /// <summary>
    /// Generic HLS download engine using ffmpeg.
    /// </summary>
    public sealed class DownloadEngine : IDownloadEngine
    {
        private readonly ILogger<DownloadEngine> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DownloadEngine"/> class.
        /// </summary>
        /// <param name="logger">Logger.</param>
        public DownloadEngine(ILogger<DownloadEngine> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> OrchestrateDownloadAsync(Uri startUrl, string outputDir, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(startUrl);
            if (!startUrl.IsAbsoluteUri)
            {
                throw new InvalidOperationException("Start URL must be absolute.");
            }

            if (string.IsNullOrWhiteSpace(outputDir))
            {
                throw new InvalidOperationException("Output file path is empty.");
            }

            var outputFile = ResolveOutputFilePath(outputDir);

            var args = FormattableString.Invariant(
                $"-y -i \"{startUrl}\" -c copy -map 0 -dn -movflags +faststart \"{outputFile}\"");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            _logger.LogInformation("Starting ffmpeg download for '{Url}'.", startUrl);
            _ = process.Start();
            var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            var stdout = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                _logger.LogError("ffmpeg failed with code {Code}. stderr: {Stderr}", process.ExitCode, stderr);
                throw new InvalidOperationException($"ffmpeg exited with code {process.ExitCode}: {stderr}");
            }

            if (!File.Exists(outputFile))
            {
                throw new IOException("ffmpeg finished but output file was not created.");
            }

            _logger.LogInformation("HLS download finished. Output file: '{OutputFile}'.", outputFile);
            if (!string.IsNullOrWhiteSpace(stdout))
            {
                _logger.LogDebug("ffmpeg stdout: {Stdout}", stdout);
            }

            return outputFile;
        }

        private static string ResolveOutputFilePath(string outputPath)
        {
            if (!Path.IsPathRooted(outputPath))
            {
                throw new InvalidOperationException("Output file path must be absolute.");
            }

            var extension = Path.GetExtension(outputPath);
            if (!string.Equals(extension, ".mkv", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Output file path must use .mkv extension.");
            }

            var parentDirectory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(parentDirectory))
            {
                throw new InvalidOperationException("Target file path is missing a parent directory.");
            }

            Directory.CreateDirectory(parentDirectory);
            return outputPath;
        }
    }
}
