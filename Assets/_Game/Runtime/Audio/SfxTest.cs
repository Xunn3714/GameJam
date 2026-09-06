using UnityEngine;

public class SfxTest : MonoBehaviour
{
    [SerializeField] private AudioClip[] sfxClips;
    [SerializeField] private float playInterval = 0.3f;

    private AudioSource sfxSource;
    private FlockMovementController flockMovement;
    private float timer;

    void Awake()
    {
        sfxSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        flockMovement = FindFirstObjectByType<FlockMovementController>();
    }

    void Update()
    {
        if (flockMovement == null)
        {
            flockMovement = FindFirstObjectByType<FlockMovementController>();
            return;
        }

        if (!flockMovement.IsMoving)
        {
            timer = 0f;
            return;
        }

        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            PlayRandomSfx();
            timer = playInterval;
        }
    }

    private void PlayRandomSfx()
    {
        if (sfxClips == null || sfxClips.Length == 0)
            return;

        int randomIndex = Random.Range(0, sfxClips.Length);
        sfxSource.PlayOneShot(sfxClips[randomIndex]);
    }
}