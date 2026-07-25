using System;
using System.Collections.Generic;
using System.Text;

namespace SentinelTelemetry.Service
{
    public class PauseController
    {
        private volatile bool _isPaused = false;
        public bool IsPaused => _isPaused;

        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;
    }
}
