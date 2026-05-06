using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace TestHarnessV2.WebSocket
{
    internal interface IWebSocketClient : IAsyncDisposable
    {
        event EventHandler? Opened;
        event EventHandler<WebSocketMessageReceivedEventArgs>? MessageReceived;
        event EventHandler<WebSocketErrorEventArgs>? ErrorOccurred;
        event EventHandler? Closed;

        WebSocketState State { get; }

        Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
        Task DisconnectAsync(CancellationToken cancellationToken);
    }

    internal sealed class WebSocketMessageReceivedEventArgs : EventArgs
    {
        public WebSocketMessageReceivedEventArgs(string message)
        {
            Message = message;
        }

        public string Message { get; }
    }

    internal sealed class WebSocketErrorEventArgs : EventArgs
    {
        public WebSocketErrorEventArgs(string message, Exception? exception = null)
        {
            Message = message;
            Exception = exception;
        }

        public string Message { get; }
        public Exception? Exception { get; }
    }
}
