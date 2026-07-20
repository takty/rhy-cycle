#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CadentCable.Abstractions;

namespace CadentCable.Core
{
    public sealed class RelayConnection<TPayload> : IAsyncDisposable
    {
        private const int NormalClosureCode = 1000;
        private const int AbnormalClosureCode = 1006;

        private readonly object _stateGate = new object();
        private readonly object _syncGate = new object();
        private readonly CCRuntime _runtime;
        private readonly IProtocolSerializer _protocolSerializer;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _joinLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();
        private readonly bool _autoSync;
        private readonly int _syncIntervalMs;

        private ICCWebSocket? _socket;
        private CancellationTokenSource? _receiveCts;
        private Task? _receiveTask;
        private CancellationTokenSource? _syncCts;
        private Task? _syncTask;
        private bool _disposed;

        public RelayConnection(
            RelayConnectionOptions options,
            CCRuntime runtime,
            IProtocolSerializer protocolSerializer)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _protocolSerializer = protocolSerializer ?? throw new ArgumentNullException(nameof(protocolSerializer));

            ServerUrl = RequireText(options.ServerUrl, nameof(options.ServerUrl));
            RoomId = RequireText(options.RoomId, nameof(options.RoomId)).ToUpperInvariant();
            DisplayName = RequireText(options.DisplayName, nameof(options.DisplayName));
            OwnerToken = EmptyToNull(options.OwnerToken);
            MemberId = EmptyToNull(options.MemberId);
            ResumeToken = EmptyToNull(options.ResumeToken);
            _autoSync = options.AutoSync;
            _syncIntervalMs = options.SyncIntervalMs > 0 ? options.SyncIntervalMs : 3000;
        }

        public string ServerUrl { get; }
        public string RoomId { get; }
        public string? OwnerToken { get; }

        public string DisplayName { get; private set; }
        public string? MemberId { get; private set; }
        public string? ResumeToken { get; private set; }
        public double? Rtt { get; private set; }
        public double? OffsetToServerTime { get; private set; }

        public event Action<RelayEvent<TPayload>>? EventReceived;
        public event Action<OpenEvent<TPayload>>? Open;
        public event Action<CloseEvent<TPayload>>? Close;
        public event Action<ErrorEvent<TPayload>>? Error;
        public event Action<JoinedEvent<TPayload>>? Joined;
        public event Action<PendingEvent<TPayload>>? Pending;
        public event Action<JoinRequestEvent<TPayload>>? JoinRequest;
        public event Action<JoinRejectedEvent<TPayload>>? JoinRejected;
        public event Action<MemberJoinedEvent<TPayload>>? MemberJoined;
        public event Action<MemberUpdatedEvent<TPayload>>? MemberUpdated;
        public event Action<MemberLeftEvent<TPayload>>? MemberLeft;
        public event Action<TickEvent<TPayload>>? Tick;
        public event Action<HeartbeatEvent<TPayload>>? Heartbeat;
        public event Action<RoomClosedEvent<TPayload>>? RoomClosed;
        public event Action<SyncStatusEvent<TPayload>>? SyncStatus;
        public event Action<UnknownEvent<TPayload>>? Unknown;

        public void SetDisplayName(string displayName)
        {
            ThrowIfDisposed();
            DisplayName = RequireText(displayName, nameof(displayName));
        }

        public async Task JoinAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await _joinLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();

                lock (_stateGate)
                {
                    if (_socket != null)
                    {
                        return;
                    }
                }

                if ((MemberId == null) != (ResumeToken == null))
                {
                    throw new InvalidOperationException(
                        "Both MemberId and ResumeToken are required to resume.");
                }

                Uri uri = UrlUtility.BuildWebSocketUrl(
                    ServerUrl,
                    Routes.WebSocket,
                    new Dictionary<string, object?>
                    {
                        { "roomId", RoomId },
                        { "displayName", DisplayName },
                        { "ownerToken", OwnerToken },
                        { "memberId", MemberId },
                        { "resumeToken", ResumeToken },
                    });

                ICCWebSocket socket = _runtime.WebSocketFactory.Create();
                lock (_stateGate)
                {
                    _socket = socket;
                }

