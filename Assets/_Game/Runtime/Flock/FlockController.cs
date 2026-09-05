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

    [Header("Huddle")]
    [Tooltip("抱团时羊群半径缩小到原来的多少倍（狼嚎提示期间）。")]
    [SerializeField, Range(0.2f, 1f)] private float huddleCompactness = 0.55f;
    [Tooltip("松散 ↔ 抱团 的过渡速度（每秒变化量）。")]
    [SerializeField, Min(0.05f)] private float huddleTransitionSpeed = 1.2f;

    [Header("Flock Shape")]
    [Tooltip("羊群长轴转向当前移动方向的速度（弧度/秒）。")]
    [SerializeField, Min(0.1f)] private float shapeDirectionTurnSpeed = 4f;

    [Header("Member Separation")]
    [Tooltip("检查成员是否与主群断开的时间间隔。")]
    [SerializeField, Min(0.1f)] private float separationCheckInterval = 0.4f;
    [Tooltip("两只羊在这个距离内且中间没有阻挡时，视为属于同一群。")]
    [SerializeField, Min(0.5f)] private float mainGroupLinkDistance = 3.5f;
    [Tooltip("断开成员与主群最近成员超过这个距离后，才开始计算脱队时间。")]
    [SerializeField, Min(0.5f)] private float detachDistanceFromMainGroup = 7f;
    [Tooltip("持续断开且远离主群多久后正式脱队，避免短暂拉开造成误判。")]
    [SerializeField, Min(0f)] private float detachDelay = 1.5f;
    [Tooltip("正式脱队时向外散开的初速度。")]
    [SerializeField, Min(0f)] private float detachScatterSpeed = 1.2f;
    [Tooltip("阻断羊群连通性的实体层；留空时运行期使用 Blocking 层。")]
    [SerializeField] private LayerMask separationBlockingLayers;

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
    [Tooltip("停下后允许左右踱步的成员比例。")]
    [SerializeField, Range(0f, 1f)] private float idlePacingRatio = 0.2f;
    [SerializeField, Min(0)] private int maximumIdlePacingMembers = 24;

    private readonly List<SheepMember> members = new List<SheepMember>();
    private readonly Dictionary<Vector2Int, List<SheepMember>> memberGrid =
        new Dictionary<Vector2Int, List<SheepMember>>();
    private readonly List<List<SheepMember>> memberGridBucketPool = new List<List<SheepMember>>();
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
    private Vector2 groupActionDirection = Vector2.right;
    private Collider2D pendingGroupActionBlocker;
    private bool hasPendingGroupActionBlock;
    private SheepMember mainGroupAnchor;
    private float nextSeparationCheckTime;
    private readonly HashSet<SheepMember> connectedMainGroup = new HashSet<SheepMember>();
    private readonly Queue<SheepMember> connectivityTraversal = new Queue<SheepMember>();
    private readonly List<SheepMember> separationNeighborBuffer = new List<SheepMember>(32);
    private readonly Dictionary<SheepMember, float> disconnectedSince =
        new Dictionary<SheepMember, float>();
    private readonly List<SheepMember> detachingMembers = new List<SheepMember>();

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
    public bool IsGroupActionActive { get; private set; }
    public bool IsGroupActionHolding { get; private set; }
    public Vector2 GroupActionDirection => groupActionDirection;
    public SheepMember MainGroupAnchor => mainGroupAnchor;

    /// <summary>羊群椭圆长轴方向；停止移动后保留最后方向，避免外形突然转回水平。</summary>
    public Vector2 ShapeForward => shapeForward;

    public IReadOnlyList<SheepMember> Members => members;

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

    /// <summary>整群主动动作期间临时提高成员速度，使所有羊与中心同步。</summary>
    public void SetActionMemberSpeedMultiplier(float multiplier)
    {
        actionMemberSpeedMultiplier = Mathf.Max(1f, multiplier);
    }

    internal void SetGroupActionState(bool active, bool holding, Vector2 forwardDirection)
    {
        bool actionStarted = active && !IsGroupActionActive;
        if (!active || !IsGroupActionActive)
        {
            pendingGroupActionBlocker = null;
            hasPendingGroupActionBlock = false;
        }

        if (forwardDirection.sqrMagnitude > 0.0001f)
            groupActionDirection = forwardDirection.normalized;

        IsGroupActionActive = active;
        IsGroupActionHolding = active && holding;

        if (actionStarted)
        {
            // 主动动作只使用左右翻面：水平分量决定朝向；近似竖直时保留上一次朝向。
            if (Mathf.Abs(groupActionDirection.x) >= 0.15f)
                FacingIntentLeft = groupActionDirection.x < 0f;

            facingIntentRevision = (facingIntentRevision + 1) & int.MaxValue;
            HasActiveFacingIntent = true;
            hasPendingFacing = false;
            facingCommittedThisHold = true;
            pendingFacingDuration = 0f;
        }
    }

    internal void ReportGroupActionMemberBlocked(Collider2D blocker)
    {
        if (IsGroupActionActive && !IsGroupActionHolding && !hasPendingGroupActionBlock)
        {
            pendingGroupActionBlocker = blocker;
            hasPendingGroupActionBlock = true;
        }
    }

    internal bool TryConsumeGroupActionMemberBlock(out Collider2D blocker)
    {
        if (!hasPendingGroupActionBlock)
        {
            blocker = null;
            return false;
        }

        blocker = pendingGroupActionBlocker;
        pendingGroupActionBlocker = null;
        hasPendingGroupActionBlock = false;
        return true;
    }

    internal bool HasMovementLockedMembers()
    {
        for (int index = 0; index < members.Count; index++)
        {
            SheepFlockAgent agent = members[index] != null ? members[index].Agent : null;
            if (agent != null && agent.IsMovementLocked)
                return true;
        }

        return false;
    }

    public event Action<RecruitableSheep, int> SheepRecruited;
    public event Action<int> MemberCountChanged;
    public event Action<int> MembersSeparated;
    public event Action<bool> FenceChargeImpact;


    private void Awake()
    {
        movementController ??=
            GetComponent<FlockMovementController>();

        if (separationBlockingLayers.value == 0)
            separationBlockingLayers = MovementBlocking.DefaultMask();

        if (startingMembers == null)
            return;

        foreach (SheepMember member in startingMembers)
        {
            AddMember(member);
        }
    }


    private void FixedUpdate()
    {
        fixedStepIndex = (fixedStepIndex + 1) & int.MaxValue;
        UpdateFacingIntent();
        UpdateShapeForward();

        float huddleTarget = IsHuddling ? huddleCompactness : 1f;
        float targetCompactness = Mathf.Min(huddleTarget, manualCompactness);
        Compactness = Mathf.MoveTowards(
            Compactness,
            targetCompactness,
            huddleTransitionSpeed * Time.fixedDeltaTime);

        RebuildMemberGrid();
        UpdateMemberSeparation();
    }

    private void UpdateFacingIntent()
    {
        // E 动作从开始到结束都锁定同一份水平朝向，避免竖直冲刺时各成员
        // 按自己的微小横向位移反复翻面。
        if (IsGroupActionActive)
        {
            HasActiveFacingIntent = true;
            hasPendingFacing = false;
            facingCommittedThisHold = true;
            pendingFacingDuration = 0f;
            return;
        }

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
        Vector2 movementVelocity = IsGroupActionActive
            ? groupActionDirection
            : MovementVelocity;
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
    public bool ShouldUpdateSteering(int simulationSlot)
    {
        if (IsGroupActionActive)
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
    public bool CanIdlePace(int simulationSlot)
    {
        if (IsMoving || IsHuddling || IsGroupActionActive || maximumIdlePacingMembers <= 0)
            return false;

        int eligibleCount = MemberCount;
        if (eligibleCount == 0)
            return false;

        int eligibleIndex = simulationSlot;
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

        ScatteredSheep scattered = sheep.GetComponent<ScatteredSheep>();
        bool isReturningMember = scattered != null && scattered.IsScattered;

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

        if (!isReturningMember)
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

        if (!isReturningMember && GameStatsManager.Instance != null)
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
        disconnectedSince.Remove(member);
        if (mainGroupAnchor == member)
            mainGroupAnchor = FindReplacementMainGroupAnchor();
        memberGridDirty = true;
        RefreshSimulationSlots(index);

        if (!deferMemberCountChanged)
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
        if (mainGroupAnchor == null)
            mainGroupAnchor = member;
        memberGridDirty = true;

        member.SetAgent(agent);

        agent.SetFlock(this);
        agent.SetSimulationSlot(members.Count - 1);

        if (members.Count > HighestMemberCount)
        {
            HighestMemberCount = members.Count;
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


    private void UpdateMemberSeparation()
    {
        if (Time.time < nextSeparationCheckTime)
            return;

        nextSeparationCheckTime = Time.time + Mathf.Max(0.1f, separationCheckInterval);
        EvaluateMemberSeparation(Time.time);
    }


    private void EvaluateMemberSeparation(float currentTime)
    {
        if (members.Count <= 1 || IsGroupActionActive)
        {
            disconnectedSince.Clear();
            return;
        }

        if (mainGroupAnchor == null || mainGroupAnchor.Flock != this)
            mainGroupAnchor = FindReplacementMainGroupAnchor();
        if (mainGroupAnchor == null)
            return;

        CollectConnectedMainGroup();

        float effectiveDetachDistance = Mathf.Max(mainGroupLinkDistance, detachDistanceFromMainGroup);
        detachingMembers.Clear();
        for (int index = 0; index < members.Count; index++)
        {
            SheepMember member = members[index];
            if (member == null || connectedMainGroup.Contains(member))
            {
                if (member != null)
                    disconnectedSince.Remove(member);
                continue;
            }

            if (IsNearConnectedMainGroup(member, effectiveDetachDistance))
            {
                disconnectedSince.Remove(member);
                continue;
            }

            if (!disconnectedSince.TryGetValue(member, out float separatedAt))
            {
                disconnectedSince.Add(member, currentTime);
                separatedAt = currentTime;
            }

            if (currentTime - separatedAt >= Mathf.Max(0f, detachDelay))
                detachingMembers.Add(member);
        }

        DetachSeparatedMembers();
    }


    private void CollectConnectedMainGroup()
    {
        connectedMainGroup.Clear();
        connectivityTraversal.Clear();
        connectedMainGroup.Add(mainGroupAnchor);
        connectivityTraversal.Enqueue(mainGroupAnchor);

        float linkDistance = Mathf.Max(0.5f, mainGroupLinkDistance);
        float linkDistanceSquared = linkDistance * linkDistance;
        while (connectivityTraversal.Count > 0 && connectedMainGroup.Count < members.Count)
        {
            SheepMember current = connectivityTraversal.Dequeue();
            Vector2 currentPosition = GetMemberPosition(current);
            CollectNearbyMembers(currentPosition, linkDistance, separationNeighborBuffer);

            for (int index = 0; index < separationNeighborBuffer.Count; index++)
            {
                SheepMember candidate = separationNeighborBuffer[index];
                if (candidate == null
                    || candidate.Flock != this
                    || connectedMainGroup.Contains(candidate))
                    continue;

                Vector2 candidatePosition = GetMemberPosition(candidate);
                if ((candidatePosition - currentPosition).sqrMagnitude > linkDistanceSquared)
                    continue;
                if (MovementBlocking.IsLineBlocked(
                        currentPosition,
                        candidatePosition,
                        separationBlockingLayers))
                    continue;

                connectedMainGroup.Add(candidate);
                connectivityTraversal.Enqueue(candidate);
            }
        }
    }


    private bool IsNearConnectedMainGroup(SheepMember member, float distance)
    {
        Vector2 memberPosition = GetMemberPosition(member);
        float distanceSquared = distance * distance;
        CollectNearbyMembers(memberPosition, distance, separationNeighborBuffer);
        for (int index = 0; index < separationNeighborBuffer.Count; index++)
        {
            SheepMember candidate = separationNeighborBuffer[index];
            if (candidate == null || !connectedMainGroup.Contains(candidate))
                continue;
            if ((GetMemberPosition(candidate) - memberPosition).sqrMagnitude <= distanceSquared)
                return true;
        }

        return false;
    }


    private static Vector2 GetMemberPosition(SheepMember member)
    {
        return member.Agent != null
            ? member.Agent.Position
            : (Vector2)member.transform.position;
    }


    private void DetachSeparatedMembers()
    {
        if (detachingMembers.Count == 0)
            return;

        int detachedCount = 0;
        bool wasDeferring = deferMemberCountChanged;
        deferMemberCountChanged = true;
        try
        {
            for (int index = 0; index < detachingMembers.Count; index++)
            {
                SheepMember member = detachingMembers[index];
                if (member == null || member == mainGroupAnchor || member.Flock != this)
                    continue;

                Vector2 awayFromMainGroup = (Vector2)member.transform.position - (Vector2)mainGroupAnchor.transform.position;
                Vector2 direction = awayFromMainGroup.sqrMagnitude > 0.0001f
                    ? awayFromMainGroup.normalized
                    : UnityEngine.Random.insideUnitCircle.normalized;
                direction = (direction + UnityEngine.Random.insideUnitCircle * 0.45f).normalized;
                if (direction.sqrMagnitude <= 0.0001f)
                    direction = Vector2.right;

                ScatteredSheep.Scatter(
                    member,
                    direction * detachScatterSpeed * UnityEngine.Random.Range(0.8f, 1.2f));
                detachedCount++;
            }
        }
        finally
        {
            deferMemberCountChanged = wasDeferring;
        }

        detachingMembers.Clear();
        if (detachedCount <= 0)
            return;

        memberGridDirty = true;
        if (!wasDeferring)
            MemberCountChanged?.Invoke(MemberCount);
        MembersSeparated?.Invoke(detachedCount);
    }


    private SheepMember FindReplacementMainGroupAnchor()
    {
        SheepMember closest = null;
        float closestDistance = float.PositiveInfinity;
        Vector2 center = Center;
        for (int index = 0; index < members.Count; index++)
        {
            SheepMember candidate = members[index];
            if (candidate == null)
                continue;

            float distance = ((Vector2)candidate.transform.position - center).sqrMagnitude;
            if (distance < closestDistance)
            {
                closest = candidate;
                closestDistance = distance;
            }
        }

        return closest;
    }


    private void OnValidate()
    {
        largeFlockThreshold = Mathf.Max(mediumFlockThreshold + 1, largeFlockThreshold);
        mediumSteeringInterval = Mathf.Max(1, mediumSteeringInterval);
        largeSteeringInterval = Mathf.Max(1, largeSteeringInterval);
        maximumIdlePacingMembers = Mathf.Max(0, maximumIdlePacingMembers);
        separationCheckInterval = Mathf.Max(0.1f, separationCheckInterval);
        mainGroupLinkDistance = Mathf.Max(0.5f, mainGroupLinkDistance);
        detachDistanceFromMainGroup = Mathf.Max(mainGroupLinkDistance, detachDistanceFromMainGroup);
        detachDelay = Mathf.Max(0f, detachDelay);
        detachScatterSpeed = Mathf.Max(0f, detachScatterSpeed);
        memberGridDirty = true;
    }
}
