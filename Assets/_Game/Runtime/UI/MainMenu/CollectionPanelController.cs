using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CollectionPanelController : MonoBehaviour
{
    [Header("Collection")]
    public Transform contentRoot;
    public SheepCardView sheepCardPrefab;


    [Header("Top")]
    public TMP_Text progressText;


    [Header("Detail")]
    public Image detailImage;
    public TMP_Text sheepNameText;
    public TMP_Text rarityText;
    public TMP_Text countText;
    public TMP_Text descriptionText;
    public TMP_Text abilityNameText;
    public TMP_Text abilityDescriptionText;
    public Image sentenceImage;


    private SheepCardView selectedCard;


    private void OnEnable()
    {
        RefreshCollection();
    }


    // ============================================================
    // REFRESH
    // ============================================================

    public void RefreshCollection()
    {
        ClearCards();


        selectedCard =
            null;


        if (SheepCollectionManager.Instance == null)
        {
            Debug.LogWarning(
                "CollectionPanelController: SheepCollectionManager Instance not found."
            );


            ClearDetail();

            return;
        }


        UpdateProgress();


        List<SheepCollectionEntry> sheepList =
            SheepCollectionManager.Instance
                .GetAllSheep();


        SheepCollectionEntry firstUnlocked =
            null;


        SheepCardView firstUnlockedCard =
            null;


        foreach (SheepCollectionEntry sheep
                 in sheepList)
        {
            if (sheep == null)
                continue;


            bool unlocked =
                SheepCollectionManager.Instance
                    .IsUnlocked(
                        sheep.sheepId
                    );


            int encounterCount =
                SheepCollectionManager.Instance
                    .GetEncounterCount(
                        sheep.sheepId
                    );


            SheepCollectionEntry capturedSheep =
                sheep;


            SheepCardView card =
                Instantiate(
                    sheepCardPrefab,
                    contentRoot
                );


            Sprite cardSprite =
                unlocked
                    ? sheep.icon
                    : null;


            card.Setup(
                cardSprite,
                sheep.displayName,
                encounterCount,
                sheep.quality,
                unlocked,
                () =>
                    SelectCard(
                        card,
                        capturedSheep
                    )
            );


            if (unlocked &&
                firstUnlocked == null)
            {
                firstUnlocked =
                    sheep;


                firstUnlockedCard =
                    card;
            }
        }


        // 默认选中第一只已经解锁的羊
        if (firstUnlocked != null &&
            firstUnlockedCard != null)
        {
            SelectCard(
                firstUnlockedCard,
                firstUnlocked
            );
        }
        else
        {
            ClearDetail();
        }
    }


    // ============================================================
    // PROGRESS
    // ============================================================

    private void UpdateProgress()
    {
        if (SheepCollectionManager.Instance == null)
            return;


        int unlocked =
            SheepCollectionManager.Instance
                .GetUnlockedCount();


        int total =
            SheepCollectionManager.Instance
                .GetTotalCount();


        if (progressText != null)
        {
            progressText.text =
                $"已解锁  {unlocked} / {total}";
        }
    }


    // ============================================================
    // CARD SELECT
    // ============================================================

    private void SelectCard(
        SheepCardView card,
        SheepCollectionEntry sheep)
    {
        if (card == null ||
            sheep == null)
        {
            return;
        }


        if (selectedCard != null &&
            selectedCard != card)
        {
            selectedCard.SetSelected(
                false
            );
        }


        selectedCard =
            card;


        selectedCard.SetSelected(
            true
        );


        ShowSheepDetail(
            sheep
        );
    }


    // ============================================================
    // DETAIL
    // ============================================================

    public void ShowSheepDetail(
        SheepCollectionEntry sheep)
    {
        if (sheep == null)
            return;


        if (SheepCollectionManager.Instance == null)
            return;


        bool unlocked =
            SheepCollectionManager.Instance
                .IsUnlocked(
                    sheep.sheepId
                );


        if (!unlocked)
            return;


        int encounterCount =
            SheepCollectionManager.Instance
                .GetEncounterCount(
                    sheep.sheepId
                );


        // ========================================================
        // IMAGE
        // ========================================================

        if (detailImage != null)
        {
            detailImage.sprite =
                sheep.icon;


            detailImage.enabled =
                sheep.icon != null;
        }


        // ========================================================
        // NAME
        // ========================================================

        if (sheepNameText != null)
        {
            sheepNameText.text =
                sheep.displayName;
        }


        // ========================================================
        // RARITY
        // ========================================================

        if (rarityText != null)
        {
            rarityText.richText =
                true;


            rarityText.text =
                SheepCardView.GetRarityRichText(
                    sheep.quality,
                    sheep.rarityName
                );


            // 彩色羊的每个字自己带颜色，
            // 所以整个 TMP 保持白色。
            if (sheep.quality ==
                SheepQuality.EasterEgg)
            {
                rarityText.color =
                    Color.white;
            }
            else
            {
                rarityText.color =
                    SheepCardView.GetQualityColor(
                        sheep.quality
                    );
            }
        }


        // ========================================================
        // COUNT
        // ========================================================

        if (countText != null)
        {
            countText.text =
                $"遇到过 {encounterCount} 次";
        }


        // ========================================================
        // DESCRIPTION
        // ========================================================

        if (descriptionText != null)
        {
            descriptionText.text =
                sheep.description;
        }


        // ========================================================
        // ABILITY
        // ========================================================

        if (abilityNameText != null)
        {
            abilityNameText.text =
                sheep.abilityName;
        }


        if (abilityDescriptionText != null)
        {
            abilityDescriptionText.text =
                sheep.abilityDescription;
        }


        // ========================================================
        // SENTENCE IMAGE
        // ========================================================

        if (sentenceImage != null)
        {
            sentenceImage.sprite =
                sheep.sentenceImage;


            sentenceImage.enabled =
                sheep.sentenceImage != null;
        }
    }


    // ============================================================
    // CLEAR DETAIL
    // ============================================================

    private void ClearDetail()
    {
        if (detailImage != null)
        {
            detailImage.sprite =
                null;


            detailImage.enabled =
                false;
        }


        if (sheepNameText != null)
        {
            sheepNameText.text =
                "选择一只羊";
        }


        if (rarityText != null)
        {
            rarityText.text =
                "";
        }


        if (countText != null)
        {
            countText.text =
                "";
        }


        if (descriptionText != null)
        {
            descriptionText.text =
                "在草原上遇见新的羊，\n它的资料就会记录在这里。";
        }


        if (abilityNameText != null)
        {
            abilityNameText.text =
                "";
        }


        if (abilityDescriptionText != null)
        {
            abilityDescriptionText.text =
                "";
        }


        if (sentenceImage != null)
        {
            sentenceImage.sprite =
                null;


            sentenceImage.enabled =
                false;
        }
    }


    // ============================================================
    // CLEAR CARDS
    // ============================================================

    private void ClearCards()
    {
        if (contentRoot == null)
            return;


        for (
            int i = contentRoot.childCount - 1;
            i >= 0;
            i--)
        {
            Destroy(
                contentRoot
                    .GetChild(i)
                    .gameObject
            );
        }
    }
}
