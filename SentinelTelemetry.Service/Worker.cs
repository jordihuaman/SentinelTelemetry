using Microsoft.Extensions.Configuration;
using SentinelTelemetry.Core.Interfaces;
using SentinelTelemetry.Core.Models;
using SentinelTelemetry.Persistence;
using SentinelTelemetry.Telemetry;
using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace SentinelTelemetry.Service
{
    public class Worker : BackgroundService
    {
        private readonly ClipboardCollector _clipboardCollector = new();
        private readonly IdleCollector _idleCollector = new(TimeSpan.FromSeconds(30));
        private (int X, int Y)? _lastPointerPosition;
        private WindowEvent? _lastWindowEvent;

        private readonly ExplorerCollector _explorerCollector = new();
        private readonly FileCollector _fileCollector;

        private readonly ApplicationCollector _applicationCollector;

        private readonly SessionCollector _sessionCollector;

        private readonly ILogger<Worker> _logger;
        private readonly IEventRepository _repository;
        private readonly PointerCollector _pointerCollector;
        private readonly KeyboardCollector _keyboardCollector;
        private readonly WindowCollector _windowCollector;
        private readonly KeyboardHook _keyboardHook;
        private readonly PauseController _pauseController;

        public Worker(ILogger<Worker> logger, IEventRepository repository, IConfiguration configuration, PauseController pauseController)
        {
            _pauseController = pauseController;
            _fileCollector = new FileCollector(configuration, logger);
            _applicationCollector = new ApplicationCollector();

            _logger = logger;
            _repository = repository;

            _pointerCollector = new PointerCollector();
            _keyboardCollector = new KeyboardCollector();
            _windowCollector = new WindowCollector();

            _keyboardHook = new KeyboardHook();
            _keyboardHook.KeyPressed += _keyboardCollector.RegisterKeyPress;
            _keyboardHook.Start();

            _sessionCollector = new SessionCollector();
            _sessionCollector.SessionEventOccurred += OnSessionEventOccurred;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_pauseController.IsPaused)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                try
                {

                    var fileEvents = _fileCollector.DetectChanges();
                    foreach (var fileEvent in fileEvents)
                    {
                        await _repository.SaveEventAsync(fileEvent);
                    }

                    var explorerEvents = _explorerCollector.DetectChanges();
                    foreach (var explorerEvent in explorerEvents)
                    {
                        await _repository.SaveEventAsync(explorerEvent);
                    }

                    foreach (var clipboardEvent in _clipboardCollector.DetectChanges())
                    {
                        await _repository.SaveEventAsync(clipboardEvent);
                    }

                    var appEvents = _applicationCollector.DetectChanges();
                    foreach (var appEvent in appEvents)
                    {
                        await _repository.SaveEventAsync(appEvent);
                    }

                    var pointerEvent = (PointerEvent)_pointerCollector.Capture();
                    var keyboardEvent = (KeyboardEvent)_keyboardCollector.Capture();
                    var windowEvent = (WindowEvent)_windowCollector.Capture();

                    bool pointerHadActivity = await ProcessPointerEventAsync(pointerEvent);
                    bool keyboardHadActivity = keyboardEvent.KeyPressCount > 0;

                    if (keyboardHadActivity)
                    {
                        await _repository.SaveEventAsync(keyboardEvent);
                    }

                    var idleEvent = _idleCollector.Update(pointerHadActivity || keyboardHadActivity);
                    if (idleEvent != null)
                    {
                        await _repository.SaveEventAsync(idleEvent);
                    }

                    bool windowChanged = _lastWindowEvent == null
                        || _lastWindowEvent.ProcessName != windowEvent.ProcessName
                        || _lastWindowEvent.WindowTitle != windowEvent.WindowTitle;

                    if (windowChanged)
                    {
                        await _repository.SaveEventAsync(windowEvent);
                        _lastWindowEvent = windowEvent;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error capturando o guardando eventos");
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        private async Task<bool> ProcessPointerEventAsync(PointerEvent current)
        {
            const double movementThreshold = 4.0;

            bool hasClick = current.LeftClick || current.RightClick;
            bool hasMoved = HasMovedBeyondThreshold(current.X, current.Y, movementThreshold);

            if (!hasClick && !hasMoved)
            {
                return false;
            }

            current.EventType = hasClick
                ? (current.LeftClick ? "LeftClick" : "RightClick")
                : "MouseMove";

            await _repository.SaveEventAsync(current);
            _lastPointerPosition = (current.X, current.Y);

            return true;
        }
        
        private async void OnSessionEventOccurred(EventBase sessionEvent)
        {
            try
            {
                await _repository.SaveEventAsync(sessionEvent);
                _logger.LogInformation("Evento de sesión: {EventType}", sessionEvent.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando evento de sesión");
            }
        }


        private bool HasMovedBeyondThreshold(int x, int y, double threshold)
        {
            if (_lastPointerPosition == null)
            {
                return true;
            }

            var (lastX, lastY) = _lastPointerPosition.Value;
            double distance = Math.Sqrt(Math.Pow(x - lastX, 2) + Math.Pow(y - lastY, 2));
            return distance >= threshold;
        }
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _fileCollector.Dispose();
            _keyboardHook.Dispose();
            _sessionCollector.Dispose();
            await base.StopAsync(stoppingToken);
            _clipboardCollector.Stop();
        }
    }
}
