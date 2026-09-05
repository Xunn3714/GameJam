using UnityEngine;

[RequireComponent(typeof(FlockMovementController))]
public class FlockFootstepAudio : MonoBehaviour
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
        // Use the existing flock movement component on this GameObject.
        movementController = GetComponent<FlockMovementController>();
    }

    private void Update()
    {
        // Do not play footsteps while the flock is standing still.
        if (!movementController.IsMoving)
        {
            timer = 0f;
            return;
        }

        // Count down until the next footstep can be played.
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

        // 70% chance to use grass footsteps,
        // 30% chance to use sand footsteps.
        if (Random.value < grassChance)
        {
            clip = GetRandomClip(grassClips);

            // Fall back to sand if no grass clips are assigned.
            if (clip == null)
                clip = GetRandomClip(sandClips);
        }
        else
        {
            clip = GetRandomClip(sandClips);

            // Fall back to grass if no sand clips are assigned.
            if (clip == null)
                clip = GetRandomClip(grassClips);
        }

        // Use the project's existing AudioManager and SFX channel.
        if (clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip);
        }
    }

    private AudioClip GetRandomClip(AudioClip[] clips)
    {
        // Return nothing if this sound pool has not been configured.
        if (clips == null || clips.Length == 0)
            return null;

        // Pick one random clip from the selected sound pool.
        return clips[Random.Range(0, clips.Length)];
    }
}