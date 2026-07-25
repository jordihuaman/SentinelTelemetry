using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelTelemetry.Core.Models
{
    public class ApplicationEvent : EventBase
    {
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
    }

}
