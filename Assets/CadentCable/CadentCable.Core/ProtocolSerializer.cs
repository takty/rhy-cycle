#nullable enable

using System;
using System.Collections.Generic;
using CadentCable.Abstractions;

namespace CadentCable.Core
{
    public interface IProtocolSerializer
    {
        string SerializeCreateRoomOptions(CreateRoomOptions options);
        ProtocolCreateRoomResponse DeserializeCreateRoomResponse(string json);

        string SerializeData<TPayload>(double clientTime, TPayload payload);
        string SerializeApprove(string requestId);
        string SerializeLeave();
        string SerializeCloseRoom();
        string SerializeSyncRequest(double clientSendTime);
        string SerializeSyncReport(
            double clientSendTime,
            double serverRecvTime,
            double serverSendTime,
            double clientRecvTime);

        RelayEvent<TPayload> DeserializeEvent<TPayload>(string json);
    }

    public sealed class ProtocolSerializer : IProtocolSerializer
    {
        private readonly IJsonSerializer _json;

        public ProtocolSerializer(IJsonSerializer json)
        {
            _json = json ?? throw new ArgumentNullException(nameof(json));
        }

        public string SerializeCreateRoomOptions(CreateRoomOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return _json.Serialize(options);
        }

        public ProtocolCreateRoomResponse DeserializeCreateRoomResponse(string json)
        {
            ICCJsonObject obj = _json.ParseObject(json);
            Require(obj, "ok", CCJsonValueKind.Boolean);

            OkEnvelope envelope = obj.ToObject<OkEnvelope>();
            if (!envelope.Ok)
            {
                if (obj.HasProperty("error"))
                {
                    Require(obj, "error", CCJsonValueKind.String);
                }

                ErrorEnvelope error = obj.ToObject<ErrorEnvelope>();
                return new ProtocolCreateRoomResponse
                {
                    Ok = false,
                    Error = error.Error,
                };
            }

            Require(obj, "roomId", CCJsonValueKind.String);
            RequireEnum(obj, "roomMode", "broadcast", "remote");
            Require(obj, "approvalRatio", CCJsonValueKind.Number);
            Require(obj, "ownerToken", CCJsonValueKind.String);
            Optional(obj, "joinUrl", CCJsonValueKind.String, allowNull: true);
            Optional(obj, "ownerJoinUrl", CCJsonValueKind.String, allowNull: true);

            CreateRoomResult result = obj.ToObject<CreateRoomResult>();
            return new ProtocolCreateRoomResponse
            {
                Ok = true,
                Result = result,
            };
        }

        public string SerializeData<TPayload>(double clientTime, TPayload payload)
        {
            return _json.Serialize(new DataMessage<TPayload>
            {
                Type = EventTypes.Data,
                ClientTime = clientTime,
                Payload = payload,
            });
        }

        public string SerializeApprove(string requestId)
        {
            return _json.Serialize(new ApproveMessage
            {
                Type = EventTypes.Approve,
                RequestId = requestId,
            });
        }

        public string SerializeLeave()
        {
            return _json.Serialize(new TypeOnlyMessage { Type = EventTypes.Leave });
        }

        public string SerializeCloseRoom()
        {
            return _json.Serialize(new TypeOnlyMessage { Type = EventTypes.CloseRoom });
        }

        public string SerializeSyncRequest(double clientSendTime)
        {
            return _json.Serialize(new SyncRequestMessage
            {
                Type = EventTypes.SyncRequest,
                ClientSendTime = clientSendTime,
            });
        }

        public string SerializeSyncReport(
            double clientSendTime,
            double serverRecvTime,
            double serverSendTime,
            double clientRecvTime)
        {
            return _json.Serialize(new SyncReportMessage
            {
                Type = EventTypes.SyncReport,
                ClientSendTime = clientSendTime,
                ServerRecvTime = serverRecvTime,
                ServerSendTime = serverSendTime,
                ClientRecvTime = clientRecvTime,
            });
        }

