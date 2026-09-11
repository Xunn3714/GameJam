using UnityEngine;

[DisallowMultipleComponent]
public sealed class CreditsDecorationMotion : MonoBehaviour
{
    [SerializeField, Min(0f)] private float horizontalDistance = 0.2f;
    [SerializeField, Min(0f)] private float verticalDistance = 0.12f;
    [SerializeField, Min(0.1f)] private float frequency = 0.8f;
    [SerializeField] private float phase;

    private Vector3 origin;


    private void OnEnable()
    {
        origin = transform.localPosition;
    }


    private void Update()
    {
        float time = Time.unscaledTime * frequency + phase;
        transform.localPosition = origin + new Vector3(
            Mathf.Sin(time) * horizontalDistance,
            Mathf.Sin(time * 1.7f) * verticalDistance,
            0f);
    }


    public void Configure(float horizontal, float vertical, float speed, float startPhase)
    {
        horizontalDistance = Mathf.Max(0f, horizontal);
        verticalDistance = Mathf.Max(0f, vertical);
        frequency = Mathf.Max(0.1f, speed);
        phase = startPhase;
    }
}
