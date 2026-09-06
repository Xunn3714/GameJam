using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultPanelView : MonoBehaviour
{
    [Header("Texts")]
    [SerializeField] private TMP_Text resultTitle;
    [SerializeField] private TMP_Text resultDescription;
    [SerializeField] private TMP_Text sheepCountLabel;
    [SerializeField] private TMP_Text scoreLabel;
    [SerializeField] private TMP_Text recruitCountLabel;
    [SerializeField] private TMP_Text lostCountLabel;
    [SerializeField] private TMP_Text timeLabel;

    [Header("Button")]
    [SerializeField] private Button returnTitleButton;

    public event Action ReturnTitleRequested;

    private bool isShowing;

    private void Awake()
    {
        if (returnTitleButton != null)
        {
            returnTitleButton.onClick.AddListener(HandleReturnTitle);
        }
    }

    private void OnDestroy()
    {
        if (returnTitleButton != null)
        {
            returnTitleButton.onClick.RemoveListener(HandleReturnTitle);
        }
    }

    public void ShowVictory(
        string description,
        int currentSheep,
        int score,
        int recruited,
        int lost,
        float elapsedSeconds,
        string title = "冲出草原！")
    {
        ShowResult(
            title,
            description,
            currentSheep,
            score,
            recruited,
            lost,
            elapsedSeconds);
    }

    public void ShowDefeat(
        string description,
        int currentSheep,
        int score,
        int recruited,
        int lost,
        float elapsedSeconds)
    {
        ShowResult(
            "全军覆没",
            description,
            currentSheep,
            score,
            recruited,
            lost,
            elapsedSeconds);
    }

    private void ShowResult(
        string title,
        string description,
        int currentSheep,
        int score,
        int recruited,
        int lost,
        float elapsedSeconds)
    {
        // UI 层的第二层保险：避免同一个结果面板重复打开。
        if (isShowing)
        {
            return;
        }

        isShowing = true;

        resultTitle.text = title;
        resultDescription.text = description;

        sheepCountLabel.text = $"当前羊数：{currentSheep}";
        scoreLabel.text = $"当前得分：{score}";
        recruitCountLabel.text = $"成功招募：{recruited}";
        lostCountLabel.text = $"损失羊数：{lost}";
        timeLabel.text = $"游戏用时：{FormatTime(elapsedSeconds)}";

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        isShowing = false;
        gameObject.SetActive(false);
    }

    private void HandleReturnTitle()
    {
        ReturnTitleRequested?.Invoke();
    }

    private static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;

        return $"{minutes:00}:{remainingSeconds:00}";
    }
}
