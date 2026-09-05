using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

[CreateAssetMenu(fileName = "SpecialSheepPool", menuName = "Sheep MVP/Special Sheep Pool")]
public sealed class SpecialSheepPool : ScriptableObject
{
    [Serializable]
    private sealed class Entry
    {
        [SerializeField] private RecruitableSheep prefab;
        [SerializeField, Min(0f)] private float weight = 1f;

        public RecruitableSheep Prefab => prefab;
        public float Weight => weight;
    }

    [SerializeField] private List<Entry> entries = new();

    public RecruitableSheep Pick(Random random, RecruitableSheep fallback)
    {
        float totalWeight = 0f;
        foreach (Entry entry in entries)
        {
            if (entry?.Prefab != null && entry.Weight > 0f)
                totalWeight += entry.Weight;
        }

        if (totalWeight <= 0f)
            return fallback;

        double choice = random.NextDouble() * totalWeight;
        foreach (Entry entry in entries)
        {
            if (entry?.Prefab == null || entry.Weight <= 0f)
                continue;

            choice -= entry.Weight;
            if (choice <= 0d)
                return entry.Prefab;
        }

        return fallback;
    }
}
