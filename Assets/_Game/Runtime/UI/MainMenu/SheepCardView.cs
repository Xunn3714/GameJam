using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SheepCardView : MonoBehaviour
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
        "用于显示羊的品阶颜色。")]
    [SerializeField]
    private Outline rarityOutline;


    private Action onClick;


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
                nameText.text =
                    sheepName;
            }


            if (countText != null)
            {
                countText.text =
                    $"遇到过 {encounterCount} 次";
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
            return;


        // 未解锁时不提前透露羊的品阶
        if (!unlocked)
        {
            rarityOutline.effectColor =
                new Color32(
                    156,
                    148,
                    126,
                    170
                );

            return;
        }


        rarityOutline.effectColor =
            GetQualityColor(
                quality
            );
    }


    public static Color GetQualityColor(
        SheepQuality quality)
    {
        switch (quality)
        {
            case SheepQuality.Green:
                return new Color32(120, 220, 110, 255);


            case SheepQuality.Blue:
                return new Color32(95, 165, 255, 255);


            case SheepQuality.Purple:
                return new Color32(190, 120, 245, 255);


            case SheepQuality.Gold:
                return new Color32(255, 205, 70, 255);


            case SheepQuality.EasterEgg:
                // Card 边框只能使用一种颜色，
                // 彩色羊先使用较鲜艳的玫红色。
                // 右侧品阶文字会真正使用红/蓝/黄三色。
                return new Color32(255, 100, 180, 255);


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
}
