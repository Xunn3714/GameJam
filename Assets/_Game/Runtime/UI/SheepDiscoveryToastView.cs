using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>首次发现羊种时，在左下角复用图鉴详情卡进行庆祝。</summary>
public sealed class SheepDiscoveryToastView : MonoBehaviour
{
    private const float EnterDuration = 0.28f;
    private const float FadeDuration = 0.18f;
    private const float DisplayScale = 0.78f;
    private static readonly Vector2 PopupSize = new Vector2(420f, 560f);
    private static readonly Vector2 DisplayPosition = new Vector2(28f, 28f);

    private readonly Queue<Discovery> pending = new Queue<Discovery>();
    private readonly HashSet<string> announcedTypes = new HashSet<string>();
    private CanvasGroup group;
    private SheepDetailCardView card;
    private RectTransform cardRect;
    private Outline rarityBorder;
    private Discovery current;
    private float age;
    private bool showing;
    private float requestedAlpha;

    private readonly struct Discovery
    {
        public readonly SheepCollectionEntry Entry;
        public readonly int EncounterCount;

        public Discovery(SheepCollectionEntry entry, int encounterCount)
        {
            Entry = entry;
            EncounterCount = encounterCount;
        }
    }

    public static SheepDiscoveryToastView Create(
        Transform parent,
        SheepDetailCardView detailCardTemplate,
        Sprite fallbackPanelSprite)
    {
        SheepDetailCardView popupCard = detailCardTemplate != null
            ? Instantiate(detailCardTemplate, parent, false)
            : CreateFallbackCard(parent, fallbackPanelSprite);
        if (popupCard == null)
            return null;

        popupCard.SetFlavorTextAllowed(false);
        ApplyCompactLayout(popupCard);
        GameObject cardObject = popupCard.gameObject;
        cardObject.name = "SheepDiscoveryCard";
        cardObject.SetActive(true);
        cardObject.transform.SetAsLastSibling();

        RectTransform rect = cardObject.transform as RectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = DisplayPosition;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * DisplayScale;

        foreach (Graphic graphic in cardObject.GetComponentsInChildren<Graphic>(true))
            graphic.raycastTarget = false;

        CanvasGroup canvasGroup = cardObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = cardObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        Shadow shadow = cardObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.08f, 0.07f, 0.05f, 0.34f);
        shadow.effectDistance = new Vector2(10f, -10f);
        shadow.useGraphicAlpha = true;

        Outline border = cardObject.AddComponent<Outline>();
        border.effectColor = SheepCardView.GetQualityColor(SheepQuality.Common);
        border.effectDistance = new Vector2(6f, -6f);
        border.useGraphicAlpha = true;

