using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Text;
using SentinelTelemetry.Core.Models;
// Este collector detecta cuando haces algun cambio de sesion (Bloqueo de sesion/Desbloqueo/Cerrar sesion/Remote connect/R.Disconnect)
namespace SentinelTelemetry.Telemetry
{
    public class SessionCollector : IDisposable
    {
        public event Action<EventBase>? SessionEventOccurred;
        public SessionCollector()
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            string? eventType = e.Reason switch
            {
                SessionSwitchReason.SessionLock => "Lock",
                SessionSwitchReason.SessionUnlock => "Unlock",
                SessionSwitchReason.SessionLogon => "Login",
                SessionSwitchReason.SessionLogoff => "Logout",
                SessionSwitchReason.RemoteConnect => "RemoteConnect",
                SessionSwitchReason.RemoteDisconnect => "RemoteDisconnect",
                _ => null
            };

            if (eventType != null)
            {
                Raise(eventType);
            }
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            string? eventType = e.Mode switch
            {
                PowerModes.Suspend => "Suspend",
                PowerModes.Resume => "Resume",
                _ => null
            };

            if (eventType != null)
            {
                Raise(eventType);
            }
        }

        private void Raise(string eventType)
        {
            SessionEventOccurred?.Invoke(new SessionEvent
            {
                Source = "Session",
                Category = "System",
                EventType = eventType
            });
        }

        public void Dispose()
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
    }
}
