using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MvpHudView : MonoBehaviour
{
    [SerializeField] private TMP_Text taskText;
    [SerializeField] private TMP_Text flockCountText;

    private int displayedMemberCount;
    private int displayedPoopStock = -1;
    private int displayedPoopCapacity;

    private void Awake()
    {
        MvpTmpUiFont.Apply(taskText);
        MvpTmpUiFont.Apply(flockCountText);

        if (flockCountText != null)
        {
            flockCountText.fontSize = 30f;
            flockCountText.rectTransform.sizeDelta = new Vector2(380f, 50f);
        }

        if (taskText != null)
        {
            taskText.fontSize = 25f;
            taskText.alignment = TextAlignmentOptions.TopLeft;
            taskText.color = MvpUiFactory.Paper;
            taskText.rectTransform.sizeDelta = new Vector2(460f, 260f);
            taskText.rectTransform.anchoredPosition = new Vector2(265f, -190f);
        }
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

        displayedMemberCount = memberCount;
        RefreshStatusText();
    }

    public void UpdateObjectives(
        IReadOnlyList<MvpObjectiveSnapshot> objectives,
        int memberCount)
    {
        if (taskText != null)
        {
            StringBuilder content = new("任务列表\n");

            if (objectives != null)
            {
                foreach (MvpObjectiveSnapshot objective in objectives)
                {
                    content.Append(objective.IsComplete ? "☑ " : "☐ ");
                    content.Append(objective.Title);

                    if (!objective.IsComplete && objective.Target > 0)
                        content.Append($"  {objective.Progress}/{objective.Target}");

                    if (!objective.IsRequired)
                        content.Append("（支线）");

                    if (objective.IsNew)
                        content.Append("  新");

                    content.AppendLine();
                }
            }

            taskText.text = content.ToString();
        }

        displayedMemberCount = memberCount;
        RefreshStatusText();
    }

    public void UpdatePoopStock(int stored, int capacity)
    {
        displayedPoopStock = Mathf.Max(0, stored);
        displayedPoopCapacity = Mathf.Max(0, capacity);
        RefreshStatusText();
    }

    private void RefreshStatusText()
    {
        if (flockCountText == null)
            return;

        flockCountText.text = displayedPoopCapacity > 0
            ? $"族群：{displayedMemberCount}  大便：{displayedPoopStock}/{displayedPoopCapacity}"
            : $"族群：{displayedMemberCount}";
    }
}
