using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MvpHudView : MonoBehaviour
{
    [SerializeField] private TMP_Text taskText;
    [SerializeField] private TMP_Text flockCountText;

    public void UpdateProgress(
        int progress,
        int target,
        int speciesCount)
    {
        if (taskText != null)
        {
            taskText.text = $"Find Sheep: {progress}/{target}";
        }

        if (flockCountText != null)
        {
            flockCountText.text = $"Group: {speciesCount}";
        }
    }
}
