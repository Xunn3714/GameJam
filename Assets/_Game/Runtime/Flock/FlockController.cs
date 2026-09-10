using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class FlockController : MonoBehaviour
{
    [Header("Flock")]
    [SerializeField] private FlockMovementController movementController;
    [SerializeField] private SheepMember[] startingMembers;

    [Header("Flock Shape")]
    [Tooltip("羊群长轴转向当前移动方向的速度（弧度/秒）。")]
    [SerializeField, Min(0.1f)] private float shapeDirectionTurnSpeed = 4f;

    [Header("Member Separation")]
    [Tooltip("检查成员是否仍处于羊群成员场内的时间间隔。")]
    [SerializeField, Min(0.1f)] private float separationCheckInterval = 0.4f;
    [Tooltip("成员场不会小于这个半径。这里只用于成员资格判定，不会施加吸引力。")]
    [FormerlySerializedAs("detachDistanceFromMainGroup")]
    [SerializeField, Min(0.5f)] private float minimumMembershipRadius = 5.5f;
    [Tooltip("成员场半径公式的基础值：基础值 + 成长值 × sqrt(当前成员数)。")]
    [SerializeField, Min(0f)] private float membershipRadiusBase = 3.5f;
    [Tooltip("当前成员越多，成员场半径按平方根增长，避免大羊群半径线性膨胀。")]
    [SerializeField, Min(0f)] private float membershipRadiusGrowth = 1.2f;
    [Tooltip("持续处于成员场外多久后正式脱队，避免短暂越界造成误判。")]
    [SerializeField, Min(0f)] private float detachDelay = 1.5f;
    [Tooltip("正式脱队时向外散开的初速度。")]
    [SerializeField, Min(0f)] private float detachScatterSpeed = 1.2f;

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
    private float actionMemberSpeedMultiplier = 1f;
    private Vector2 groupActionDirection = Vector2.right;
    private readonly List<Collider2D> pendingGroupActionBlockers = new List<Collider2D>();
    private readonly HashSet<Transform> breachedFenceGroups = new HashSet<Transform>();
    private float nextSeparationCheckTime;
    private float currentMembershipRadius = 5.5f;
    private readonly Dictionary<SheepMember, float> outsideMembershipSince =
        new Dictionary<SheepMember, float>();
    private readonly List<SheepMember> detachingMembers = new List<SheepMember>();
    private readonly HashSet<SheepMember> detachingMemberSet = new HashSet<SheepMember>();

    public int RecruitedCount { get; private set; }
    public int MemberCount => members.Count;

    /// <summary>本局达到过的最大羊数（只升不降），供围栏门槛、阶段等使用。</summary>
    public int HighestMemberCount { get; private set; }

    public Vector2 Center => movementController != null
        ? (Vector2)movementController.transform.position
        : (Vector2)transform.position;

    /// <summary>羊群当前的速度上限（含阶段倍率）；没有移动控制器时给个默认值。</summary>
    public float CurrentSpeedLimit => movementController != null ? movementController.CurrentSpeedLimit : 4f;

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
    public bool IsGroupActionAnticipating { get; private set; }
    public bool IsGroupActionFollowThrough { get; private set; }
    public float GroupActionFollowThroughSpeed { get; private set; }
    public bool GroupActionFollowThroughHardImpact { get; private set; }
    public Vector2 GroupActionDirection => groupActionDirection;
    public float CurrentMembershipRadius => Mathf.Max(minimumMembershipRadius, currentMembershipRadius);

    /// <summary>羊群椭圆长轴方向；停止移动后保留最后方向，避免外形突然转回水平。</summary>
    public Vector2 ShapeForward => shapeForward;

    public IReadOnlyList<SheepMember> Members => members;

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
            pendingGroupActionBlockers.Clear();
            breachedFenceGroups.Clear();
        }

        if (forwardDirection.sqrMagnitude > 0.0001f)
            groupActionDirection = forwardDirection.normalized;

        IsGroupActionActive = active;
        IsGroupActionHolding = active && holding;
        IsGroupActionAnticipating = false;
        IsGroupActionFollowThrough = false;
        GroupActionFollowThroughSpeed = 0f;
        GroupActionFollowThroughHardImpact = false;

        if (actionStarted)
        {
            outsideMembershipSince.Clear();

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

    internal void SetGroupActionAnticipating(bool anticipating)
    {
        IsGroupActionAnticipating = IsGroupActionActive
            && IsGroupActionHolding
            && anticipating;
    }

    internal void SetGroupActionFollowThrough(bool active, float speed, bool hardImpact)
    {
        IsGroupActionFollowThrough = IsGroupActionActive && active && speed > 0f;
        GroupActionFollowThroughSpeed = IsGroupActionFollowThrough
            ? Mathf.Max(0f, speed)
            : 0f;
        GroupActionFollowThroughHardImpact = IsGroupActionFollowThrough && hardImpact;
    }

    internal void ReportGroupActionMemberBlocked(Collider2D blocker)
    {
        if (IsGroupActionActive
            && !IsGroupActionHolding
            && !IsGroupActionFollowThrough
            && blocker != null
            && !pendingGroupActionBlockers.Contains(blocker))
        {
            pendingGroupActionBlockers.Add(blocker);
        }
    }

    internal int ConsumeGroupActionMemberBlocks(List<Collider2D> results)
    {
        if (results == null)
            return 0;

        results.Clear();
        results.AddRange(pendingGroupActionBlockers);
        pendingGroupActionBlockers.Clear();
        return results.Count;
    }

    internal void RegisterGroupActionFenceBreach(FenceObstacle fence)
    {
        if (fence == null)
            return;

        breachedFenceGroups.Add(GetFenceGroup(fence));
    }

    internal bool IsGroupActionFenceBreach(Collider2D blocker)
    {
        FenceObstacle fence = blocker != null
            ? blocker.GetComponentInParent<FenceObstacle>()
            : null;
        return fence != null && breachedFenceGroups.Contains(GetFenceGroup(fence));
    }

    private static Transform GetFenceGroup(FenceObstacle fence)
    {
        return fence.transform.parent != null
            ? fence.transform.parent
            : fence.transform;
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

    /// <summary>
    /// 普通移动只要求至少一只羊沿输入方向仍有可走路径。成员各自处理碰撞、
    /// 绕行和脱队；只有全员被挡时，目标中心才停下。
    /// </summary>
    internal bool AreAllMembersBlocked(Vector2 displacement)
    {
        if (displacement.sqrMagnitude <= 0.000001f)
            return false;

        Vector2 direction = displacement.normalized;
        bool foundMember = false;
        for (int index = 0; index < members.Count; index++)
        {
            SheepMember member = members[index];
            if (member == null)
                continue;

            SheepFlockAgent agent = member.Agent;
            // 缺少当前方向的有效报告时先允许中心前进，避免初始化或转向时误锁。
            if (agent == null
                || !agent.TryGetNormalMovementBlocked(direction, out bool blocked))
                return false;

            foundMember = true;
            if (!blocked)
                return false;
        }

        return foundMember;
    }

    public event Action<RecruitableSheep, int> SheepRecruited;
    public event Action<int> MemberCountChanged;
    public event Action<int> MembersSeparated;
    public event Action<bool> FenceChargeImpact;


    private void Awake()
    {
        movementController ??=
            GetComponent<FlockMovementController>();

        currentMembershipRadius = Mathf.Max(minimumMembershipRadius, membershipRadiusBase);

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
        if (IsMoving || IsGroupActionActive || maximumIdlePacingMembers <= 0)
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
        if (isReturningMember && !IsInsideMembershipField(member.transform.position))
            return false;

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
        outsideMembershipSince.Remove(member);
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
        memberGridDirty = true;

        member.SetAgent(agent);

        agent.SetFlock(this);
        agent.SetSimulationSlot(members.Count - 1);
        ScatteredSheep.Ensure(member);

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
        if (IsGroupActionActive)
        {
            outsideMembershipSince.Clear();
            return;
        }

        currentMembershipRadius = CalculateMembershipRadius();
        float membershipRadiusSquared = currentMembershipRadius * currentMembershipRadius;
        detachingMembers.Clear();

        // 距离脱队不能移除最后一只羊。中心与成员位置现在允许暂时分离，
        // 单羊被障碍挡住时仍应保留控制锚点并继续尝试追赶。
        if (members.Count <= 1)
        {
            outsideMembershipSince.Clear();
            return;
        }

        for (int index = 0; index < members.Count; index++)
        {
            SheepMember member = members[index];
            if (member == null)
                continue;

            Vector2 offsetFromCenter = GetMemberPosition(member) - Center;
            if (offsetFromCenter.sqrMagnitude <= membershipRadiusSquared)
            {
                outsideMembershipSince.Remove(member);
                continue;
            }

            if (!outsideMembershipSince.TryGetValue(member, out float separatedAt))
            {
                outsideMembershipSince.Add(member, currentTime);
                separatedAt = currentTime;
            }

            if (currentTime - separatedAt >= Mathf.Max(0f, detachDelay))
                detachingMembers.Add(member);
        }

        PreserveNearestMemberWhenAllWouldDetach();

        DetachSeparatedMembers();
    }


    private void PreserveNearestMemberWhenAllWouldDetach()
    {
        if (detachingMembers.Count == 0 || detachingMembers.Count < members.Count)
            return;

        SheepMember nearest = null;
        float nearestDistanceSquared = float.PositiveInfinity;
        for (int index = 0; index < detachingMembers.Count; index++)
        {
            SheepMember candidate = detachingMembers[index];
            if (candidate == null)
                continue;

            float distanceSquared = (GetMemberPosition(candidate) - Center).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared)
            {
                nearest = candidate;
                nearestDistanceSquared = distanceSquared;
            }
        }

        if (nearest == null)
            return;

        detachingMembers.Remove(nearest);
        outsideMembershipSince.Remove(nearest);
    }


    private float CalculateMembershipRadius()
    {
        float minimumRadius = Mathf.Max(0.5f, minimumMembershipRadius);
        return Mathf.Max(
            minimumRadius,
            membershipRadiusBase + membershipRadiusGrowth * Mathf.Sqrt(members.Count));
    }


    internal bool IsInsideMembershipField(Vector2 position)
    {
        float radius = CurrentMembershipRadius;
        return (position - Center).sqrMagnitude <= radius * radius;
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

        detachingMemberSet.Clear();
        for (int index = 0; index < detachingMembers.Count; index++)
        {
            SheepMember member = detachingMembers[index];
            if (member != null && member.Flock == this)
                detachingMemberSet.Add(member);
        }

        int firstRemovedIndex = members.Count;
        for (int index = members.Count - 1; index >= 0; index--)
        {
            SheepMember member = members[index];
            if (member == null || !detachingMemberSet.Contains(member))
                continue;

            member.Agent?.SetFlock(null);
            member.Leave(this);
            outsideMembershipSince.Remove(member);
            members.RemoveAt(index);
            firstRemovedIndex = index;
        }

        if (firstRemovedIndex < members.Count)
            RefreshSimulationSlots(firstRemovedIndex);

        int detachedCount = 0;
        bool wasDeferring = deferMemberCountChanged;
        deferMemberCountChanged = true;
        try
        {
            for (int index = 0; index < detachingMembers.Count; index++)
            {
                SheepMember member = detachingMembers[index];
                if (member == null || !detachingMemberSet.Contains(member))
                    continue;

                Vector2 awayFromCenter = (Vector2)member.transform.position - Center;
                Vector2 direction = awayFromCenter.sqrMagnitude > 0.0001f
                    ? awayFromCenter.normalized
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
        detachingMemberSet.Clear();
        if (detachedCount <= 0)
            return;

        memberGridDirty = true;
        if (!wasDeferring)
            MemberCountChanged?.Invoke(MemberCount);
        MembersSeparated?.Invoke(detachedCount);
    }

    private void OnValidate()
    {
        largeFlockThreshold = Mathf.Max(mediumFlockThreshold + 1, largeFlockThreshold);
        mediumSteeringInterval = Mathf.Max(1, mediumSteeringInterval);
        largeSteeringInterval = Mathf.Max(1, largeSteeringInterval);
        maximumIdlePacingMembers = Mathf.Max(0, maximumIdlePacingMembers);
        separationCheckInterval = Mathf.Max(0.1f, separationCheckInterval);
        minimumMembershipRadius = Mathf.Max(0.5f, minimumMembershipRadius);
        membershipRadiusBase = Mathf.Max(0f, membershipRadiusBase);
        membershipRadiusGrowth = Mathf.Max(0f, membershipRadiusGrowth);
        detachDelay = Mathf.Max(0f, detachDelay);
        detachScatterSpeed = Mathf.Max(0f, detachScatterSpeed);
        currentMembershipRadius = Mathf.Max(currentMembershipRadius, minimumMembershipRadius);
        memberGridDirty = true;
    }
}
