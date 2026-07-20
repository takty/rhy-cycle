using System;
using UnityEngine;

public sealed class CourseController : MonoBehaviour
{
    [SerializeField]
    private float bpm = 120.0f;

    [SerializeField]
    private int beatsPerMeasure = 4;

    [SerializeField]
    private bool clockwise = true;

    public event Action<int> MeasureStarted;

    private bool isRunning;
    private double startTime;
    private int currentMeasure = -1;

    private double MeasureDuration =>
        60.0 / bpm * beatsPerMeasure;

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        double elapsed = AudioSettings.dspTime - startTime;
        int measure =
            (int)Math.Floor(elapsed / MeasureDuration);

        double positionInMeasure =
            (elapsed % MeasureDuration) / MeasureDuration;

        float angle =
            (float)(positionInMeasure * 360.0);

        if (clockwise)
        {
            angle = -angle;
        }

        transform.localRotation =
            Quaternion.Euler(0.0f, 0.0f, angle);

        while (currentMeasure < measure)
        {
            currentMeasure++;
            MeasureStarted?.Invoke(currentMeasure);

            Debug.Log(
                $"Measure started: {currentMeasure + 1}"
            );
        }
    }

    public void StartCourse()
    {
        startTime = AudioSettings.dspTime;
        currentMeasure = -1;
        isRunning = true;
    }

    public void StopCourse()
    {
        isRunning = false;
    }
}
