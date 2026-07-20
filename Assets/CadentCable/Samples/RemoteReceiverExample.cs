#nullable enable

using System.Threading.Tasks;
using CadentCable.Core;
using UnityEngine;

namespace CadentCable.Samples
{
    public sealed class RemoteReceiverExample : MonoBehaviour
    {
        [SerializeField] private SampleRelayConnection? _relay;

        private async void Start()
        {
            if (_relay == null)
            {
                Debug.LogError("SampleRelayConnection is not assigned.");
                return;
            }

            _relay.Tick += OnTick;
            _relay.RoomClosed += e => Debug.Log("Room closed: " + e.Reason);
            _relay.Error += e => Debug.LogError(e.Code + ": " + e.Message);

            try
            {
                CreateRoomResult result = await _relay.CreateRoomAndJoinAsync(
                    new CreateRoomOptions
                    {
                        RoomMode = RoomMode.Remote,
                        ApprovalRatio = 0,
                    });

                Debug.Log("Room ID: " + result.RoomId);
                Debug.Log("Controller WebSocket URL template: " + result.JoinUrl);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private void OnTick(TickEvent<SamplePayload> tick)
        {
            foreach (QueuedMessage<SamplePayload> message in tick.Messages)
            {
                SamplePayload input = message.Payload;
                Debug.Log(
                    message.DisplayName + ": " + input.Action +
                    " pressed=" + input.Pressed +
                    " x=" + input.X +
                    " y=" + input.Y);
            }
        }
    }
}
