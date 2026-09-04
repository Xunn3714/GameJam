using UnityEngine;

// Small spawn feedback; the ability owns lifetime and capacity.
public sealed class PoopVisual : MonoBehaviour
{
    private Vector3 fullScale;
    private float age;

    private void Awake()
    {
        fullScale = transform.localScale;
        transform.localScale = fullScale * 0.4f;
    }

    private void Update()
    {
        age += Time.deltaTime;
        transform.localScale = fullScale * Mathf.Lerp(0.4f, 1f, age / 0.18f);
        if (age >= 0.18f) enabled = false;
    }
}
