using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TaskChecklistView : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TMP_Text taskHeaderText;

    [Header("Current Task")]
    [SerializeField] private GameObject taskRow01;
    [SerializeField] private TMP_Text taskTitle01;
    [SerializeField] private Image checkIcon01;
    [SerializeField] private TMP_Text progress01;
    [SerializeField] private Image progressFill01;

    [Header("Rows hidden by the sequential Alpha task flow")]
    [SerializeField] private GameObject taskRow02;
    [SerializeField] private Image checkIcon02;
    [SerializeField] private TMP_Text progress02;

    [SerializeField] private GameObject taskRow03;
    [SerializeField] private Image checkIcon03;
    [SerializeField] private TMP_Text progress03;

    [SerializeField] private GameObject taskRow04;
    [SerializeField] private Image checkIcon04;
    [SerializeField] private TMP_Text progress04;

    [Header("Group")]
    [SerializeField] private TMP_Text groupCountText;

    [Header("Sprites")]
    [SerializeField] private Sprite uncheckedSprite;
    [SerializeField] private Sprite checkedSprite;

    private void Awake()
    {
        ApplyClearTypography(taskHeaderText);
        ApplyClearTypography(taskTitle01);
        ApplyClearTypography(progress01);
        ApplyClearTypography(groupCountText);
    }

    public void ApplyObjectives(
        IReadOnlyList<MvpObjectiveSnapshot> objectives,
        int memberCount)
    {
        if (groupCountText != null)
        {
            groupCountText.text = $"羊群：{memberCount}";
        }

        SetRowActive(taskRow01, checkIcon01, false);
        SetRowActive(taskRow02, checkIcon02, false);
        SetRowActive(taskRow03, checkIcon03, false);
        SetRowActive(taskRow04, checkIcon04, false);

        if (taskTitle01 != null)
            taskTitle01.text = string.Empty;

        ResetTask(checkIcon01, progress01);
        SetProgressFill(progressFill01, 0f);
        ResetTask(checkIcon02, progress02);
        ResetTask(checkIcon03, progress03);
        ResetTask(checkIcon04, progress04);

        if (objectives == null || objectives.Count == 0)
            return;

        MvpObjectiveSnapshot current = objectives[0];
        SetRowActive(taskRow01, checkIcon01, true);
        if (taskTitle01 != null)
            taskTitle01.text = current.Title;
        ApplyTask(checkIcon01, progress01, current);
        SetProgressFill(
            progressFill01,
            current.Target > 0
                ? (float)current.Progress / current.Target
                : current.IsComplete ? 1f : 0f);
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
            progressText.text = objective.Target > 0
                ? $"{objective.Progress}/{objective.Target}"
                : objective.IsComplete ? "完成" : string.Empty;
        }
    }

    private void ResetTask(Image icon, TMP_Text progressText)
    {
        if (icon != null)
            icon.sprite = uncheckedSprite;
        if (progressText != null)
            progressText.text = string.Empty;
    }

    private static void SetProgressFill(Image fill, float value)
    {
        if (fill != null)
            fill.fillAmount = Mathf.Clamp01(value);
    }

    private static void ApplyClearTypography(TMP_Text text)
    {
        if (text == null)
            return;

        MvpTmpUiFont.Apply(text);
        text.enableAutoSizing = false;
        text.characterSpacing = 0f;
        text.lineSpacing = 0f;
        text.extraPadding = true;
    }

    private static void SetRowActive(GameObject configuredRow, Component fallbackChild, bool active)
    {
        GameObject row = configuredRow;
        if (row == null && fallbackChild != null && fallbackChild.transform.parent != null)
            row = fallbackChild.transform.parent.gameObject;

        if (row != null)
            row.SetActive(active);
    }
}
