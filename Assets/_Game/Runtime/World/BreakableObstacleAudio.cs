using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BreakableObstacle))]
public sealed class BreakableObstacleAudio : MonoBehaviour
{
    private const float SharedClipCooldown = 0.35f;

    [Header("Break SFX")]
    [SerializeField] private AudioClip breakClip;
    [SerializeField] private AudioClip[] randomBreakClips;
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

        AudioClip clipToPlay = GetBreakClip();

        if (clipToPlay != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clipToPlay, volumeScale, SharedClipCooldown);
        }

    private AudioClip GetBreakClip()
    {
        if (randomBreakClips != null && randomBreakClips.Length > 0)
        {
            int startIndex = Random.Range(0, randomBreakClips.Length);

            for (int i = 0; i < randomBreakClips.Length; i++)
            {
                AudioClip clip =
                    randomBreakClips[(startIndex + i) % randomBreakClips.Length];

                if (clip != null)
                    return clip;
            }
        }

        return breakClip;
    }
}
