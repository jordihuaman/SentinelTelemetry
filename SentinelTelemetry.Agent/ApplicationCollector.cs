using SentinelTelemetry.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
// Este collector detecta el ciclo de vida de una app, es decir detecta cuando se abrió y cuando se cerro.
namespace SentinelTelemetry.Telemetry
{
    public class ApplicationCollector
    {
        private Dictionary<int, string> _knownProcesses = new();
        private bool _initialized = false;

        public List<EventBase> DetectChanges()
        {
            var events = new List<EventBase>();
            var currentProcesses = GetVisibleProcesses();

            if (!_initialized)
            {
                // Primera ejecución: solo fijamos la "foto" inicial, sin generar eventos.
                // Si no hiciéramos esto, cada app que ya estaba abierta ANTES de arrancar
                // el servicio se reportaría como "recién abierta", lo cual sería falso.
                _knownProcesses = currentProcesses;
                _initialized = true;
                return events;
            }

            // PIDs que están ahora pero no estaban antes -> se abrieron
            foreach (var pid in currentProcesses.Keys)
            {
                if (!_knownProcesses.ContainsKey(pid))
                {
                    events.Add(new ApplicationEvent
                    {
                        Source = "Application",
                        Category = "Lifecycle",
                        EventType = "Opened",
                        ProcessId = pid,
                        ProcessName = currentProcesses[pid]
                    });
                }
            }

            // PIDs que estaban antes pero ya no están -> se cerraron
            foreach (var pid in _knownProcesses.Keys)
            {
                if (!currentProcesses.ContainsKey(pid))
                {
                    events.Add(new ApplicationEvent
                    {
                        Source = "Application",
                        Category = "Lifecycle",
                        EventType = "Closed",
                        ProcessId = pid,
                        ProcessName = _knownProcesses[pid]
                    });
                }
            }

            _knownProcesses = currentProcesses;
            return events;
        }

        private Dictionary<int, string> GetVisibleProcesses()
        {
            var result = new Dictionary<int, string>();

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        result[process.Id] = process.ProcessName;
                    }
                }
                catch
                {
                    // Algunos procesos (protegidos, del sistema, o que se cerraron
                    // justo en este instante) no dejan leer sus propiedades.
                    // Los ignoramos sin romper el ciclo completo.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return result;
        }
    }
}