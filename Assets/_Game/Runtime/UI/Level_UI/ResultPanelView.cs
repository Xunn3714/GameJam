using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultPanelView : MonoBehaviour
{
    public sealed class JourneySummary
    {
        public bool Victory { get; set; }
        public bool IsTrueEnding { get; set; }
        public int CurrentSheep { get; set; }
        public int HighestSheep { get; set; }
        public int RecruitedSheep { get; set; }
        public int LostSheep { get; set; }
        public int DestructionScore { get; set; }
        public int DestroyedObjects { get; set; }
        public int TrueEndingFlockSize { get; set; }
        public float ElapsedSeconds { get; set; }
        public float HoleSquareMeters { get; set; }
        public float CaitaiJin { get; set; }
        public int PoopCount { get; set; }
        public string FeaturedSheepName { get; set; }
        public int FeaturedSheepCount { get; set; }
        public Sprite FeaturedSheepSprite { get; set; }
    }

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

    [Header("Journey Summary")]
    [SerializeField] private Sprite defeatPoopSprite;

    public event Action ReturnTitleRequested;

    private bool isShowing;
    private Image journeySummaryPaper;
    private Image journeyStatsPaper;
    private Image featuredSheepImage;
    private TMP_Text journeyStatsHeading;
    private TMP_Text featuredSheepHeading;
    private TMP_Text featuredSheepName;
    private TMP_Text featuredSheepCount;
    private TMP_Text poopCountText;
    private TMP_Text retryHint;

    public void StyleJourneySummary(JourneySummary summary)
    {
        if (summary == null || resultDescription == null)
            return;

        BuildJourneySummaryLayout();
        ApplyPrimaryStats(summary);

        journeyStatsHeading.text = $"旅程统计　<size=75%>{FormatTime(summary.ElapsedSeconds)}</size>";
        bool showDefeatPoop = !summary.Victory && defeatPoopSprite != null;
        bool hasFeaturedSheep = summary.Victory
            && summary.FeaturedSheepSprite != null
            && summary.FeaturedSheepCount > 0;
        featuredSheepImage.enabled = showDefeatPoop || hasFeaturedSheep;
        featuredSheepImage.sprite = showDefeatPoop
            ? defeatPoopSprite
            : hasFeaturedSheep ? summary.FeaturedSheepSprite : null;
        MvpUiFactory.Anchor(featuredSheepImage.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f,
            new Vector2(265f, showDefeatPoop ? 15f : 35f),
            showDefeatPoop ? new Vector2(360f, 230f) : new Vector2(300f, 190f));

        featuredSheepHeading.text = showDefeatPoop ? "最后留下的……" : "本局代表羊";
        featuredSheepName.text = showDefeatPoop
            ? "一坨屎"
            : hasFeaturedSheep ? summary.FeaturedSheepName : "没有特殊羊同行";
        featuredSheepCount.text = showDefeatPoop
            ? "羊没了，屎还在"
            : hasFeaturedSheep ? $"最终同行 {summary.FeaturedSheepCount} 只" : "下次再遇见新的伙伴吧";
        poopCountText.text = $"本局拉屎　<size=145%><b>{summary.PoopCount}</b></size> 次";
        retryHint.text = "按 R 再来一局";
    }

    private void BuildJourneySummaryLayout()
    {
        if (journeyStatsPaper != null)
            return;

        Transform parent = resultDescription.rectTransform.parent;
        resultDescription.gameObject.SetActive(false);

        journeyStatsPaper = CreatePaper("JourneyStatsPaper", parent, new Vector2(-300f, -28f), new Vector2(430f, 400f));
        journeySummaryPaper = CreatePaper("JourneySummaryPaper", parent, new Vector2(265f, -28f), new Vector2(560f, 400f));
        journeyStatsPaper.transform.SetSiblingIndex(0);
        journeySummaryPaper.transform.SetSiblingIndex(1);

        journeyStatsHeading = CreateText("JourneyStatsHeading", parent, "旅程统计", new Vector2(-300f, 140f),
            new Vector2(370f, 48f), 27f, TextAlignmentOptions.Center, FontStyles.Bold);

        featuredSheepHeading = CreateText("FeaturedSheepHeading", parent, "本局代表羊", new Vector2(265f, 142f),
            new Vector2(500f, 44f), 26f, TextAlignmentOptions.Center, FontStyles.Bold);
        featuredSheepHeading.color = new Color32(65, 57, 39, 255);

        featuredSheepImage = MvpUiFactory.CreateImage("FeaturedSheepImage", parent, Color.white);
        MvpUiFactory.Anchor(featuredSheepImage.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f,
            new Vector2(265f, 35f), new Vector2(300f, 190f));
        featuredSheepImage.preserveAspect = true;
        featuredSheepImage.raycastTarget = false;

        featuredSheepName = CreateText("FeaturedSheepName", parent, string.Empty, new Vector2(265f, -76f),
            new Vector2(500f, 42f), 27f, TextAlignmentOptions.Center, FontStyles.Bold);
        featuredSheepCount = CreateText("FeaturedSheepCount", parent, string.Empty, new Vector2(265f, -112f),
            new Vector2(500f, 34f), 20f, TextAlignmentOptions.Center);

        Image divider = MvpUiFactory.CreateImage("JourneySummaryDivider", parent, new Color32(101, 88, 59, 65));
        MvpUiFactory.Anchor(divider.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f,
            new Vector2(265f, -140f), new Vector2(450f, 2f));
        divider.raycastTarget = false;

        poopCountText = CreateText("PoopCount", parent, string.Empty, new Vector2(265f, -174f),
            new Vector2(500f, 52f), 22f, TextAlignmentOptions.Center, FontStyles.Bold);
        retryHint = CreateText("RetryHint", parent, string.Empty, new Vector2(265f, -210f),
            new Vector2(500f, 26f), 17f, TextAlignmentOptions.Center);
        retryHint.color = new Color32(101, 88, 59, 220);

        PositionPrimaryStatLabels();
    }

    private void ApplyPrimaryStats(JourneySummary summary)
    {
        if (summary.IsTrueEnding)
        {
            sheepCountLabel.text = $"踩塔羊数：{summary.TrueEndingFlockSize}";
            scoreLabel.text = $"菜薹重量：{summary.CaitaiJin:0.#} 斤";
            recruitCountLabel.text = $"大洞面积：{summary.HoleSquareMeters:0.#} ㎡";
        }
        else
        {
            sheepCountLabel.text = summary.Victory
                ? $"成功带出：{summary.CurrentSheep} 只"
                : $"当前羊数：{summary.CurrentSheep} 只";
            scoreLabel.text = $"本局最多：{summary.HighestSheep} 只";
            recruitCountLabel.text = $"成功招募：{summary.RecruitedSheep} 只";
        }

        lostCountLabel.text = $"被狼叼走：{summary.LostSheep} 只";
        timeLabel.text = $"破坏得分：{summary.DestructionScore}（{summary.DestroyedObjects} 件）";
    }

    private void PositionPrimaryStatLabels()
    {
        TMP_Text[] labels = { sheepCountLabel, scoreLabel, recruitCountLabel, lostCountLabel, timeLabel };
        float[] y = { 78f, 18f, -42f, -102f, -162f };
        for (int index = 0; index < labels.Length; index++)
        {
            TMP_Text label = labels[index];
            if (label == null)
                continue;

            MvpUiFactory.Anchor(label.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f,
                new Vector2(-300f, y[index]), new Vector2(360f, 46f));
            label.fontSize = 23f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 23f;
            label.alignment = TextAlignmentOptions.Left;
            label.color = new Color32(65, 57, 39, 255);
        }
    }

    private static Image CreatePaper(string name, Transform parent, Vector2 position, Vector2 size)
    {
        Image paper = MvpUiFactory.CreateImage(name, parent, new Color32(238, 229, 195, 180));
        MvpUiFactory.Anchor(paper.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size);
        paper.raycastTarget = false;
        Outline outline = paper.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(101, 88, 59, 60);
        outline.effectDistance = new Vector2(1f, -1f);
        return paper;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        FontStyles style = FontStyles.Normal)
    {
        TMP_Text text = MvpUiFactory.CreateText(name, parent, value, fontSize, alignment);
        MvpUiFactory.Anchor(text.rectTransform, Vector2.one * 0.5f, Vector2.one * 0.5f, position, size);
        text.fontStyle = style;
        text.color = new Color32(65, 57, 39, 255);
        return text;
    }

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
