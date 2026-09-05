using System.Collections.Generic;
using UnityEngine;

public sealed class SheepNameGenerator : MonoBehaviour
{
    [SerializeField] private SheepNamePool namePool;
    [SerializeField] private SheepIdentity[] sheep;
    [SerializeField] private FlockController flockController;

    private readonly List<string> availableNames = new();
    private readonly List<SheepIdentity> targets = new();

    private void Start()
    {
        AssignNames();
    }

    private void AssignNames()
    {
        availableNames.Clear();
        targets.Clear();

        if (namePool != null)
        {
            foreach (string name in namePool.Names)
            {
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string trimmed = name.Trim();

                if (!availableNames.Contains(trimmed))
                    availableNames.Add(trimmed);
            }
        }

        Shuffle(availableNames);

        foreach (RecruitableSheep recruitable in FindObjectsByType<RecruitableSheep>(
                     FindObjectsInactive.Exclude))
        {
            AddTarget(recruitable.GetComponent<SheepIdentity>());
        }

        AddTargets(sheep);

        // Older MVP scenes only serialized the five recruitable sheep. Include
        // the initial flock member as well so every visible sheep has a name.
        flockController ??= FindAnyObjectByType<FlockController>();
        if (flockController != null)
        {
            foreach (SheepMember member in flockController.Members)
            {
                if (member != null)
                    AddTarget(member.GetComponent<SheepIdentity>());
            }
        }

        int fallbackIndex = 1;

        for (int i = 0; i < targets.Count; i++)
        {
            string assignedName;

            if (availableNames.Count > 0)
            {
                assignedName = availableNames[0];
                availableNames.RemoveAt(0);
            }
            else
            {
                assignedName = $"小羊 {fallbackIndex:00}";
                fallbackIndex++;
            }

            targets[i].AssignName(assignedName);

            Debug.Log($"{targets[i].name} 的名字是：{assignedName}", targets[i]);
        }
    }

    private void AddTargets(SheepIdentity[] identities)
    {
        if (identities == null)
            return;

        foreach (SheepIdentity identity in identities)
            AddTarget(identity);
    }

    private void AddTarget(SheepIdentity identity)
    {
        if (identity != null && identity.gameObject.activeInHierarchy && !targets.Contains(identity))
            targets.Add(identity);
    }

    private static void Shuffle(List<string> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);

            (list[i], list[randomIndex]) =
                (list[randomIndex], list[i]);
        }
    }
}
