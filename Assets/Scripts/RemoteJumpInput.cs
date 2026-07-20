using CadentCable.Core;
using UnityEngine;

public sealed class RemoteJumpInput : MonoBehaviour
{
    [SerializeField]
    private FamicomRelayConnection relay;

    private bool previousA;
    private bool jumpRequested;

    private void OnEnable()
    {
        if (relay != null)
        {
            relay.Tick += OnTick;
        }
    }

    private void OnDisable()
    {
        if (relay != null)
        {
            relay.Tick -= OnTick;
        }
    }

    private void OnTick(
        TickEvent<FamicomControllerState> tick)
    {
        foreach (
            QueuedMessage<FamicomControllerState> message
            in tick.Messages)
        {
            FamicomControllerState state =
                message.Payload;

            if (state.A && !previousA)
            {
                jumpRequested = true;
            }

            previousA = state.A;
        }
    }

    public bool ConsumeJumpPressed()
    {
        if (!jumpRequested)
        {
            return false;
        }

        jumpRequested = false;
        return true;
    }
}
