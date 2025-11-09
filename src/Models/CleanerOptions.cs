using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace audio_recorder.Models
{
    public class CleanerOptions
    {
        /// <summary>
        /// Enable/disable automatic cleanup.  
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Number of most recent files to keep. Older ones will be deleted.
        /// </summary>
        public int MaxFilesToKeep { get; set; } = 100;
    }
}
