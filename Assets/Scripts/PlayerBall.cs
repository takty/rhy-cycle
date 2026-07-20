using System;
using System.Collections;
using UnityEngine;

public sealed class PlayerBall : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer spriteRenderer;

    [SerializeField]
    private float eliminationDuration = 0.3f;

    [SerializeField]
    [Range(3, 12)]
    private int fragmentCount = 6;

    [SerializeField]
    private float fragmentDistance = 1.0f;

    private bool isEliminating;

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
        if (isEliminating)
        {
            return;
        }

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

    public void PlayElimination()
    {
        if (isEliminating)
        {
            return;
        }

        isEliminating = true;

        Collider2D[] colliders =
            GetComponents<Collider2D>();

        foreach (Collider2D collider in colliders)
        {
            collider.enabled = false;
        }

        StartCoroutine(
            PlayEliminationAnimation()
        );
    }

    private IEnumerator PlayEliminationAnimation()
    {
        if (spriteRenderer == null ||
            spriteRenderer.sprite == null)
        {
            Destroy(gameObject);
            yield break;
        }

        Sprite sprite =
            spriteRenderer.sprite;

        Color originalColor =
            spriteRenderer.color;

        SpriteRenderer[] fragments =
            new SpriteRenderer[fragmentCount];

        Vector2[] directions =
            new Vector2[fragmentCount];

        for (int i = 0;
            i < fragmentCount;
            i++)
        {
            float angle =
                2.0f *
                Mathf.PI *
                i /
                fragmentCount;

            directions[i] = new Vector2(
                Mathf.Cos(angle),
                Mathf.Sin(angle)
            );

            GameObject fragmentObject =
                new GameObject(
                    $"Fragment_{i}"
                );

            fragmentObject.transform.SetParent(
                transform,
                false
            );

            fragmentObject.transform.localPosition =
                Vector3.zero;

            fragmentObject.transform.localRotation =
                Quaternion.Euler(
                    0.0f,
                    0.0f,
                    i * 23.0f
                );

            fragmentObject.transform.localScale =
                Vector3.one * 0.32f;

            SpriteRenderer fragment =
                fragmentObject.AddComponent<
                    SpriteRenderer
                >();

            fragment.sprite = sprite;
            fragment.color = originalColor;
            fragment.sortingLayerID =
                spriteRenderer.sortingLayerID;
            fragment.sortingOrder =
                spriteRenderer.sortingOrder + 1;

            fragments[i] = fragment;
        }

        spriteRenderer.enabled = false;

        float elapsed = 0.0f;

        while (elapsed < eliminationDuration)
        {
            elapsed += Time.deltaTime;

            float progress =
                Mathf.Clamp01(
                    elapsed /
                    eliminationDuration
                );

            float movement =
                fragmentDistance *
                (
                    1.0f -
                    Mathf.Pow(
                        1.0f - progress,
                        2.0f
                    )
                );

            float fall =
                0.35f *
                progress *
                progress;

            float alpha =
                1.0f - progress;

            for (int i = 0;
                i < fragments.Length;
                i++)
            {
                SpriteRenderer fragment =
                    fragments[i];

                if (fragment == null)
                {
                    continue;
                }

                fragment.transform.localPosition =
                    directions[i] * movement +
                    Vector2.down * fall;

                fragment.transform.Rotate(
                    0.0f,
                    0.0f,
                    360.0f *
                    Time.deltaTime *
                    (
                        i % 2 == 0
                            ? 1.0f
                            : -1.0f
                    )
                );

                Color color = originalColor;
                color.a = alpha;
                fragment.color = color;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
