using System;
using UnityEngine;

/// <summary>狼群进攻节奏的阶段。</summary>
public enum WolfEventPhase
{
    /// <summary>生长空挡：羊群平静，15~20 秒。</summary>
    Calm,
    /// <summary>狼嚎提示：3 秒，提示玩家狼要来了。</summary>
    Howl,
    /// <summary>开始攻击：一只狼来啦，时长由事件本身决定。</summary>
    Attack,
    /// <summary>事件结束：攻击结束、狼跑路，之后重新开始计时。</summary>
    Retreat,
    /// <summary>蛰伏：羊群还没达到人数门槛，狼群尚未出现，不计时。</summary>
    Dormant
}

/// <summary>
/// 狼群进攻节奏控制：
/// 生长空挡(15~20s) → 狼嚎提示(3s) → 开始攻击(由狼决定) → 事件结束 → 重新计时。
/// 每一轮通过 <see cref="WolfSpawner.SpawnWolf"/> 放出一只狼，自身不负责狼的行为。
/// </summary>
[DisallowMultipleComponent]
public sealed class WolfEventDirector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WolfSpawner spawner;
    [SerializeField] private FlockController flock;

    [Header("Start Condition")]
    [Tooltip("羊群达到这么多只之后才开始狼群倒计时；0 表示一开始就计时。")]
    [SerializeField, Min(0)] private int requiredMemberCount = 6;

    [Header("Huddle")]
    [Tooltip("狼嚎提示期间让羊群抱团。")]
    [SerializeField] private bool huddleDuringHowl = true;
    [Tooltip("狼冲进来的攻击阶段是否继续抱团；关掉则狼一出现羊群就散开。")]
    [SerializeField] private bool huddleDuringAttack = true;

    [Header("Rhythm (seconds)")]
    [Tooltip("生长空挡的最短时长。")]
    [SerializeField, Min(0f)] private float calmDurationMin = 15f;
    [Tooltip("生长空挡的最长时长。")]
    [SerializeField, Min(0f)] private float calmDurationMax = 20f;
    [Tooltip("狼嚎提示时长。")]
    [SerializeField, Min(0f)] private float howlDuration = 3f;
    [Tooltip("“攻击结束 狼跑路”停留多久再重新计时。")]
    [SerializeField, Min(0f)] private float retreatDuration = 1.5f;
    [Tooltip("攻击阶段的兜底上限，防止狼卡住导致节奏停摆。")]
    [SerializeField, Min(1f)] private float attackTimeout = 25f;
    [Tooltip("第一轮生长空挡是否缩短（方便测试）。0 表示按正常时长。")]
    [SerializeField, Min(0f)] private float firstCalmDurationOverride;

    [Header("Audio")]
    [Tooltip("狼嚎提示时播放的音效，走 AudioManager.PlaySFX；没有 AudioManager 时用本地 AudioSource。")]
    [SerializeField] private AudioClip howlClip;
    [SerializeField] private AudioSource fallbackAudioSource;

    [Header("Control")]
    [SerializeField] private bool runOnStart = true;

    private float phaseTimer;
    private float phaseDuration;
    private Wolf activeWolf;
    private bool isRunning;

    public WolfEventPhase Phase { get; private set; } = WolfEventPhase.Dormant;
    public int RoundIndex { get; private set; }
    public bool IsRunning => isRunning;
    public int RequiredMemberCount => requiredMemberCount;
    public int CurrentMemberCount => flock != null ? flock.MemberCount : 0;

    /// <summary>当前阶段剩余秒数；攻击 / 蛰伏阶段返回 -1（没有倒计时）。</summary>
    public float PhaseTimeRemaining =>
        Phase == WolfEventPhase.Attack || Phase == WolfEventPhase.Dormant
            ? -1f
            : Mathf.Max(0f, phaseDuration - phaseTimer);

    /// <summary>当前阶段进度 0~1；攻击 / 蛰伏阶段恒为 0。</summary>
    public float PhaseProgress =>
        Phase == WolfEventPhase.Attack || Phase == WolfEventPhase.Dormant || phaseDuration <= 0f
            ? 0f
            : Mathf.Clamp01(phaseTimer / phaseDuration);

    public event Action<WolfEventPhase> PhaseChanged;
    public event Action<Wolf> WolfReleased;

    private void Awake()
    {
        if (spawner == null)
        {
            spawner = GetComponent<WolfSpawner>();
        }

        if (flock == null && spawner != null)
        {
            flock = spawner.Flock;
        }

        // 节奏由本组件掌控，关掉生成器自己的定时。
        if (spawner != null)
        {
            spawner.StopSpawning();
        }
    }

    private void Start()
    {
        if (runOnStart)
        {
            Run();
        }
    }

    public void Run()
    {
        if (isRunning)
            return;

        isRunning = true;
        RoundIndex = 0;
        EnterPhase(HasReachedStartCondition() ? WolfEventPhase.Calm : WolfEventPhase.Dormant);
    }

    /// <summary>停止节奏（例如游戏结束）。已在场上的狼不受影响。</summary>
    public void Stop()
    {
        isRunning = false;
        SetHuddle(false);
        if (activeWolf != null)
        {
            activeWolf.Finished -= HandleWolfFinished;
            activeWolf = null;
        }
    }

    private bool HasReachedStartCondition()
    {
        return requiredMemberCount <= 0 || flock == null || flock.MemberCount >= requiredMemberCount;
    }

    private void SetHuddle(bool huddle)
    {
        if (flock != null)
        {
            flock.SetHuddle(huddle);
        }
    }

    private void Update()
    {
        if (!isRunning || Time.timeScale == 0f)
            return;

        phaseTimer += Time.deltaTime;

        switch (Phase)
        {
            case WolfEventPhase.Dormant:
                if (HasReachedStartCondition())
                {
                    EnterPhase(WolfEventPhase.Calm);
                }
                break;

            case WolfEventPhase.Calm:
                if (phaseTimer >= phaseDuration)
                {
                    EnterPhase(WolfEventPhase.Howl);
                }
                break;

            case WolfEventPhase.Howl:
                if (phaseTimer >= phaseDuration)
                {
                    EnterPhase(WolfEventPhase.Attack);
                }
                break;

            case WolfEventPhase.Attack:
                if (activeWolf == null || phaseTimer >= attackTimeout)
                {
                    EnterPhase(WolfEventPhase.Retreat);
                }
                break;

            case WolfEventPhase.Retreat:
                if (phaseTimer >= phaseDuration)
                {
                    EnterPhase(WolfEventPhase.Calm);
                }
                break;
        }
    }

    private void EnterPhase(WolfEventPhase phase)
    {
        Phase = phase;
        phaseTimer = 0f;

        switch (phase)
        {
            case WolfEventPhase.Dormant:
                phaseDuration = 0f;
                SetHuddle(false);
                break;

            case WolfEventPhase.Calm:
                RoundIndex++;
                phaseDuration = RoundIndex == 1 && firstCalmDurationOverride > 0f
                    ? firstCalmDurationOverride
                    : UnityEngine.Random.Range(calmDurationMin, calmDurationMax);
                SetHuddle(false);
                break;

            case WolfEventPhase.Howl:
                phaseDuration = howlDuration;
                SetHuddle(huddleDuringHowl);
                PlayHowl();
                break;

            case WolfEventPhase.Attack:
                phaseDuration = attackTimeout;
                SetHuddle(huddleDuringHowl && huddleDuringAttack);
                ReleaseWolf();
                break;

            case WolfEventPhase.Retreat:
                phaseDuration = retreatDuration;
                SetHuddle(false);
                break;
        }

        PhaseChanged?.Invoke(phase);
    }

    private void ReleaseWolf()
    {
        if (spawner == null)
        {
            Debug.LogWarning("WolfEventDirector has no WolfSpawner; skipping attack.", this);
            return;
        }

        activeWolf = spawner.SpawnWolf();
        if (activeWolf != null)
        {
            activeWolf.Finished += HandleWolfFinished;
            WolfReleased?.Invoke(activeWolf);
        }
    }

    private void HandleWolfFinished(Wolf wolf)
    {
        if (wolf == activeWolf)
        {
            activeWolf = null;
        }
    }

    private void PlayHowl()
    {
        if (howlClip == null)
            return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(howlClip);
            return;
        }

        if (fallbackAudioSource != null)
        {
            fallbackAudioSource.PlayOneShot(howlClip);
        }
    }

    private void OnValidate()
    {
        calmDurationMax = Mathf.Max(calmDurationMax, calmDurationMin);
    }
}
