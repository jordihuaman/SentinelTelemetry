using SentinelTelemetry.Core.Models;
using System;
// Este collector detecta cuando estas inactivo luego de cierto tiempo, registra tambien cuando regresa la actividad.
namespace SentinelTelemetry.Telemetry
{
    public class IdleCollector
    {
        private readonly TimeSpan _idleThreshold;
        private DateTime _lastActivityTime = DateTime.UtcNow;
        private bool _isIdle = false;

        public bool IsIdle => _isIdle;

        public IdleCollector(TimeSpan? idleThreshold = null)
        {
            _idleThreshold = idleThreshold ?? TimeSpan.FromSeconds(30);
        }

        public EventBase? Update(bool hasActivity)
        {
            var now = DateTime.UtcNow;

            if (hasActivity)
            {
                _lastActivityTime = now;

                if (_isIdle)
                {
                    _isIdle = false;
                    return new IdleEvent
                    {
                        Source = "Idle",
                        Category = "Attention",
                        EventType = "IdleEnded"
                    };
                }

                return null;
            }

            if (!_isIdle && now - _lastActivityTime >= _idleThreshold)
            {
                _isIdle = true;
                return new IdleEvent
                {
                    Source = "Idle",
                    Category = "Attention",
                    EventType = "IdleStarted"
                };
            }

            return null;
        }
    }
}