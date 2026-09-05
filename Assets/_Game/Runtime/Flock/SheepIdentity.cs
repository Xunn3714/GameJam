using UnityEngine;

public class SheepIdentity : MonoBehaviour
{
    [Header("Collection")]
    public string sheepId;

    public string DisplayName { get; private set; }


    public void AssignName(string displayName)
    {
        DisplayName = displayName;
    }
}
