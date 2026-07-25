using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelTelemetry.Core.Models
{
    public class FileEvent : EventBase
    {
        public string FilePath { get; set; } = string.Empty;
        public string? OldFilePath { get; set; } // solo se usa en Renamed
    }
}
