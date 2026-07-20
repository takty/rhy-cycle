using System;
using UnityEngine;

public sealed class GameScore : MonoBehaviour
{
    [SerializeField]
    [Min(0.0f)]
    private float basePointsPerSecond = 10.0f;

    private bool isRunning;
    private double score;
    private double lastDspTime;
    private int aliveCount;
    private int maxAliveCount;

    public bool IsRunning => isRunning;

    public int CurrentScore =>
        score >= int.MaxValue
            ? int.MaxValue
            : (int)Math.Floor(score);

    public int MaxAliveCount =>
        maxAliveCount;

    private void Update()
    {
        AccumulateScore();
    }

    public void StartGame(
        int initialAliveCount)
    {
        score = 0.0;

        aliveCount =
            Mathf.Max(0, initialAliveCount);

        maxAliveCount =
            aliveCount;

        lastDspTime =
            AudioSettings.dspTime;

        isRunning = true;
    }

    public void SetAliveCount(
        int newAliveCount)
    {
        if (!isRunning)
        {
            return;
        }

        AccumulateScore();

        aliveCount =
            Mathf.Max(0, newAliveCount);

        maxAliveCount =
            Mathf.Max(
                maxAliveCount,
                aliveCount
            );
    }

    public void EndGame()
    {
        if (!isRunning)
        {
            return;
        }

        AccumulateScore();

        isRunning = false;
        aliveCount = 0;
    }

    private void AccumulateScore()
    {
        if (!isRunning)
        {
            return;
        }

        double currentTime =
            AudioSettings.dspTime;

        double elapsed =
            currentTime - lastDspTime;

        lastDspTime =
            currentTime;

        if (elapsed <= 0.0 ||
            aliveCount <= 0)
        {
            return;
        }

        double pointsPerSecond =
            basePointsPerSecond *
            aliveCount *
            (aliveCount + 1) /
            2.0;

        score +=
            pointsPerSecond * elapsed;
    }
}