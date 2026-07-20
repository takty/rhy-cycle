using System;
using System.Collections.Generic;
using CadentCable.Core;
using UnityEngine;

public sealed class RemotePlayerInput : MonoBehaviour
{
    [SerializeField]
    private FamicomRelayConnection relay;

    private readonly Dictionary<string, bool> previousA = new();

    public event Action<string> APressed;

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

    private void OnTick(TickEvent<FamicomControllerState> tick)
    {
        foreach (QueuedMessage<FamicomControllerState> message in tick.Messages)
        {
            string memberId = message.From;
            bool currentA = message.Payload.A;

            previousA.TryGetValue(memberId, out bool wasPressed);

            if (currentA && !wasPressed)
            {
                APressed?.Invoke(memberId);
            }

            previousA[memberId] = currentA;
        }
    }

    public void RemovePlayer(string memberId)
    {
        previousA.Remove(memberId);
    }
}
