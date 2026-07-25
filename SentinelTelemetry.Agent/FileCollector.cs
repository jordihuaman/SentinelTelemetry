using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SentinelTelemetry.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
// Este collector, recopila informacion sobre modificaciones en torno a los archivos. Renombramiento, Creacion, Borrar, o moverlos de lugar.
namespace SentinelTelemetry.Telemetry
{
    public class FileCollector : IDisposable
    {
        private readonly List<FileSystemWatcher> _watchers = new();
        private readonly HashSet<string> _excludedFolderNames;
        private readonly ILogger? _logger;

        private readonly ConcurrentQueue<FileEvent> _immediateEvents = new();
        private readonly ConcurrentDictionary<string, FileEvent> _pendingModifications = new();

        public FileCollector(IConfiguration configuration, ILogger? logger = null)
        {
            _logger = logger;

            var watchedFolders = configuration.GetSection("FileWatcher:WatchedFolders").Get<List<string>>()
                ?? new List<string>();

            var excludedNames = configuration.GetSection("FileWatcher:ExcludedFolderNames").Get<List<string>>()
                ?? new List<string>();

            _excludedFolderNames = new HashSet<string>(excludedNames, StringComparer.OrdinalIgnoreCase);

            foreach (var folder in watchedFolders)
            {
                if (!Directory.Exists(folder))
                {
                    _logger?.LogWarning("FileCollector: carpeta no encontrada, se omite: {Folder}", folder);
                    continue;
                }

                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    InternalBufferSize = 65536, // 64KB en vez de los 8KB por defecto, para reducir riesgo de overflow
                    NotifyFilter = NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.LastWrite
                };

                watcher.Created += (s, e) => OnCreatedOrDeleted(e, "Created");
                watcher.Deleted += (s, e) => OnCreatedOrDeleted(e, "Deleted");
                watcher.Renamed += OnRenamed;
                watcher.Changed += OnChanged;
                watcher.Error += OnError;

                watcher.EnableRaisingEvents = true;
                _watchers.Add(watcher);
            }
        }

        private bool IsExcluded(string path)
        {
            var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return segments.Any(segment => _excludedFolderNames.Contains(segment));
        }

        private void OnCreatedOrDeleted(FileSystemEventArgs e, string eventType)
        {
            if (IsExcluded(e.FullPath)) return;

            _immediateEvents.Enqueue(new FileEvent
            {
                Source = "FileCollector",
                Category = "File",
                EventType = eventType,
                FilePath = e.FullPath
            });
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (IsExcluded(e.FullPath)) return;

            _immediateEvents.Enqueue(new FileEvent
            {
                Source = "FileCollector",
                Category = "File",
                EventType = "Renamed",
                FilePath = e.FullPath,
                OldFilePath = e.OldFullPath
            });
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            if (IsExcluded(e.FullPath)) return;
            if (Directory.Exists(e.FullPath)) return; // ignora "cambios" en carpetas, solo interesan archivos

            // Windows dispara varios "Changed" seguidos por un solo guardado real.
            // Usamos el path como clave: cada nuevo evento SOBRESCRIBE al anterior en el diccionario,
            // así el Worker solo ve un "Modified" por archivo por ciclo, no diez.
            _pendingModifications[e.FullPath] = new FileEvent
            {
                Source = "FileCollector",
                Category = "File",
                EventType = "Modified",
                FilePath = e.FullPath
            };
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            _logger?.LogError(e.GetException(), "FileCollector: error en FileSystemWatcher (posible overflow de buffer)");
        }

        public List<EventBase> DetectChanges()
        {
            var events = new List<EventBase>();

            while (_immediateEvents.TryDequeue(out var evt))
            {
                events.Add(evt);
            }

            foreach (var key in _pendingModifications.Keys.ToList())
            {
                if (_pendingModifications.TryRemove(key, out var evt))
                {
                    events.Add(evt);
                }
            }

            return events;
        }

        public void Dispose()
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }
    }
}