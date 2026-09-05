using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WolfEventDirector))]
public sealed class WolfEventAudio : MonoBehaviour
{
    [Header("Wolf SFX")]
    [SerializeField] private AudioClip spawnClip;
    [SerializeField] private AudioClip[] attackClips;
    [SerializeField] private AudioClip captureClip;

    [SerializeField, Range(0f, 1f)] private float spawnVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float attackVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float captureVolume = 1f;

    private WolfEventDirector director;
    private readonly HashSet<Wolf> observedWolves = new HashSet<Wolf>();

    private void Awake()
    {
        director = GetComponent<WolfEventDirector>();
    }

    private void OnEnable()
    {
        if (director != null)
            director.WolfReleased += HandleWolfReleased;
    }

    private void OnDisable()
    {
        if (director != null)
            director.WolfReleased -= HandleWolfReleased;

        foreach (Wolf wolf in observedWolves)
            StopObserving(wolf);
        observedWolves.Clear();
    }

    public void Configure(
        AudioClip spawn,
        AudioClip[] attacks,
        AudioClip capture,
        float spawnScale = 1f,
        float attackScale = 1f,
        float captureScale = 1f)
    {
        spawnClip = spawn;
        attackClips = attacks;
        captureClip = capture;
        spawnVolume = Mathf.Clamp01(spawnScale);
        attackVolume = Mathf.Clamp01(attackScale);
        captureVolume = Mathf.Clamp01(captureScale);
    }

    private void HandleWolfReleased(Wolf wolf)
    {
        if (spawnClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(spawnClip, spawnVolume);

        if (wolf != null && observedWolves.Add(wolf))
        {
            wolf.ChargeStarted += HandleChargeStarted;
            wolf.Attacked += HandleWolfAttacked;
            wolf.Finished += HandleWolfFinished;
        }
    }

    private void HandleChargeStarted(Wolf wolf)
    {
        if (wolf != null)
            wolf.ChargeStarted -= HandleChargeStarted;

        if (attackClips == null || attackClips.Length == 0)
            return;

        AudioClip clip = attackClips[Random.Range(0, attackClips.Length)];

        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip, attackVolume);
    }

    private void HandleWolfAttacked(Wolf wolf, WolfAttackResult result)
    {
        if (result.CapturedSheep == null)
            return;

        if (captureClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySheepSFX(captureClip, captureVolume);
    }

    private void HandleWolfFinished(Wolf wolf)
    {
        StopObserving(wolf);
        observedWolves.Remove(wolf);
    }

    private void StopObserving(Wolf wolf)
    {
        if (wolf == null)
            return;

        wolf.ChargeStarted -= HandleChargeStarted;
        wolf.Attacked -= HandleWolfAttacked;
        wolf.Finished -= HandleWolfFinished;
    }
}
