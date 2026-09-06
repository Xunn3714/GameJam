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

    private static readonly Color Ink = new Color32(65, 57, 39, 255);
    private static readonly Color MutedInk = new Color32(101, 88, 59, 255);
    private static readonly string[] Encouragements =
    {
        "每一只羊，\n都是一段值得记录的冒险。",
        "走得慢一点也没关系，\n总有同伴愿意等你。",
        "曾经独自出发的你，\n已经成为许多羊的方向。",
        "偶尔走散，也别灰心。\n下一次相遇，依然值得期待。",
        "草原很大，路还很长，\n有你同行，就不算远。",
        "勇敢不一定是跑得最快，\n也可以是再出发一次。"
    };

    // Cosmetic choices must not advance the gameplay random sequence.
    private readonly System.Random decorationRandom = new System.Random();
    private ScrollRect scrollRect;
    private Image companionImage;
    private TMP_Text companionName;
    private TMP_Text companionNote;
    private TMP_Text encouragement;

    private void OnEnable()
    {
        RefreshStats();
    }

    public void RefreshStats()
    {
        if (contentRoot == null || statRowPrefab == null)
            return;

        EnsureLayout();
        ClearRows();
        List<GameStatEntry> stats = GameStatsManager.Instance != null
            ? GameStatsManager.Instance.GetAllStats() : new List<GameStatEntry>();

        if (emptyStateText != null)
        {
            emptyStateText.text = "完成一段旅程，\n让这里留下你的足迹。";
            emptyStateText.gameObject.SetActive(stats.Count == 0);
        }

        foreach (GameStatEntry stat in stats)
        {
            string value = !stat.hasValue
                && (stat.key == "largest_win_flock" || stat.key == "smallest_win_flock")
                ? "—" : stat.GetFormattedValue();
            AddRow(stat.displayName, value);

            if (stat.key == "sheep_collected" && SheepCollectionManager.Instance != null)
            {
                SheepCollectionManager collection = SheepCollectionManager.Instance;
                AddRow("已发现羊种", $"{collection.GetUnlockedCount()} / {collection.GetTotalCount()} 种");
            }
        }

        RefreshCompanion();
        UpdateScrollbarVisibility();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void AddRow(string title, string value)
    {
        StatRowView row = Instantiate(statRowPrefab, contentRoot);
        LayoutElement layout = row.GetComponent<LayoutElement>();
        if (layout == null) layout = row.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = 0f;
        layout.preferredWidth = -1f;
        layout.minHeight = layout.preferredHeight = 76f;
        layout.flexibleHeight = 0f;
        row.Setup(title, value);
        StyleRowText(row.nameText, false);
        StyleRowText(row.valueText, true);
    }

    private static void StyleRowText(TMP_Text label, bool isValue)
    {
        if (label == null) return;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(isValue ? 1f : 0f, 0.5f);
        rect.anchoredPosition = new Vector2(isValue ? -22f : 22f, 0f);
        rect.sizeDelta = new Vector2(isValue ? 172f : 430f, 60f);
        label.fontSize = 24f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 24f;
        label.color = Ink;
        label.alignment = isValue ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        MvpTmpUiFont.Apply(label);
    }

    private void EnsureLayout()
    {
        if (companionImage != null)
            return;
        scrollRect = contentRoot.GetComponentInParent<ScrollRect>(true);
        if (scrollRect == null)
            return;

        RectTransform scrollTransform = (RectTransform)scrollRect.transform;
        Place(scrollTransform, new Vector2(-325f, -58f), new Vector2(800f, 570f));
        RectTransform content = (RectTransform)contentRoot;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup rows = content.GetComponent<VerticalLayoutGroup>();
        if (rows == null) rows = content.gameObject.AddComponent<VerticalLayoutGroup>();
        rows.padding = new RectOffset(22, 34, 20, 20);
        rows.spacing = 10f;
        rows.childAlignment = TextAnchor.UpperCenter;
        rows.childControlWidth = rows.childControlHeight = true;
        rows.childForceExpandWidth = true;
        rows.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (emptyStateText != null)
            Place(emptyStateText.rectTransform, new Vector2(-325f, -58f), new Vector2(680f, 120f));

        Transform window = scrollTransform.parent;
        Image panel = MvpUiFactory.CreateImage("StatisticsCompanion", window,
            new Color32(238, 229, 195, 150));
        panel.raycastTarget = false;
        Place(panel.rectTransform, new Vector2(465f, -58f), new Vector2(470f, 570f));
        Outline border = panel.gameObject.AddComponent<Outline>();
        border.effectColor = new Color32(101, 88, 59, 55);
        border.effectDistance = new Vector2(1f, -1f);

        CreateLabel(panel.transform, "CompanionHeading", "一路同行", -32f, 22f, 35f).color = MutedInk;
        Image quotePaper = MvpUiFactory.CreateImage("QuotePaper", panel.transform,
            new Color32(249, 237, 198, 255));
        Place(quotePaper.rectTransform, new Vector2(0f, 137f), new Vector2(414f, 142f));
        quotePaper.raycastTarget = false;
        Outline quoteBorder = quotePaper.gameObject.AddComponent<Outline>();
        quoteBorder.effectColor = new Color32(181, 143, 68, 160);
        quoteBorder.effectDistance = new Vector2(1.5f, -1.5f);
        encouragement = MvpUiFactory.CreateText("Encouragement", quotePaper.transform,
            string.Empty, 27f, TextAlignmentOptions.Center);
        MvpUiFactory.Stretch(encouragement.rectTransform, 16f);
        encouragement.color = Ink;
        encouragement.enableAutoSizing = true;
        encouragement.fontSizeMin = 22f;
        encouragement.fontSizeMax = 27f;

        companionImage = MvpUiFactory.CreateImage("CollectedSheep", panel.transform, Color.white);
        Place(companionImage.rectTransform, new Vector2(0f, -37f), new Vector2(260f, 205f));
        companionImage.preserveAspect = true;
        companionImage.raycastTarget = false;
        companionName = CreateLabel(panel.transform, "SheepName", string.Empty, -437f, 29f, 43f);
        companionName.fontStyle = FontStyles.Bold;
        companionNote = CreateLabel(panel.transform, "SheepNote", string.Empty, -480f, 20f, 32f);
        companionNote.color = MutedInk;
        CreateLabel(panel.transform, "ClosingWords", "继续让更多的羊，加入你的故事吧。", -523f, 20f, 35f);

        TMP_Text scope = MvpUiFactory.CreateText("StatisticsScope", window,
            "旅程纪录在结算后更新 · 收集数量实时累计", 18f, TextAlignmentOptions.Center);
        scope.color = MutedInk;
        Place(scope.rectTransform, new Vector2(-325f, -369f), new Vector2(800f, 30f));
    }

    private static TMP_Text CreateLabel(Transform parent, string name, string text, float y, float size, float height)
    {
        TMP_Text label = MvpUiFactory.CreateText(name, parent, text, size, TextAlignmentOptions.Center);
        MvpUiFactory.Anchor(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, y), new Vector2(420f, height));
        label.color = Ink;
        return label;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        MvpUiFactory.Anchor(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
    }

    private void RefreshCompanion()
    {
        if (companionImage == null)
            return;
        List<SheepCollectionEntry> candidates = new List<SheepCollectionEntry>();
        HashSet<string> seen = new HashSet<string>();
        SheepCollectionManager collection = SheepCollectionManager.Instance;
        if (collection != null)
        {
            foreach (SheepCollectionEntry entry in collection.GetAllSheep())
            {
                if (entry != null && entry.icon != null && collection.IsUnlocked(entry.sheepId)
                    && seen.Add(entry.sheepId))
                    candidates.Add(entry);
            }
        }

        bool hasCompanion = candidates.Count > 0;
        companionImage.enabled = hasCompanion;
        if (!hasCompanion)
        {
            companionImage.sprite = null;
            companionName.text = "下一位旅伴，还在草原等你";
            companionNote.text = "遇见新的羊，这里就会多一位朋友";
            encouragement.text = "每一段热闹的旅程，\n都从勇敢迈出第一步开始。";
            return;
        }

        SheepCollectionEntry companion = candidates[decorationRandom.Next(candidates.Count)];
        companionImage.sprite = companion.icon;
        companionName.text = companion.displayName;
        companionNote.text = "图鉴里，与你相遇过的老朋友";
        encouragement.text = Encouragements[decorationRandom.Next(Encouragements.Length)];
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
        if (scrollRect == null || scrollRect.verticalScrollbar == null)
            return;
        RectTransform content = (RectTransform)contentRoot;
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
        float viewportHeight = scrollRect.viewport != null ? scrollRect.viewport.rect.height : 0f;
        scrollRect.verticalScrollbar.gameObject.SetActive(content.rect.height > viewportHeight + 1f);
    }
}
