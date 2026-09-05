using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BreakableObstacle))]
public sealed class BreakableObstacleAudio : MonoBehaviour
{
    private const float SharedClipCooldown = 0.2f;

    [Header("Break SFX")]
    [SerializeField] private AudioClip breakClip;
    [SerializeField, Range(0f, 1f)] private float volumeScale = 0.6f;

    private BreakableObstacle breakableObstacle;

    private void Awake()
    {
        breakableObstacle = GetComponent<BreakableObstacle>();
    }

    public void Configure(AudioClip clip, float scale = 0.6f)
    {
        breakClip = clip;
        volumeScale = Mathf.Clamp01(scale);
    }

    private void OnEnable()
    {
        if (breakableObstacle != null)
            breakableObstacle.Broken += HandleBroken;
    }

    private void OnDisable()
    {
        if (breakableObstacle != null)
            breakableObstacle.Broken -= HandleBroken;
    }

    private void HandleBroken(BreakableObstacle brokenObstacle)
    {
        if (brokenObstacle != breakableObstacle)
            return;

        if (breakClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(breakClip, volumeScale, SharedClipCooldown);
    }
}
