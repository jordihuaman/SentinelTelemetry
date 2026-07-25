using SentinelTelemetry.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelTelemetry.Core.Interfaces
{
    public interface IEventCollector
    {
        EventBase Capture();
    }
}
