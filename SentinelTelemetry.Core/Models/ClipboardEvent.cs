using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelTelemetry.Core.Models
{
    public class ClipboardEvent : EventBase
    {
        public string ContentType { get; set; } = string.Empty;
        public int? ContentLength { get; set; }
    }
}
