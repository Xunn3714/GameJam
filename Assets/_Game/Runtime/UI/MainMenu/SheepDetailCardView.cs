using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>图鉴右侧详情卡的数据绑定，可供图鉴本体和首次发现弹卡共同复用。</summary>
[DisallowMultipleComponent]
public sealed class SheepDetailCardView : MonoBehaviour
{
    [SerializeField] private Image sheepImage;
    [SerializeField] private TMP_Text sheepNameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text abilityNameText;
    [SerializeField] private TMP_Text abilityDescriptionText;
    [SerializeField] private Image sentenceImage;

    public Image SheepImage => sheepImage;
    public TMP_Text SheepNameText => sheepNameText;
    public TMP_Text RarityText => rarityText;
    public TMP_Text CountText => countText;
    public TMP_Text DescriptionText => descriptionText;

    public void Configure(
        Image image,
        TMP_Text name,
        TMP_Text rarity,
        TMP_Text count,
        TMP_Text description,
        TMP_Text abilityName,
        TMP_Text abilityDescription,
        Image sentence)
    {
        sheepImage = image;
        sheepNameText = name;
        rarityText = rarity;
        countText = count;
        descriptionText = description;
        abilityNameText = abilityName;
        abilityDescriptionText = abilityDescription;
        sentenceImage = sentence;

        ConfigureImage(sheepImage);
        ConfigureSingleLineText(sheepNameText, 20f, 30f);
        ConfigureSingleLineText(rarityText, 15f, 22f);
        ConfigureSingleLineText(countText, 14f, 18f);
        ConfigureParagraphText(descriptionText, 12, 15f, 19f);
        ConfigureSingleLineText(abilityNameText, 15f, 21f);
        ConfigureParagraphText(abilityDescriptionText, 3, 14f, 18f);
    }

    public void Show(SheepCollectionEntry sheep, int encounterCount)
    {
        if (sheep == null)
            return;

        if (sheepImage != null)
        {
            ConfigureImage(sheepImage);
            sheepImage.sprite = sheep.icon;
            sheepImage.enabled = sheep.icon != null;
        }

        if (sheepNameText != null)
            sheepNameText.text = sheep.displayName;

        if (rarityText != null)
        {
            rarityText.richText = true;
            rarityText.text = SheepCardView.GetRarityRichText(sheep.quality, sheep.rarityName);
            rarityText.color = sheep.quality == SheepQuality.EasterEgg
                ? Color.white
                : SheepCardView.GetQualityColor(sheep.quality);
        }

        if (countText != null)
            countText.text = $"发现次数：{Mathf.Max(0, encounterCount)}";

        if (descriptionText != null)
            descriptionText.text = string.IsNullOrWhiteSpace(sheep.description)
                ? "暂无描述。"
                : sheep.description.Trim();

        bool hasAbilityName = !string.IsNullOrWhiteSpace(sheep.abilityName);
        if (abilityNameText != null)
        {
            abilityNameText.gameObject.SetActive(hasAbilityName);
            abilityNameText.text = sheep.abilityName;
        }

        bool hasAbilityDescription = !string.IsNullOrWhiteSpace(sheep.abilityDescription);
        if (abilityDescriptionText != null)
        {
            abilityDescriptionText.gameObject.SetActive(hasAbilityDescription);
            abilityDescriptionText.text = sheep.abilityDescription;
        }

        if (sentenceImage != null)
        {
            sentenceImage.sprite = sheep.sentenceImage;
            sentenceImage.enabled = sheep.sentenceImage != null;
        }
    }

    public void Clear()
    {
        if (sheepImage != null)
        {
            sheepImage.sprite = null;
            sheepImage.enabled = false;
        }

        if (sheepNameText != null)
            sheepNameText.text = "选择一只羊";
        if (rarityText != null)
            rarityText.text = string.Empty;
        if (countText != null)
            countText.text = string.Empty;
        if (descriptionText != null)
            descriptionText.text = "在草原上遇见新的羊，\n它的资料就会记录在这里。";
        if (abilityNameText != null)
        {
            abilityNameText.gameObject.SetActive(false);
            abilityNameText.text = string.Empty;
        }
        if (abilityDescriptionText != null)
        {
            abilityDescriptionText.gameObject.SetActive(false);
            abilityDescriptionText.text = string.Empty;
        }
        if (sentenceImage != null)
        {
            sentenceImage.sprite = null;
            sentenceImage.enabled = false;
        }
    }

    private static void ConfigureImage(Image image)
    {
        if (image == null)
            return;

        image.type = Image.Type.Simple;
        image.preserveAspect = true;
    }

    private static void ConfigureSingleLineText(
        TMP_Text text,
        float minimumSize,
        float maximumSize)
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

    private static void ConfigureParagraphText(
        TMP_Text text,
        int maximumLines,
        float minimumSize,
        float maximumSize)
    {
        if (text == null)
            return;

        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.maxVisibleLines = maximumLines;
    }
}
