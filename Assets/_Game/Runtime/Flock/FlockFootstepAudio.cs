using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FlockMovementController))]
public sealed class FlockFootstepAudio : MonoBehaviour
{
    [Header("Footstep Clips")]
    [SerializeField] private AudioClip[] grassClips;
    [SerializeField] private AudioClip[] sandClips;

    [Header("Footstep Settings")]
    [SerializeField, Range(0f, 1f)] private float grassChance = 0.7f;
    [SerializeField, Min(0.05f)] private float playInterval = 0.35f;

    private FlockMovementController movementController;
    private float timer;

    private void Awake()
    {
        movementController = GetComponent<FlockMovementController>();
    }

    public void Configure(AudioClip[] grass, AudioClip[] sand, float grassWeight = 0.7f, float interval = 0.35f)
    {
        grassClips = grass;
        sandClips = sand;
        grassChance = Mathf.Clamp01(grassWeight);
        playInterval = Mathf.Max(0.05f, interval);
    }

    private void Update()
    {
        if (Time.timeScale == 0f || movementController == null || !movementController.IsMoving)
            return;

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            PlayRandomFootstep();
            timer = playInterval;
        }
    }

    private void PlayRandomFootstep()
    {
        AudioClip clip;

        if (Random.value < grassChance)
        {
            clip = GetRandomClip(grassClips);

            if (clip == null)
                clip = GetRandomClip(sandClips);
        }
        else
        {
            clip = GetRandomClip(sandClips);

            if (clip == null)
                clip = GetRandomClip(grassClips);
        }

        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip);
    }

    private static AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        return clips[Random.Range(0, clips.Length)];
    }
}
