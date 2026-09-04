using System.Collections.Generic;
using UnityEngine;

public sealed class SheepNameGenerator : MonoBehaviour
{
    [SerializeField] private SheepNamePool namePool;
    [SerializeField] private SheepIdentity[] sheep;

    private readonly List<string> availableNames = new();

    private void Start()
    {
        AssignNames();
    }

    private void AssignNames()
    {
        availableNames.Clear();

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

        int fallbackIndex = 1;

        for (int i = 0; i < sheep.Length; i++)
        {
            if (sheep[i] == null)
                continue;

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

            sheep[i].AssignName(assignedName);

            Debug.Log($"{sheep[i].name} 的名字是：{assignedName}", sheep[i]);
        }
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
