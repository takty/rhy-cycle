using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(EdgeCollider2D))]
public sealed class DonutCourse : MonoBehaviour
{
    [SerializeField]
    [Min(1.0f)]
    private float innerRadius = 4.0f;

    [SerializeField]
    [Min(0.1f)]
    private float thickness = 0.8f;

    [SerializeField]
    [Range(16, 256)]
    private int segments = 128;

    [SerializeField]
    private int sortingOrder = -10;

    private LineRenderer line;
    private EdgeCollider2D edge;

    public float InnerRadius => innerRadius;

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnValidate()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        line = GetComponent<LineRenderer>();
        edge = GetComponent<EdgeCollider2D>();

        if (line == null || edge == null)
        {
            return;
        }

        Vector3[] linePoints = new Vector3[segments];
        Vector2[] colliderPoints = new Vector2[segments + 1];

        for (int i = 0; i < segments; i++)
        {
            float angle =
                2.0f * Mathf.PI * i / segments;

            float x = Mathf.Cos(angle) * innerRadius;
            float y = Mathf.Sin(angle) * innerRadius;

            linePoints[i] = new Vector3(x, y, 0.0f);
            colliderPoints[i] = new Vector2(x, y);
        }

        colliderPoints[segments] = colliderPoints[0];

        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.startWidth = thickness;
        line.endWidth = thickness;
        line.sortingOrder = sortingOrder;
        line.SetPositions(linePoints);

        edge.points = colliderPoints;
    }
}
