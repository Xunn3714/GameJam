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
    public bool IsMoving => movementController != null && movementController.IsMoving;
    public IReadOnlyList<SheepMember> Members => members;

    public event Action<RecruitableSheep, int> SheepRecruited;
    public event Action<int> MemberCountChanged;

    private void Awake()
    {
        movementController ??= GetComponent<FlockMovementController>();

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

        SheepMember member = sheep.GetComponent<SheepMember>();
        if (member == null)
        {
            member = sheep.gameObject.AddComponent<SheepMember>();
        }

        if (!AddMember(member))
            return false;

        sheep.CompleteRecruitment();
        RecruitedCount++;
        SheepRecruited?.Invoke(sheep, RecruitedCount);

        Debug.Log(
            $"{sheep.name} joined the flock. Current member count: {MemberCount}",
            sheep);
        return true;
    }

    public bool Remove(SheepMember member)
    {
        int index = members.IndexOf(member);
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
        if (member == null || members.Contains(member) || !member.Join(this))
            return false;

        if (member.GetComponent<SheepIdentity>() == null)
        {
            member.gameObject.AddComponent<SheepIdentity>();
        }

        SheepFlockAgent agent = member.GetComponent<SheepFlockAgent>();
        if (agent == null)
        {
            agent = member.gameObject.AddComponent<SheepFlockAgent>();
        }

        members.Add(member);
        member.SetAgent(agent);
        agent.SetFlock(this);
        MemberCountChanged?.Invoke(MemberCount);
        return true;
    }
}
