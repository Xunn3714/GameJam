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

    [Header("Description")]
    [TextArea(2, 5)]
    public string description;

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
