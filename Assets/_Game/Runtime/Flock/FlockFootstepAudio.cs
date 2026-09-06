using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(FlockMovementController))]
public sealed class FlockFootstepAudio : MonoBehaviour
{
    [Header("Footstep Clips")]
    [SerializeField] private AudioClip[] grassClips;

    [Header("Footstep Settings")]
    [SerializeField, Min(0.05f)] private float playInterval = 0.3f;

    private FlockMovementController movementController;
    private float timer;

    private void Awake()
    {
        movementController = GetComponent<FlockMovementController>();
    }

    public void Configure(AudioClip[] grass, AudioClip[] sand, float grassWeight = 0.7f, float interval = 0.3f)
    {
        // Keep the old interface for compatibility with existing setup code.
        // Sand and grassWeight are no longer used because footsteps are now 100% grass.
        grassClips = grass;
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
        AudioClip clip = GetRandomClip(grassClips);

        // Use the project's existing AudioManager and SFX channel.
        if (clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip);
        }
    }

    private static AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        return clips[Random.Range(0, clips.Length)];
    }
}
