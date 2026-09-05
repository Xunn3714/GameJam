using System.Collections.Generic;
using UnityEngine;

public sealed class MvpSessionStats
{
    private float startedAt;

    public int PoopUses { get; private set; }

    public void Begin()
    {
        startedAt = Time.time;
        PoopUses = 0;
    }

    public void RecordPoop() => PoopUses++;

    public MvpResultSnapshot Complete(IReadOnlyList<SheepMember> members, int recruitedTotal)
    {
        List<string> names = new();

        if (members != null)
        {
            foreach (SheepMember member in members)
            {
                if (member == null)
                    continue;

                SheepIdentity identity = member.GetComponent<SheepIdentity>();
                string displayName = identity != null ? identity.DisplayName : null;
                names.Add(string.IsNullOrWhiteSpace(displayName) ? "未命名小羊" : displayName);
            }
        }

        int currentScore = 0;
        if (members != null)
        {
            foreach (SheepMember member in members)
            {
                if (member == null)
                    continue;

                SheepIdentity identity = member.GetComponent<SheepIdentity>();
                currentScore += MvpSheepCatalog.Find(
                    identity != null ? identity.SheepTypeId : MvpSheepCatalog.DefaultTypeId).Score;
            }
        }

        return new MvpResultSnapshot(
            Mathf.Max(0f, Time.time - startedAt),
            names,
            recruitedTotal,
            PoopUses,
            currentScore);
    }
}

public sealed class MvpResultSnapshot
{
    public MvpResultSnapshot(
        float elapsedSeconds,
        IReadOnlyList<string> memberNames,
        int recruitedTotal,
        int poopUses,
        int currentScore)
    {
        ElapsedSeconds = elapsedSeconds;
        MemberNames = memberNames ?? new List<string>();
        RecruitedTotal = recruitedTotal;
        PoopUses = poopUses;
        CurrentScore = currentScore;
    }

    public float ElapsedSeconds { get; }
    public IReadOnlyList<string> MemberNames { get; }
    public int CurrentFlockCount => MemberNames.Count;
    public int RecruitedTotal { get; }
    public int PoopUses { get; }
    public int CurrentScore { get; }
}
