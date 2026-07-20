using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]

public sealed class PlayerStack : MonoBehaviour
{
    [SerializeField]
    private PlayerBall ballPrefab;

    [SerializeField]
    private float ballDiameter = 0.65f;

    [SerializeField]
    private float ballSpacing = 0.58f;

    [SerializeField]
    private float gravityScale = 7.0f;

    [SerializeField]
    private float minimumJumpSpeed = 16.0f;

    [SerializeField]
    private float maximumJumpSpeed = 20.0f;

    private Rigidbody2D body;
    private Vector3 startPosition;

    public bool IsGrounded { get; private set; }


    private readonly Dictionary<string, PlayerBall> balls =
        new Dictionary<string, PlayerBall>();

    private readonly List<string> order =
        new List<string>();

    public int Count => order.Count;

    public event Action<string> PlayerHitObstacle;

    public void AddPlayer(
        string memberId,
        string displayName)
    {
        if (balls.ContainsKey(memberId))
        {
            return;
        }

        if (order.Count == 0)
        {
            transform.position = startPosition;

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0.0f;
            body.simulated = true;

            IsGrounded = false;
        }

        PlayerBall ball =
            Instantiate(ballPrefab, transform);

        ball.Initialize(
            memberId,
            displayName,
            CreatePlayerColor(memberId)
        );
        ball.BeginProtection();

        ball.HitObstacle += OnBallHitObstacle;

        balls.Add(memberId, ball);
        order.Add(memberId);

        Relayout();
    }

    public void RemovePlayer(string memberId)
    {
        if (!TakePlayer(
            memberId,
            out PlayerBall ball))
        {
            return;
        }

        if (ball != null)
        {
            Destroy(ball.gameObject);
        }

        UpdateAfterRemoval();
    }
    public void EliminatePlayer(
        string memberId)
    {
        if (!TakePlayer(
            memberId,
            out PlayerBall ball))
        {
            return;
        }

        if (ball != null)
        {
            ball.transform.SetParent(
                null,
                true
            );

            ball.PlayElimination();
        }

        UpdateAfterRemoval();
    }
    private bool TakePlayer(
       string memberId,
       out PlayerBall ball)
    {
        if (!balls.TryGetValue(
            memberId,
            out ball))
        {
            return false;
        }

        balls.Remove(memberId);
        order.Remove(memberId);

        if (ball != null)
        {
            ball.HitObstacle -=
                OnBallHitObstacle;
        }

        return true;
    }

    private void UpdateAfterRemoval()
    {
        Relayout();

        if (order.Count > 0)
        {
            return;
        }

        body.linearVelocity =
            Vector2.zero;

        body.angularVelocity =
            0.0f;

        body.simulated = false;

        transform.position =
            startPosition;

        IsGrounded = false;
    }

    private void Awake()
    {
        startPosition = transform.position;

        body = GetComponent<Rigidbody2D>();

        body.gravityScale = gravityScale;
        body.constraints =
            RigidbodyConstraints2D.FreezePositionX |
            RigidbodyConstraints2D.FreezeRotation;

        body.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;

        body.simulated = false;
    }

    public bool TryJump(float synchronization)
    {
        if (!IsGrounded)
        {
            return false;
        }

        float jumpSpeed = Mathf.Lerp(
            minimumJumpSpeed,
            maximumJumpSpeed,
            Mathf.Clamp01(synchronization)
        );

        body.linearVelocity = new Vector2(
            0.0f,
            jumpSpeed
        );

        IsGrounded = false;
        return true;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        CheckGroundCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        CheckGroundCollision(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.collider.GetComponent<DonutCourse>() != null)
        {
            IsGrounded = false;
        }
    }

    private void CheckGroundCollision(Collision2D collision)
    {
        if (collision.collider.GetComponent<DonutCourse>() == null)
        {
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.5f)
            {
                IsGrounded = true;
                return;
            }
        }
    }

    public bool Contains(string memberId)
    {
        return balls.ContainsKey(memberId);
    }

    private void Relayout()
    {
        for (int i = 0; i < order.Count; i++)
        {
            PlayerBall ball = balls[order[i]];

            ball.transform.localPosition =
                new Vector3(
                    0.0f,
                    i * ballSpacing,
                    0.0f
                );

            ball.transform.localRotation =
                Quaternion.identity;

            ball.transform.localScale =
                Vector3.one * ballDiameter;
        }
    }

    private static Color CreatePlayerColor(
        string memberId)
    {
        uint hash = 2166136261;

        foreach (char c in memberId)
        {
            hash ^= c;
            hash *= 16777619;
        }

        float hue = (hash % 360) / 360.0f;

        return Color.HSVToRGB(
            hue,
            0.65f,
            1.0f
        );
    }

    private void OnBallHitObstacle(
    PlayerBall ball)
    {
        if (ball == null ||
            !balls.ContainsKey(ball.MemberId))
        {
            return;
        }

        PlayerHitObstacle?.Invoke(ball.MemberId);
    }
}
