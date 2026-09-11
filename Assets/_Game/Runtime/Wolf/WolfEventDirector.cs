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
/// 生长空挡(12~16s) → 狼嚎提示(3s) → 开始攻击(由狼决定) → 事件结束 → 重新计时。
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

    [Header("Rhythm (seconds)")]
    [Tooltip("生长空挡的最短时长。")]
    [SerializeField, Min(0f)] private float calmDurationMin = 12f;
    [Tooltip("生长空挡的最长时长。")]
    [SerializeField, Min(0f)] private float calmDurationMax = 16f;
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

    [Header("Pack Attacks (多狼协作)")]
    [Tooltip("多狼编队执行器；为空时每轮只放一只狼。")]
    [SerializeField] private WolfFormationRunner formationRunner;
    [Tooltip("可选的编队列表。每轮攻击时在满足回合 / 羊数门槛的条目里按权重抽一个；没有可用条目就放一只狼。")]
    [SerializeField] private WolfFormationEntry[] formations = Array.Empty<WolfFormationEntry>();

    [Header("Attack Schedule (正式节奏表)")]
    [Tooltip("指定后由阶段解锁、威胁强度与新鲜度共同选择攻击，忽略上面的 formations 列表。")]
    [SerializeField] private WolfAttackSchedule schedule;
    [Tooltip("节奏表动态生成编队时用的参数模板：普通狼 / 长狼 prefab、间距、时长等；type 字段会被覆盖。")]
    [SerializeField] private WolfFormation formationTemplate = new WolfFormation();

    [Header("Control")]
    [SerializeField] private bool runOnStart = true;

    private float phaseTimer;
    private float phaseDuration;
    private Wolf activeWolf;
    private bool isRunning;
    private bool formationActive;
    private readonly WolfLossTracker lossTracker = new WolfLossTracker();
    private readonly WolfAttackSelectionState attackSelection = new WolfAttackSelectionState();

    public WolfEventPhase Phase { get; private set; } = WolfEventPhase.Dormant;
    public int RoundIndex { get; private set; }
    public bool IsRunning => isRunning;
    public int RequiredMemberCount => requiredMemberCount;
    public int CurrentMemberCount => flock != null ? flock.MemberCount : 0;
    /// <summary>这一轮攻击的名字（独狼 / 编队名），攻击阶段之外为空。</summary>
    public string CurrentAttackName { get; private set; } = string.Empty;
    /// <summary>按节奏表抽到的攻击方式；没用节奏表或攻击阶段之外为 null。</summary>
    public WolfAttackType? CurrentAttackType { get; private set; }
    /// <summary>本局按攻击方式统计的损失（节奏表模式下维护）。</summary>
    public WolfLossTracker LossTracker => lossTracker;
    public WolfAttackSchedule Schedule => schedule;
    public AudioClip HowlClip => howlClip;
    /// <summary>边缘来袭提示统一使用普通狼头像，避免长狼的横向皮肤被压成细条。</summary>
    public Sprite ThreatIndicatorSprite => spawner != null && spawner.WolfPrefab != null
        ? spawner.WolfPrefab.ThreatIndicatorSprite
        : null;
    /// <summary>当前羊数对应的节奏表阶段下标；没用节奏表为 -1。</summary>
    public int CurrentStageIndex => schedule != null ? schedule.GetStageIndex(CurrentMemberCount) : -1;
    /// <summary>本局已经解锁的最高狼袭阶段；阶段不会因暂时减员降低。</summary>
    public int HighestStageIndex => schedule != null ? attackSelection.HighestStageIndex : -1;

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
    /// <summary>节奏表抽到攻击方式时触发（在放狼之前）。</summary>
    public event Action<WolfAttackType> AttackChosen;
    /// <summary>吓跑模式的狼掉头逃跑时触发。</summary>
    public event Action<Wolf> WolfScared;
    /// <summary>吓跑的狼被羊群踹飞时触发。</summary>
    public event Action<Wolf> WolfKicked;

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

        if (formationRunner == null)
        {
            formationRunner = GetComponent<WolfFormationRunner>();
        }
        if (formationRunner != null)
        {
            formationRunner.WolfLaunched += HandleFormationWolfLaunched;
        }
    }

    private void OnDestroy()
    {
        if (formationRunner != null)
        {
            formationRunner.WolfLaunched -= HandleFormationWolfLaunched;
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
        lossTracker.Clear();
        attackSelection.Reset();
        attackSelection.ObserveStage(schedule, CurrentStageIndex);
        EnterPhase(HasReachedStartCondition() ? WolfEventPhase.Calm : WolfEventPhase.Dormant);
    }

    /// <summary>停止节奏（例如游戏结束）。已在场上的狼不受影响。</summary>
    public void Stop()
    {
        isRunning = false;
        if (activeWolf != null)
        {
            activeWolf.Finished -= HandleWolfFinished;
            activeWolf = null;
        }
        if (formationRunner != null)
        {
            formationRunner.Stop();
        }
        formationActive = false;
        CurrentAttackName = string.Empty;
        CurrentAttackType = null;
    }

    private bool HasReachedStartCondition()
    {
        return requiredMemberCount <= 0 || flock == null || flock.MemberCount >= requiredMemberCount;
    }

    private void Update()
    {
        if (!isRunning || Time.timeScale == 0f)
            return;

        phaseTimer += Time.deltaTime;
        UpdateStageTransitions();

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
                if (IsAttackFinished())
                {
                    EnterPhase(WolfEventPhase.Retreat);
                }
                else if (phaseTimer >= attackTimeout)
                {
                    AbortTimedOutAttack();
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
                break;

            case WolfEventPhase.Calm:
                RoundIndex++;
                if (RoundIndex == 1 && firstCalmDurationOverride > 0f)
                {
                    phaseDuration = firstCalmDurationOverride;
                }
                else if (schedule != null)
                {
                    Vector2 range = schedule.GetCalmDurationRangeForStage(attackSelection.HighestStageIndex);
                    phaseDuration = UnityEngine.Random.Range(range.x, range.y);
                }
                else
                {
                    phaseDuration = UnityEngine.Random.Range(calmDurationMin, calmDurationMax);
                }
                break;

            case WolfEventPhase.Howl:
                phaseDuration = howlDuration;
                PlayHowl();
                break;

            case WolfEventPhase.Attack:
                phaseDuration = attackTimeout;
                ReleaseAttack();
                break;

            case WolfEventPhase.Retreat:
                phaseDuration = retreatDuration;
                // 攻击超时兜底进来时编队可能还在放狼，必须一起停掉，否则空挡阶段还会继续出狼。
                if (formationRunner != null)
                {
                    formationRunner.Stop();
                }
                formationActive = false;
                CurrentAttackName = string.Empty;
                CurrentAttackType = null;
                break;
        }

        PhaseChanged?.Invoke(phase);
    }

    /// <summary>只记录本局达到过的最高阶段；升级不会打断当前狼袭或空挡。</summary>
    private void UpdateStageTransitions()
    {
        if (schedule == null)
            return;

        attackSelection.ObserveStage(schedule, CurrentStageIndex);
    }

    private bool IsAttackFinished()
    {
        bool singleDone = activeWolf == null;
        bool formationDone = !formationActive || formationRunner == null || !formationRunner.IsRunning;
        return singleDone && formationDone;
    }

    private void AbortTimedOutAttack()
    {
        if (activeWolf != null)
            activeWolf.ForceRetreat();
        if (formationRunner != null)
            formationRunner.AbortAndRetreat();
        formationActive = false;
        Debug.LogWarning($"Wolf attack timed out after {attackTimeout:0.0}s and was forced to retreat.", this);
    }

    private void ReleaseAttack()
    {
        if (spawner == null)
        {
            Debug.LogWarning("WolfEventDirector has no WolfSpawner; skipping attack.", this);
            return;
        }

        if (schedule != null)
        {
            ReleaseScheduledAttack();
            return;
        }

        WolfFormation formation = PickFormation();
        if (formation != null && formation.type != WolfFormationType.Single && formationRunner != null)
        {
            formationActive = true;
            CurrentAttackName = formation.DisplayName;
            formationRunner.Play(formation);
            return;
        }

        CurrentAttackName = WolfFormation.DefaultName(WolfFormationType.Single);
        activeWolf = spawner.SpawnWolf();
        if (activeWolf != null)
        {
            activeWolf.Finished += HandleWolfFinished;
            WolfReleased?.Invoke(activeWolf);
        }
    }

    /// <summary>
    /// 节奏表模式：阶段解锁攻击，导演先抽威胁强度，再在同档中按基础权重与新鲜度抽具体攻击。
    /// 阶段首次解锁时优先展示新攻击；大型袭击后安排基础袭击恢复。
    /// </summary>
    private void ReleaseScheduledAttack()
    {
        int members = CurrentMemberCount;
        attackSelection.ObserveStage(schedule, schedule.GetStageIndex(members));
        WolfAttackType? picked = attackSelection.Pick(schedule, () => UnityEngine.Random.value);

        if (!picked.HasValue)
        {
            // 这个阶段不放狼（例如教学阶段）：攻击阶段会立刻结束。
            CurrentAttackName = string.Empty;
            CurrentAttackType = null;
            return;
        }

        WolfAttackType type = picked.Value;
        CurrentAttackType = type;
        CurrentAttackName = WolfAttackTypes.DisplayName(type);
        AttackChosen?.Invoke(type);

        // 狼的速度跟着羊群倍率涨，但涨得更快。
        float flockScale = flock != null ? flock.SpeedMultiplier : 1f;
        spawner.SetSpeedScale(Mathf.Pow(Mathf.Max(0.1f, flockScale), schedule.WolfSpeedExponent));

        if (type == WolfAttackType.StraightWolf || type == WolfAttackType.SmartWolf)
        {
            bool scared = members > schedule.ScareThreshold;
            activeWolf = spawner.SpawnWolf(type == WolfAttackType.SmartWolf, scared);
            if (activeWolf != null)
            {
                activeWolf.AttackType = type;
                activeWolf.Finished += HandleWolfFinished;
                activeWolf.Attacked += HandleWolfAttackedForStats;
                activeWolf.Scared += HandleWolfScared;
                activeWolf.Kicked += HandleWolfKicked;
                WolfReleased?.Invoke(activeWolf);
            }
            return;
        }

        if (formationRunner == null)
        {
            Debug.LogWarning("WolfEventDirector has no WolfFormationRunner; falling back to a single wolf.", this);
            activeWolf = spawner.SpawnWolf();
            if (activeWolf != null)
            {
                activeWolf.AttackType = type;
                activeWolf.Finished += HandleWolfFinished;
                activeWolf.Attacked += HandleWolfAttackedForStats;
                WolfReleased?.Invoke(activeWolf);
            }
            return;
        }

        WolfFormation formation = formationTemplate.CloneAs(WolfAttackTypes.ToFormationType(type));
        if (formation.wolfPrefab == null)
            formation.wolfPrefab = spawner.WolfPrefab;
        formationActive = true;
        formationRunner.Play(formation);
    }

    private void HandleWolfAttackedForStats(Wolf wolf, WolfAttackResult result)
    {
        if (wolf == null || result.CapturedSheep == null)
            return;

        lossTracker.RecordTaken(wolf.AttackType, wolf.IsLongWolf);
    }

    private void HandleWolfScared(Wolf wolf)
    {
        WolfScared?.Invoke(wolf);
    }

    private void HandleWolfKicked(Wolf wolf)
    {
        WolfKicked?.Invoke(wolf);
    }

    /// <summary>在满足回合 / 羊数门槛的编队里按权重抽一个；没有就返回 null（放一只狼）。</summary>
    private WolfFormation PickFormation()
    {
        if (formations == null || formations.Length == 0)
            return null;

        float totalWeight = 0f;
        foreach (WolfFormationEntry entry in formations)
        {
            if (IsEligible(entry))
                totalWeight += entry.weight;
        }
        if (totalWeight <= 0f)
            return null;

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        foreach (WolfFormationEntry entry in formations)
        {
            if (!IsEligible(entry))
                continue;
            roll -= entry.weight;
            if (roll <= 0f)
                return entry.formation;
        }
        return null;
    }

    private bool IsEligible(WolfFormationEntry entry)
    {
        return entry != null
            && entry.formation != null
            && entry.weight > 0f
            && RoundIndex >= entry.minRound
            && CurrentMemberCount >= entry.minMemberCount;
    }

    private void HandleFormationWolfLaunched(Wolf wolf)
    {
        ApplyLongWolfStageScale(wolf);
        if (wolf != null && CurrentAttackType.HasValue)
        {
            wolf.AttackType = CurrentAttackType.Value;
            wolf.Attacked += HandleWolfAttackedForStats;
        }
        WolfReleased?.Invoke(wolf);
    }

    private void ApplyLongWolfStageScale(Wolf wolf)
    {
        if (schedule == null || wolf == null)
            return;

        LongWolfSweep sweep = wolf.GetComponent<LongWolfSweep>();
        if (sweep != null)
            sweep.SetRuntimeWidthMultiplier(schedule.GetLongWolfWidthMultiplierForStage(attackSelection.HighestStageIndex));
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