        public RelayEvent<TPayload> DeserializeEvent<TPayload>(string json)
        {
            ICCJsonObject obj;
            try
            {
                obj = _json.ParseObject(json);
            }
            catch (CCJsonException ex)
            {
                return MakeError<TPayload>(ex.Code, ex.Message);
            }
            catch (Exception ex)
            {
                return MakeError<TPayload>("invalid_json", ex.Message);
            }

            if (!obj.TryGetString("type", out string type) || string.IsNullOrEmpty(type))
            {
                return MakeError<TPayload>("invalid_message", "Message must contain a string property named 'type'.");
            }

            try
            {
                switch (type)
                {
                    case EventTypes.Joined:
                        ValidateJoined(obj);
                        return obj.ToObject<JoinedEvent<TPayload>>();

                    case EventTypes.Pending:
                        ValidatePending(obj);
                        return obj.ToObject<PendingEvent<TPayload>>();

                    case EventTypes.JoinRequest:
                        ValidateJoinRequest(obj);
                        return obj.ToObject<JoinRequestEvent<TPayload>>();

                    case EventTypes.JoinRejected:
                        ValidateJoinRejected(obj);
                        return obj.ToObject<JoinRejectedEvent<TPayload>>();

                    case EventTypes.MemberJoined:
                        ValidateMemberJoined(obj);
                        return obj.ToObject<MemberJoinedEvent<TPayload>>();

                    case EventTypes.MemberUpdated:
                        ValidateMemberUpdated(obj);
                        return obj.ToObject<MemberUpdatedEvent<TPayload>>();

                    case EventTypes.MemberLeft:
                        ValidateMemberLeft(obj);
                        return obj.ToObject<MemberLeftEvent<TPayload>>();

                    case EventTypes.Tick:
                        ValidateTick(obj);
                        return obj.ToObject<TickEvent<TPayload>>();

                    case EventTypes.Heartbeat:
                        ValidateHeartbeat(obj);
                        return obj.ToObject<HeartbeatEvent<TPayload>>();

                    case EventTypes.RoomClosed:
                        ValidateRoomClosed(obj);
                        return obj.ToObject<RoomClosedEvent<TPayload>>();

                    case EventTypes.SyncResponse:
                        ValidateSyncResponse(obj);
                        return obj.ToObject<SyncResponseEvent<TPayload>>();

                    case EventTypes.SyncStatus:
                        ValidateSyncStatus(obj);
                        return obj.ToObject<SyncStatusEvent<TPayload>>();

                    case EventTypes.Error:
                        ValidateError(obj);
                        return obj.ToObject<ErrorEvent<TPayload>>();

                    default:
                        return new UnknownEvent<TPayload>
                        {
                            UnknownType = type,
                            RawJson = obj.RawJson,
                        };
                }
            }
            catch (CCJsonException ex)
            {
                return MakeError<TPayload>(ex.Code, ex.Message);
            }
            catch (Exception ex)
            {
                return MakeError<TPayload>("invalid_message", ex.Message);
            }
        }

        private static void ValidateJoined(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "roomId", CCJsonValueKind.String);
            Require(obj, "memberId", CCJsonValueKind.String);
            Require(obj, "resumeToken", CCJsonValueKind.String);
            Require(obj, "resumed", CCJsonValueKind.Boolean);
            Require(obj, "displayName", CCJsonValueKind.String);
            RequireEnum(obj, "roomMode", "broadcast", "remote");
            RequireEnum(obj, "role", "member", "receiver", "controller");
            ValidateMembers(obj, "members");
        }

