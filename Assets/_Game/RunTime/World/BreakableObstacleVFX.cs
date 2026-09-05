using UnityEngine;

[RequireComponent(typeof(BreakableObstacle))]
public class BreakableObstacleVFX : MonoBehaviour
{
    [Header("Break Particles")]
    [SerializeField] private ParticleSystem breakParticlesPrefab;

    [Header("Fragments")]
    [SerializeField] private GameObject fragmentPrefab;
    [SerializeField, Min(0)] private int fragmentCount = 6;

    private BreakableObstacle breakableObstacle;

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
        Vector3 spawnPosition = transform.position;

        // Spawn the common break particle effect.
        if (breakParticlesPrefab != null)
        {
            ParticleSystem particles =
                Instantiate(breakParticlesPrefab, spawnPosition, Quaternion.identity);

            particles.Play();
        }

        // Spawn physical fragments if this obstacle has a fragment prefab.
        if (fragmentPrefab != null)
        {
            for (int i = 0; i < fragmentCount; i++)
            {
                Instantiate(fragmentPrefab, spawnPosition, Quaternion.identity);
            }
        }
    }
}