        SheepDiscoveryToastView view = cardObject.AddComponent<SheepDiscoveryToastView>();
        view.card = popupCard;
        view.cardRect = rect;
        view.group = canvasGroup;
        view.rarityBorder = border;
        return view;
    }

    public void Show(string typeId, SheepCollectionEntry entry, int encounterCount)
    {
        if (string.IsNullOrWhiteSpace(typeId)
            || entry == null
            || !announcedTypes.Add(typeId))
            return;

        pending.Enqueue(new Discovery(entry, Mathf.Max(1, encounterCount)));
    }

    public static float GetHoldDuration(SheepQuality quality)
    {
        switch (quality)
        {
            case SheepQuality.Green: return 2.5f;
            case SheepQuality.Blue: return 3f;
            case SheepQuality.Purple: return 3.8f;
            case SheepQuality.Gold: return 4.6f;
            case SheepQuality.EasterEgg: return 5.5f;
            default: return 2f;
        }
    }

    private void Update()
    {
        if (group == null)
            return;

        bool paused = Time.timeScale <= 0f;
        if (!paused)
            Advance(Time.deltaTime);
        group.alpha = paused ? 0f : requestedAlpha;
    }

    private void Advance(float deltaTime)
    {
        if (!showing)
        {
            if (pending.Count == 0)
                return;

            current = pending.Dequeue();
            card.Show(current.Entry, current.EncounterCount);
            ApplyCompactLayout(card);
            if (rarityBorder != null)
                rarityBorder.effectColor = SheepCardView.GetQualityColor(current.Entry.quality);
            age = 0f;
            showing = true;
        }

        age += Mathf.Max(0f, deltaTime);
        float holdDuration = GetHoldDuration(current.Entry.quality);
        float duration = EnterDuration + holdDuration + FadeDuration;
        float enter = Mathf.Clamp01(age / EnterDuration);
        float leave = Mathf.Clamp01((duration - age) / FadeDuration);
        requestedAlpha = Mathf.Min(Mathf.Clamp01(age / FadeDuration), leave);

        float bounceScale = enter < 0.68f
            ? Mathf.Lerp(0.52f, 1.12f, enter / 0.68f)
            : Mathf.Lerp(1.12f, 1f, (enter - 0.68f) / 0.32f);
        cardRect.localScale = Vector3.one * (DisplayScale * bounceScale);
        cardRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-5f, 0f, enter));
        float hop = Mathf.Sin(enter * Mathf.PI) * 38f - (1f - enter) * 18f;
        cardRect.anchoredPosition = DisplayPosition + Vector2.up * hop;

        if (age < duration)
            return;

        showing = false;
        requestedAlpha = 0f;
        cardRect.anchoredPosition = DisplayPosition;
        cardRect.localRotation = Quaternion.identity;
        cardRect.localScale = Vector3.one * DisplayScale;
    }

    private static void ApplyCompactLayout(SheepDetailCardView popupCard)
    {
        if (popupCard == null)
            return;

        RectTransform panel = popupCard.transform as RectTransform;
        if (panel != null)
            panel.sizeDelta = PopupSize;

        SetImageRect(popupCard.SheepImage != null ? popupCard.SheepImage.rectTransform : null,
            new Vector2(0f, -205f), new Vector2(360f, 300f));
        ConfigurePopupText(popupCard.SheepNameText,
            new Vector2(0f, -18f), new Vector2(380f, 48f), 28f, 36f, 1);
        ConfigurePopupText(popupCard.CountText,
            new Vector2(0f, -360f), new Vector2(380f, 34f), 18f, 22f, 1);
        if (popupCard.CountText != null)
            popupCard.CountText.text = "首次发现！";
        ConfigurePopupText(popupCard.DescriptionText,
            new Vector2(0f, -410f), new Vector2(380f, 120f), 18f, 22f, 4);

        if (popupCard.RarityText != null)
            popupCard.RarityText.gameObject.SetActive(false);
        SetChildActive(popupCard.transform, "Txt_AbilityName", false);
        SetChildActive(popupCard.transform, "Txt_AbilityDescription", false);
        SetChildActive(popupCard.transform, "Sheep_Sentence", false);
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetImageRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void ConfigurePopupText(
        TMP_Text text,
        Vector2 position,
        Vector2 size,
        float minimumSize,
        float maximumSize,
        int maximumLines)
    {
        if (text == null)
            return;

        SetRect(text.rectTransform, position, size);
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = minimumSize;
        text.fontSizeMax = maximumSize;
        text.maxVisibleLines = maximumLines;
        text.textWrappingMode = maximumLines > 1
            ? TextWrappingModes.Normal
            : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
    }

    private static void SetChildActive(Transform parent, string childName, bool active)
    {
        Transform child = parent != null ? parent.Find(childName) : null;
        if (child != null)
            child.gameObject.SetActive(active);
    }

    private static SheepDetailCardView CreateFallbackCard(Transform parent, Sprite panelSprite)
    {
        Image panel = MvpUiFactory.CreateImage("SheepDiscoveryCard", parent,
            panelSprite != null ? Color.white : MvpUiFactory.Paper);
        panel.sprite = panelSprite;
        panel.rectTransform.sizeDelta = new Vector2(470f, 570f);

        Image sheepImage = MvpUiFactory.CreateImage("SheepImage", panel.transform, Color.white);
        sheepImage.preserveAspect = true;
        MvpUiFactory.Anchor(sheepImage.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -132f), new Vector2(220f, 190f));

        TMP_Text name = CreateText(panel.transform, "Txt_SheepName", -245f, 30f);
        TMP_Text rarity = CreateText(panel.transform, "Txt_Rarity", -285f, 21f);
        TMP_Text count = CreateText(panel.transform, "Txt_Count", -320f, 18f);
        TMP_Text description = CreateText(panel.transform, "Txt_Description", -375f, 19f);
        TMP_Text abilityName = CreateText(panel.transform, "Txt_AbilityName", -430f, 21f);
        TMP_Text ability = CreateText(panel.transform, "Txt_AbilityDescription", -485f, 18f);

        SheepDetailCardView card = panel.gameObject.AddComponent<SheepDetailCardView>();
        card.Configure(
            sheepImage,
            name,
            rarity,
            count,
            description,
            null,
            abilityName,
            ability,
            null,
            false);
        return card;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        float y,
        float fontSize)
    {
        TMP_Text text = MvpUiFactory.CreateText(name, parent, string.Empty, fontSize,
            TextAlignmentOptions.Center);
        MvpUiFactory.Anchor(text.rectTransform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, y), new Vector2(400f, 44f));
        text.enableAutoSizing = true;
        text.fontSizeMin = 14f;
        text.fontSizeMax = fontSize;
        return text;
    }
}
