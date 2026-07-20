#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CadentCable.Abstractions
{
    public interface IClock
    {
        double Now();
    }

    public interface ITimer
    {
        Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
    }

    public enum CCWebSocketState
    {
        None,
        Connecting,
        Open,
        CloseSent,
        CloseReceived,
        Closed,
        Aborted,
    }

    public enum CCWebSocketMessageType
    {
        Text,
        Binary,
        Close,
    }

    public sealed class CCWebSocketMessage
    {
        private CCWebSocketMessage(
            CCWebSocketMessageType messageType,
            string? text,
            int? closeCode,
            string? closeReason)
        {
            MessageType = messageType;
            Text = text;
            CloseCode = closeCode;
            CloseReason = closeReason;
        }

        public CCWebSocketMessageType MessageType { get; }
        public string? Text { get; }
        public int? CloseCode { get; }
        public string? CloseReason { get; }

        public static CCWebSocketMessage CreateText(string text)
        {
            return new CCWebSocketMessage(CCWebSocketMessageType.Text, text, null, null);
        }

        public static CCWebSocketMessage CreateBinary()
        {
            return new CCWebSocketMessage(CCWebSocketMessageType.Binary, null, null, null);
        }

        public static CCWebSocketMessage CreateClose(int? code, string? reason)
        {
            return new CCWebSocketMessage(CCWebSocketMessageType.Close, null, code, reason);
        }
    }

    public interface ICCWebSocket : IAsyncDisposable
    {
        CCWebSocketState State { get; }

        Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
        Task SendTextAsync(string text, CancellationToken cancellationToken);
        Task<CCWebSocketMessage> ReceiveAsync(CancellationToken cancellationToken);
        Task CloseAsync(int closeCode, string reason, CancellationToken cancellationToken);
        void Abort();
    }

    public interface ICCWebSocketFactory
    {
        ICCWebSocket Create();
    }

    public sealed class CCHttpResponse
    {
        public CCHttpResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        public int StatusCode { get; }
        public string Body { get; }
        public bool IsSuccessStatusCode => StatusCode >= 200 && StatusCode <= 299;
    }

    public interface ICHttpTransport
    {
        Task<CCHttpResponse> PostJsonAsync(
            Uri uri,
            string json,
            CancellationToken cancellationToken);
    }

    public sealed class CCRuntime : IAsyncDisposable
    {
        private readonly bool _ownsComponents;
        private bool _disposed;

        public CCRuntime(
            IClock clock,
            ITimer timer,
            ICCWebSocketFactory webSocketFactory,
            ICHttpTransport http,
            bool ownsComponents = false)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Timer = timer ?? throw new ArgumentNullException(nameof(timer));
            WebSocketFactory = webSocketFactory ?? throw new ArgumentNullException(nameof(webSocketFactory));
            Http = http ?? throw new ArgumentNullException(nameof(http));
            _ownsComponents = ownsComponents;
        }

        public IClock Clock { get; }
        public ITimer Timer { get; }
        public ICCWebSocketFactory WebSocketFactory { get; }
        public ICHttpTransport Http { get; }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_ownsComponents)
            {
                return;
            }

            await DisposeObjectAsync(Http).ConfigureAwait(false);
            await DisposeObjectAsync(WebSocketFactory).ConfigureAwait(false);
            await DisposeObjectAsync(Timer).ConfigureAwait(false);
            await DisposeObjectAsync(Clock).ConfigureAwait(false);
        }

        private static async ValueTask DisposeObjectAsync(object value)
        {
            if (value is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                return;
            }

            if (value is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
