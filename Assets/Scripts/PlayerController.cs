using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    private RemoteJumpInput remoteInput;
    
    [SerializeField]
    private float jumpSpeed = 8.0f;

    private Rigidbody2D body;
    private bool isGrounded = true;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        bool keyboardJump =
            Keyboard.current != null &&
            Keyboard.current.spaceKey.wasPressedThisFrame;

        bool remoteJump =
            remoteInput != null &&
            remoteInput.ConsumeJumpPressed();

        if (isGrounded && (keyboardJump || remoteJump))
        {
            body.linearVelocity = new Vector2(
                body.linearVelocity.x,
                jumpSpeed
            );

            isGrounded = false;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.name == "Ground")
        {
            isGrounded = true;
        }
    }
}
