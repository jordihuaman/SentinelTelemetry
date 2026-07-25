using SentinelTelemetry.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using SentinelTelemetry.Core.Models;
// De momento este collector registra el conteo de las teclas presionadas por segundo.
namespace SentinelTelemetry.Telemetry
{
    public class KeyboardCollector : IEventCollector
    {

        private int _keyPressCount = 0;
        public void RegisterKeyPress()
        {
            _keyPressCount++;
        }

        public EventBase Capture()
        {
            var evento = new KeyboardEvent
            {
                Source = "Keyboard",
                Category = "Input",
                EventType = "KeyboardSnapshot",
                KeyPressCount = _keyPressCount
            };

            _keyPressCount = 0; // reinicia el contador para el próximo intervalo
            return evento;
        }
    }
}
