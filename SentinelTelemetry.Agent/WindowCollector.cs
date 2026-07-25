using SentinelTelemetry.Core.Interfaces;
using SentinelTelemetry.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
// Este collector detecta que pagina tienes en primer plano, o que pantalla estas mirando.
namespace SentinelTelemetry.Telemetry
{
    public class WindowCollector : IEventCollector
    {
        public EventBase Capture()
        {
            var handle = NativeMethods.GetForegroundWindow();

            var titleBuilder = new StringBuilder(256);
            NativeMethods.GetWindowText(handle, titleBuilder, titleBuilder.Capacity);

            NativeMethods.GetWindowThreadProcessId(handle, out uint processId);
            var process = System.Diagnostics.Process.GetProcessById((int)processId);

            return new WindowEvent
            {
                Source = "Window",
                Category = "System",
                EventType = "ActiveWindowSnapshot",
                ProcessName = process.ProcessName,
                WindowTitle = titleBuilder.ToString()
            };
        }
    }
}
