using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace audio_recorder.ALSA
{
    public class AlsaException : Exception
    {
        public AlsaException(int error, string? method = null)
            : base($"Alsa error: {AlsaNative.GetAlsaErrorMessage(error)}, code: {error} occured {(!string.IsNullOrEmpty(method) ? $"while calling {method}" : string.Empty)}")
        {
            Error = error;
        }

        public int Error
        {
            get;
        }
    }
}