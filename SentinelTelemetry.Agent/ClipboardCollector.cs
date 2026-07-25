using System;
using System.Collections.Generic;
using System.Text;
using SentinelTelemetry.Core.Models;
using System.Collections.Concurrent;
// Este collector detecta cuando copias algo en el clipboard. Puede ser texto, imagen o filedrop(que sera eso)
namespace SentinelTelemetry.Telemetry
{
    public class ClipboardCollector
    {

        private readonly ClipboardHook _hook;
        private readonly ConcurrentQueue<(string ContentType, int? ContentLength)> _pendingChanges = new();

        public ClipboardCollector()
        {
            _hook = new ClipboardHook();
            _hook.ClipboardChanged += OnClipboardChanged;
            _hook.Start();
        }

        private void OnClipboardChanged(string contentType, int? contentLength)
        {
            _pendingChanges.Enqueue((contentType, contentLength));
        }

        public List<EventBase> DetectChanges()
        {
            var events = new List<EventBase>();

            while (_pendingChanges.TryDequeue(out var change))
            {
                events.Add(new ClipboardEvent
                {
                    Source = "ClipboardCollector",
                    Category = "Clipboard",
                    EventType = "ClipboardChanged",
                    ContentType = change.ContentType,
                    ContentLength = change.ContentLength
                });
            }

            return events;
        }

        public void Stop() => _hook.Stop();
    }
}
