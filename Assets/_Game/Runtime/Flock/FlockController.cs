using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class FlockController : MonoBehaviour
{
    [Header("Flock")]
    [SerializeField] private FlockMovementController movementController;
    [SerializeField] private SheepMember[] startingMembers;

    [Header("Leader Ripple")]
    [Tooltip("保留多少秒的移动速度历史，供外围的羊延迟跟随领头羊。")]
    [SerializeField, Min(0.1f)] private float velocityHistorySeconds = 2f;

    [Header("Huddle")]
    [Tooltip("抱团时羊群半径缩小到原来的多少倍（狼嚎提示期间）。")]
    [SerializeField, Range(0.2f, 1f)] private float huddleCompactness = 0.55f;
    [Tooltip("松散 ↔ 抱团 的过渡速度（每秒变化量）。")]
    [SerializeField, Min(0.05f)] private float huddleTransitionSpeed = 1.2f;

    [Header("Flock Shape")]
    [Tooltip("羊群长轴转向当前移动方向的速度（弧度/秒）。")]
    [SerializeField, Min(0.1f)] private float shapeDirectionTurnSpeed = 4f;

    [Header("Turning")]
    [Tooltip("水平输入持续多久后才让整个羊群翻面；过滤轻点造成的全群转向波。")]
    [SerializeField, Min(0f)] private float facingIntentCommitDelay = 0.1f;

    [Header("Performance")]
    [Tooltip("邻居空间网格的单元尺寸；应不小于常用的羊间距。")]
    [SerializeField, Min(0.5f)] private float neighborCellSize = 3.2f;
    [SerializeField, Min(2)] private int mediumFlockThreshold = 50;
    [SerializeField, Min(2)] private int largeFlockThreshold = 120;
    [SerializeField, Range(1, 4)] private int mediumSteeringInterval = 2;
    [SerializeField, Range(1, 6)] private int largeSteeringInterval = 3;

    [Header("Recruited Idle Pacing")]
    [Tooltip("停下后允许左右踱步的非领头羊比例。")]
    [SerializeField, Range(0f, 1f)] private float idlePacingRatio = 0.2f;
    [SerializeField, Min(0)] private int maximumIdlePacingMembers = 24;

    private readonly List<SheepMember> members = new List<SheepMember>();
    private readonly Dictionary<Vector2Int, List<SheepMember>> memberGrid =
        new Dictionary<Vector2Int, List<SheepMember>>();
    private readonly List<List<SheepMember>> memberGridBucketPool = new List<List<SheepMember>>();
    private Vector2[] velocityHistory;
    private int historyHead = -1;
    private int historyCount;
    private bool deferMemberCountChanged;
    private bool memberGridDirty = true;
    private int fixedStepIndex;
    private Vector2 shapeForward = Vector2.right;
    private float pendingFacingDuration;
    private int facingIntentRevision;
    private bool pendingFacingLeft;
    private bool hasPendingFacing;
    private bool facingCommittedThisHold;
    private float manualCompactness = 1f;
    private float actionMemberSpeedMultiplier = 1f;

    public int RecruitedCount { get; private set; }
    public int MemberCount => members.Count;

    /// <summary>本局达到过的最大羊数（只升不降），供围栏门槛、阶段等使用。</summary>
    public int HighestMemberCount { get; private set; }

    public Vector2 Center => movementController != null
        ? (Vector2)movementController.transform.position
        : (Vector2)transform.position;

    public Vector2 MovementVelocity => movementController != null
        ? movementController.DesiredVelocity
        : Vector2.zero;

    public bool IsMoving =>
        movementController != null &&
        movementController.IsMoving;

    public bool HasMoveInput => movementController != null && movementController.HasMoveInput;
    public bool HasActiveFacingIntent { get; private set; }
    public bool FacingIntentLeft { get; private set; }
    public int FacingIntentRevision => facingIntentRevision;

    /// <summary>羊群椭圆长轴方向；停止移动后保留最后方向，避免外形突然转回水平。</summary>
    public Vector2 ShapeForward => shapeForward;

    public IReadOnlyList<SheepMember> Members => members;

    /// <summary>玩家直接操控的那只羊：贴着羊群中心移动，其余羊以它为起点向外扩散跟随。</summary>
    public SheepMember Leader { get; private set; }

    /// <summary>当前紧凑程度：1 = 松散的一大群，越小越抱团。由 SheepFlockAgent 读取来缩放半径。</summary>
    public float Compactness { get; private set; } = 1f;

    /// <summary>Q 收拢提供的紧凑度通道；1 为常态，越小排列越紧。</summary>
    public float ManualCompactness => manualCompactness;

    /// <summary>是否处于抱团状态（目标值；实际半径会平滑过渡）。</summary>
    public bool IsHuddling { get; private set; }
    public bool IsCompressed => IsHuddling || manualCompactness < 0.999f;

    /// <summary>整体速度倍率：中心移动速度和每只羊的最大速度 / 加速度都乘它。</summary>
    public float SpeedMultiplier { get; private set; } = 1f;
    public float MemberSpeedMultiplier => SpeedMultiplier * actionMemberSpeedMultiplier;

    public void SetSpeedMultiplier(float multiplier)
    {
        SpeedMultiplier = Mathf.Max(0.1f, multiplier);
        if (movementController != null)
        {
            movementController.SetSpeedMultiplier(SpeedMultiplier);
        }
    }

    public void SetManualCompactness(float compactness)
    {
        manualCompactness = Mathf.Clamp(compactness, 0.2f, 1f);
    }

    /// <summary>冲刺期间临时提高成员追随速度，使整群能跟上中心。</summary>
    public void SetActionMemberSpeedMultiplier(float multiplier)
    {
        actionMemberSpeedMultiplier = Mathf.Max(1f, multiplier);
    }

    public event Action<RecruitableSheep, int> SheepRecruited;
    public event Action<int> MemberCountChanged;
    public event Action<SheepMember> LeaderChanged;
    public event Action<bool> FenceChargeImpact;


    private void Awake()
    {
        movementController ??=
            GetComponent<FlockMovementController>();

        if (startingMembers == null)
            return;

        foreach (SheepMember member in startingMembers)
        {
            AddMember(member);
        }

        SelectLeader();
    }


    private void FixedUpdate()
    {
        fixedStepIndex = (fixedStepIndex + 1) & int.MaxValue;
        RecordMovementVelocity(MovementVelocity);
        UpdateFacingIntent();
        UpdateShapeForward();

        float huddleTarget = IsHuddling ? huddleCompactness : 1f;
        float targetCompactness = Mathf.Min(huddleTarget, manualCompactness);
        Compactness = Mathf.MoveTowards(
            Compactness,
            targetCompactness,
            huddleTransitionSpeed * Time.fixedDeltaTime);

        RebuildMemberGrid();
    }


    /// <summary>
    /// 速度大小继续使用历史形成柔性跟随，但方向始终采用当前输入/中心速度。
    /// 中心已经停下时丢弃历史尾巴，避免一次轻点在很久后传到外围。
    /// </summary>
    public Vector2 GetDelayedDriveVelocity(float secondsAgo)
    {
        Vector2 currentVelocity = MovementVelocity;
        Vector2 input = movementController != null ? movementController.MoveInput : Vector2.zero;
        if (input.sqrMagnitude <= 0.0001f && currentVelocity.sqrMagnitude <= 0.0001f)
            return Vector2.zero;

        Vector2 historicalVelocity = GetMovementVelocity(secondsAgo);
        float delayedSpeed = historicalVelocity.magnitude;
        if (delayedSpeed <= 0.0001f)
            return Vector2.zero;

        Vector2 currentDirection = currentVelocity.sqrMagnitude > 0.0001f
            ? currentVelocity.normalized
            : input.normalized;
        return currentDirection * delayedSpeed;
    }


    private void UpdateFacingIntent()
    {
        Vector2 input = movementController != null ? movementController.MoveInput : Vector2.zero;
        if (Mathf.Abs(input.x) < 0.15f)
        {
            HasActiveFacingIntent = false;
            hasPendingFacing = false;
            facingCommittedThisHold = false;
            pendingFacingDuration = 0f;
            return;
        }

        bool candidateFacingLeft = input.x < 0f;
        if (!hasPendingFacing || candidateFacingLeft != pendingFacingLeft)
        {
            pendingFacingLeft = candidateFacingLeft;
            pendingFacingDuration = 0f;
            hasPendingFacing = true;
            facingCommittedThisHold = false;
            HasActiveFacingIntent = false;
        }

        if (!facingCommittedThisHold)
        {
            pendingFacingDuration += Time.fixedDeltaTime;
            if (pendingFacingDuration < facingIntentCommitDelay)
                return;

            FacingIntentLeft = pendingFacingLeft;
            facingIntentRevision = (facingIntentRevision + 1) & int.MaxValue;
            facingCommittedThisHold = true;
        }

        HasActiveFacingIntent = true;
    }


    private void UpdateShapeForward()
    {
        Vector2 movementVelocity = MovementVelocity;
        if (movementVelocity.sqrMagnitude <= 0.0001f)
            return;

        Vector3 rotated = Vector3.RotateTowards(
            shapeForward,
            movementVelocity.normalized,
            shapeDirectionTurnSpeed * Time.fixedDeltaTime,
            0f);
        Vector2 nextForward = new Vector2(rotated.x, rotated.y);
        if (nextForward.sqrMagnitude > 0.0001f)
            shapeForward = nextForward.normalized;
    }


    /// <summary>
    /// 把指定位置周围网格里的成员写入复用缓冲区。调用方再按实际半径筛选，
    /// 避免每只羊逐帧扫描整个羊群。
    /// </summary>
    public void CollectNearbyMembers(Vector2 position, float radius, List<SheepMember> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        results.Clear();
        if (members.Count == 0)
            return;

        if (memberGridDirty)
            RebuildMemberGrid();

        float cellSize = Mathf.Max(0.5f, neighborCellSize);
        Vector2Int centerCell = PositionToCell(position, cellSize);
        int cellRange = Mathf.Max(0, Mathf.CeilToInt(Mathf.Max(0f, radius) / cellSize));
        for (int y = -cellRange; y <= cellRange; y++)
        {
            for (int x = -cellRange; x <= cellRange; x++)
            {
                Vector2Int cell = new Vector2Int(centerCell.x + x, centerCell.y + y);
                if (!memberGrid.TryGetValue(cell, out List<SheepMember> cellMembers))
                    continue;

                for (int index = 0; index < cellMembers.Count; index++)
                {
                    SheepMember member = cellMembers[index];
                    if (member != null)
                        results.Add(member);
                }
            }
        }
    }


    /// <summary>大羊群只错峰重算转向；实际位移仍在每个物理帧执行。</summary>
    public bool ShouldUpdateSteering(int simulationSlot, bool isLeader)
    {
        if (isLeader)
            return true;

        int interval = GetSteeringUpdateInterval();
        return interval <= 1 || (fixedStepIndex + Mathf.Max(0, simulationSlot)) % interval == 0;
    }


    public int GetSteeringUpdateInterval()
    {
        if (MemberCount <= mediumFlockThreshold)
            return 1;

        return MemberCount <= largeFlockThreshold
            ? Mathf.Max(1, mediumSteeringInterval)
            : Mathf.Max(1, largeSteeringInterval);
    }


    /// <summary>把 idle 移动平均分散在羊群里，并严格限制同时具备资格的数量。</summary>
    public bool CanIdlePace(int simulationSlot, bool isLeader)
    {
        if (isLeader || IsMoving || IsHuddling || maximumIdlePacingMembers <= 0)
            return false;

        int eligibleCount = Mathf.Max(0, MemberCount - (Leader != null ? 1 : 0));
        if (eligibleCount == 0)
            return false;

        int leaderSlot = Leader != null && Leader.Agent != null
            ? Leader.Agent.SimulationSlot
            : -1;
        int eligibleIndex = simulationSlot > leaderSlot ? simulationSlot - 1 : simulationSlot;
        if (eligibleIndex < 0 || eligibleIndex >= eligibleCount)
            return false;

        int budget = Mathf.Min(
            maximumIdlePacingMembers,
            Mathf.CeilToInt(eligibleCount * idlePacingRatio));
        if (budget <= 0)
            return false;

        int previousBucket = eligibleIndex * budget / eligibleCount;
        int nextBucket = (eligibleIndex + 1) * budget / eligibleCount;
        return nextBucket > previousBucket;
    }


    private void RebuildMemberGrid()
    {
        for (int index = 0; index < memberGridBucketPool.Count; index++)
            memberGridBucketPool[index].Clear();
        memberGrid.Clear();

        float cellSize = Mathf.Max(0.5f, neighborCellSize);
        int nextBucketIndex = 0;
        for (int index = 0; index < members.Count; index++)
        {
            SheepMember member = members[index];
            if (member == null)
                continue;

            Vector2 position = member.Agent != null
                ? member.Agent.Position
                : (Vector2)member.transform.position;
            Vector2Int cell = PositionToCell(position, cellSize);
            if (!memberGrid.TryGetValue(cell, out List<SheepMember> cellMembers))
            {
                if (nextBucketIndex < memberGridBucketPool.Count)
                {
                    cellMembers = memberGridBucketPool[nextBucketIndex];
                }
                else
                {
                    cellMembers = new List<SheepMember>(8);
                    memberGridBucketPool.Add(cellMembers);
                }

                nextBucketIndex++;
                memberGrid.Add(cell, cellMembers);
            }

            cellMembers.Add(member);
        }

        memberGridDirty = false;
    }


    private static Vector2Int PositionToCell(Vector2 position, float cellSize)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.y / cellSize));
    }


    /// <summary>让羊群抱团（true）或恢复松散（false）。</summary>
    public void SetHuddle(bool huddle)
    {
        IsHuddling = huddle;
    }


    /// <summary>
    /// 取 secondsAgo 秒之前的羊群移动速度。外围的羊用它来延迟跟随，形成从领头羊向外扩散的效果。
    /// </summary>
    public Vector2 GetMovementVelocity(float secondsAgo)
    {
        if (historyCount == 0 || secondsAgo <= 0f)
            return MovementVelocity;

        int stepsAgo = Mathf.RoundToInt(secondsAgo / Time.fixedDeltaTime);
        stepsAgo = Mathf.Clamp(stepsAgo, 0, historyCount - 1);
        int index = (historyHead - stepsAgo + velocityHistory.Length) % velocityHistory.Length;
        return velocityHistory[index];
    }


    private void RecordMovementVelocity(Vector2 velocity)
    {
        if (velocityHistory == null)
        {
            int capacity = Mathf.Max(2, Mathf.CeilToInt(velocityHistorySeconds / Time.fixedDeltaTime) + 1);
            velocityHistory = new Vector2[capacity];
        }

        historyHead = (historyHead + 1) % velocityHistory.Length;
        velocityHistory[historyHead] = velocity;
        historyCount = Mathf.Min(historyCount + 1, velocityHistory.Length);
    }


    /// <summary>选离羊群中心最近的成员当领头羊。</summary>
    private void SelectLeader()
    {
        SheepMember best = null;
        float bestDistance = float.MaxValue;
        Vector2 center = Center;

        foreach (SheepMember member in members)
        {
            if (member == null)
                continue;

            float distance = ((Vector2)member.transform.position - center).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = member;
            }
        }

        if (best == Leader)
            return;

        Leader = best;
        LeaderChanged?.Invoke(Leader);
    }


    public bool TryRecruit(RecruitableSheep sheep)
    {
        if (sheep == null || sheep.IsRecruited)
            return false;

        SheepMember member =
            sheep.GetComponent<SheepMember>();

        if (member == null)
        {
            member =
                sheep.gameObject.AddComponent<SheepMember>();
        }

        // 招募是一次完整状态提交：成员与招募计数都更新后，再统一通知 UI。
        // 否则订阅者会短暂读到“成员已增加、招募数还没增加”的半成品状态。
        deferMemberCountChanged = true;
        bool added;
        try
        {
            added = AddMember(member);
        }
        finally
        {
            deferMemberCountChanged = false;
        }

        if (!added)
            return false;

        // 原有招募完成逻辑
        sheep.CompleteRecruitment(this);

        RecruitedCount++;

        MemberCountChanged?.Invoke(MemberCount);

        SheepRecruited?.Invoke(
            sheep,
            RecruitedCount
        );


        // =========================
        // 羊羊图鉴
        // =========================

        SheepIdentity identity =
            member.GetComponent<SheepIdentity>();

        if (identity != null &&
            !string.IsNullOrEmpty(identity.SheepTypeId) &&
            SheepCollectionManager.Instance != null)
        {
            SheepCollectionManager.Instance
                .EncounterSheep(identity.SheepTypeId);
        }


        // =========================
        // Statistics
        // 累计收集的羊
        // =========================

        if (GameStatsManager.Instance != null)
        {
            // RegisterStat 可以重复调用。
            // 如果已经注册，只会更新显示信息，
            // 不会把原有统计数值清零。
            GameStatsManager.Instance.RegisterStat(
                "sheep_collected",
                "累计收集的羊",
                StatValueType.Integer,
                " 只",
                10
            );

            GameStatsManager.Instance.AddStat(
                "sheep_collected",
                1
            );
        }


        Debug.Log(
            $"{sheep.name} joined the flock. Current member count: {MemberCount}",
            sheep
        );

        return true;
    }


    public bool Remove(SheepMember member)
    {
        int index =
            members.IndexOf(member);

        if (index < 0)
            return false;

        if (member.Agent != null)
        {
            member.Agent.SetFlock(null);
        }

        member.Leave(this);

        members.RemoveAt(index);
        memberGridDirty = true;
        RefreshSimulationSlots(index);

        if (member == Leader)
        {
            SelectLeader();
        }

        MemberCountChanged?.Invoke(MemberCount);

        return true;
    }


    public void RejectCurrentMovement()
    {
        if (movementController != null)
        {
            movementController.RejectCurrentMovement();
        }
    }

    /// <summary>玩家按交互键冲撞围栏时上报一次反馈，由关卡决定镜头等全局表现。</summary>
    internal void ReportFenceChargeImpact(bool hardImpact)
    {
        FenceChargeImpact?.Invoke(hardImpact);
    }


    private bool AddMember(SheepMember member)
    {
        if (member == null ||
            members.Contains(member) ||
            !member.Join(this))
        {
            return false;
        }

        // 保留原有 SheepIdentity 逻辑
        if (member.GetComponent<SheepIdentity>() == null)
        {
            member.gameObject
                .AddComponent<SheepIdentity>();
        }

        SheepFlockAgent agent =
            member.GetComponent<SheepFlockAgent>();

        if (agent == null)
        {
            agent =
                member.gameObject
                    .AddComponent<SheepFlockAgent>();
        }

        members.Add(member);
        memberGridDirty = true;

        member.SetAgent(agent);

        agent.SetFlock(this);
        agent.SetSimulationSlot(members.Count - 1);

        if (members.Count > HighestMemberCount)
        {
            HighestMemberCount = members.Count;
        }

        if (Leader == null)
        {
            SelectLeader();
        }

        if (!deferMemberCountChanged)
        {
            MemberCountChanged?.Invoke(MemberCount);
        }

        return true;
    }


    private void RefreshSimulationSlots(int startIndex)
    {
        for (int index = Mathf.Max(0, startIndex); index < members.Count; index++)
        {
            SheepFlockAgent agent = members[index] != null ? members[index].Agent : null;
            agent?.SetSimulationSlot(index);
        }
    }


    private void OnValidate()
    {
        largeFlockThreshold = Mathf.Max(mediumFlockThreshold + 1, largeFlockThreshold);
        mediumSteeringInterval = Mathf.Max(1, mediumSteeringInterval);
        largeSteeringInterval = Mathf.Max(1, largeSteeringInterval);
        maximumIdlePacingMembers = Mathf.Max(0, maximumIdlePacingMembers);
        memberGridDirty = true;
    }
}
