using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelTelemetry.Core.Models
{
    public abstract class EventBase
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public String Source { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public String EventType { get; set; } = string.Empty;
        public int Version { get; set; } = 1;
    }
}