        private static void ValidatePending(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "roomId", CCJsonValueKind.String);
            Require(obj, "requestId", CCJsonValueKind.String);
            Require(obj, "displayName", CCJsonValueKind.String);
            Require(obj, "requiredApprovals", CCJsonValueKind.Number);
            Require(obj, "timeoutMs", CCJsonValueKind.Number);
        }

        private static void ValidateJoinRequest(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "roomId", CCJsonValueKind.String);
            Require(obj, "requestId", CCJsonValueKind.String);
            Require(obj, "displayName", CCJsonValueKind.String);
            Require(obj, "requiredApprovals", CCJsonValueKind.Number);
            RequireEnum(obj, "status", "created", "updated", "expired", "canceled");
            Require(obj, "approvals", CCJsonValueKind.Number);
            Require(obj, "expiresAt", CCJsonValueKind.Number);
            Optional(obj, "reason", CCJsonValueKind.String, allowNull: false);
        }

        private static void ValidateJoinRejected(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "roomId", CCJsonValueKind.String);
            Require(obj, "requestId", CCJsonValueKind.String);
            Require(obj, "reason", CCJsonValueKind.String);
        }

        private static void ValidateMemberJoined(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "roomId", CCJsonValueKind.String);
            Require(obj, "memberId", CCJsonValueKind.String);
            Require(obj, "displayName", CCJsonValueKind.String);
            ValidateMembers(obj, "members");
        }

        private static void ValidateMemberUpdated(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "roomId", CCJsonValueKind.String);
            Require(obj, "memberId", CCJsonValueKind.String);
            Require(obj, "displayName", CCJsonValueKind.String);
            RequireEnum(obj, "state", "connected", "disconnected");
            ValidateMembers(obj, "members");
        }

        private static void ValidateMemberLeft(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "roomId", CCJsonValueKind.String);
            Require(obj, "memberId", CCJsonValueKind.String);
            Require(obj, "displayName", CCJsonValueKind.String);
        }

        private static void ValidateTick(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "roomId", CCJsonValueKind.String);
            Require(obj, "tickSeq", CCJsonValueKind.Number);
            Require(obj, "messages", CCJsonValueKind.Array);

            IReadOnlyList<ICCJsonObject> messages = obj.GetObjectArray("messages");
            foreach (ICCJsonObject message in messages)
            {
                Require(message, "from", CCJsonValueKind.String);
                Require(message, "displayName", CCJsonValueKind.String);
                Optional(message, "clientTime", CCJsonValueKind.Number, allowNull: false);
                Require(message, "eventTime", CCJsonValueKind.Number);
                Require(message, "receivedAt", CCJsonValueKind.Number);
                if (!message.HasProperty("payload"))
                {
                    throw InvalidMessage("Property 'payload' is required in every queued message.");
                }
            }
        }

        private static void ValidateHeartbeat(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "roomId", CCJsonValueKind.String);
            Require(obj, "tickSeq", CCJsonValueKind.Number);
            ValidateMembers(obj, "members");
        }

        private static void ValidateRoomClosed(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "roomId", CCJsonValueKind.String);
            Require(obj, "reason", CCJsonValueKind.String);
        }

        private static void ValidateSyncResponse(ICCJsonObject obj)
        {
            Require(obj, "clientSendTime", CCJsonValueKind.Number);
            Require(obj, "serverRecvTime", CCJsonValueKind.Number);
            Require(obj, "serverSendTime", CCJsonValueKind.Number);
        }

        private static void ValidateSyncStatus(ICCJsonObject obj)
        {
            Require(obj, "serverTime", CCJsonValueKind.Number);
            Require(obj, "rtt", CCJsonValueKind.Number);
            Require(obj, "offsetToServerTime", CCJsonValueKind.Number);
        }

        private static void ValidateError(ICCJsonObject obj)
        {
            Optional(obj, "code", CCJsonValueKind.String, allowNull: false);
            Optional(obj, "message", CCJsonValueKind.String, allowNull: false);
        }

        private static void ValidateMembers(ICCJsonObject obj, string propertyName)
        {
            Require(obj, propertyName, CCJsonValueKind.Array);
            IReadOnlyList<ICCJsonObject> members = obj.GetObjectArray(propertyName);
            foreach (ICCJsonObject member in members)
            {
                Require(member, "memberId", CCJsonValueKind.String);
                Require(member, "displayName", CCJsonValueKind.String);
                RequireEnum(member, "role", "member", "receiver", "controller");
                RequireEnum(member, "state", "connected", "disconnected");
            }
        }

        private static void Require(ICCJsonObject obj, string propertyName, CCJsonValueKind expectedKind)
        {
            if (!obj.HasProperty(propertyName))
            {
                throw InvalidMessage("Required property '" + propertyName + "' is missing.");
            }

            CCJsonValueKind actualKind = obj.GetValueKind(propertyName);
            if (actualKind != expectedKind)
            {
                throw InvalidMessage(
                    "Property '" + propertyName + "' must be " + expectedKind + ", but was " + actualKind + ".");
            }
        }

        private static void Optional(
            ICCJsonObject obj,
            string propertyName,
            CCJsonValueKind expectedKind,
            bool allowNull)
        {
            if (!obj.HasProperty(propertyName))
            {
                return;
            }

            CCJsonValueKind actualKind = obj.GetValueKind(propertyName);
            if (allowNull && actualKind == CCJsonValueKind.Null)
            {
                return;
            }

            if (actualKind != expectedKind)
            {
                throw InvalidMessage(
                    "Property '" + propertyName + "' must be " + expectedKind + ", but was " + actualKind + ".");
            }
        }

        private static void RequireEnum(ICCJsonObject obj, string propertyName, params string[] allowedValues)
        {
            Require(obj, propertyName, CCJsonValueKind.String);
            if (!obj.TryGetString(propertyName, out string value))
            {
                throw InvalidMessage("Property '" + propertyName + "' must be a string.");
            }

            foreach (string allowedValue in allowedValues)
            {
                if (string.Equals(value, allowedValue, StringComparison.Ordinal))
                {
                    return;
                }
            }

            throw InvalidMessage("Property '" + propertyName + "' contains an unknown value: " + value + ".");
        }

        private static CCJsonException InvalidMessage(string message)
        {
            return new CCJsonException("invalid_message", message);
        }

        private static ErrorEvent<TPayload> MakeError<TPayload>(string code, string message)
        {
            return new ErrorEvent<TPayload>
            {
                Code = code,
                Message = message,
            };
        }

        private sealed class OkEnvelope
        {
            public bool Ok { get; set; }
        }

        private sealed class ErrorEnvelope
        {
            public string? Error { get; set; }
        }

        private sealed class TypeOnlyMessage
        {
            public string Type { get; set; } = string.Empty;
        }

        private sealed class DataMessage<TPayload>
        {
            public string Type { get; set; } = string.Empty;
            public double ClientTime { get; set; }
            public TPayload Payload { get; set; } = default!;
        }

        private sealed class ApproveMessage
        {
            public string Type { get; set; } = string.Empty;
            public string RequestId { get; set; } = string.Empty;
        }

        private sealed class SyncRequestMessage
        {
            public string Type { get; set; } = string.Empty;
            public double ClientSendTime { get; set; }
        }

        private sealed class SyncReportMessage
        {
            public string Type { get; set; } = string.Empty;
            public double ClientSendTime { get; set; }
            public double ServerRecvTime { get; set; }
            public double ServerSendTime { get; set; }
            public double ClientRecvTime { get; set; }
        }
    }
}
