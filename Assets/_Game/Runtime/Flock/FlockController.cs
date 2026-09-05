using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlockController : MonoBehaviour
{
    [Header("Flock")]
    [SerializeField] private FlockMovementController movementController;
    [SerializeField] private SheepMember[] startingMembers;

    private readonly List<SheepMember> members = new List<SheepMember>();

    public int RecruitedCount { get; private set; }
    public int MemberCount => members.Count;

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

    public event Action<RecruitableSheep, int> SheepRecruited;
    public event Action<int> MemberCountChanged;


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
            !string.IsNullOrEmpty(identity.sheepId) &&
            SheepCollectionManager.Instance != null)
        {
            SheepCollectionManager.Instance
                .EncounterSheep(identity.sheepId);
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

        MemberCountChanged?.Invoke(MemberCount);

        return true;
    }
}
