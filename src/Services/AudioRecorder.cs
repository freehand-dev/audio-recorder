using audio_recorder.ALSA;
using audio_recorder.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NAudio.Wave;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using YamlDotNet.Core.Tokens;

namespace audio_recorder.Services
{
    public class AudioRecorder : BackgroundService
    {
        private readonly ILogger<AudioRecorder> _logger;

        private readonly RecorderOptions _recorderOptions;

        public AudioRecorder(ILogger<AudioRecorder> logger, IOptions<RecorderOptions> recorderOptions)
        {
            _logger = logger;
            _recorderOptions = recorderOptions.Value;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException("Audio recording is only supported on Linux with ALSA.");
            }

            if (!_recorderOptions.Enabled)
            {
                _logger.LogWarning("Audio recorder is disabled.");
                return;
            }

            if (!Directory.Exists(_recorderOptions.Directory))
            {
                Directory.CreateDirectory(_recorderOptions.Directory);
                _logger.LogInformation("Created directory: {Directory}", _recorderOptions.Directory);
            }

            foreach (var device in AlsaDevice.GetAvailableInputDevices())
            {
                _logger.LogInformation(device.ToString());
            }

            await using var audioInput = new AlsaDeviceCapture(_logger)
            {
                DeviceName = _recorderOptions.DeviceName,
                SampleRate = _recorderOptions.SampleRate,
            };

            int bytesPerSecond = audioInput.SampleRate * audioInput.Channels * audioInput.BytesPerSample;
            int maxBytesPerFile = bytesPerSecond * (int)TimeSpan.FromMinutes(_recorderOptions.MaxFileDurationMinutes).TotalSeconds;

            int fileIndex = 1;
            long currentFileBytes = 0;

            WaveFileWriter? waveWriter = null;
            try
            {
                await audioInput.StartAsync();
                _logger.LogInformation("Audio recording started.");

                ChannelReader<byte[]> reader = audioInput.AudioReader;

                while (!stoppingToken.IsCancellationRequested || await reader.WaitToReadAsync(stoppingToken))
                {
                    var buffer = await reader.ReadAsync(stoppingToken);

                    if (waveWriter == null || currentFileBytes + buffer.Length > maxBytesPerFile)
                    {
                        if (waveWriter != null)
                        {
                            waveWriter.Dispose();
                            fileIndex++;
                            currentFileBytes = 0;
                            _logger.LogInformation("Finished writing file #{FileIndex}, switching to new file.", fileIndex - 1);
                        }

                        string fileName = Path.Combine(_recorderOptions.Directory,
                            GenerateFilename(_recorderOptions.FileNameFormat, fileIndex, DateTime.UtcNow));

                        var waveFormat = new WaveFormat(audioInput.SampleRate, audioInput.BitsPerSample, audioInput.Channels);
                        waveWriter = new WaveFileWriter(fileName, waveFormat);

                        _logger.LogInformation("Started writing new WAV file: {FileName}", fileName);
                    }

                    waveWriter.Write(buffer);
                    currentFileBytes += buffer.Length;
                }               
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Audio recording cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during audio recording.");
            }
            finally
            {
                waveWriter?.Dispose();
                await audioInput.StopAsync();
                _logger.LogInformation("Audio recording stopped.");
            }
        }


        private static string GenerateFilename(string pattern, int number, DateTime dateTime)
        {
            return Regex.Replace(
                Regex.Replace(
                    Regex.Replace(pattern, @"\{date:(.*?)\}", m => dateTime.ToString(m.Groups[1].Value)),
                    @"\{time:(.*?)\}", m => dateTime.ToString(m.Groups[1].Value)),
                @"\{num:(0+)\}", m => number.ToString().PadLeft(m.Groups[1].Value.Length, '0'));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public override async Task StartAsync(CancellationToken stoppingToken)
        {
            if (OperatingSystem.IsLinux())
            {
                AlsaDevice.Init();
            }
            await base.StartAsync(stoppingToken);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            await base.StopAsync(stoppingToken);
        }

    }
}
