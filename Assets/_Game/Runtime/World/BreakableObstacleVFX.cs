using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BreakableObstacle))]
public sealed class BreakableObstacleVFX : MonoBehaviour
{
    [Header("Fragments")]
    [SerializeField] private GameObject fragmentPrefab;
    [SerializeField, Min(0)] private int fragmentCount = 6;

    [Header("Spawn")]
    [SerializeField] private Vector2 spawnOffset = Vector2.zero;
    [SerializeField, Min(0f)] private float fragmentSpawnRadius;

    private BreakableObstacle breakableObstacle;

    public void Configure(
        GameObject physicalFragmentPrefab,
        int physicalFragmentCount,
        Vector2 offset,
        float spawnRadius)
    {
        fragmentPrefab = physicalFragmentPrefab;
        fragmentCount = Mathf.Max(0, physicalFragmentCount);
        spawnOffset = offset;
        fragmentSpawnRadius = Mathf.Max(0f, spawnRadius);
    }

    private void Awake()
    {
        breakableObstacle = GetComponent<BreakableObstacle>();
    }

    private void OnEnable()
    {
        if (breakableObstacle != null)
        {
            breakableObstacle.Broken += HandleBroken;
        }
    }

    private void OnDisable()
    {
        if (breakableObstacle != null)
        {
            breakableObstacle.Broken -= HandleBroken;
        }
    }

    private void HandleBroken(BreakableObstacle obstacle)
    {
        Vector3 spawnPosition = transform.position + (Vector3)spawnOffset;

        // Only colored fragments are emitted; legacy white-particle references are ignored.
        // Spawn physical fragments if this obstacle has a fragment prefab.
        if (fragmentPrefab != null)
        {
            for (int i = 0; i < fragmentCount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * fragmentSpawnRadius;

                Instantiate(
                    fragmentPrefab,
                    spawnPosition + (Vector3)randomOffset,
                    Quaternion.identity
                );
            }
        }
    }
}
