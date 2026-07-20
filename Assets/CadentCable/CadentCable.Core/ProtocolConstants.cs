#nullable enable

namespace CadentCable.Core
{
    public static class Routes
    {
        public const string WebSocket = "ws";
        public const string Rooms = "rooms";
        public const string Health = "health";
    }

    public static class EventTypes
    {
        public const string Open = "open";
        public const string Close = "close";
        public const string Error = "error";

        public const string Data = "data";
        public const string Approve = "approve";
        public const string Leave = "leave";
        public const string CloseRoom = "closeRoom";

        public const string SyncRequest = "syncRequest";
        public const string SyncResponse = "syncResponse";
        public const string SyncReport = "syncReport";
        public const string SyncStatus = "syncStatus";

        public const string Joined = "joined";
        public const string Pending = "pending";
        public const string JoinRequest = "joinRequest";
        public const string JoinRejected = "joinRejected";
        public const string MemberJoined = "memberJoined";
        public const string MemberUpdated = "memberUpdated";
        public const string MemberLeft = "memberLeft";
        public const string Tick = "tick";
        public const string Heartbeat = "heartbeat";
        public const string RoomClosed = "roomClosed";
    }

    public static class RoomClosedReasons
    {
        public const string EmptyTimeout = "empty_timeout";
        public const string OwnerClosed = "owner_closed";
        public const string ReceiverTimeout = "receiver_timeout";
    }

    public enum RoomMode
    {
        Broadcast,
        Remote,
    }

    public enum MemberRole
    {
        Member,
        Receiver,
        Controller,
    }

    public enum MemberState
    {
        Connected,
        Disconnected,
    }

    public enum JoinRequestStatus
    {
        Created,
        Updated,
        Expired,
        Canceled,
    }
}
