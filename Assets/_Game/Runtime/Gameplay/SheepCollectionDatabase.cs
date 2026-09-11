using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class SheepCollectionEntry
{
    [Header("Identity")]
    public string sheepId;
    public string displayName;

    [Header("Visual")]
    public Sprite icon;

    [Header("Rarity")]
    public SheepQuality quality = SheepQuality.Common;

    // 例如：
    // 普通羊 / 绿色羊 / 蓝色羊 / 紫色羊 / 金色羊 / 彩色羊
    public string rarityName;

    [Header("Description")]
    [TextArea(2, 5)]
    public string description;

    [Header("Flavor Text")]
    [TextArea(3, 8)]
    public string flavorText;

    [Header("Special Ability")]
    public string abilityName;

    [TextArea(2, 5)]
    public string abilityDescription;

    [Header("Sentence Image")]
    public Sprite sentenceImage;

    [Header("Display Order")]
    public int order;
}


[CreateAssetMenu(
    fileName = "SheepCollectionDatabase",
    menuName = "GameJam/Sheep Collection Database")]
public class SheepCollectionDatabase : ScriptableObject
{
    public List<SheepCollectionEntry> sheepList =
        new List<SheepCollectionEntry>();


    public SheepCollectionEntry GetSheep(string sheepId)
    {
        return sheepList.Find(
            sheep => sheep.sheepId == sheepId
        );
    }


    public List<SheepCollectionEntry> GetAllSheep()
    {
        List<SheepCollectionEntry> result =
            new List<SheepCollectionEntry>(sheepList);

        result.Sort(
            (a, b) => a.order.CompareTo(b.order)
        );

        return result;
    }
}
