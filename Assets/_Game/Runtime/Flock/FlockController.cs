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

    private readonly List<SheepMember> members = new List<SheepMember>();
    private Vector2[] velocityHistory;
    private int historyHead = -1;
    private int historyCount;

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

    public IReadOnlyList<SheepMember> Members => members;

    /// <summary>玩家直接操控的那只羊：贴着羊群中心移动，其余羊以它为起点向外扩散跟随。</summary>
    public SheepMember Leader { get; private set; }

    /// <summary>当前紧凑程度：1 = 松散的一大群，越小越抱团。由 SheepFlockAgent 读取来缩放半径。</summary>
    public float Compactness { get; private set; } = 1f;

    /// <summary>是否处于抱团状态（目标值；实际半径会平滑过渡）。</summary>
    public bool IsHuddling { get; private set; }

    /// <summary>整体速度倍率：中心移动速度和每只羊的最大速度 / 加速度都乘它。</summary>
    public float SpeedMultiplier { get; private set; } = 1f;

    public void SetSpeedMultiplier(float multiplier)
    {
        SpeedMultiplier = Mathf.Max(0.1f, multiplier);
        if (movementController != null)
        {
            movementController.SetSpeedMultiplier(SpeedMultiplier);
        }
    }

    public event Action<RecruitableSheep, int> SheepRecruited;
    public event Action<int> MemberCountChanged;
    public event Action<SheepMember> LeaderChanged;


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
        RecordMovementVelocity(MovementVelocity);

        float targetCompactness = IsHuddling ? huddleCompactness : 1f;
        Compactness = Mathf.MoveTowards(
            Compactness,
            targetCompactness,
            huddleTransitionSpeed * Time.fixedDeltaTime);
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

        // 先确认羊能真正加入羊群
        if (!AddMember(member))
            return false;

        // 原有招募完成逻辑
        sheep.CompleteRecruitment();

        RecruitedCount++;

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

        member.SetAgent(agent);

        agent.SetFlock(this);

        if (members.Count > HighestMemberCount)
        {
            HighestMemberCount = members.Count;
        }

        if (Leader == null)
        {
            SelectLeader();
        }

        MemberCountChanged?.Invoke(MemberCount);

        return true;
    }
}
