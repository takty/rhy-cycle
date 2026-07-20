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

    private int previousScore = -1;
    private int previousAliveCount = -1;
    private int previousWaitingCount = -1;

    private void Start()
    {
        UpdateHighScores();
        UpdatePlayerCounts();
        UpdateCurrentScore();
    }

    private void Update()
    {
        UpdateCurrentScore();
        UpdatePlayerCounts();
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
}