using UnityEngine;

[RequireComponent(typeof(WolfEventDirector))]
public class WolfEventAudio : MonoBehaviour
{
    [Header("Wolf SFX")]
    [SerializeField] private AudioClip spawnClip;
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField] private AudioClip captureClip;

    [SerializeField, Range(0f, 1f)] private float spawnVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float attackVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float captureVolume = 1f;

    private WolfEventDirector director;

    private void Awake()
    {
        director = GetComponent<WolfEventDirector>();
    }

    private void OnEnable()
    {
        if (director != null)
        {
            director.WolfReleased += HandleWolfReleased;
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.WolfReleased -= HandleWolfReleased;
        }
    }

    private void HandleWolfReleased(Wolf wolf)
    {
        // Play the wolf's spawn / ready sound.
        if (spawnClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(spawnClip, spawnVolume);
        }

        // Listen for the exact moment this wolf begins charging.
        if (wolf != null)
        {
            wolf.ChargeStarted += HandleChargeStarted;
            wolf.Attacked += HandleWolfAttacked;
        }
    }

    private void HandleChargeStarted(Wolf wolf)
    {
        // Each wolf only begins its charge once.
        if (wolf != null)
        {
            wolf.ChargeStarted -= HandleChargeStarted;
        }

        if (attackClips == null || attackClips.Length == 0)
            return;

        AudioClip clip = attackClips[Random.Range(0, attackClips.Length)];

        if (clip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(clip, attackVolume);
        }
    }

    private void HandleWolfAttacked(Wolf wolf, WolfAttackResult result)
    {
        // Only play this sound when the wolf actually captured a sheep.
        if (wolf == null || !wolf.IsCarryingSheep)
            return;

        if (captureClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySheepSFX(captureClip, captureVolume);
        }
    }
}