using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public sealed class GameHud : MonoBehaviour
{
    [SerializeField]
    private RhyCycleGameManager gameManager;

    [SerializeField]
    private GameScore gameScore;

    [SerializeField]
    private GameHighScores gameHighScores;

    [SerializeField]
    private TMP_Text currentScoreText;

    [SerializeField]
    private TMP_Text playerCountText;

    [SerializeField]
    private TMP_Text highScoresText;

    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private float startMessageDuration = 1.0f;

    private bool previousGameRunning;
    private float startMessageEndTime;

    private int previousScore = -1;
    private int previousAliveCount = -1;
    private int previousWaitingCount = -1;

    private void Start()
    {
        previousGameRunning =
            gameManager != null &&
            gameManager.IsGameRunning;

        if (previousGameRunning)
        {
            startMessageEndTime =
                Time.unscaledTime +
                startMessageDuration;
        }

        UpdateHighScores();
        UpdatePlayerCounts();
        UpdateCurrentScore();
        UpdateStatus();
    }

    private void Update()
    {
        UpdateCurrentScore();
        UpdatePlayerCounts();
        UpdateStatus();
    }

    public void UpdateHighScores()
    {
        if (highScoresText == null ||
            gameHighScores == null)
        {
            return;
        }

        StringBuilder builder =
            new StringBuilder();

        builder.AppendLine("HIGH SCORES");

        IReadOnlyList<GameHighScores.Entry> entries =
            gameHighScores.Entries;

        for (int i = 0; i < 5; i++)
        {
            if (i < entries.Count)
            {
                GameHighScores.Entry entry =
                    entries[i];

                builder.Append(
                    $"{i + 1}. " +
                    $"{entry.Score:N0}  " +
                    $"MAX {entry.MaxAliveCount}"
                );
            }
            else
            {
                builder.Append(
                    $"{i + 1}. ---"
                );
            }

            if (i < 4)
            {
                builder.AppendLine();
            }
        }

        highScoresText.text =
            builder.ToString();
    }

    private void UpdateCurrentScore()
    {
        if (currentScoreText == null ||
            gameScore == null)
        {
            return;
        }

        int currentScore =
            gameScore.CurrentScore;

        if (currentScore == previousScore)
        {
            return;
        }

        previousScore = currentScore;

        currentScoreText.text =
            $"SCORE  {currentScore:N0}";
    }

    private void UpdatePlayerCounts()
    {
        if (playerCountText == null ||
            gameManager == null)
        {
            return;
        }

        int aliveCount =
            gameManager.AlivePlayerCount;

        int waitingCount =
            gameManager.WaitingPlayerCount;

        if (aliveCount == previousAliveCount &&
            waitingCount == previousWaitingCount)
        {
            return;
        }

        previousAliveCount = aliveCount;
        previousWaitingCount = waitingCount;

        if (waitingCount > 0)
        {
            playerCountText.text =
                $"ALIVE  {aliveCount}    " +
                $"WAITING  {waitingCount}";
        }
        else
        {
            playerCountText.text =
                $"ALIVE  {aliveCount}";
        }
    }

    private void UpdateStatus()
    {
        if (statusText == null ||
            gameManager == null)
        {
            return;
        }

        bool isGameRunning =
            gameManager.IsGameRunning;

        if (isGameRunning &&
            !previousGameRunning)
        {
            startMessageEndTime =
                Time.unscaledTime +
                startMessageDuration;
        }

        previousGameRunning =
            isGameRunning;

        if (isGameRunning &&
            Time.unscaledTime <
            startMessageEndTime)
        {
            statusText.text = "START!";
            return;
        }

        int connectedCount =
            gameManager.ConnectedPlayerCount;

        int waitingCount =
            gameManager.WaitingPlayerCount;

        int eliminatedCount =
            gameManager.EliminatedPlayerCount;

        if (connectedCount == 0)
        {
            statusText.text =
                "SCAN THE QR CODE TO JOIN";
            return;
        }

        if (isGameRunning)
        {
            StringBuilder builder =
                new StringBuilder("PLAYING");

            if (waitingCount > 0)
            {
                builder.AppendLine();
                builder.Append(
                    waitingCount == 1
                        ? "1 PLAYER JOINS NEXT MEASURE"
                        : $"{waitingCount} PLAYERS JOIN NEXT MEASURE"
                );
            }

            if (eliminatedCount > 0)
            {
                builder.AppendLine();
                builder.Append(
                    "PRESS A TO REJOIN"
                );
            }

            statusText.text =
                builder.ToString();

            return;
        }

        if (gameManager.IsRestartPending)
        {
            statusText.text =
                "GAME OVER\n" +
                "NEW GAME STARTS NEXT MEASURE";

            return;
        }

        StringBuilder gameOverBuilder =
            new StringBuilder();

        gameOverBuilder.AppendLine(
            "GAME OVER"
        );

        gameOverBuilder.AppendLine(
            $"SCORE  " +
            $"{gameManager.LastFinalScore:N0}"
        );

        if (gameManager
            .LastGameEnteredHighScores)
        {
            gameOverBuilder.AppendLine(
                "NEW HIGH SCORE!"
            );
        }

        gameOverBuilder.Append(
            "PRESS A TO REJOIN"
        );

        statusText.text =
            gameOverBuilder.ToString();
    }
}