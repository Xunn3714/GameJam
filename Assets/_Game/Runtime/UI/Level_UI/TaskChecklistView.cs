using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TaskChecklistView : MonoBehaviour
{
    [Header("Task 01 - Pen")]
    [SerializeField] private Image checkIcon01;
    [SerializeField] private TMP_Text progress01;

    [Header("Task 02 - Grow Flock")]
    [SerializeField] private Image checkIcon02;
    [SerializeField] private TMP_Text progress02;

    [Header("Task 03 - Escape")]
    [SerializeField] private Image checkIcon03;
    [SerializeField] private TMP_Text progress03;

    [Header("Task 04 - Special Sheep")]
    [SerializeField] private Image checkIcon04;
    [SerializeField] private TMP_Text progress04;

    [Header("Group")]
    [SerializeField] private TMP_Text groupCountText;

    [Header("Sprites")]
    [SerializeField] private Sprite uncheckedSprite;
    [SerializeField] private Sprite checkedSprite;


    public void ApplyObjectives(
        IReadOnlyList<MvpObjectiveSnapshot> objectives,
        int memberCount)
    {
        if (groupCountText != null)
        {
            groupCountText.text = $"Group: {memberCount}";
        }

        if (objectives == null)
            return;

        foreach (MvpObjectiveSnapshot objective in objectives)
        {
            switch (objective.Id)
            {
                case "alpha.pen":
                    ApplyTask(
                        checkIcon01,
                        progress01,
                        objective);
                    break;

                case "alpha.exit_unlock":
                    ApplyTask(
                        checkIcon02,
                        progress02,
                        objective);
                    break;

                case "alpha.escape":
                    ApplyTask(
                        checkIcon03,
                        progress03,
                        objective);
                    break;

                case "alpha.special":
                    ApplyTask(
                        checkIcon04,
                        progress04,
                        objective);
                    break;
            }
        }
    }


    private void ApplyTask(
        Image icon,
        TMP_Text progressText,
        MvpObjectiveSnapshot objective)
    {
        if (icon != null)
        {
            icon.sprite =
                objective.IsComplete
                    ? checkedSprite
                    : uncheckedSprite;
        }

        if (progressText != null)
        {
            progressText.text =
                $"{objective.Progress}/{objective.Target}";
        }
    }
}
