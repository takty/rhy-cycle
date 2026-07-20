using System.Collections.Generic;
using CadentCable.Core;
using UnityEngine;
using System.Collections;

public sealed class RhyCycleGameManager : MonoBehaviour
{
    [SerializeField]
    private CourseController course;
    [SerializeField]
    private PlayerStack playerStack;
    [SerializeField]
    private float jumpInputWindow = 0.15f;

    [SerializeField]
    [Range(0.0f, 1.0f)]
    private float minimumSynchronization = 0.4f;

    private readonly HashSet<string> jumpInputs =
        new HashSet<string>();

    private Coroutine jumpInputCoroutine;

    private enum PlayerState
    {
        Waiting,
        Alive,
        Eliminated,
    }

    private sealed class PlayerEntry
    {
        public string MemberId;
        public string DisplayName;
        public PlayerState State;
    }

    [SerializeField]
    private FamicomRelayConnection relay;

    [SerializeField]
    private RemotePlayerInput remoteInput;

    private readonly Dictionary<string, PlayerEntry> players = new();

    private bool isGameRunning;

    private void OnEnable()
    {
        if (relay != null)
        {
            relay.EventReceived += OnRelayEvent;
        }

        if (remoteInput != null)
        {
            remoteInput.APressed += OnAPressed;
        }

        if (course != null)
        {
            course.MeasureStarted += OnMeasureStarted;
        }
    }

    private void OnDisable()
    {
        if (relay != null)
        {
            relay.EventReceived -= OnRelayEvent;
        }

        if (remoteInput != null)
        {
            remoteInput.APressed -= OnAPressed;
        }

        if (course != null)
        {
            course.MeasureStarted -= OnMeasureStarted;
        }
    }

    private void OnRelayEvent(
        RelayEvent<FamicomControllerState> relayEvent)
    {
        switch (relayEvent)
        {
            case MemberJoinedEvent<FamicomControllerState> joined:
                AddPlayer(joined.MemberId, joined.DisplayName);
                break;

            case MemberLeftEvent<FamicomControllerState> left:
                RemovePlayer(left.MemberId);
                break;
        }
    }

    private void AddPlayer(
        string memberId,
        string displayName)
    {
        if (players.ContainsKey(memberId))
        {
            return;
        }

        bool joinImmediately = !isGameRunning;

        PlayerEntry player = new PlayerEntry
        {
            MemberId = memberId,
            DisplayName = displayName,
            State = joinImmediately
                ? PlayerState.Alive
                : PlayerState.Waiting,
        };

        players.Add(memberId, player);

        Debug.Log(
            $"Player joined: {displayName} ({memberId}), " +
            $"state={player.State}"
        );

        if (joinImmediately)
        {
            playerStack.AddPlayer(
                memberId,
                displayName
            );

            StartGame();
        }
    }

    private void RemovePlayer(string memberId)
    {
        if (!players.Remove(memberId))
        {
            return;
        }

        playerStack.RemovePlayer(memberId);
        remoteInput.RemovePlayer(memberId);

        Debug.Log($"Player left: {memberId}");
    }

    private void OnAPressed(string memberId)
    {
        if (!players.TryGetValue(
            memberId,
            out PlayerEntry player))
        {
            return;
        }

        switch (player.State)
        {
            case PlayerState.Alive:
                RegisterJumpInput(memberId);
                break;

            case PlayerState.Eliminated:
                Debug.Log($"Reentry requested: {memberId}");
                break;

            case PlayerState.Waiting:
                Debug.Log($"Waiting player pressed A: {memberId}");
                break;
        }
    }

    private void StartGame()
    {
        isGameRunning = true;
        course.StartCourse();

        Debug.Log("Game started.");
    }

    private void OnMeasureStarted(int measure)
    {
        foreach (PlayerEntry player in players.Values)
        {
            if (player.State != PlayerState.Waiting)
            {
                continue;
            }

            player.State = PlayerState.Alive;

            playerStack.AddPlayer(
                player.MemberId,
                player.DisplayName
            );

            Debug.Log(
                $"Player entered at measure start: " +
                $"{player.MemberId}"
            );
        }
    }

    private void RegisterJumpInput(string memberId)
    {
        if (!playerStack.IsGrounded)
        {
            return;
        }

        jumpInputs.Add(memberId);

        if (jumpInputCoroutine == null)
        {
            jumpInputCoroutine =
                StartCoroutine(EvaluateJumpInputs());
        }
    }

    private IEnumerator EvaluateJumpInputs()
    {
        yield return new WaitForSecondsRealtime(
            jumpInputWindow
        );

        int aliveCount = CountAlivePlayers();

        if (aliveCount > 0)
        {
            float synchronization =
                (float)jumpInputs.Count / aliveCount;

            Debug.Log(
                $"Jump synchronization: " +
                $"{jumpInputs.Count}/{aliveCount} " +
                $"({synchronization:P0})"
            );

            if (synchronization >= minimumSynchronization)
            {
                playerStack.TryJump(synchronization);
            }
        }

        jumpInputs.Clear();
        jumpInputCoroutine = null;
    }

    private int CountAlivePlayers()
    {
        int count = 0;

        foreach (PlayerEntry player in players.Values)
        {
            if (player.State == PlayerState.Alive)
            {
                count++;
            }
        }

        return count;
    }
}
