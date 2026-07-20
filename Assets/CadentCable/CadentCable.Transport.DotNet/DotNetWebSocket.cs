#nullable enable

using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CadentCable.Abstractions;

namespace CadentCable.Transport.DotNet
{
    public sealed class DotNetWebSocketFactory : ICCWebSocketFactory
    {
        public ICCWebSocket Create()
        {
            return new DotNetWebSocket();
        }
    }

    public sealed class DotNetWebSocket : ICCWebSocket
    {
        private const int ReceiveBufferSize = 8192;
        private readonly ClientWebSocket _socket = new ClientWebSocket();
        private bool _disposed;

        public CCWebSocketState State
        {
            get
            {
                switch (_socket.State)
                {
                    case WebSocketState.None:
                        return CCWebSocketState.None;
                    case WebSocketState.Connecting:
                        return CCWebSocketState.Connecting;
                    case WebSocketState.Open:
                        return CCWebSocketState.Open;
                    case WebSocketState.CloseSent:
                        return CCWebSocketState.CloseSent;
                    case WebSocketState.CloseReceived:
                        return CCWebSocketState.CloseReceived;
                    case WebSocketState.Closed:
                        return CCWebSocketState.Closed;
                    case WebSocketState.Aborted:
                        return CCWebSocketState.Aborted;
                    default:
                        return CCWebSocketState.None;
                }
            }
        }

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            return _socket.ConnectAsync(uri, cancellationToken);
        }

        public Task SendTextAsync(string text, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return _socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                cancellationToken);
        }

        public async Task<CCWebSocketMessage> ReceiveAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            byte[] buffer = new byte[ReceiveBufferSize];

            using (MemoryStream stream = new MemoryStream())
            {
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        cancellationToken).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        int? closeCode = result.CloseStatus.HasValue
                            ? (int)result.CloseStatus.Value
                            : (int?)null;
                        return CCWebSocketMessage.CreateClose(
                            closeCode,
                            result.CloseStatusDescription);
                    }

                    if (result.Count > 0)
                    {
                        stream.Write(buffer, 0, result.Count);
                    }
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    return CCWebSocketMessage.CreateBinary();
                }

                string text = Encoding.UTF8.GetString(stream.ToArray());
                return CCWebSocketMessage.CreateText(text);
            }
        }

        public Task CloseAsync(
            int closeCode,
            string reason,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            WebSocketCloseStatus status = (WebSocketCloseStatus)closeCode;
            return _socket.CloseOutputAsync(status, reason, cancellationToken);
        }

        public void Abort()
        {
            if (!_disposed)
            {
                _socket.Abort();
            }
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _socket.Dispose();
            }

            return default;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DotNetWebSocket));
            }
        }
    }
}
