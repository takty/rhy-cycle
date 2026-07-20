#nullable enable

using System;
using System.Collections.Generic;

namespace CadentCable.Core
{
    public sealed class CreateRoomOptions
    {
        public string? RoomId { get; set; }
        public RoomMode RoomMode { get; set; } = RoomMode.Broadcast;
        public double ApprovalRatio { get; set; }
    }

    public sealed class CreateRoomResult
    {
        public bool Ok { get; set; }
        public string RoomId { get; set; } = string.Empty;
        public RoomMode RoomMode { get; set; }
        public double ApprovalRatio { get; set; }
        public string OwnerToken { get; set; } = string.Empty;
        public string? JoinUrl { get; set; }
        public string? OwnerJoinUrl { get; set; }
    }

    public sealed class ProtocolCreateRoomResponse
    {
        public bool Ok { get; set; }
        public CreateRoomResult? Result { get; set; }
        public string? Error { get; set; }
    }

    public sealed class MemberInfo
    {
        public string MemberId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public MemberRole Role { get; set; }
        public MemberState State { get; set; }
    }

    public sealed class QueuedMessage<TPayload>
    {
        public string From { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public double? ClientTime { get; set; }
        public double EventTime { get; set; }
        public double ReceivedAt { get; set; }
        public TPayload Payload { get; set; } = default!;
    }

    public sealed class RelayConnectionOptions
    {
        public string ServerUrl { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? OwnerToken { get; set; }
        public string? MemberId { get; set; }
        public string? ResumeToken { get; set; }
        public bool AutoSync { get; set; } = true;
        public int SyncIntervalMs { get; set; } = 3000;
    }

    public sealed class CadentCableRequestException : Exception
    {
        public CadentCableRequestException(
            string message,
            int? statusCode = null,
            string? errorCode = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }

        public int? StatusCode { get; }
        public string? ErrorCode { get; }
    }
}