                try
                {
                    await socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    ClearSocket(socket);
                    await socket.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
                catch (Exception ex)
                {
                    ClearSocket(socket);
                    await socket.DisposeAsync().ConfigureAwait(false);
                    Emit(new ErrorEvent<TPayload>
                    {
                        Code = "websocket_error",
                        Message = ex.Message,
                    });
                    throw;
                }

                CancellationTokenSource receiveCts = CancellationTokenSource.CreateLinkedTokenSource(
                    _disposeCts.Token);
                lock (_stateGate)
                {
                    _receiveCts = receiveCts;
                }

                Emit(new OpenEvent<TPayload>());

                Task receiveTask = RunReceiveLoopAsync(socket, receiveCts.Token);
                lock (_stateGate)
                {
                    if (ReferenceEquals(_socket, socket))
                    {
                        _receiveTask = receiveTask;
                    }
                }

                if (_autoSync)
                {
                    StartSync();
                }
            }
            finally
            {
                _joinLock.Release();
            }
        }

        public async Task LeaveAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            Exception? sendException = null;
            ICCWebSocket? socket = GetSocket();

            if (socket != null && socket.State == CCWebSocketState.Open)
            {
                try
                {
                    await SendRawAsync(
                        _protocolSerializer.SerializeLeave(),
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    sendException = ex;
                }
            }

            MemberId = null;
            ResumeToken = null;
            StopSync();

            if (socket != null)
            {
                await CloseSocketAsync(socket, "leave", cancellationToken).ConfigureAwait(false);
            }

            if (sendException != null)
            {
                throw sendException;
            }
        }

        public Task CloseRoomAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return SendRawAsync(
                _protocolSerializer.SerializeCloseRoom(),
                cancellationToken);
        }

        public Task SendDataAsync(
            TPayload payload,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            string json = _protocolSerializer.SerializeData(_runtime.Clock.Now(), payload);
            return SendRawAsync(json, cancellationToken);
        }

        public Task ApproveAsync(
            string requestId,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(requestId))
            {
                throw new ArgumentException("Request ID is required.", nameof(requestId));
            }

            return SendRawAsync(
                _protocolSerializer.SerializeApprove(requestId),
                cancellationToken);
        }

        public Task SyncOnceAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return SendRawAsync(
                _protocolSerializer.SerializeSyncRequest(_runtime.Clock.Now()),
                cancellationToken);
        }

        public void StartSync()
        {
            ThrowIfDisposed();
            StopSync();

            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(
                _disposeCts.Token);
            lock (_syncGate)
            {
                _syncCts = cts;
            }

            Task task = RunSyncLoopAsync(cts.Token);
            lock (_syncGate)
            {
                if (ReferenceEquals(_syncCts, cts))
                {
                    _syncTask = task;
                }
            }
        }

        public void StopSync()
        {
            CancellationTokenSource? cts;
            lock (_syncGate)
            {
                cts = _syncCts;
                _syncCts = null;
                _syncTask = null;
            }

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopSync();
            _disposeCts.Cancel();

            ICCWebSocket? socket = GetSocket();
            if (socket != null)
            {
                socket.Abort();
            }

            Task? receiveTask;
            lock (_stateGate)
            {
                receiveTask = _receiveTask;
            }

            if (receiveTask != null)
            {
                try
                {
                    await receiveTask.ConfigureAwait(false);
                }
                catch
                {
                    // Receive-loop errors are converted to relay events.
                }
            }
            else if (socket != null)
            {
                await socket.DisposeAsync().ConfigureAwait(false);
                ClearSocket(socket);
            }

            _disposeCts.Dispose();
        }

        private async Task RunSyncLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await SyncOnceAsync(cancellationToken).ConfigureAwait(false);
                    await _runtime.Timer
                        .DelayAsync(TimeSpan.FromMilliseconds(_syncIntervalMs), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (InvalidOperationException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    Emit(new ErrorEvent<TPayload>
                    {
                        Code = "sync_error",
                        Message = ex.Message,
                    });
                    return;
                }
            }
        }

        private async Task RunReceiveLoopAsync(
            ICCWebSocket socket,
            CancellationToken cancellationToken)
        {
            int closeCode = AbnormalClosureCode;
            string closeReason = string.Empty;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    CCWebSocketMessage message = await socket
                        .ReceiveAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (message.MessageType == CCWebSocketMessageType.Close)
                    {
                        closeCode = message.CloseCode ?? NormalClosureCode;
                        closeReason = message.CloseReason ?? string.Empty;
                        break;
                    }

                    if (message.MessageType != CCWebSocketMessageType.Text || message.Text == null)
                    {
                        Emit(new ErrorEvent<TPayload>
                        {
                            Code = "unsupported_message",
                            Message = "Only JSON text messages are supported.",
                        });
                        continue;
                    }

                    await HandleMessageAsync(message.Text, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                closeReason = _disposed ? "disposed" : "canceled";
            }
            catch (Exception ex)
            {
                closeReason = ex.Message;
                if (!_disposed && !cancellationToken.IsCancellationRequested)
                {
                    Emit(new ErrorEvent<TPayload>
                    {
                        Code = "websocket_error",
                        Message = ex.Message,
                    });
                }
            }
            finally
            {
                StopSync();
                ClearSocket(socket);

                try
                {
                    await socket.DisposeAsync().ConfigureAwait(false);
                }
                catch
                {
                    // The socket may already have been aborted or disposed.
                }

                Emit(new CloseEvent<TPayload>
                {
                    Code = closeCode,
                    Reason = closeReason,
                });
            }
        }

        private async Task HandleMessageAsync(string json, CancellationToken cancellationToken)
        {
            RelayEvent<TPayload> relayEvent = _protocolSerializer.DeserializeEvent<TPayload>(json);

            if (relayEvent is JoinedEvent<TPayload> joined)
            {
                MemberId = joined.MemberId;
                ResumeToken = joined.ResumeToken;
                DisplayName = joined.DisplayName;
            }

            if (relayEvent is RoomClosedEvent<TPayload>)
            {
                MemberId = null;
                ResumeToken = null;
            }

            if (relayEvent is SyncResponseEvent<TPayload> syncResponse)
            {
                string report = _protocolSerializer.SerializeSyncReport(
                    syncResponse.ClientSendTime,
                    syncResponse.ServerRecvTime,
                    syncResponse.ServerSendTime,
                    _runtime.Clock.Now());
                await SendRawAsync(report, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (relayEvent is SyncStatusEvent<TPayload> syncStatus)
            {
                Rtt = syncStatus.Rtt;
                OffsetToServerTime = syncStatus.OffsetToServerTime;
            }

            Emit(relayEvent);
        }

        private async Task SendRawAsync(string json, CancellationToken cancellationToken)
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ICCWebSocket? socket = GetSocket();
                if (socket == null || socket.State != CCWebSocketState.Open)
                {
                    throw new InvalidOperationException("WebSocket is not open.");
                }

                await socket.SendTextAsync(json, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task CloseSocketAsync(
            ICCWebSocket socket,
            string reason,
            CancellationToken cancellationToken)
        {
            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (socket.State == CCWebSocketState.Open ||
                    socket.State == CCWebSocketState.CloseReceived)
                {
                    await socket
                        .CloseAsync(NormalClosureCode, reason, cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (socket.State != CCWebSocketState.Closed)
                {
                    socket.Abort();
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private ICCWebSocket? GetSocket()
        {
            lock (_stateGate)
            {
                return _socket;
            }
        }

        private void ClearSocket(ICCWebSocket socket)
        {
            CancellationTokenSource? receiveCts = null;
            lock (_stateGate)
            {
                if (!ReferenceEquals(_socket, socket))
                {
                    return;
                }

                _socket = null;
                receiveCts = _receiveCts;
                _receiveCts = null;
                _receiveTask = null;
            }

            if (receiveCts != null)
            {
                receiveCts.Dispose();
            }
        }

        private void Emit(RelayEvent<TPayload> relayEvent)
        {
            InvokeSafely(EventReceived, relayEvent);

            switch (relayEvent)
            {
                case OpenEvent<TPayload> value:
                    InvokeSafely(Open, value);
                    break;
                case CloseEvent<TPayload> value:
                    InvokeSafely(Close, value);
                    break;
                case ErrorEvent<TPayload> value:
                    InvokeSafely(Error, value);
                    break;
                case JoinedEvent<TPayload> value:
                    InvokeSafely(Joined, value);
                    break;
                case PendingEvent<TPayload> value:
                    InvokeSafely(Pending, value);
                    break;
                case JoinRequestEvent<TPayload> value:
                    InvokeSafely(JoinRequest, value);
                    break;
                case JoinRejectedEvent<TPayload> value:
                    InvokeSafely(JoinRejected, value);
                    break;
                case MemberJoinedEvent<TPayload> value:
                    InvokeSafely(MemberJoined, value);
                    break;
                case MemberUpdatedEvent<TPayload> value:
                    InvokeSafely(MemberUpdated, value);
                    break;
                case MemberLeftEvent<TPayload> value:
                    InvokeSafely(MemberLeft, value);
                    break;
                case TickEvent<TPayload> value:
                    InvokeSafely(Tick, value);
                    break;
                case HeartbeatEvent<TPayload> value:
                    InvokeSafely(Heartbeat, value);
                    break;
                case RoomClosedEvent<TPayload> value:
                    InvokeSafely(RoomClosed, value);
                    break;
                case SyncStatusEvent<TPayload> value:
                    InvokeSafely(SyncStatus, value);
                    break;
                case UnknownEvent<TPayload> value:
                    InvokeSafely(Unknown, value);
                    break;
            }
        }

        private static void InvokeSafely<TEvent>(Action<TEvent>? handlers, TEvent value)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<TEvent>)handler)(value);
                }
                catch
                {
                    // A consumer callback must not terminate the receive loop.
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(RelayConnection<TPayload>));
            }
        }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A non-empty value is required.", parameterName);
            }

            return value.Trim();
        }

        private static string? EmptyToNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
