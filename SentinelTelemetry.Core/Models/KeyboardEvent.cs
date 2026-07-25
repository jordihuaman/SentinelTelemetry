using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelTelemetry.Core.Models
{

    public class KeyboardEvent : EventBase
    {
        public KeyboardEvent() 
        {
            Source = "Keyboard";
        }
        public int KeyPressCount { get; set; }

    }

}
