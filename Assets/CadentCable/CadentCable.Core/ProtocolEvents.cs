#nullable enable

using System.Collections.Generic;

namespace CadentCable.Core
{
    public abstract class RelayEvent<TPayload>
    {
        public abstract string Type { get; }
    }

    public sealed class OpenEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.Open;
    }

    public sealed class CloseEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.Close;
        public int Code { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class ErrorEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.Error;
        public string? Code { get; set; }
        public string? Message { get; set; }
    }

    public sealed class JoinedEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.Joined;
        public double ServerTime { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public string MemberId { get; set; } = string.Empty;
        public string ResumeToken { get; set; } = string.Empty;
        public bool Resumed { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public RoomMode RoomMode { get; set; }
        public MemberRole Role { get; set; }
        public List<MemberInfo> Members { get; set; } = new List<MemberInfo>();
    }

    public sealed class PendingEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.Pending;
        public double ServerTime { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int RequiredApprovals { get; set; }
        public double TimeoutMs { get; set; }
    }

    public sealed class JoinRequestEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.JoinRequest;
        public double ServerTime { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int RequiredApprovals { get; set; }
        public JoinRequestStatus Status { get; set; }
        public int Approvals { get; set; }
        public double ExpiresAt { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class JoinRejectedEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.JoinRejected;
        public double ServerTime { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class MemberJoinedEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.MemberJoined;
        public double ServerTime { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public string MemberId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public List<MemberInfo> Members { get; set; } = new List<MemberInfo>();
    }

    public sealed class MemberUpdatedEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.MemberUpdated;
        public double ServerTime { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public string MemberId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public MemberState State { get; set; }
        public List<MemberInfo> Members { get; set; } = new List<MemberInfo>();
    }

    public sealed class MemberLeftEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.MemberLeft;
        public double ServerTime { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public string MemberId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class TickEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.Tick;
        public double ServerTime { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public long TickSeq { get; set; }
        public List<QueuedMessage<TPayload>> Messages { get; set; } = new List<QueuedMessage<TPayload>>();
    }

    public sealed class HeartbeatEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.Heartbeat;
        public double ServerTime { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public long TickSeq { get; set; }
        public List<MemberInfo> Members { get; set; } = new List<MemberInfo>();
    }

    public sealed class RoomClosedEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.RoomClosed;
        public double ServerTime { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }

    public sealed class SyncResponseEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.SyncResponse;
        public double ClientSendTime { get; set; }
        public double ServerRecvTime { get; set; }
        public double ServerSendTime { get; set; }
    }

    public sealed class SyncStatusEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => EventTypes.SyncStatus;
        public double ServerTime { get; set; }
        public double Rtt { get; set; }
        public double OffsetToServerTime { get; set; }
    }

    public sealed class UnknownEvent<TPayload> : RelayEvent<TPayload>
    {
        public override string Type => UnknownType;
        public string UnknownType { get; set; } = string.Empty;
        public string RawJson { get; set; } = string.Empty;
    }
}
