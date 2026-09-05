using System.Collections.Generic;
using System.Text;
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

        if (flockCountText != null)
        {
            flockCountText.text = $"族群：{memberCount}";
        }
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

        if (flockCountText != null)
            flockCountText.text = $"族群：{memberCount}";
    }
}
