using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace audio_recorder.ALSA
{
    public class AlsaHwParams : IDisposable
    {
        public const int DEFAULT_SAMPLE_RATE = 44100;
        public const string DEFAULT_DEVICE_NAME = "default";
        public const int DEFAULT_CHANNELS = 1; // Mono
        public const int DEFAULT_BITS_PER_SAMPLE = 16;
        public const int DEFAULT_PERIOD_SIZE = 1024; // Frames per period
        public const int DEFAULT_BUFFER_SIZE = 8192; // Total buffer size in frames

        private readonly IntPtr _pcmHandle;

        private IntPtr _hwParams;

        private bool _disposed = false;

        public IntPtr HwParams => _hwParams;

        public AlsaHwParams(IntPtr pcmHandle)
        {
            if (pcmHandle == IntPtr.Zero)
            {
                throw new ArgumentNullException(nameof(pcmHandle), "PCM handle cannot be null.");
            }

            _pcmHandle = pcmHandle;
            _hwParams = InitParams();
        }

        /// <summary>
        /// Allocate hardware parameters object and fill hw_params with default values
        /// </summary>
        /// <returns></returns>
        /// <exception cref="AlsaException"></exception>
        public IntPtr InitParams()
        {
            // Allocate hardware parameters object
            int ret = AlsaNative.snd_pcm_hw_params_malloc(out var hwParams);
            if (ret < 0)
            {
                throw new AlsaException(ret, nameof(AlsaNative.snd_pcm_hw_params_malloc));
            }

            // Fill hw_params with default values
            ret = AlsaNative.snd_pcm_hw_params_any(_pcmHandle, hwParams);
            if (ret < 0)
            {
                throw new AlsaException(ret, nameof(AlsaNative.snd_pcm_hw_params_any));
            }

            return hwParams;
        }

        /// <summary>
        /// Set access type
        /// </summary>
        /// <exception cref="AlsaException"></exception>
        public void SetAccess(AlsaNative.snd_pcm_access_t access = AlsaNative.snd_pcm_access_t.SND_PCM_ACCESS_RW_INTERLEAVED)
        {
            int ret = AlsaNative.snd_pcm_hw_params_set_access(_pcmHandle, _hwParams, access);
            if (ret < 0)
            {
                throw new AlsaException(ret, nameof(AlsaNative.snd_pcm_hw_params_set_access));
            }
        }

        /// <summary>
        /// Set sample format (16-bit signed little-endian)
        /// </summary>
        /// <param name="format"></param>
        /// <exception cref="AlsaException"></exception>
        public void SetFormat(AlsaNative.snd_pcm_format_t format = AlsaNative.snd_pcm_format_t.SND_PCM_FORMAT_S16_LE)
        {
            int ret = AlsaNative.snd_pcm_hw_params_set_format(_pcmHandle, _hwParams, format);
            if (ret < 0)
            {
                throw new AlsaException(ret, nameof(AlsaNative.snd_pcm_hw_params_set_format));
            }
        }

        /// <summary>
        /// Set channels (mono/stereo)
        /// </summary>
        /// <param name="channels"></param>
        /// <exception cref="AlsaException"></exception>
        public void SetChannels(int channels = DEFAULT_CHANNELS)
        {
            int ret = AlsaNative.snd_pcm_hw_params_set_channels(_pcmHandle, _hwParams, (uint)channels);
            if (ret < 0)
            {
                throw new AlsaException(ret, nameof(AlsaNative.snd_pcm_hw_params_set_channels));
            }
        }

        /// <summary>
        /// Set sample rate
        /// </summary>
        /// <param name="sampleRate"></param>
        /// <exception cref="AlsaException"></exception>
        public void SetSampleRate(int sampleRate = DEFAULT_SAMPLE_RATE)
        {
            int ret = AlsaNative.snd_pcm_hw_params_set_rate(_pcmHandle, _hwParams, (uint)sampleRate, 0);
            if (ret < 0)
            {
                throw new AlsaException(ret, nameof(AlsaNative.snd_pcm_hw_params_set_rate));
            }
        }

        /// <summary>
        /// Set period size
        /// </summary>
        /// <param name="period_size"></param>
        public void SetPeriodSize(int period_size = DEFAULT_PERIOD_SIZE)
        {
            int ret = AlsaNative.snd_pcm_hw_params_set_period_size(_pcmHandle, _hwParams, (uint)period_size, 0);
        }

        /// <summary>
        /// Set buffer size
        /// </summary>
        /// <param name="buffer_size"></param>
        public void SetBufferSize(int buffer_size = DEFAULT_BUFFER_SIZE)
        {
            int ret = AlsaNative.snd_pcm_hw_params_set_buffer_size(_pcmHandle, _hwParams, (uint)buffer_size);
        }

        /// <summary>
        /// Apply hardware parameters
        /// </summary>
        /// <exception cref="AlsaException"></exception>
        public void Apply()
        {
            int ret = AlsaNative.snd_pcm_hw_params(_pcmHandle, _hwParams);
            if (ret < 0)
            {
                throw new AlsaException(ret, nameof(AlsaNative.snd_pcm_hw_params));
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (_hwParams != IntPtr.Zero)
                {
                    AlsaNative.snd_pcm_hw_params_free(_hwParams);
                    _hwParams = IntPtr.Zero;
                }
                _disposed = true;
            }
        }

    }
}
