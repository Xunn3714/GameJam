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
    public TMP_Text countText;
    public TMP_Text descriptionText;
    public TMP_Text abilityNameText;
    public TMP_Text abilityDescriptionText;
    public Image sentenceImage;


    private void OnEnable()
    {
        RefreshCollection();
    }


    public void RefreshCollection()
    {
        ClearCards();

        if (SheepCollectionManager.Instance == null)
        {
            Debug.LogWarning(
                "SheepCollectionManager Instance not found.");

            return;
        }

        UpdateProgress();

        List<SheepCollectionEntry> sheepList =
            SheepCollectionManager.Instance.GetAllSheep();

        SheepCollectionEntry firstUnlocked = null;

        foreach (SheepCollectionEntry sheep in sheepList)
        {
            bool unlocked =
                SheepCollectionManager.Instance
                    .IsUnlocked(sheep.sheepId);

            int encounterCount =
                SheepCollectionManager.Instance
                    .GetEncounterCount(sheep.sheepId);

            SheepCollectionEntry capturedSheep = sheep;

            SheepCardView card =
                Instantiate(
                    sheepCardPrefab,
                    contentRoot);

            Sprite cardSprite =
                unlocked ? sheep.icon : null;

            card.Setup(
                cardSprite,
                sheep.displayName,
                encounterCount,
                unlocked,
                () => ShowSheepDetail(capturedSheep)
            );

            if (unlocked && firstUnlocked == null)
            {
                firstUnlocked = sheep;
            }
        }

        if (firstUnlocked != null)
        {
            ShowSheepDetail(firstUnlocked);
        }
        else
        {
            ClearDetail();
        }
    }


    private void UpdateProgress()
    {
        int unlocked =
            SheepCollectionManager.Instance
                .GetUnlockedCount();

        int total =
            SheepCollectionManager.Instance
                .GetTotalCount();

        if (progressText != null)
        {
            progressText.text =
                $"Unlocked: {unlocked} / {total}";
        }
    }


    public void ShowSheepDetail(
        SheepCollectionEntry sheep)
    {
        if (sheep == null)
            return;

        bool unlocked =
            SheepCollectionManager.Instance
                .IsUnlocked(sheep.sheepId);

        if (!unlocked)
            return;

        int encounterCount =
            SheepCollectionManager.Instance
                .GetEncounterCount(sheep.sheepId);

        if (detailImage != null)
        {
            detailImage.sprite = sheep.icon;
            detailImage.enabled = sheep.icon != null;
        }

        if (sheepNameText != null)
            sheepNameText.text = sheep.displayName;

        if (countText != null)
            countText.text =
                $"Encountered {encounterCount} times";

        if (descriptionText != null)
            descriptionText.text =
                sheep.description;

        if (abilityNameText != null)
            abilityNameText.text =
                sheep.abilityName;

        if (abilityDescriptionText != null)
            abilityDescriptionText.text =
                sheep.abilityDescription;

        if (sentenceImage != null)
        {
            sentenceImage.sprite =
                sheep.sentenceImage;

            sentenceImage.enabled =
                sheep.sentenceImage != null;
        }
    }


    private void ClearDetail()
    {
        if (detailImage != null)
        {
            detailImage.sprite = null;
            detailImage.enabled = false;
        }

        if (sheepNameText != null)
            sheepNameText.text = "";

        if (countText != null)
            countText.text = "";

        if (descriptionText != null)
            descriptionText.text = "";

        if (abilityNameText != null)
            abilityNameText.text = "";

        if (abilityDescriptionText != null)
            abilityDescriptionText.text = "";

        if (sentenceImage != null)
        {
            sentenceImage.sprite = null;
            sentenceImage.enabled = false;
        }
    }


    private void ClearCards()
    {
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(
                contentRoot.GetChild(i).gameObject);
        }
    }
}
