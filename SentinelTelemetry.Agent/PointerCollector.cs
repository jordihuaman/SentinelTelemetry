using SentinelTelemetry.Core.Interfaces;
using SentinelTelemetry.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
// Este collector detecta el movimiento del mouse y los clics que hace, por segundo.
namespace SentinelTelemetry.Telemetry
{
    public class PointerCollector : IEventCollector
    {
        public EventBase Capture()
        {
            NativeMethods.GetCursorPos(out var point);

            bool leftPressed = (NativeMethods.GetAsyncKeyState(0x01) & 0x8000) != 0;
            bool rightPressed = (NativeMethods.GetAsyncKeyState(0x02) & 0x8000) != 0;

            return new PointerEvent
            {
                Source = "Pointer",
                Category = "Input",
                EventType = "PointerSnapshot",
                X = point.X,
                Y = point.Y,
                LeftClick = leftPressed,
                RightClick = rightPressed
            };
        }

    }
}
