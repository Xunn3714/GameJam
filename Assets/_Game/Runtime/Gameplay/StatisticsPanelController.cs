using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StatisticsPanelController : MonoBehaviour
{
    [Header("UI")]
    public Transform contentRoot;
    public StatRowView statRowPrefab;
    [SerializeField] private TMP_Text emptyStateText;


    private void OnEnable()
    {
        RefreshStats();
    }


    public void RefreshStats()
    {
        ClearRows();

        if (GameStatsManager.Instance == null)
        {
            Debug.LogWarning("GameStatsManager Instance not found.");
            ShowEmptyState("暂无统计数据");
            UpdateScrollbarVisibility();
            return;
        }

        List<GameStatEntry> stats =
            GameStatsManager.Instance.GetAllStats();

        if (stats.Count == 0)
        {
            ShowEmptyState("完成一局游戏后，统计会显示在这里。");
            UpdateScrollbarVisibility();
            return;
        }

        if (emptyStateText != null)
            emptyStateText.gameObject.SetActive(false);

        foreach (GameStatEntry stat in stats)
        {
            StatRowView row =
                Instantiate(statRowPrefab, contentRoot);

            row.Setup(
                stat.displayName,
                stat.GetFormattedValue()
            );
        }

        UpdateScrollbarVisibility();
    }


    private void ShowEmptyState(string message)
    {
        if (emptyStateText == null)
            return;

        emptyStateText.text = message;
        emptyStateText.gameObject.SetActive(true);
    }


    private void ClearRows()
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject row = contentRoot.GetChild(i).gameObject;
            row.SetActive(false);
            Destroy(row);
        }
    }


    private void UpdateScrollbarVisibility()
    {
        ScrollRect scrollRect = contentRoot != null
            ? contentRoot.GetComponentInParent<ScrollRect>()
            : null;
        if (scrollRect == null || scrollRect.verticalScrollbar == null)
            return;

        RectTransform contentRect = contentRoot as RectTransform;
        if (contentRect != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        Canvas.ForceUpdateCanvases();

        float contentHeight = contentRect != null ? contentRect.rect.height : 0f;
        float viewportHeight = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
        scrollRect.verticalScrollbar.gameObject.SetActive(contentHeight > viewportHeight + 1f);
    }
}
