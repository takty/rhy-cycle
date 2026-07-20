# Cadent Cable C# / Unity client

This source bundle implements the current Cadent Cable TypeScript client API in C#.
It targets Unity 6.5 or later on the Windows Editor and Windows desktop builds.
WebGL is intentionally excluded from the initial transport implementation.

## Dependencies

Install Unity's Newtonsoft Json package:

```text
com.unity.nuget.newtonsoft-json
```

The included `CadentCable.Json.Newtonsoft.asmdef` references the assembly
`Unity.Newtonsoft.Json`.

## Layers

- `CadentCable.Abstractions`: clock, timer, HTTP, WebSocket and JSON abstractions.
- `CadentCable.Core`: protocol models, protocol serializer, room creation and `RelayConnection<TPayload>`.
- `CadentCable.Json.Newtonsoft`: Newtonsoft.Json implementation of the JSON abstraction.
- `CadentCable.Transport.DotNet`: `HttpClient` and `ClientWebSocket` transport.
- `CadentCable.Unity`: main-thread event dispatch and optional automatic reconnect.
- `Samples`: a concrete generic component and a remote receiver example.

## TypeScript correspondence

| TypeScript | C# |
| --- | --- |
| `createRoom()` | `CadentCableClient.CreateRoomAsync()` |
| `RelayConnection<TPayload>` | `RelayConnection<TPayload>` |
| `join()` | `JoinAsync()` |
| `leave()` | `LeaveAsync()` |
| `closeRoom()` | `CloseRoomAsync()` |
| `sendData()` | `SendDataAsync()` |
| `approve()` | `ApproveAsync()` |
| `syncOnce()` | `SyncOnceAsync()` |
| `startSync()` | `StartSync()` |
| `stopSync()` | `StopSync()` |
| `setDisplayName()` | `SetDisplayName()` |

`open`, `close`, and `error` remain local `RelayEvent<TPayload>` subclasses, as in
the TypeScript wrapper. `syncResponse` is handled internally and is not emitted to
application code, also matching the TypeScript implementation.

## Core usage

```csharp
CCRuntime runtime = DotNetRuntimeFactory.Create();
IProtocolSerializer protocol = new ProtocolSerializer(
    new NewtonsoftJsonSerializer());

CreateRoomResult room = await CadentCableClient.CreateRoomAsync(
    serverUrl,
    new CreateRoomOptions
    {
        RoomMode = RoomMode.Broadcast,
        ApprovalRatio = 0,
    },
    runtime,
    protocol);

var connection = new RelayConnection<MyPayload>(
    new RelayConnectionOptions
    {
        ServerUrl = serverUrl,
        RoomId = room.RoomId,
        DisplayName = "Unity",
        OwnerToken = room.OwnerToken,
    },
    runtime,
    protocol);

connection.Tick += tick =>
{
    foreach (QueuedMessage<MyPayload> message in tick.Messages)
    {
        // Use message.Payload.
    }
};

await connection.JoinAsync();
```

The owner token works in both modes. In `remote` mode it creates a receiver; in
`broadcast` mode the role remains `member`, but the connection can close the room.

## Unity usage

Unity cannot attach an open generic `MonoBehaviour<TPayload>` directly. Define a
concrete component for each payload type:

```csharp
public sealed class GameRelayConnection
    : RelayConnectionBehaviour<GamePayload>
{
}
```

Add the concrete component to a GameObject. `RelayConnectionBehaviour<TPayload>`
forwards all Core events through `Update()`, so Unity objects may be accessed safely
from its event handlers.

Automatic reconnect is disabled by default. When enabled, an unexpected disconnect
is retried every 500 ms for at most 8 seconds. Explicit `LeaveAsync()`,
`CloseRoomAsync()`, `roomClosed`, and disposal suppress reconnect.

## Shutdown semantics

- `ShutdownAsync()` sends `closeRoom` when the connection has an owner token.
- Otherwise it sends `leave`.
- `DisposeAsync()` only releases communication resources. It intentionally sends
  neither `leave` nor `closeRoom`, preserving the distinction between a temporary
  disconnect and an explicit departure.
- `OnApplicationQuit()` makes a short best-effort shutdown attempt; delivery cannot
  be guaranteed when the process is already terminating.
