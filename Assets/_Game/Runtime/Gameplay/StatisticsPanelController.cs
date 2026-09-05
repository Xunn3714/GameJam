using System.Collections.Generic;
using UnityEngine;

public class StatisticsPanelController : MonoBehaviour
{
    [Header("UI")]
    public Transform contentRoot;
    public StatRowView statRowPrefab;


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
            return;
        }

        List<GameStatEntry> stats =
            GameStatsManager.Instance.GetAllStats();

        foreach (GameStatEntry stat in stats)
        {
            StatRowView row =
                Instantiate(statRowPrefab, contentRoot);

            row.Setup(
                stat.displayName,
                stat.GetFormattedValue()
            );
        }
    }


    private void ClearRows()
    {
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }
    }
}
