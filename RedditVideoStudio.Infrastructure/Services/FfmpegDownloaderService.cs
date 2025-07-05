using Microsoft.Extensions.Logging;
using RedditVideoStudio.Core.Exceptions;
using RedditVideoStudio.Core.Interfaces;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RedditVideoStudio.Infrastructure.Services
{
    /// <summary>
    /// Implements the logic for checking and downloading FFmpeg if it's not present.
    /// </summary>
    public class FfmpegDownloaderService : IFfmpegDownloaderService
    {
        private readonly IAppConfiguration _appConfig;
        private readonly ILogger<FfmpegDownloaderService> _logger;
        private readonly HttpClient _httpClient;

        // URL for a recent, stable, essential FFmpeg build from gyan.dev
        private const string FfmpegDownloadUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

        public FfmpegDownloaderService(IAppConfiguration appConfig, ILogger<FfmpegDownloaderService> logger)
        {
            _appConfig = appConfig;
            _logger = logger;
            _httpClient = new HttpClient();
        }

        public async Task EnsureFfmpegIsAvailableAsync(IProgress<string> progress, CancellationToken cancellationToken)
        {
            var ffmpegExePath = _appConfig.Settings.Ffmpeg.FfmpegExePath;
            var ffprobeExePath = _appConfig.Settings.Ffmpeg.FfprobeExePath;
            var targetDirectory = Path.GetDirectoryName(ffmpegExePath);

            if (File.Exists(ffmpegExePath) && File.Exists(ffprobeExePath))
            {
                _logger.LogInformation("FFmpeg and ffprobe found at {Path}. No download needed.", targetDirectory);
                progress.Report("FFmpeg is already installed.");
                return;
            }

            _logger.LogWarning("FFmpeg not found. Starting download process from {Url}", FfmpegDownloadUrl);
            progress.Report($"FFmpeg not found. Starting download from {FfmpegDownloadUrl}");

            var tempZipPath = Path.Combine(Path.GetTempPath(), $"ffmpeg-download-{Guid.NewGuid()}.zip");
            var tempExtractPath = Path.Combine(Path.GetTempPath(), $"ffmpeg-extract-{Guid.NewGuid()}");

            try
            {
                // Download
                progress.Report("Downloading FFmpeg (essentials build)... This may take a moment.");
                using (var response = await _httpClient.GetAsync(FfmpegDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    using (var fileStream = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fileStream, cancellationToken);
                    }
                }
                _logger.LogInformation("Successfully downloaded FFmpeg to {TempPath}", tempZipPath);

                // Extract
                progress.Report("Download complete. Extracting files...");
                Directory.CreateDirectory(tempExtractPath);
                ZipFile.ExtractToDirectory(tempZipPath, tempExtractPath);
                _logger.LogInformation("Successfully extracted FFmpeg to {TempExtractPath}", tempExtractPath);

                // Find and Copy Executables
                progress.Report("Locating and installing executables...");
                var binDirectory = Directory.GetDirectories(tempExtractPath, "bin", SearchOption.AllDirectories).FirstOrDefault();
                if (binDirectory == null || !Directory.Exists(binDirectory))
                {
                    throw new FfmpegException("Could not find 'bin' directory in the downloaded FFmpeg archive.");
                }

                var sourceFfmpeg = Path.Combine(binDirectory, "ffmpeg.exe");
                var sourceFfprobe = Path.Combine(binDirectory, "ffprobe.exe");

                if (!File.Exists(sourceFfmpeg) || !File.Exists(sourceFfprobe))
                {
                    throw new FfmpegException("Could not find ffmpeg.exe or ffprobe.exe in the 'bin' directory of the archive.");
                }

                if (targetDirectory != null)
                {
                    Directory.CreateDirectory(targetDirectory);
                    File.Copy(sourceFfmpeg, ffmpegExePath, true);
                    File.Copy(sourceFfprobe, ffprobeExePath, true);
                }

                progress.Report("FFmpeg has been installed successfully.");
                _logger.LogInformation("FFmpeg and ffprobe have been successfully installed to {TargetPath}", targetDirectory);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempZipPath))
                {
                    File.Delete(tempZipPath);
                }
                if (Directory.Exists(tempExtractPath))
                {
                    Directory.Delete(tempExtractPath, true);
                }
            }
        }
    }
}