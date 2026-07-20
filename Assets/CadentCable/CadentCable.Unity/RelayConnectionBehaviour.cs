#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CadentCable.Abstractions;
using CadentCable.Core;
using CadentCable.Json.Newtonsoft;
using CadentCable.Transport.DotNet;
using UnityEngine;

namespace CadentCable.Unity
{
    public abstract class RelayConnectionBehaviour<TPayload> : MonoBehaviour, IAsyncDisposable
    {
        private const int DefaultReconnectIntervalMs = 500;
        private const int DefaultReconnectDurationMs = 8000;
        private const int QuitGracePeriodMs = 250;

        [Header("Cadent Cable")]
        [SerializeField] private string _serverUrl = string.Empty;
        [SerializeField] private string _displayName = "Unity";
        [SerializeField] private bool _autoSync = true;
        [SerializeField, Min(1)] private int _syncIntervalMs = 3000;

        [Header("Reconnect")]
        [SerializeField] private bool _autoReconnect;

        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
        private readonly object _reconnectGate = new object();

        private CCRuntime? _runtime;
        private IProtocolSerializer? _protocolSerializer;
        private RelayConnection<TPayload>? _connection;
        private CancellationTokenSource? _reconnectCts;
        private Task? _reconnectTask;
        private TaskCompletionSource<bool>? _joinedSignal;
        private volatile bool _intentionalTermination;
        private volatile bool _roomClosed;
        private volatile bool _disposed;
        private bool _applicationQuitting;

        public RelayConnection<TPayload>? Connection => _connection;
        public string ServerUrl => _serverUrl;
        public string DisplayName => _displayName;
        public bool AutoReconnect
        {
            get => _autoReconnect;
            set => _autoReconnect = value;
        }

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

        public async Task<CreateRoomResult> CreateRoomAndJoinAsync(
            CreateRoomOptions? roomOptions = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            EnsureServices();

            CreateRoomResult result = await CadentCableClient.CreateRoomAsync(
                _serverUrl,
                roomOptions,
                _runtime!,
                _protocolSerializer!,
                cancellationToken).ConfigureAwait(false);

            InitializeConnection(new RelayConnectionOptions
            {
                ServerUrl = _serverUrl,
                RoomId = result.RoomId,
                DisplayName = _displayName,
                OwnerToken = result.OwnerToken,
                AutoSync = _autoSync,
                SyncIntervalMs = _syncIntervalMs,
            });

            await JoinAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }

        public async Task ConnectAsync(
            string roomId,
            string? ownerToken = null,
            string? memberId = null,
            string? resumeToken = null,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            InitializeConnection(new RelayConnectionOptions
            {
                ServerUrl = _serverUrl,
                RoomId = roomId,
                DisplayName = _displayName,
                OwnerToken = ownerToken,
                MemberId = memberId,
                ResumeToken = resumeToken,
                AutoSync = _autoSync,
                SyncIntervalMs = _syncIntervalMs,
            });

            await JoinAsync(cancellationToken).ConfigureAwait(false);
        }

        public void InitializeConnection(RelayConnectionOptions options)
        {
            ThrowIfDisposed();
            if (_connection != null)
            {
                throw new InvalidOperationException("RelayConnection has already been initialized.");
            }

            EnsureServices();
            _connection = new RelayConnection<TPayload>(options, _runtime!, _protocolSerializer!);
            _connection.EventReceived += OnCoreEvent;
            _intentionalTermination = false;
            _roomClosed = false;
        }

        public Task JoinAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            RelayConnection<TPayload> connection = RequireConnection();
            _intentionalTermination = false;
            _roomClosed = false;
            return connection.JoinAsync(cancellationToken);
        }

        public Task SendDataAsync(
            TPayload payload,
            CancellationToken cancellationToken = default)
        {
            return RequireConnection().SendDataAsync(payload, cancellationToken);
        }

        public Task ApproveAsync(
            string requestId,
            CancellationToken cancellationToken = default)
        {
            return RequireConnection().ApproveAsync(requestId, cancellationToken);
        }

        public Task SyncOnceAsync(CancellationToken cancellationToken = default)
        {
            return RequireConnection().SyncOnceAsync(cancellationToken);
        }

        public void StartSync()
        {
            RequireConnection().StartSync();
        }

        public void StopSync()
        {
            _connection?.StopSync();
        }

        public async Task LeaveAsync(CancellationToken cancellationToken = default)
        {
            _intentionalTermination = true;
            StopAutoReconnect();
            await RequireConnection().LeaveAsync(cancellationToken).ConfigureAwait(false);
        }

