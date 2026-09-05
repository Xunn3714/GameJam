using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MvpHudView : MonoBehaviour
{
    [Header("Legacy HUD")]
    [SerializeField] private TMP_Text taskText;
    [SerializeField] private TMP_Text flockCountText;

    [Header("Task Panel")]
    [SerializeField] private TaskChecklistView taskChecklistView;

    private int displayedMemberCount;
    private int displayedPoopStock = -1;
    private int displayedPoopCapacity;


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
            taskText.text =
                $"找到羊：{progress}/{target}";
        }

        displayedMemberCount = memberCount;
        RefreshStatusText();
    }


    public void UpdateObjectives(
        IReadOnlyList<MvpObjectiveSnapshot> objectives,
        int memberCount)
    {
        // 原来的 HUD 继续兼容
        if (taskText != null)
        {
            StringBuilder content = new("任务列表\n");

            if (objectives != null)
            {
                foreach (MvpObjectiveSnapshot objective in objectives)
                {
                    content.Append(
                        objective.IsComplete
                            ? "☑ "
                            : "☐ ");

                    content.Append(objective.Title);

                    if (objective.Target > 0)
                    {
                        content.Append(
                            $"  {objective.Progress}/{objective.Target}");
                    }

                    if (!objective.IsRequired)
                    {
                        content.Append("（支线）");
                    }

                    if (objective.IsNew)
                    {
                        content.Append("  新");
                    }

                    content.AppendLine();
                }
            }

            taskText.text = content.ToString();
        }

        displayedMemberCount = memberCount;

        RefreshStatusText();

        // 把同一份真实任务数据送给我们的 TaskPanel
        if (taskChecklistView != null)
        {
            taskChecklistView.ApplyObjectives(
                objectives,
                memberCount);
        }
    }


    public void UpdatePoopStock(
        int stored,
        int capacity)
    {
        displayedPoopStock =
            Mathf.Max(0, stored);

        displayedPoopCapacity =
            Mathf.Max(0, capacity);

        RefreshStatusText();
    }


    private void RefreshStatusText()
    {
        if (flockCountText == null)
            return;

        flockCountText.text =
            displayedPoopCapacity > 0
                ? $"族群：{displayedMemberCount}  大便：{displayedPoopStock}/{displayedPoopCapacity}"
                : $"族群：{displayedMemberCount}";
    }
}
