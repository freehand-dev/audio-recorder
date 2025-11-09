using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace audio_recorder.Models
{
    /// <summary>
    /// Represents configuration options for the audio recording service.
    /// </summary>
    public class RecorderOptions
    {
        /// <summary>
        /// Enable/disable recording. 
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Directory for saving recordings. 
        /// </summary>
        public string Directory { get; set; } = "recordings";

        /// <summary>
        /// Filename template (`{date}`, `{time}`, `{num}`).  
        /// </summary>
        public string FileNameFormat { get; set; } = "audio_{date:yyyyMMdd}_{time:HHmmss}_{num:00000}.wav";

        /// <summary>
        /// Maximum length of a single file.  
        /// </summary>
        public int MaxFileDurationMinutes { get; set; } = 30;

        /// <summary>
        /// Audio sampling rate. 
        /// </summary>
        public int SampleRate { get; set; } = 44100;

        /// <summary>
        /// ALSA device name (`plughw:X,Y`).  
        /// </summary>
        public string DeviceName { get; set; } = "default";
    }
}
