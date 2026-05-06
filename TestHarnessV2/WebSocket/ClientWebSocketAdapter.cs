using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TestHarnessV2.WebSocket
{
    internal sealed class ClientWebSocketAdapter : IWebSocketClient
    {
        private readonly SemaphoreSlim _sync = new SemaphoreSlim(1, 1);
        private ClientWebSocket? _client;
        private CancellationTokenSource? _receiveLoopCts;
        private Task? _receiveLoopTask;

        public event EventHandler? Opened;
        public event EventHandler<WebSocketMessageReceivedEventArgs>? MessageReceived;
        public event EventHandler<WebSocketErrorEventArgs>? ErrorOccurred;
        public event EventHandler? Closed;

        public WebSocketState State => _client?.State ?? WebSocketState.None;

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            await _sync.WaitAsync(cancellationToken);
            try
            {
                if (_client?.State == WebSocketState.Open)
                    return;

                await DisconnectCoreAsync(CancellationToken.None);

                _client = new ClientWebSocket();
                _receiveLoopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                await _client.ConnectAsync(uri, cancellationToken);
                Opened?.Invoke(this, EventArgs.Empty);
                _receiveLoopTask = Task.Run(() => ReceiveLoopAsync(_client, _receiveLoopCts.Token));
            }
            finally
            {
                _sync.Release();
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken)
        {
            await _sync.WaitAsync(cancellationToken);
            try
            {
                await DisconnectCoreAsync(cancellationToken);
            }
            finally
            {
                _sync.Release();
            }
        }

        private async Task DisconnectCoreAsync(CancellationToken cancellationToken)
        {
            try
            {
                _receiveLoopCts?.Cancel();
            }
            catch
            {
                // Ignore cancellation races during shutdown.
            }

            if (_client != null)
            {
                try
                {
                    if (_client.State == WebSocketState.Open || _client.State == WebSocketState.CloseReceived)
                    {
                        await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", cancellationToken);
                    }
                }
                catch
                {
                    // If close fails, dispose will tear the socket down.
                }

                _client.Dispose();
                _client = null;
            }

            if (_receiveLoopTask != null)
            {
                try
                {
                    await _receiveLoopTask;
                }
                catch
                {
                    // Receive loop reports errors via events.
                }

                _receiveLoopTask = null;
            }

            _receiveLoopCts?.Dispose();
            _receiveLoopCts = null;
        }

        private async Task ReceiveLoopAsync(ClientWebSocket client, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];

            try
            {
                while (!cancellationToken.IsCancellationRequested && client.State == WebSocketState.Open)
                {
                    using var messageStream = new MemoryStream();

                    while (true)
                    {
                        var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Closed?.Invoke(this, EventArgs.Empty);
                            return;
                        }

                        if (result.MessageType != WebSocketMessageType.Text)
                            continue;

                        messageStream.Write(buffer, 0, result.Count);
                        if (result.EndOfMessage)
                            break;
                    }

                    string message = Encoding.UTF8.GetString(messageStream.ToArray());
                    MessageReceived?.Invoke(this, new WebSocketMessageReceivedEventArgs(message));
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown/reconnect.
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new WebSocketErrorEventArgs("WebSocket receive loop failed.", ex));
            }
            finally
            {
                Closed?.Invoke(this, EventArgs.Empty);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync(CancellationToken.None);
            _sync.Dispose();
        }
    }
}
