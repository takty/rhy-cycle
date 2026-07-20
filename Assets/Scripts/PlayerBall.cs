using System;
using UnityEngine;

public sealed class PlayerBall : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer spriteRenderer;

    public string MemberId { get; private set; }
    public string DisplayName { get; private set; }

    public event Action<PlayerBall> HitObstacle;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    public void Initialize(
        string memberId,
        string displayName,
        Color color)
    {
        MemberId = memberId;
        DisplayName = displayName;

        gameObject.name =
            $"PlayerBall_{displayName}";

        if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    private void OnCollisionEnter2D(
        Collision2D collision)
    {
        bool hitObstacle =
            collision.collider
                .GetComponent<CourseObstacle>() != null ||
            collision.otherCollider
                .GetComponent<CourseObstacle>() != null;

        if (!hitObstacle)
        {
            return;
        }

        HitObstacle?.Invoke(this);
    }

}
