using CadentCable.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public sealed class RhyCycleGameManager : MonoBehaviour
{
    [SerializeField]
    private CourseController course;
    [SerializeField]
    private PlayerStack playerStack;

    [SerializeField]
    private GameAudio gameAudio;

    [SerializeField]
    private CoursePulse coursePulse;

    [SerializeField]
    private GameScore gameScore;

    [SerializeField]
    private GameHighScores gameHighScores;

    [SerializeField]
    private float jumpInputWindow = 0.15f;

    [SerializeField]
    private GameHud gameHud;

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
    private bool restartAtNextMeasure;
    private int lastFinalScore;
    private int lastMaxAliveCount;
    private bool lastGameEnteredHighScores;

    public bool IsGameRunning =>
        isGameRunning;

    public bool IsRestartPending =>
        restartAtNextMeasure;

    public int ConnectedPlayerCount =>
        players.Count;

    public int AlivePlayerCount =>
        CountAlivePlayers();

    public int WaitingPlayerCount =>
        CountWaitingPlayers();

    public int EliminatedPlayerCount =>
        CountEliminatedPlayers();

    public int LastFinalScore =>
        lastFinalScore;

    public int LastMaxAliveCount =>
        lastMaxAliveCount;

    public bool LastGameEnteredHighScores =>
        lastGameEnteredHighScores;

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
            course.MeasureStarted +=
                OnMeasureStarted;

            course.BeatStarted +=
                OnBeatStarted;
        }

        if (playerStack != null)
        {
            playerStack.PlayerHitObstacle +=
                OnPlayerHitObstacle;
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
            course.MeasureStarted -=
                OnMeasureStarted;

            course.BeatStarted -=
                OnBeatStarted;
        }

        if (playerStack != null)
        {
            playerStack.PlayerHitObstacle -=
                OnPlayerHitObstacle;
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

        bool joinImmediately =
            !isGameRunning &&
            !restartAtNextMeasure;

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

        if (isGameRunning &&
            gameScore != null)
        {
            gameScore.SetAliveCount(
                CountAlivePlayers()
            );
        }

        Debug.Log($"Player left: {memberId}");

        CheckForGameEnd();

        if (!isGameRunning &&
            restartAtNextMeasure &&
            CountWaitingPlayers() == 0)
        {
            restartAtNextMeasure = false;
            course.StopCourse();

            Debug.Log(
                "No waiting players remain. " +
                "Course stopped."
            );
        }
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
                RequestReentry(player);
                break;

            case PlayerState.Waiting:
                Debug.Log(
                    $"Player is already waiting to enter: " +
                    $"{player.DisplayName} ({memberId})"
                );
                break;
        }
    }

    private void RequestReentry(
    PlayerEntry player)
    {
        if (isGameRunning ||
            restartAtNextMeasure)
        {
            player.State = PlayerState.Waiting;

            Debug.Log(
                $"Reentry requested: " +
                $"{player.DisplayName} " +
                $"({player.MemberId})"
            );

            return;
        }

        player.State = PlayerState.Alive;

        playerStack.AddPlayer(
        player.MemberId,
        player.DisplayName
            );

        StartGame();

        Debug.Log(
            $"Player reentered and started a new game: " +
            $"{player.DisplayName} " +
            $"({player.MemberId})"
        );
    }

    private void StartGame()
    {
        isGameRunning = true;
        restartAtNextMeasure = false;

        lastFinalScore = 0;
        lastMaxAliveCount = 0;
        lastGameEnteredHighScores = false;

        if (gameScore != null)
        {
            gameScore.StartGame(
                CountAlivePlayers()
            );
        }

        course.StartCourse();

        Debug.Log("Game started.");
    }

    private void OnMeasureStarted(int measure)
    {
        if (coursePulse != null)
        {
            coursePulse.Play();
        }

        if (!isGameRunning &&
            !restartAtNextMeasure)
        {
            return;
        }

        int enteredCount =
            ActivateWaitingPlayers();

        if (!isGameRunning &&
            restartAtNextMeasure &&
            enteredCount > 0)
        {
            isGameRunning = true;
            restartAtNextMeasure = false;

            if (gameScore != null)
            {
                gameScore.StartGame(
                    CountAlivePlayers()
                );
            }
            Debug.Log(
                $"Next game started at measure " +
                $"{measure + 1}."
            );

            return;
        }
        if (isGameRunning &&
            enteredCount > 0 &&
            gameScore != null)
        {
            gameScore.SetAliveCount(
                CountAlivePlayers()
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
                bool jumped =
                    playerStack.TryJump(
                        synchronization
                    );

                if (jumped &&
                    gameAudio != null)
                {
                    gameAudio.PlayJump(
                        synchronization
                    );
                }
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

    private void OnPlayerHitObstacle(
        string memberId)
    {
        if (!players.TryGetValue(
            memberId,
            out PlayerEntry player))
        {
            return;
        }

        if (player.State != PlayerState.Alive)
        {
            return;
        }

        EliminatePlayer(player);
    }

    private void EliminatePlayer(
        PlayerEntry player)
    {
        player.State =
            PlayerState.Eliminated;

        jumpInputs.Remove(
            player.MemberId
        );

        playerStack.EliminatePlayer(
            player.MemberId
        );

        if (gameAudio != null)
        {
            gameAudio.PlayElimination();
        }

        if (gameScore != null)
        {
            gameScore.SetAliveCount(
                CountAlivePlayers()
            );
        }

        Debug.Log(
            $"Player eliminated: " +
            $"{player.DisplayName} " +
            $"({player.MemberId}), " +
            $"alive={CountAlivePlayers()}"
        );

        CheckForGameEnd();
    }

    private int ActivateWaitingPlayers()
    {
        int enteredCount = 0;

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

            enteredCount++;

            Debug.Log(
                $"Player entered at measure start: " +
                $"{player.MemberId}"
            );
        }

        return enteredCount;
    }

    private void CheckForGameEnd()
    {
        if (!isGameRunning)
        {
            return;
        }

        if (CountAlivePlayers() > 0)
        {
            return;
        }

        int finalScore = 0;
        int maxAliveCount = 0;

        if (gameScore != null)
        {
            gameScore.EndGame();
            finalScore = gameScore.CurrentScore;
            maxAliveCount = gameScore.MaxAliveCount;
        }

        bool enteredHighScores = false;

        if (gameHighScores != null &&
            maxAliveCount > 0)
        {
            enteredHighScores =
                gameHighScores.AddScore(
                    finalScore,
                    maxAliveCount
                );
        }

        lastFinalScore = finalScore;
        lastMaxAliveCount = maxAliveCount;
        lastGameEnteredHighScores =
            enteredHighScores;

        if (gameHud != null)
        {
            gameHud.UpdateHighScores();
        }

        isGameRunning = false;

        if (jumpInputCoroutine != null)
        {
            StopCoroutine(jumpInputCoroutine);
            jumpInputCoroutine = null;
        }

        jumpInputs.Clear();

        int waitingCount =
            CountWaitingPlayers();

        restartAtNextMeasure =
            waitingCount > 0;

        if (!restartAtNextMeasure)
        {
            course.StopCourse();
        }

        Debug.Log(
            $"Game ended. " +
            $"score={finalScore}, " +
            $"maxAlive={maxAliveCount}, " +
            $"highScore={enteredHighScores}, " +
            $"waiting={waitingCount}, " +
            $"restartAtNextMeasure=" +
            $"{restartAtNextMeasure}"
        );
    }

    private int CountWaitingPlayers()
    {
        int count = 0;

        foreach (PlayerEntry player in players.Values)
        {
            if (player.State == PlayerState.Waiting)
            {
                count++;
            }
        }

        return count;
    }

    private int CountEliminatedPlayers()
    {
        int count = 0;

        foreach (PlayerEntry player in players.Values)
        {
            if (player.State ==
                PlayerState.Eliminated)
            {
                count++;
            }
        }

        return count;
    }

    private void OnBeatStarted(
        int measure,
        int beatInMeasure)
    {
        if (gameAudio != null)
        {
            gameAudio.PlayMeasure();
        }
    }
}
