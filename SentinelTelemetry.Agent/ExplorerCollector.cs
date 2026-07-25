using SentinelTelemetry.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
// Este colector detecta el expolador de archivos y donde estas navegando mostrando la ruta completa.

namespace SentinelTelemetry.Telemetry
{
    public class ExplorerCollector
    {
        private Dictionary<int, string> _previousWindows = new();
        private bool _isFirstTick = true;

        private static readonly Guid ShellWindowsClsid = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
        public List<EventBase> DetectChanges()
        {
            var events = new List<EventBase>();
            var currentWindows = new Dictionary<int, string>();

            Type? shellWindowsType = Type.GetTypeFromCLSID(ShellWindowsClsid);
            dynamic shellWindows = Activator.CreateInstance(shellWindowsType!)!;

            foreach (dynamic window in shellWindows)
            {
                try
                {
                    string fullName = (string)window.FullName;
                    string processName = Path.GetFileNameWithoutExtension(fullName);

                    if (!string.Equals(processName, "explorer", StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // descarta ventanas que no son Explorador de archivos
                    }

                    string folderPath = new Uri((string)window.LocationURL).LocalPath;
                    int hwnd = (int)window.HWND;
                    currentWindows[hwnd] = folderPath;
                }
                catch
                {
                    continue; // ventana cerrada entre enumeración y lectura, o carpeta virtual sin ruta local
                }
            }

            if (_isFirstTick)
            {
                _previousWindows = currentWindows;
                _isFirstTick = false;
                return events;
            }

            foreach (var (hwnd, path) in currentWindows)
            {
                if (!_previousWindows.ContainsKey(hwnd))
                {
                    events.Add(CreateEvent("FolderOpened", path));
                }
                else if (_previousWindows[hwnd] != path)
                {
                    events.Add(CreateEvent("FolderChanged", path));
                }
            }

            foreach (var (hwnd, path) in _previousWindows)
            {
                if (!currentWindows.ContainsKey(hwnd))
                {
                    events.Add(CreateEvent("FolderClosed", path));
                }
            }

            _previousWindows = currentWindows;
            return events;
        }

        private static ExplorerEvent CreateEvent(string eventType, string folderPath) => new()
        {
            Source = "ExplorerCollector",
            Category = "Explorer",
            EventType = eventType,
            FolderPath = folderPath
        };
    }
}
