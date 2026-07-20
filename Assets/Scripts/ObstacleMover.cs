using UnityEngine;

public class ObstacleMover : MonoBehaviour
{
    [SerializeField]
    private float speed = 5.0f;

    private void Update()
    {
        transform.position += Vector3.left * speed * Time.deltaTime;

        if (transform.position.x < -10.0f)
        {
            transform.position = new Vector3(
                10.0f,
                transform.position.y,
                transform.position.z
            );
        }
    }
}
