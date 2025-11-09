using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static audio_recorder.ALSA.AlsaNative;

namespace audio_recorder.ALSA
{
    public class AlsaDevice : IDisposable
    {
        private bool _disposed = false;

        private IntPtr _pcmHandle = IntPtr.Zero;

        public IntPtr PCMHandle { get => _pcmHandle; }

        private bool _isOpen => _pcmHandle != IntPtr.Zero;

        public AlsaHwParams HwParams
        {
            set
            {
                int ret = AlsaNative.snd_pcm_hw_params(_pcmHandle, value.HwParams);
                if (ret < 0)
                    throw new AlsaException(ret, nameof(AlsaNative.snd_pcm_hw_params));
            }
        }

        public AlsaDevice(string deviceName, AlsaNative.snd_pcm_stream_t stream)
        {
            int ret = AlsaNative.snd_pcm_open(ref _pcmHandle, deviceName, stream, 0);
            if (ret < 0)
                throw new AlsaException(ret, nameof(AlsaNative.snd_pcm_open));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AlsaException"></exception>
        public void Drop()
        {
            if (!_isOpen)
                throw new InvalidOperationException("Device is not open.");

            int err = AlsaNative.snd_pcm_drop(_pcmHandle);
            if (err < 0)
                throw new AlsaException(err, nameof(AlsaNative.snd_pcm_drop));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AlsaException"></exception>
        public void Drain()
        {
            if (!_isOpen)
                throw new InvalidOperationException("Device is not open.");

            int err = AlsaNative.snd_pcm_drain(_pcmHandle);
            if (err < 0)
                throw new AlsaException(err, nameof(AlsaNative.snd_pcm_drain));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AlsaException"></exception>
        public void Prepare()
        {
            if (!_isOpen)
                throw new InvalidOperationException("Device is not open.");

            int err = AlsaNative.snd_pcm_prepare(_pcmHandle);
            if (err < 0)
                throw new AlsaException(err, nameof(AlsaNative.snd_pcm_prepare));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="AlsaException"></exception>
        public void Close()
        {
            if (!_isOpen)
                throw new InvalidOperationException("Device is not open.");

            int err = AlsaNative.snd_pcm_close(_pcmHandle);
            if (err < 0)
                throw new AlsaException(err, nameof(AlsaNative.snd_pcm_close));

            _pcmHandle = IntPtr.Zero;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        /// <returns></returns>
        /// <exception cref="AlsaException"></exception>
        public int Recover(int error)
        {
            if (!_isOpen)
                return -AlsaNative.EINVAL;

            int err = AlsaNative.snd_pcm_recover(_pcmHandle, error, 1);
            if (err < 0)
                throw new AlsaException(err, nameof(AlsaNative.snd_pcm_recover));

            return err;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="AlsaException"></exception>
        public int Resume()
        {
            if (!_isOpen)
                return -AlsaNative.EINVAL;

            int err = AlsaNative.snd_pcm_resume(_pcmHandle);
            if (err < 0 && err != -AlsaNative.EAGAIN)
                throw new AlsaException(err, nameof(AlsaNative.snd_pcm_resume));

            return err;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AlsaException"></exception>
        public void Start()
        {
            if (!_isOpen)
                throw new InvalidOperationException("Device is not open.");

            int err = AlsaNative.snd_pcm_start(_pcmHandle);
            if (err < 0)
                throw new AlsaException(err, nameof(AlsaNative.snd_pcm_start));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="frames"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="AlsaException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        public async ValueTask<int> ReadPcmInterleavedAsync(Memory<byte> buffer, int frames, CancellationToken cancellationToken = default)
        {
            if (!_isOpen)
                throw new InvalidOperationException("Device is not open.");

            if (!MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
                throw new InvalidOperationException("Buffer must be array-backed.");

            if (segment.Array == null)
                throw new InvalidOperationException("Buffer array is null.");

            GCHandle? handle = null;
            try
            {
                handle = GCHandle.Alloc(segment.Array!, GCHandleType.Pinned);
                IntPtr ptr = handle.Value.AddrOfPinnedObject();

                while (!cancellationToken.IsCancellationRequested)
                {
                    int result = snd_pcm_readi(_pcmHandle, ptr, (uint)frames);
                    if (result >= 0)
                        return result;

                    int err = -result;

                    switch (err)
                    {
                        case AlsaNative.EAGAIN:
                            await Task.Delay(5, cancellationToken);
                            break;

                        case AlsaNative.EPIPE:
                            Recover(AlsaNative.EPIPE);
                            break;

                        case AlsaNative.ESTRPIPE:
                            int resumeResult;
                            do
                            {
                                resumeResult = Resume();
                                if (resumeResult == -AlsaNative.EAGAIN)
                                    await Task.Delay(100, cancellationToken);
                            } while (resumeResult == -AlsaNative.EAGAIN);

                            if (resumeResult < 0)
                                Prepare();
                            break;

                        default:
                            if (Recover(-1) < 0)
                                throw new AlsaException(result, nameof(AlsaNative.snd_pcm_readi));
                            break;
                    }
                }
                throw new OperationCanceledException(cancellationToken);
            }
            finally
            {
                if (handle.HasValue && handle.Value.IsAllocated)
                    handle.Value.Free();
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
                if (_isOpen)
                {
                    AlsaNative.snd_pcm_close(_pcmHandle);
                    _pcmHandle = IntPtr.Zero;
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="DllNotFoundException"></exception>
        public static void Init()
        {
            Console.WriteLine("Initializing ALSA audio subsystem...");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                throw new PlatformNotSupportedException("ALSA is supported only on Linux.");

            // Check if ALSA library is available
            if (!NativeLibrary.TryLoad(AlsaNative.ALSA_LIBRARY, out IntPtr alsaHandle))
            {
                throw new DllNotFoundException($"Could not load ALSA library. Please ensure '{AlsaNative.ALSA_LIBRARY}' is installed on your system.");
            }

            NativeLibrary.Free(alsaHandle);
            Console.WriteLine($"ALSA library loaded successfully: {AlsaNative.ALSA_LIBRARY}");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="AlsaException"></exception>
        public static IEnumerable<AlsaDeviceInfo> GetAvailableInputDevices()
        {
            const int SND_PCM_NONBLOCK = 1;
            IntPtr hints = IntPtr.Zero;

            int err = AlsaNative.snd_device_name_hint(-1, "pcm", out hints);
            if (err < 0)
                throw new AlsaException(err, nameof(AlsaNative.snd_device_name_hint));

            try
            {
                for (IntPtr current = hints; current != IntPtr.Zero; current += IntPtr.Size)
                {
                    IntPtr hint = Marshal.ReadIntPtr(current);
                    if (hint == IntPtr.Zero)
                        break;

                    string name = Marshal.PtrToStringAnsi(AlsaNative.snd_device_name_get_hint(hint, "NAME")) ?? string.Empty;
                    string desc = Marshal.PtrToStringAnsi(AlsaNative.snd_device_name_get_hint(hint, "DESC")) ?? string.Empty;
                    string ioid = Marshal.PtrToStringAnsi(AlsaNative.snd_device_name_get_hint(hint, "IOID")) ?? string.Empty;

                    if (!string.IsNullOrWhiteSpace(name) &&
                        (string.IsNullOrEmpty(ioid) || ioid.Equals("Input", StringComparison.OrdinalIgnoreCase)) &&
                        !name.Equals("null", StringComparison.OrdinalIgnoreCase))
                    {
                        IntPtr handle = IntPtr.Zero;
                        if (snd_pcm_open(ref handle, name, snd_pcm_stream_t.SND_PCM_STREAM_CAPTURE, SND_PCM_NONBLOCK) == 0)
                        {
                            snd_pcm_close(handle);
                            yield return new AlsaDeviceInfo
                            {
                                Name = name,
                                Description = desc,
                                IOID = ioid
                            };
                        }
                    }
                }
            }
            finally
            {
                AlsaNative.snd_device_name_free_hint(hints);
            }
        }

    }
}
