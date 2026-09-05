using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SheepIdentity : MonoBehaviour
{
    [SerializeField] private string sheepTypeId = MvpSheepCatalog.DefaultTypeId;

    public string DisplayName { get; private set; }
    public string SheepTypeId => string.IsNullOrWhiteSpace(sheepTypeId)
        ? MvpSheepCatalog.DefaultTypeId
        : sheepTypeId;

    public void AssignName(string displayName)
    {
        DisplayName = displayName;
    }

    /// <summary>运行时指定羊的类型 id（例如刷新器按权重决定特殊羊时）。</summary>
    public void AssignType(string typeId)
    {
        if (!string.IsNullOrWhiteSpace(typeId))
            sheepTypeId = typeId.Trim();
    }
}

public sealed class MvpSheepCatalogEntry
{
    public MvpSheepCatalogEntry(
        string id,
        string displayName,
        int rarityLevel,
        string rarityName,
        int score,
        string animationGroupName)
    {
        Id = id;
        DisplayName = displayName;
        RarityLevel = rarityLevel;
        RarityName = rarityName;
        Score = score;
        AnimationGroupName = animationGroupName;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public int RarityLevel { get; }
    public string RarityName { get; }
    public int Score { get; }
    public string AnimationGroupName { get; }
}

public static class MvpSheepCatalog
{
    public const string DefaultTypeId = "sheep.mvp.common";

    private static readonly List<MvpSheepCatalogEntry> entries = new()
    {
        new MvpSheepCatalogEntry(
            DefaultTypeId,
            "绵羊",
            1,
            "普通",
            1,
            "MVP Prototype")
    };

    public static IReadOnlyList<MvpSheepCatalogEntry> Entries => entries;

    public static MvpSheepCatalogEntry Find(string id)
    {
        foreach (MvpSheepCatalogEntry entry in entries)
        {
            if (string.Equals(entry.Id, id, StringComparison.Ordinal))
                return entry;
        }

        return entries[0];
    }
}
