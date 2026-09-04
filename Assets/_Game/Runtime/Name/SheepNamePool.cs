using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SheepNamePool",
    menuName = "Game/Sheep Name Pool")]
public sealed class SheepNamePool : ScriptableObject
{
    [SerializeField] private List<string> names = new();

    public IReadOnlyList<string> Names => names;
}
