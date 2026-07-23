using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public sealed class CourseController : MonoBehaviour
{
    [SerializeField]
    private float bpm = 120.0f;

    [SerializeField]
    private int beatsPerMeasure = 4;

    [SerializeField]
    private bool clockwise = true;

    public event Action<int> MeasureStarted;
    public event Action<int, int> BeatStarted;

    public event Action<int> PhysicsMeasureStarted;

    private bool isRunning;
    private double startTime;
    private int currentMeasure = -1;
    private int currentBeat = -1;
    private int currentPhysicsMeasure = -1;

    private Rigidbody2D body;

    private double BeatDuration =>
        60.0 / bpm;

    private double MeasureDuration =>
        BeatDuration * beatsPerMeasure;

    private void Update()
    {
        if (!isRunning)
        {
            return;
        }

        CalculateCourseState(
            out int beat,
            out int measure,
            out int beatInMeasure,
            out _
        );

        while (currentMeasure < measure)
        {
            currentMeasure++;
            MeasureStarted?.Invoke(currentMeasure);

            Debug.Log(
                $"Measure started: {currentMeasure + 1}"
            );
        }

        if (currentBeat != beat)
        {
            currentBeat = beat;

            BeatStarted?.Invoke(
                measure,
                beatInMeasure
            );

            Debug.Log(
                $"Beat started: " +
                $"measure={measure + 1}, " +
                $"beat={beatInMeasure + 1}"
            );
        }
    }
    public void StartCourse()
    {
        startTime =
            AudioSettings.dspTime;

        currentMeasure = -1;
        currentBeat = -1;
        currentPhysicsMeasure = -1;

        isRunning = true;
    }

    public void StopCourse()
    {
        isRunning = false;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0.0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void FixedUpdate()
    {
        if (!isRunning)
        {
            return;
        }

        CalculateCourseState(
            out _,
            out int measure,
            out _,
            out float angle
        );

        body.MoveRotation(angle);

        while (currentPhysicsMeasure < measure)
        {
            currentPhysicsMeasure++;

            PhysicsMeasureStarted?.Invoke(
                currentPhysicsMeasure
            );
        }
    }

    private void CalculateCourseState(
        out int beat,
        out int measure,
        out int beatInMeasure,
        out float angle)
    {
        double elapsed = Math.Max(
            0.0,
            AudioSettings.dspTime - startTime
        );

        beat = (int)Math.Floor(
            elapsed / BeatDuration
        );

        measure =
            beat / beatsPerMeasure;

        beatInMeasure =
            beat % beatsPerMeasure;

        double positionInMeasure =
            (elapsed % MeasureDuration) /
            MeasureDuration;

        angle =
            (float)(positionInMeasure * 360.0);

        if (clockwise)
        {
            angle = -angle;
        }
    }
}
