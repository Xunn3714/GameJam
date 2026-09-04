using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PoopAbility : MonoBehaviour
{
    [SerializeField] private InputActionReference poopAction;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject poopPrefab;
    [SerializeField] private AudioClip poopClip;
    [SerializeField, Min(0f)] private float cooldownSeconds = 2f;
    [SerializeField, Min(0.01f)] private float lifetimeSeconds = 10f;
    [SerializeField, Min(1)] private int maxActivePoops = 10;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private InputAction runtimePoopAction;
    private float readyAt;
    private bool controlEnabled = true;

    public event Action Used;
    public float RemainingCooldown => Mathf.Max(0f, readyAt - Time.time);
    public bool ControlEnabled => controlEnabled;
    public int ActivePoopCount
    {
        get
        {
            spawned.RemoveAll(instance => instance == null);
            return spawned.Count;
        }
    }
    public bool IsAtCapacity => ActivePoopCount >= maxActivePoops;

    private void OnEnable()
    {
        runtimePoopAction = poopAction != null ? poopAction.action?.Clone() : null;
        runtimePoopAction?.Enable();
    }

    private void OnDisable()
    {
        runtimePoopAction?.Dispose();
        runtimePoopAction = null;
    }

    private void Update()
    {
        if (runtimePoopAction != null && runtimePoopAction.WasPressedThisFrame()) TryUse();
    }

    public void SetControlEnabled(bool value) => controlEnabled = value;

    public bool TryUse()
    {
        if (!isActiveAndEnabled || !controlEnabled || Time.timeScale == 0f || RemainingCooldown > 0f ||
            poopPrefab == null || spawnPoint == null || IsAtCapacity) return false;

        GameObject instance = Instantiate(poopPrefab, spawnPoint.position, Quaternion.identity);
        spawned.Add(instance);
        Destroy(instance, Mathf.Max(0.01f, lifetimeSeconds));
        readyAt = Time.time + Mathf.Max(0f, cooldownSeconds);
        if (poopClip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(poopClip);
        Used?.Invoke();
        return true;
    }

    private void OnDestroy()
    {
        foreach (GameObject instance in spawned)
            if (instance != null) Destroy(instance);
        spawned.Clear();
    }
}
