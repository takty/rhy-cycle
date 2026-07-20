using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CourseObstacleLayout : MonoBehaviour
{
    [SerializeField]
    private CourseController courseController;

    [SerializeField]
    private DonutCourse donutCourse;

    [SerializeField]
    private GameObject obstaclePrefab;

    [SerializeField]
    private float obstacleLength = 1.2f;

    [SerializeField]
    private float obstacleWidth = 0.35f;

    [SerializeField]
    private float phaseOffset = 45.0f;

    private readonly List<GameObject> obstacles = new();

    private readonly float[][] patterns =
    {
        //new float[]
        //{
        //    0.0f, 90.0f, 180.0f, 270.0f,
        //},

        new float[]
        {
            0.0f, 180.0f,
        },

        //new float[]
        //{
        //    0.0f, 45.0f, 90.0f, 135.0f,
        //    180.0f, 225.0f, 270.0f, 315.0f,
        //},

        //new float[]
        //{
        //    0.0f, 135.0f, 180.0f, 315.0f,
        //},
    };

    private int currentPatternIndex = -1;
    private int remainingMeasures;
    private bool firstMeasureStarted;

    private void OnEnable()
    {
        if (courseController != null)
        {
            courseController.MeasureStarted += OnMeasureStarted;
        }
    }

    private void OnDisable()
    {
        if (courseController != null)
        {
            courseController.MeasureStarted -= OnMeasureStarted;
        }
    }

    private void Start()
    {
        SelectNextPattern();
    }

    private void OnMeasureStarted(int measure)
    {
        if (!firstMeasureStarted)
        {
            firstMeasureStarted = true;
            return;
        }

        remainingMeasures--;

        if (remainingMeasures <= 0)
        {
            SelectNextPattern();
        }
    }

    private void SelectNextPattern()
    {
        int nextPatternIndex;

        if (currentPatternIndex < 0)
        {
            nextPatternIndex =
                UnityEngine.Random.Range(0, patterns.Length);
        }
        else
        {
            nextPatternIndex =
                UnityEngine.Random.Range(0, patterns.Length - 1);

            if (nextPatternIndex >= currentPatternIndex)
            {
                nextPatternIndex++;
            }
        }

        currentPatternIndex = nextPatternIndex;
        remainingMeasures = UnityEngine.Random.Range(1, 5);

        ApplyPattern(patterns[currentPatternIndex]);

        Debug.Log(
            $"Rhythm pattern: {currentPatternIndex}, " +
            $"duration: {remainingMeasures} measures"
        );
    }

    public void ApplyPattern(float[] angles)
    {
        ClearObstacles();

        foreach (float angle in angles)
        {
            CreateObstacle(angle + phaseOffset);
        }
    }

    private void CreateObstacle(float angle)
    {
        GameObject obstacle =
            Instantiate(obstaclePrefab, transform);

        obstacle.name = "Obstacle";

        float radius =
            donutCourse.InnerRadius -
            obstacleLength * 0.5f;

        float radians = angle * Mathf.Deg2Rad;

        obstacle.transform.localPosition = new Vector3(
            Mathf.Cos(radians) * radius,
            Mathf.Sin(radians) * radius,
            0.0f
        );

        obstacle.transform.localRotation =
            Quaternion.Euler(0.0f, 0.0f, angle);

        obstacle.transform.localScale = new Vector3(
            obstacleLength,
            obstacleWidth,
            1.0f
        );

        obstacles.Add(obstacle);
    }

    private void ClearObstacles()
    {
        foreach (GameObject obstacle in obstacles)
        {
            if (obstacle != null)
            {
                obstacle.SetActive(false);
                Destroy(obstacle);
            }
        }

        obstacles.Clear();
    }
}
