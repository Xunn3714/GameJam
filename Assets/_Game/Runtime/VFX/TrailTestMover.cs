using UnityEngine;

public class TrailTestMover : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveDistance = 3f;
    [SerializeField] private float moveSpeed = 1.5f;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.time * moveSpeed) * moveDistance;

        transform.position = new Vector3(
            startPosition.x + offset,
            startPosition.y,
            startPosition.z
        );
    }
}
