using System;
using System.Threading;
using System.Threading.Tasks;
using RouletteDisplay.WebSocket;

namespace RouletteDisplay.Monitoring
{
    internal sealed class RouletteSocketMonitor : IRouletteMonitor
    {
        private readonly IWebSocketClient _webSocketClient;
        private readonly IRefreshCoordinator _refreshCoordinator;
        private readonly Uri _socketUri;
        private readonly TimeSpan _roundResultTimeout;
        private readonly TimeSpan _reconnectDelay;
        private readonly bool _enabled;
        private readonly bool _requireFirstRoundResultBeforeRefresh;
        private readonly object _stateLock = new object();

        private CancellationTokenSource? _cts;
        private Task? _runTask;
        private DateTime _lastRoundResultUtc = DateTime.MinValue;
        private bool _hasSeenRoundResult;
        private bool _hasLoggedMonitorDisabled;

        public RouletteSocketMonitor(
            IWebSocketClient webSocketClient,
            IRefreshCoordinator refreshCoordinator,
            AppSettings settings)
        {
            _webSocketClient = webSocketClient;
            _refreshCoordinator = refreshCoordinator;
            _enabled = settings.WebSocketMonitorEnabled;
            _socketUri = new Uri(settings.WebSocketUrl);
            _roundResultTimeout = TimeSpan.FromSeconds(Math.Max(5, settings.RoundResultTimeoutSeconds));
            _reconnectDelay = TimeSpan.FromSeconds(Math.Max(1, settings.WebSocketReconnectDelaySeconds));
            _requireFirstRoundResultBeforeRefresh = settings.RequireFirstRoundResultBeforeRefresh;

            _webSocketClient.Opened += OnOpened;
            _webSocketClient.MessageReceived += OnMessageReceived;
            _webSocketClient.ErrorOccurred += OnErrorOccurred;
            _webSocketClient.Closed += OnClosed;
        }

        public event EventHandler<RefreshRequest>? RefreshRequested;

        public void Start()
        {
            if (!_enabled)
            {
                if (!_hasLoggedMonitorDisabled)
                {
                    _hasLoggedMonitorDisabled = true;
                    Logger.Log("[WS-MONITOR] WebSocket monitor disabled in settings.");
                }

                return;
            }

            lock (_stateLock)
            {
                if (_runTask != null)
                    return;

                _cts = new CancellationTokenSource();
                _runTask = Task.Run(() => RunAsync(_cts.Token));
            }
        }

        public async Task StopAsync()
        {
            CancellationTokenSource? cts;
            Task? runTask;

            lock (_stateLock)
            {
                cts = _cts;
                runTask = _runTask;
                _cts = null;
                _runTask = null;
            }

            if (cts == null)
                return;

            cts.Cancel();

            try
            {
                await _webSocketClient.DisconnectAsync(CancellationToken.None);
            }
            catch
            {
                // Best effort during shutdown.
            }

            if (runTask != null)
            {
                try
                {
                    await runTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected during shutdown.
                }
            }

            cts.Dispose();
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            Logger.Log($"[WS-MONITOR] Starting monitor for {_socketUri}");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _webSocketClient.ConnectAsync(_socketUri, cancellationToken);
                    await MonitorConnectionAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Logger.LogError("[WS-MONITOR] Connection cycle failed.", ex);
                }

                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    await Task.Delay(_reconnectDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            Logger.Log("[WS-MONITOR] Monitor stopped.");
        }

        private async Task MonitorConnectionAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                EvaluateTimeout();

                if (_webSocketClient.State != System.Net.WebSockets.WebSocketState.Open)
                    return;

                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        private void EvaluateTimeout()
        {
            DateTime nowUtc = DateTime.UtcNow;
            DateTime lastRoundResultUtc;
            bool hasSeenRoundResult;

            lock (_stateLock)
            {
                lastRoundResultUtc = _lastRoundResultUtc;
                hasSeenRoundResult = _hasSeenRoundResult;
            }

            if (_requireFirstRoundResultBeforeRefresh && !hasSeenRoundResult)
                return;

            if (lastRoundResultUtc == DateTime.MinValue)
                return;

            TimeSpan elapsed = nowUtc - lastRoundResultUtc;
            if (elapsed < _roundResultTimeout)
                return;

            string reason = $"No round_result received for {elapsed.TotalSeconds:F0}s (threshold {_roundResultTimeout.TotalSeconds:F0}s).";
            if (_refreshCoordinator.TryCreateRequest(reason, out var request) && request != null)
            {
                Logger.Log($"[WS-MONITOR] Refresh requested. {reason}");
                RefreshRequested?.Invoke(this, request);

                lock (_stateLock)
                {
                    // Reset the timer baseline after requesting a refresh so cooldown controls repeats.
                    _lastRoundResultUtc = nowUtc;
                }
            }
        }

        private void OnOpened(object? sender, EventArgs e)
        {
            Logger.Log($"[WS-MONITOR] Connected to {_socketUri}");
        }

        private void OnClosed(object? sender, EventArgs e)
        {
            Logger.Log("[WS-MONITOR] WebSocket closed.");
        }

        private void OnErrorOccurred(object? sender, WebSocketErrorEventArgs e)
        {
            Logger.LogError("[WS-MONITOR] " + e.Message, e.Exception);
        }

        private void OnMessageReceived(object? sender, WebSocketMessageReceivedEventArgs e)
        {
            try
            {
                RouletteEvent rouletteEvent = RouletteEventParser.Parse(e.Message);
                switch (rouletteEvent.EventType)
                {
                    case RouletteEventType.RoundResult:
                        lock (_stateLock)
                        {
                            _hasSeenRoundResult = true;
                            _lastRoundResultUtc = DateTime.UtcNow;
                        }

                        Logger.Log($"[WS-MONITOR] round_result received{FormatEgmIdSuffix(rouletteEvent.EgmId)}.");
                        break;

                    case RouletteEventType.UiPing:
                        Logger.Log($"[WS-MONITOR] ui_ping received{FormatEgmIdSuffix(rouletteEvent.EgmId)}.");
                        break;

                    case RouletteEventType.SessionInitialized:
                        Logger.Log($"[WS-MONITOR] session_initialized received{FormatEgmIdSuffix(rouletteEvent.EgmId)}.");
                        break;

                    default:
                        if (!string.IsNullOrWhiteSpace(rouletteEvent.RawEventType))
                            Logger.Log($"[WS-MONITOR] Event received: {rouletteEvent.RawEventType}{FormatEgmIdSuffix(rouletteEvent.EgmId)}.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("[WS-MONITOR] Failed to parse incoming websocket message.", ex);
            }
        }

        private static string FormatEgmIdSuffix(string? egmId)
        {
            return string.IsNullOrWhiteSpace(egmId) ? string.Empty : $" (egmId={egmId})";
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
            _webSocketClient.Opened -= OnOpened;
            _webSocketClient.MessageReceived -= OnMessageReceived;
            _webSocketClient.ErrorOccurred -= OnErrorOccurred;
            _webSocketClient.Closed -= OnClosed;
            await _webSocketClient.DisposeAsync();
        }
    }
}
