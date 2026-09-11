using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SheepCardView : MonoBehaviour, ISelectHandler
{
    [Header("UI")]
    public Image sheepImage;
    public TMP_Text nameText;
    public TMP_Text countText;
    public Button button;


    [Header("Card State")]
    [SerializeField]
    private Sprite normalBackground;

    [SerializeField]
    private Sprite selectedBackground;


    [Header("Rarity")]
    [Tooltip(
        "挂在 SheepCard 根对象上的 UI Outline，" +
        "与品质底色、标签一起显示羊的品阶。")]
    [SerializeField]
    private Outline rarityOutline;

    private Image qualityWash;
    private Image qualityBadge;
    private TMP_Text qualityBadgeText;
    private GameObject rainbowSegments;

    private Action onClick;

    public Image QualityWash => qualityWash;
    public Image QualityBadge => qualityBadge;
    public TMP_Text QualityBadgeText => qualityBadgeText;


    // ============================================================
    // NEW SETUP
    // ============================================================

    public void Setup(
        Sprite sprite,
        string sheepName,
        int encounterCount,
        SheepQuality quality,
        bool unlocked,
        Action clickAction)
    {
        onClick =
            clickAction;


        // ========================================================
        // IMAGE
        // ========================================================

        if (sheepImage != null)
        {
            sheepImage.type = Image.Type.Simple;
            sheepImage.preserveAspect = true;
            sheepImage.sprite =
                sprite;


            sheepImage.enabled =
                sprite != null;
        }


        // ========================================================
        // TEXT
        // ========================================================

        if (unlocked)
        {
            if (nameText != null)
            {
                ConfigureSingleLineText(nameText, 14f, 21f);
                nameText.text =
                    sheepName;
            }


            if (countText != null)
            {
                ConfigureSingleLineText(countText, 12f, 15f);
                countText.text =
                    $"发现 {Mathf.Max(0, encounterCount)} 次";
            }
        }
        else
        {
            if (nameText != null)
            {
                nameText.text =
                    "???";
            }


            if (countText != null)
            {
                countText.text =
                    "尚未解锁";
            }
        }


        // ========================================================
        // RARITY BORDER
        // ========================================================

        ApplyRarity(
            quality,
            unlocked
        );


        // ========================================================
        // BUTTON
        // ========================================================

        if (button != null)
        {
            SetSelected(false);


            button.interactable =
                unlocked;


            button.onClick.RemoveAllListeners();


            button.onClick.AddListener(
                HandleClick
            );
        }
    }


    // ============================================================
    // OLD SETUP COMPATIBILITY
    // ============================================================

    public void Setup(
        Sprite sprite,
        string sheepName,
        int encounterCount,
        bool unlocked,
        Action clickAction)
    {
        Setup(
            sprite,
            sheepName,
            encounterCount,
            SheepQuality.Common,
            unlocked,
            clickAction
        );
    }


    // ============================================================
    // SELECTED STATE
    // ============================================================

    public void SetSelected(
        bool selected)
    {
        if (button == null ||
            button.image == null)
        {
            return;
        }


        Sprite target =
            selected
                ? selectedBackground
                : normalBackground;


        if (target != null)
        {
            button.image.sprite =
                target;
        }
    }


    // ============================================================
    // RARITY
    // ============================================================

    private void ApplyRarity(
        SheepQuality quality,
        bool unlocked)
    {
        if (rarityOutline == null)
            rarityOutline = GetComponent<Outline>();

        EnsureQualityVisuals();

        Color qualityColor = unlocked
            ? GetQualityColor(quality)
            : new Color32(156, 148, 126, 255);

        if (rarityOutline != null)
        {
            rarityOutline.effectColor = qualityColor;
            rarityOutline.effectDistance = new Vector2(5f, -5f);
        }

        if (qualityWash != null)
        {
            qualityWash.color = new Color(
                qualityColor.r,
                qualityColor.g,
                qualityColor.b,
                unlocked ? 0.18f : 0.08f);
        }

        if (qualityBadge != null)
            qualityBadge.color = unlocked ? qualityColor : new Color32(120, 116, 108, 245);

        if (qualityBadgeText != null)
        {
            qualityBadgeText.richText = quality == SheepQuality.EasterEgg && unlocked;
            qualityBadgeText.text = unlocked
                ? GetBadgeText(quality)
                : "未发现";
        }

        if (unlocked && quality == SheepQuality.EasterEgg)
            EnsureRainbowSegments();
        else if (rainbowSegments != null)
            rainbowSegments.SetActive(false);
    }

    private void EnsureQualityVisuals()
    {
        if (qualityWash == null)
        {
            Transform existing = transform.Find("QualityWash");
            if (existing != null)
                qualityWash = existing.GetComponent<Image>();
        }

        if (qualityWash == null)
        {
            GameObject washObject = new GameObject(
                "QualityWash",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            washObject.layer = gameObject.layer;
            RectTransform washRect = washObject.GetComponent<RectTransform>();
            washRect.SetParent(transform, false);
            washRect.anchorMin = Vector2.zero;
            washRect.anchorMax = Vector2.one;
            washRect.offsetMin = new Vector2(8f, 8f);
            washRect.offsetMax = new Vector2(-8f, -8f);
            qualityWash = washObject.GetComponent<Image>();
            qualityWash.raycastTarget = false;
            washObject.transform.SetAsFirstSibling();
        }

        if (qualityBadge == null)
        {
            Transform existing = transform.Find("QualityBadge");
            if (existing != null)
                qualityBadge = existing.GetComponent<Image>();
        }

        if (qualityBadge == null)
        {
            GameObject badgeObject = new GameObject(
                "QualityBadge",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            badgeObject.layer = gameObject.layer;
            RectTransform badgeRect = badgeObject.GetComponent<RectTransform>();
            badgeRect.SetParent(transform, false);
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-12f, -12f);
            badgeRect.sizeDelta = new Vector2(76f, 30f);
            qualityBadge = badgeObject.GetComponent<Image>();
            qualityBadge.raycastTarget = false;
        }

        if (qualityBadgeText == null)
        {
            Transform existing = qualityBadge.transform.Find("Label");
            if (existing != null)
                qualityBadgeText = existing.GetComponent<TMP_Text>();
        }

        if (qualityBadgeText == null)
        {
            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.layer = gameObject.layer;
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(qualityBadge.transform, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(3f, 1f);
            labelRect.offsetMax = new Vector2(-3f, -1f);
            qualityBadgeText = labelObject.GetComponent<TMP_Text>();
            qualityBadgeText.font = nameText != null ? nameText.font : null;
            qualityBadgeText.fontSize = 15f;
            qualityBadgeText.fontStyle = FontStyles.Bold;
            qualityBadgeText.alignment = TextAlignmentOptions.Center;
            qualityBadgeText.color = Color.white;
            qualityBadgeText.overflowMode = TextOverflowModes.Ellipsis;
            qualityBadgeText.raycastTarget = false;
        }

        qualityBadgeText.transform.SetAsLastSibling();
    }

    private void EnsureRainbowSegments()
    {
        if (rainbowSegments == null)
        {
            Transform existing = qualityBadge.transform.Find("RainbowSegments");
            rainbowSegments = existing != null ? existing.gameObject : null;
        }

        if (rainbowSegments == null)
        {
            rainbowSegments = new GameObject("RainbowSegments", typeof(RectTransform));
            rainbowSegments.layer = gameObject.layer;
            RectTransform segmentsRect = rainbowSegments.GetComponent<RectTransform>();
            segmentsRect.SetParent(qualityBadge.transform, false);
            segmentsRect.anchorMin = Vector2.zero;
            segmentsRect.anchorMax = Vector2.one;
            segmentsRect.offsetMin = Vector2.zero;
            segmentsRect.offsetMax = Vector2.zero;

            Color[] colors =
            {
                new Color32(69, 201, 90, 255),
                new Color32(75, 142, 255, 255),
                new Color32(168, 91, 234, 255),
                new Color32(242, 185, 40, 255),
                new Color32(255, 88, 169, 255)
            };
            for (int index = 0; index < colors.Length; index++)
            {
                GameObject segment = new GameObject(
                    $"Segment{index + 1}",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                segment.layer = gameObject.layer;
                RectTransform segmentRect = segment.GetComponent<RectTransform>();
                segmentRect.SetParent(segmentsRect, false);
                segmentRect.anchorMin = new Vector2(index / (float)colors.Length, 0f);
                segmentRect.anchorMax = new Vector2((index + 1f) / colors.Length, 1f);
                segmentRect.offsetMin = Vector2.zero;
                segmentRect.offsetMax = Vector2.zero;
                Image segmentImage = segment.GetComponent<Image>();
                segmentImage.color = colors[index];
                segmentImage.raycastTarget = false;
            }
        }

        rainbowSegments.SetActive(true);
        qualityBadgeText.transform.SetAsLastSibling();
    }

    private static void ConfigureSingleLineText(TMP_Text text, float minimumSize, float maximumSize)
    {
        if (text == null)
            return;

        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.maxVisibleLines = 1;
    }

    private static string GetBadgeText(SheepQuality quality)
    {
        switch (quality)
        {
            case SheepQuality.Green:
                return "绿色";
            case SheepQuality.Blue:
                return "蓝色";
            case SheepQuality.Purple:
                return "紫色";
            case SheepQuality.Gold:
                return "金色";
            case SheepQuality.EasterEgg:
                return "彩色";
            case SheepQuality.Common:
            default:
                return "普通";
        }
    }


    public static Color GetQualityColor(
        SheepQuality quality)
    {
        switch (quality)
        {
            case SheepQuality.Green:
                return new Color32(69, 201, 90, 255);


            case SheepQuality.Blue:
                return new Color32(75, 142, 255, 255);


            case SheepQuality.Purple:
                return new Color32(168, 91, 234, 255);


            case SheepQuality.Gold:
                return new Color32(242, 185, 40, 255);


            case SheepQuality.EasterEgg:
                return new Color32(255, 88, 169, 255);


            case SheepQuality.Common:
            default:
                return new Color32(70, 155, 120, 255);
        }
    }


    // ============================================================
    // RARITY TEXT
    // ============================================================

    public static string GetRarityName(
        SheepQuality quality,
        string configuredName)
    {
        if (!string.IsNullOrWhiteSpace(
            configuredName))
        {
            return configuredName.Trim();
        }


        switch (quality)
        {
            case SheepQuality.Green:
                return "绿色羊";

            case SheepQuality.Blue:
                return "蓝色羊";

            case SheepQuality.Purple:
                return "紫色羊";

            case SheepQuality.Gold:
                return "金色羊";

            case SheepQuality.EasterEgg:
                return "彩色羊";

            case SheepQuality.Common:
            default:
                return "普通羊";
        }
    }


    public static string GetRarityRichText(
        SheepQuality quality,
        string configuredName)
    {
        if (quality ==
            SheepQuality.EasterEgg)
        {
            // 三原色：
            // 彩 = 红
            // 色 = 蓝
            // 羊 = 黄
            return
                "（" +
                "<color=#E05252>彩</color>" +
                "<color=#4C7ED6>色</color>" +
                "<color=#E4B63E>羊</color>" +
                "）";
        }


        string rarityName =
            GetRarityName(
                quality,
                configuredName
            );


        return rarityName;
    }


    // ============================================================
    // CLICK
    // ============================================================

    private void HandleClick()
    {
        onClick?.Invoke();
    }

    public void OnSelect(BaseEventData eventData)
    {
        if (button != null && button.interactable)
            onClick?.Invoke();
    }

    public void Focus()
    {
        if (button != null && button.interactable)
            button.Select();
    }
}
