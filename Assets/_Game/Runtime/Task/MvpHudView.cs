using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MvpHudView : MonoBehaviour
{
    [SerializeField] private TMP_Text taskText;
    [SerializeField] private TMP_Text flockCountText;

    private void Awake()
    {
        MvpTmpUiFont.Apply(taskText);
        MvpTmpUiFont.Apply(flockCountText);
    }

    public void UpdateProgress(
        int progress,
        int target,
        int memberCount)
    {
        if (taskText != null)
        {
            taskText.text = $"找到羊：{progress}/{target}";
        }

        if (flockCountText != null)
        {
            flockCountText.text = $"族群：{memberCount}";
        }
    }
}
