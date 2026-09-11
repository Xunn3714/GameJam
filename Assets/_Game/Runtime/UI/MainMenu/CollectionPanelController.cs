using System.Collections;
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
    private SheepDetailCardView detailCard;


    private Image scrollbarSheepIcon;
    private ScrollRect collectionScroll;
    private Coroutine ensureVisibleRoutine;

    private void OnEnable()
    {
        EnsureScrollbarSheepIcon();
        GetOrCreateDetailCard();
        RefreshCollection();
    }

    public SheepDetailCardView GetOrCreateDetailCard()
    {
        if (detailCard != null)
            return detailCard;

        Transform detailRoot = detailImage != null ? detailImage.transform.parent : null;
        if (detailRoot == null)
            return null;

        detailCard = detailRoot.GetComponent<SheepDetailCardView>();
        if (detailCard == null)
            detailCard = detailRoot.gameObject.AddComponent<SheepDetailCardView>();

        detailCard.Configure(
            detailImage,
            sheepNameText,
            rarityText,
            countText,
            descriptionText,
            abilityNameText,
            abilityDescriptionText,
            sentenceImage);
        return detailCard;
    }

    private void EnsureScrollbarSheepIcon()
    {
        if (contentRoot == null)
            return;

        if (collectionScroll == null)
            collectionScroll = contentRoot.GetComponentInParent<ScrollRect>(true);

        if (scrollbarSheepIcon != null)
            return;

        RectTransform handle = collectionScroll != null && collectionScroll.verticalScrollbar != null
            ? collectionScroll.verticalScrollbar.handleRect : null;
        if (handle == null)
            return;

        Sprite sheepSprite = Resources.Load<Sprite>("SheepScrollbarThumb");
        if (sheepSprite == null)
            return;

        GameObject iconObject = new GameObject("SheepScrollbarIcon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconObject.layer = handle.gameObject.layer;
        RectTransform iconRect = (RectTransform)iconObject.transform;
        iconRect.SetParent(handle, false);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = Vector2.zero;
        iconRect.sizeDelta = new Vector2(80f, 60f);

        scrollbarSheepIcon = iconObject.GetComponent<Image>();
        scrollbarSheepIcon.sprite = sheepSprite;
        scrollbarSheepIcon.preserveAspect = true;
        // The existing green handle continues to receive clicks and drags.
        scrollbarSheepIcon.raycastTarget = false;
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
            firstUnlockedCard.Focus();
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

        QueueEnsureCardVisible(card);
    }

    private void QueueEnsureCardVisible(SheepCardView card)
    {
        if (!isActiveAndEnabled || card == null)
            return;

        if (ensureVisibleRoutine != null)
            StopCoroutine(ensureVisibleRoutine);

        ensureVisibleRoutine = StartCoroutine(
            EnsureCardVisibleAfterLayout(card.transform as RectTransform));
    }

    private IEnumerator EnsureCardVisibleAfterLayout(RectTransform cardRect)
    {
        yield return null;
        ensureVisibleRoutine = null;
        EnsureCardVisible(cardRect);
    }

    public void EnsureCardVisible(RectTransform cardRect)
    {
        if (cardRect == null || contentRoot == null)
            return;

        if (collectionScroll == null)
            collectionScroll = contentRoot.GetComponentInParent<ScrollRect>(true);

        RectTransform viewport = collectionScroll != null
            ? collectionScroll.viewport
            : null;
        RectTransform content = contentRoot as RectTransform;
        if (collectionScroll == null || viewport == null || content == null)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        Bounds viewportBounds = new Bounds(viewport.rect.center, viewport.rect.size);
        Bounds contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            viewport,
            content);
        Bounds cardBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            viewport,
            cardRect);
        float hiddenHeight = contentBounds.size.y - viewportBounds.size.y;
        if (hiddenHeight <= 0.01f)
            return;

        float position = collectionScroll.verticalNormalizedPosition;
        if (cardBounds.max.y > viewportBounds.max.y)
        {
            position += (cardBounds.max.y - viewportBounds.max.y) / hiddenHeight;
        }
        else if (cardBounds.min.y < viewportBounds.min.y)
        {
            position -= (viewportBounds.min.y - cardBounds.min.y) / hiddenHeight;
        }

        collectionScroll.StopMovement();
        collectionScroll.verticalNormalizedPosition = Mathf.Clamp01(position);
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

        GetOrCreateDetailCard()?.Show(sheep, encounterCount);
    }


    // ============================================================
    // CLEAR DETAIL
    // ============================================================

    private void ClearDetail()
    {
        GetOrCreateDetailCard()?.Clear();
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