        public async Task CloseRoomAsync(CancellationToken cancellationToken = default)
        {
            _intentionalTermination = true;
            StopAutoReconnect();
            await RequireConnection().CloseRoomAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            RelayConnection<TPayload>? connection = _connection;
            if (connection == null)
            {
                return Task.CompletedTask;
            }

            return connection.OwnerToken != null
                ? CloseRoomForShutdownAsync(connection, cancellationToken)
                : LeaveForShutdownAsync(connection, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopAutoReconnect();

            RelayConnection<TPayload>? connection = _connection;
            _connection = null;
            if (connection != null)
            {
                connection.EventReceived -= OnCoreEvent;
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            CCRuntime? runtime = _runtime;
            _runtime = null;
            if (runtime != null)
            {
                await runtime.DisposeAsync().ConfigureAwait(false);
            }
        }

        protected virtual void Update()
        {
            while (_mainThreadQueue.TryDequeue(out Action? action))
            {
                action();
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _applicationQuitting = true;

            try
            {
                using (CancellationTokenSource cts = new CancellationTokenSource(QuitGracePeriodMs))
                {
                    Task shutdown = ShutdownAsync(cts.Token);
                    shutdown.Wait(QuitGracePeriodMs);
                }
            }
            catch
            {
                // Application shutdown is best-effort only.
            }

            try
            {
                DisposeAsync().AsTask().Wait(QuitGracePeriodMs);
            }
            catch
            {
                // The process is terminating; resource cleanup cannot be guaranteed.
            }
        }

        protected virtual void OnDestroy()
        {
            if (!_applicationQuitting && !_disposed)
            {
                _ = DisposeAsync();
            }
        }

        private async Task CloseRoomForShutdownAsync(
            RelayConnection<TPayload> connection,
            CancellationToken cancellationToken)
        {
            _intentionalTermination = true;
            StopAutoReconnect();
            await connection.CloseRoomAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task LeaveForShutdownAsync(
            RelayConnection<TPayload> connection,
            CancellationToken cancellationToken)
        {
            _intentionalTermination = true;
            StopAutoReconnect();
            await connection.LeaveAsync(cancellationToken).ConfigureAwait(false);
        }

        private void OnCoreEvent(RelayEvent<TPayload> relayEvent)
        {
            if (relayEvent is JoinedEvent<TPayload>)
            {
                TaskCompletionSource<bool>? signal;
                lock (_reconnectGate)
                {
                    signal = _joinedSignal;
                }
                signal?.TrySetResult(true);
            }
            else if (relayEvent is RoomClosedEvent<TPayload>)
            {
                _roomClosed = true;
                StopAutoReconnect();
            }
            else if (relayEvent is CloseEvent<TPayload>)
            {
                BeginAutoReconnectIfNeeded();
            }

            _mainThreadQueue.Enqueue(() => DispatchOnMainThread(relayEvent));
        }

        private void BeginAutoReconnectIfNeeded()
        {
            RelayConnection<TPayload>? connection = _connection;
            if (!_autoReconnect ||
                _intentionalTermination ||
                _roomClosed ||
                _disposed ||
                connection == null ||
                connection.MemberId == null ||
                connection.ResumeToken == null)
            {
                return;
            }

            lock (_reconnectGate)
            {
                if (_reconnectTask != null && !_reconnectTask.IsCompleted)
                {
                    return;
                }

                _reconnectCts = new CancellationTokenSource();
                CancellationTokenSource cts = _reconnectCts;
                _reconnectTask = ReconnectLoopAsync(connection, cts);
            }
        }

        private async Task ReconnectLoopAsync(
            RelayConnection<TPayload> connection,
            CancellationTokenSource ownerCts)
        {
            CancellationToken cancellationToken = ownerCts.Token;
            double deadline = _runtime!.Clock.Now() + DefaultReconnectDurationMs;

            try
            {
                while (!cancellationToken.IsCancellationRequested &&
                       !_intentionalTermination &&
                       !_roomClosed &&
                       _runtime.Clock.Now() < deadline)
                {
                    TaskCompletionSource<bool> joinedSignal = new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    lock (_reconnectGate)
                    {
                        _joinedSignal = joinedSignal;
                    }

                    try
                    {
                        await connection.JoinAsync(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch
                    {
                        // Retry until the fixed reconnect window expires.
                    }

                    double remaining = deadline - _runtime.Clock.Now();
                    if (remaining <= 0)
                    {
                        return;
                    }

                    int delayMs = (int)Math.Min(DefaultReconnectIntervalMs, remaining);
                    Task delay = _runtime.Timer.DelayAsync(
                        TimeSpan.FromMilliseconds(delayMs),
                        cancellationToken);
                    Task completed = await Task.WhenAny(joinedSignal.Task, delay).ConfigureAwait(false);
                    if (completed == joinedSignal.Task && joinedSignal.Task.Result)
                    {
                        return;
                    }

                    await delay.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected when reconnect is canceled by leave, closeRoom, or disposal.
            }
            finally
            {
                lock (_reconnectGate)
                {
                    if (ReferenceEquals(_reconnectCts, ownerCts))
                    {
                        _joinedSignal = null;
                        _reconnectTask = null;
                        _reconnectCts = null;
                    }
                }
                ownerCts.Dispose();
            }
        }

        private void StopAutoReconnect()
        {
            CancellationTokenSource? cts;
            lock (_reconnectGate)
            {
                cts = _reconnectCts;
            }
            cts?.Cancel();
        }

        private void DispatchOnMainThread(RelayEvent<TPayload> relayEvent)
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

        private void EnsureServices()
        {
            if (_runtime == null)
            {
                _runtime = DotNetRuntimeFactory.Create();
            }

            if (_protocolSerializer == null)
            {
                _protocolSerializer = new ProtocolSerializer(new NewtonsoftJsonSerializer());
            }
        }

        private RelayConnection<TPayload> RequireConnection()
        {
            ThrowIfDisposed();
            return _connection ?? throw new InvalidOperationException(
                "RelayConnection has not been initialized.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
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
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
