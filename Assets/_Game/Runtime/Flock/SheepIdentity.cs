using UnityEngine;

[DisallowMultipleComponent]
public sealed class SheepIdentity : MonoBehaviour
{
    public string DisplayName { get; private set; }

    public void AssignName(string displayName)
    {
        DisplayName = displayName;
    }
}
