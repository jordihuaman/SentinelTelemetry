using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelTelemetry.Core.Models
{
    public class PointerEvent : EventBase
    {
        public int X{ get; set; }
        public int Y{ get; set; }
        public bool LeftClick { get; set; }
        public bool RightClick { get; set; }
    }
}
