using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class BreakFragment : MonoBehaviour
{
    [Header("Fragment Motion")]
    [SerializeField] private float minSpeed = 2f;
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private float minAngularSpeed = -360f;
    [SerializeField] private float maxAngularSpeed = 360f;

    [Header("Lifetime")]
    [SerializeField] private float lifetime = 0.8f;

    private void Start()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();

        Vector2 direction = Random.insideUnitCircle.normalized;
        float speed = Random.Range(minSpeed, maxSpeed);

        body.linearVelocity = direction * speed;
        body.angularVelocity = Random.Range(minAngularSpeed, maxAngularSpeed);

        Destroy(gameObject, lifetime);
    }
}