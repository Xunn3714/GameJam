using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PoopAbility : MonoBehaviour
{
    [SerializeField] private InputActionReference poopAction;
    [Tooltip("归档场景的单羊后备生成点；Alpha 使用每只成员羊的脚下位置。")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private GameObject poopPrefab;
    [SerializeField] private AudioClip poopClip;

    [Header("Timing And Capacity")]
    [SerializeField, Min(0f)] private float cooldownSeconds = 2f;
    [SerializeField, Min(0.01f)] private float lifetimeSeconds = 10f;
    [SerializeField, Min(1)] private int maxActivePoops = 100;

    [Header("Flock Wave")]
    [SerializeField, Min(0f)] private float ringIntervalSeconds = 0.2f;
    [Tooltip("按羊到羊群中心的距离分圈；默认与成员避让半径一致。")]
    [SerializeField, Min(0.1f)] private float ringWidth = 1.9f;
    [Tooltip("从羊贴图下边缘向上回收一点，作为脚下位置。")]
    [SerializeField] private float footOffset = 0.08f;

    private readonly List<GameObject> spawned = new List<GameObject>();
    private InputAction runtimePoopAction;
    private FlockController flockController;
    private Coroutine poopSequence;
    private float readyAt;
    private bool controlEnabled = true;
    private bool sequenceRunning;

    public event Action Used;

    // 兼容归档 MVP 场景的旧 HUD 接口。当前玩法没有库存，只有冷却和场上数量上限。
    public event Action<int, int> StockChanged;

    public float RemainingCooldown => Mathf.Max(0f, readyAt - Time.time);
    public float RemainingRecharge => 0f;
    public bool ControlEnabled => controlEnabled;
    public bool SequenceRunning => sequenceRunning;
    public int StoredPoops => maxActivePoops;
    public int MaxStoredPoops => maxActivePoops;
    public float CooldownSeconds => cooldownSeconds;
    public float LifetimeSeconds => lifetimeSeconds;
    public int MaxActivePoops => maxActivePoops;
    public float RingIntervalSeconds => ringIntervalSeconds;
    public float RingWidth => ringWidth;
    public int ActivePoopCount
    {
        get
        {
            RemoveDestroyedInstances();
            return spawned.Count;
        }
    }
    public bool IsAtCapacity => ActivePoopCount >= maxActivePoops;

    private void Awake()
    {
        ValidateSettings();
        CacheFlockReference();
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
        CancelSequence();
    }

    private void Update()
    {
        if (runtimePoopAction != null && runtimePoopAction.WasPressedThisFrame())
            TryUse();
    }

    private void OnValidate() => ValidateSettings();

    public void Configure(
        InputActionReference action,
        GameObject prefab,
        float cooldown,
        float lifetime,
        int maximumActive,
        float waveInterval,
        float radialRingWidth = 1.9f,
        float positionFootOffset = 0.08f)
    {
        poopAction = action;
        poopPrefab = prefab;
        cooldownSeconds = cooldown;
        lifetimeSeconds = lifetime;
        maxActivePoops = maximumActive;
        ringIntervalSeconds = waveInterval;
        ringWidth = radialRingWidth;
        footOffset = positionFootOffset;
        ValidateSettings();
        CacheFlockReference();
    }

    public void SetControlEnabled(bool value)
    {
        controlEnabled = value;
        if (!value)
            CancelSequence();
    }

    public bool TryUse()
    {
        if (!isActiveAndEnabled || !controlEnabled || Time.timeScale == 0f ||
            RemainingCooldown > 0f || sequenceRunning || poopPrefab == null)
            return false;

        CacheFlockReference();
        List<List<SheepMember>> rings = BuildMemberRings();
        if (flockController != null && rings.Count == 0)
            return false;

        readyAt = Time.time + cooldownSeconds;
        sequenceRunning = true;

        if (flockController == null)
        {
            SpawnPoop(spawnPoint != null ? spawnPoint.position : transform.position, null);
            sequenceRunning = false;
        }
        else if (Application.isPlaying)
        {
            poopSequence = StartCoroutine(PlayFlockWave(rings));
        }
        else
        {
            SpawnAllRingsImmediately(rings);
            sequenceRunning = false;
        }

        if (poopClip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(poopClip);

        Used?.Invoke();
        StockChanged?.Invoke(StoredPoops, MaxStoredPoops);
        return true;
    }

    /// <summary>点击单只羊时触发的一次性拉屎，复用整群技能同样的落点、容量和动画逻辑。</summary>
    public bool TryPoopAt(SheepMember member)
    {
        if (member == null || !isActiveAndEnabled || poopPrefab == null || Time.timeScale == 0f)
            return false;

        SpriteRenderer sheepRenderer = member.GetComponent<SpriteRenderer>();
        Vector3 position = member.transform.position;
        if (sheepRenderer != null)
            position.y = sheepRenderer.bounds.min.y + footOffset;

        SpawnPoop(position, sheepRenderer);
        SheepVisualAnimator.Ensure(member.gameObject)?.PlayPoopReaction();
        return true;
    }

    public static int CalculateRingIndex(float distanceFromCenter, float radialRingWidth)
    {
        float safeWidth = Mathf.Max(0.1f, radialRingWidth);
        return Mathf.Max(0, Mathf.FloorToInt(Mathf.Max(0f, distanceFromCenter) / safeWidth));
    }

    private List<List<SheepMember>> BuildMemberRings()
    {
        List<List<SheepMember>> result = new List<List<SheepMember>>();
        if (flockController == null)
            return result;

        SortedDictionary<int, List<SheepMember>> rings = new SortedDictionary<int, List<SheepMember>>();
        Vector2 center = flockController.Center;
        IReadOnlyList<SheepMember> members = flockController.Members;
        for (int index = 0; index < members.Count; index++)
        {
            SheepMember member = members[index];
            if (member == null)
                continue;

            float distance = Vector2.Distance(center, member.transform.position);
            int ringIndex = CalculateRingIndex(distance, ringWidth);
            if (!rings.TryGetValue(ringIndex, out List<SheepMember> ring))
            {
                ring = new List<SheepMember>();
                rings.Add(ringIndex, ring);
            }

            ring.Add(member);
        }

        foreach (KeyValuePair<int, List<SheepMember>> pair in rings)
            result.Add(pair.Value);
        return result;
    }

    private IEnumerator PlayFlockWave(IReadOnlyList<List<SheepMember>> rings)
    {
        WaitForSeconds ringDelay = ringIntervalSeconds > 0f
            ? new WaitForSeconds(ringIntervalSeconds)
            : null;

        for (int ringIndex = 0; ringIndex < rings.Count; ringIndex++)
        {
            SpawnRing(rings[ringIndex]);
            if (ringIndex < rings.Count - 1 && ringDelay != null)
                yield return ringDelay;
        }

        poopSequence = null;
        sequenceRunning = false;
    }

    private void SpawnAllRingsImmediately(IReadOnlyList<List<SheepMember>> rings)
    {
        for (int index = 0; index < rings.Count; index++)
            SpawnRing(rings[index]);
    }

    private void SpawnRing(IReadOnlyList<SheepMember> ring)
    {
        for (int index = 0; index < ring.Count; index++)
        {
            SheepMember member = ring[index];
            if (member == null || member.Flock != flockController)
                continue;

            SpriteRenderer sheepRenderer = member.GetComponent<SpriteRenderer>();
            Vector3 position = member.transform.position;
            if (sheepRenderer != null)
                position.y = sheepRenderer.bounds.min.y + footOffset;

            SpawnPoop(position, sheepRenderer);
            SheepVisualAnimator.Ensure(member.gameObject)?.PlayPoopReaction();
        }
    }

    private void SpawnPoop(Vector3 position, SpriteRenderer sheepRenderer)
    {
        RemoveDestroyedInstances();
        EvictOldestUntilSpaceIsAvailable();

        GameObject instance = Instantiate(poopPrefab, position, Quaternion.identity);
        spawned.Add(instance);

        PoopVisual visual = instance.GetComponent<PoopVisual>();
        if (visual != null)
            visual.Configure(lifetimeSeconds, sheepRenderer);
        else if (Application.isPlaying)
            Destroy(instance, lifetimeSeconds);
    }

    private void EvictOldestUntilSpaceIsAvailable()
    {
        while (spawned.Count >= maxActivePoops)
        {
            GameObject oldest = spawned[0];
            spawned.RemoveAt(0);
            DestroyTrackedInstance(oldest, true);
        }
    }

    private void RemoveDestroyedInstances()
    {
        spawned.RemoveAll(instance => instance == null);
    }

    private void CacheFlockReference()
    {
        flockController ??= GetComponent<FlockController>();
    }

    private void CancelSequence()
    {
        if (poopSequence != null)
        {
            StopCoroutine(poopSequence);
            poopSequence = null;
        }

        sequenceRunning = false;
    }

    private void ValidateSettings()
    {
        cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        lifetimeSeconds = Mathf.Max(0.01f, lifetimeSeconds);
        maxActivePoops = Mathf.Max(1, maxActivePoops);
        ringIntervalSeconds = Mathf.Max(0f, ringIntervalSeconds);
        ringWidth = Mathf.Max(0.1f, ringWidth);
    }

    private void DestroyTrackedInstance(GameObject instance, bool playEffect)
    {
        if (instance == null)
            return;

        PoopVisual visual = instance.GetComponent<PoopVisual>();
        if (visual != null)
        {
            visual.Despawn(playEffect);
            return;
        }

        instance.SetActive(false);
        if (Application.isPlaying)
            Destroy(instance);
        else
            DestroyImmediate(instance);
    }

    private void OnDestroy()
    {
        for (int index = spawned.Count - 1; index >= 0; index--)
            DestroyTrackedInstance(spawned[index], false);
        spawned.Clear();
    }
}
