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
    [Tooltip("留空会自动从 taskRow02 的子物体里找（除 progress02 外的第一个文本）。")]
    [SerializeField] private TMP_Text taskTitle02;

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

    private const float AdditionalTaskRowHeight = 82f;
    private const float MinimumProgressTextWidth = 96f;
    private RectTransform panelRect;
    private float singleTaskPanelHeight;

    private void Awake()
    {
        panelRect = transform as RectTransform;
        if (panelRect != null)
            singleTaskPanelHeight = panelRect.sizeDelta.y;

        ApplyClearTypography(taskHeaderText);
        ApplyClearTypography(taskTitle01);
        ApplyClearTypography(taskTitle02);
        ApplyClearTypography(progress01);
        EnsureCounterIsFullyVisible(progress01);
        EnsureCounterIsFullyVisible(progress02);
        EnsureCounterIsFullyVisible(progress03);
        EnsureCounterIsFullyVisible(progress04);
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
        ResizePanelForTaskCount(objectives != null ? objectives.Count : 0);

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

        // 第二行留给支线任务（例如宝通寺的「寻找？？」）。
        if (objectives.Count < 2)
            return;

        MvpObjectiveSnapshot extra = objectives[1];
        SetRowActive(taskRow02, checkIcon02, true);
        ResolveTitle02();
        if (taskTitle02 != null)
            taskTitle02.text = extra.Title;
        ApplyTask(checkIcon02, progress02, extra);
    }

    private void ResizePanelForTaskCount(int taskCount)
    {
        if (panelRect == null)
            panelRect = transform as RectTransform;
        if (panelRect == null)
            return;

        if (singleTaskPanelHeight <= 0f)
            singleTaskPanelHeight = panelRect.sizeDelta.y;

        int visibleTaskCount = Mathf.Clamp(taskCount, 1, 4);
        Vector2 size = panelRect.sizeDelta;
        size.y = singleTaskPanelHeight + AdditionalTaskRowHeight * (visibleTaskCount - 1);
        panelRect.sizeDelta = size;
    }

    /// <summary>第二行的标题文本没在 Inspector 里连的话，自己从行里找一个。</summary>
    private void ResolveTitle02()
    {
        if (taskTitle02 != null || taskRow02 == null)
            return;

        foreach (TMP_Text candidate in taskRow02.GetComponentsInChildren<TMP_Text>(true))
        {
            if (candidate == progress02)
                continue;

            taskTitle02 = candidate;
            ApplyClearTypography(taskTitle02);
            return;
        }
    }

    private static void EnsureCounterIsFullyVisible(TMP_Text counter)
    {
        if (counter == null)
            return;

        RectTransform rect = counter.rectTransform;
        Vector2 size = rect.sizeDelta;
        size.x = Mathf.Max(size.x, MinimumProgressTextWidth);
        rect.sizeDelta = size;
        counter.enableWordWrapping = false;
        counter.overflowMode = TextOverflowModes.Overflow;
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
