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

    [Header("Stock")]
    [SerializeField, Min(1)] private int maxStoredPoops = 5;
    [SerializeField, Min(0.01f)] private float rechargeSeconds = 10f;
    [SerializeField, Min(0f)] private float cooldownSeconds = 0.5f;

    [Header("World")]
    [SerializeField, Min(0.01f)] private float lifetimeSeconds = 5f;
    [SerializeField, Min(1)] private int maxActivePoops = 2;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private InputAction runtimePoopAction;
    private int storedPoops;
    private float nextRechargeAt = float.PositiveInfinity;
    private float readyAt;
    private bool controlEnabled = true;

    public event Action Used;
    public event Action<int, int> StockChanged;

    public float RemainingCooldown => Mathf.Max(0f, readyAt - Time.time);
    public float RemainingRecharge => storedPoops >= maxStoredPoops
        ? 0f
        : Mathf.Max(0f, nextRechargeAt - Time.time);
    public bool ControlEnabled => controlEnabled;
    public int StoredPoops => storedPoops;
    public int MaxStoredPoops => maxStoredPoops;
    public int ActivePoopCount
    {
        get
        {
            spawned.RemoveAll(instance => instance == null);
            return spawned.Count;
        }
    }
    public bool IsAtCapacity => ActivePoopCount >= maxActivePoops;

    private void Awake()
    {
        ValidateSettings();
        storedPoops = maxStoredPoops;
    }

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
        RechargeStock();
        if (runtimePoopAction != null && runtimePoopAction.WasPressedThisFrame()) TryUse();
    }

    private void OnValidate() => ValidateSettings();

    public void SetControlEnabled(bool value) => controlEnabled = value;

    public bool TryUse()
    {
        RechargeStock();
        if (!isActiveAndEnabled || !controlEnabled || Time.timeScale == 0f || RemainingCooldown > 0f ||
            poopPrefab == null || spawnPoint == null || storedPoops <= 0 || IsAtCapacity) return false;

        GameObject instance = Instantiate(poopPrefab, spawnPoint.position, Quaternion.identity);
        spawned.Add(instance);

        PoopVisual visual = instance.GetComponent<PoopVisual>();
        if (visual != null)
            visual.BeginLifetime(lifetimeSeconds);
        else
            Destroy(instance, lifetimeSeconds);

        ConsumeStock();
        readyAt = Time.time + Mathf.Max(0f, cooldownSeconds);
        if (poopClip != null && AudioManager.Instance != null) AudioManager.Instance.PlaySFX(poopClip);
        Used?.Invoke();
        return true;
    }

    private void ConsumeStock()
    {
        bool wasFull = storedPoops >= maxStoredPoops;
        storedPoops = Mathf.Max(0, storedPoops - 1);

        if (wasFull || float.IsPositiveInfinity(nextRechargeAt))
            nextRechargeAt = Time.time + rechargeSeconds;

        StockChanged?.Invoke(storedPoops, maxStoredPoops);
    }

    private void RechargeStock()
    {
        if (storedPoops >= maxStoredPoops)
        {
            nextRechargeAt = float.PositiveInfinity;
            return;
        }

        if (float.IsPositiveInfinity(nextRechargeAt))
            nextRechargeAt = Time.time + rechargeSeconds;

        bool changed = false;
        while (storedPoops < maxStoredPoops && Time.time >= nextRechargeAt)
        {
            storedPoops++;
            nextRechargeAt += rechargeSeconds;
            changed = true;
        }

        if (storedPoops >= maxStoredPoops)
            nextRechargeAt = float.PositiveInfinity;

        if (changed)
            StockChanged?.Invoke(storedPoops, maxStoredPoops);
    }

    private void ValidateSettings()
    {
        maxStoredPoops = Mathf.Max(1, maxStoredPoops);
        rechargeSeconds = Mathf.Max(0.01f, rechargeSeconds);
        cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        lifetimeSeconds = Mathf.Max(0.01f, lifetimeSeconds);
        maxActivePoops = Mathf.Max(1, maxActivePoops);
    }

    private void OnDestroy()
    {
        foreach (GameObject instance in spawned)
            if (instance != null) Destroy(instance);
        spawned.Clear();
    }
}
