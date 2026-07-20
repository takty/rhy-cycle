using UnityEngine;

public sealed class PlayerBall : MonoBehaviour
{
    [SerializeField]
    private SpriteRenderer spriteRenderer;

    public string MemberId { get; private set; }
    public string DisplayName { get; private set; }

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
}
