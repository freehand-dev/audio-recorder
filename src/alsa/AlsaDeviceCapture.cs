using NAudio.Wave.Asio;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using static audio_recorder.ALSA.AlsaNative;

namespace audio_recorder.ALSA
{
    [SupportedOSPlatform("linux")]
    public class AlsaDeviceCapture: IAsyncDisposable
    {
        private readonly ILogger? _logger;
        private AlsaDevice? _device;
        private Channel<byte[]> _channel;
        private CancellationTokenSource? _recordingCts;
        private Task? _recordingTask;

        /// <summary>
        /// 
        /// </summary>
        public ChannelReader<byte[]> AudioReader => _channel?.Reader!;

        /// <summary>
        /// 
        /// </summary>
        public string DeviceName { get; set; } = AlsaHwParams.DEFAULT_DEVICE_NAME;

        /// <summary>
        /// 
        /// </summary>
        public int SampleRate { get; set; } = AlsaHwParams.DEFAULT_SAMPLE_RATE;

        /// <summary>
        /// 
        /// </summary>
        public int Channels { get; private set; } = AlsaHwParams.DEFAULT_CHANNELS;

        /// <summary>
        /// 
        /// </summary>
        public int BitsPerSample { get; private set; } = AlsaHwParams.DEFAULT_BITS_PER_SAMPLE;

        /// <summary>
        /// 
        /// </summary>
        public int BytesPerSample => BitsPerSample / 8;


        public AlsaDeviceCapture(ILogger? logger)
        {
            _logger = logger;

            _channel = Channel.CreateBounded<byte[]>(
                new BoundedChannelOptions(100)
                {
                    FullMode = BoundedChannelFullMode.DropOldest,
                    SingleReader = true,
                    SingleWriter = true   
                }
            );
        }

        public ValueTask StartAsync()
        {
            if (_recordingTask != null && !_recordingTask.IsCompleted)
                throw new InvalidOperationException("Recording already started");

            _recordingCts = new CancellationTokenSource();

            _device = new AlsaDevice(DeviceName, AlsaNative.snd_pcm_stream_t.SND_PCM_STREAM_CAPTURE);

            _logger?.LogDebug($"Capture device opened: {this.DeviceName}");

            // Set hardware parameters
            _logger?.LogDebug($"Configuring device: rate={this.SampleRate}Hz, channels={this.Channels}");

            using (AlsaHwParams hwParams = new AlsaHwParams(_device.PCMHandle))
            {
                hwParams.SetAccess(AlsaNative.snd_pcm_access_t.SND_PCM_ACCESS_RW_INTERLEAVED);
                hwParams.SetFormat(AlsaNative.snd_pcm_format_t.SND_PCM_FORMAT_S16_LE);
                hwParams.SetSampleRate(this.SampleRate);
                hwParams.SetChannels(this.Channels);
                hwParams.SetBufferSize();
                hwParams.SetPeriodSize();
                hwParams.Apply();
            }

            _device.Prepare();

            _logger?.LogInformation($"Device configured successfully");

            _recordingTask = Task.Run(() => RecordLoopAsync(_recordingCts.Token));

            return ValueTask.CompletedTask;
        }

        public async Task StopAsync()
        {
            if (_recordingTask == null || _recordingTask.IsCompleted)
                return;

            _recordingCts?.Cancel();

            if (await Task.WhenAny(_recordingTask, Task.Delay(TimeSpan.FromSeconds(2))) != _recordingTask)
            {
                _logger?.LogWarning("Recording task did not respond to cancellation. Forcing PCM drop.");
                try
                {
                    _device?.Drop();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to forcibly drop ALSA PCM stream");
                }
            }

            _channel.Writer.TryComplete();
        }

        private async Task RecordLoopAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Starting ALSA recording");

            _device?.Start();

            int bytesPerFrame = Channels * BytesPerSample;
            int framesPerBuffer = SampleRate / 10;
            int bufferSize = framesPerBuffer * bytesPerFrame;

            _logger?.LogDebug($"Recording buffer size: {bufferSize} bytes ({framesPerBuffer} frames)");

            byte[] buffer = new byte[bufferSize];

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    int framesRead = await _device!.ReadPcmInterleavedAsync(buffer.AsMemory(0, bufferSize), framesPerBuffer, cancellationToken);

                    var copy = new byte[framesRead * bytesPerFrame];
                    Buffer.BlockCopy(buffer, 0, copy, 0, copy.Length);
                    await _channel.Writer.WriteAsync(copy, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Recording task cancelled");
                    break;
                }
                catch (AlsaException ex)
                {
                    _logger?.LogError(ex, "Unrecoverable ALSA error during recording");
                    throw; 
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while stopping ALSA capture");
            }

            _device?.Dispose();
        }
    }
}
