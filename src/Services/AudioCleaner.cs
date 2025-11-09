using audio_recorder.Models;
using Microsoft.Extensions.Options;
using System.IO;
using System.Threading;

namespace audio_recorder.Services
{
    public class AudioCleaner : BackgroundService
    {
        private readonly ILogger<AudioCleaner> _logger;
        private readonly CleanerOptions _cleanerOptions;
        private readonly RecorderOptions _recorderOptions;

        public AudioCleaner(ILogger<AudioCleaner> logger, IOptions<CleanerOptions> cleanerOptions, IOptions<RecorderOptions> recorderOptions)
        {
            _logger = logger;
            _cleanerOptions = cleanerOptions.Value;
            _recorderOptions = recorderOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_cleanerOptions.Enabled)
            {
                _logger.LogWarning("Audio cleaner is disabled.");
                return;
            }

            var runInterval = TimeSpan.FromMinutes(_recorderOptions.MaxFileDurationMinutes);
            using var timer = new PeriodicTimer(runInterval);
            _logger.LogInformation("Audio cleaner started. Cleaning every {Interval}", runInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CleanOldFiles(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while cleaning audio files");
                }
            }
        }

        private async Task CleanOldFiles(CancellationToken cancellationToken)
        {
            string directoryPath = _recorderOptions.Directory;
            string extension = Path.GetExtension(_recorderOptions.FileNameFormat);

            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWarning("Directory not found: {Directory}", directoryPath);
                return;
            }

            var files = new DirectoryInfo(directoryPath)
                .EnumerateFiles($"*{extension}")
                .OrderByDescending(f => f.CreationTimeUtc)
                .ToList();


            if (files.Count <= _cleanerOptions.MaxFilesToKeep)
            {
                _logger.LogInformation("No files to delete. Total files: {Count}", files.Count);
                return;
            }

            foreach (var file in files.Skip(_cleanerOptions.MaxFilesToKeep))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await Task.Yield();

                    file.Delete();
                    _logger.LogInformation("Deleted file: {File}", file.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete file: {File}", file.FullName);
                }
            }
        }
    }
}

