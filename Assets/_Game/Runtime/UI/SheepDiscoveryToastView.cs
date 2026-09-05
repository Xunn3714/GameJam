using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>First collection celebrations have their own queue, separate from gameplay banners.</summary>
public sealed class SheepDiscoveryToastView : MonoBehaviour
{
    private const float FadeDuration = 0.18f;
    private readonly Queue<Discovery> pending = new Queue<Discovery>();
    private readonly HashSet<string> announcedTypes = new HashSet<string>();
    private CanvasGroup group;
    private TMP_Text label;
    private Discovery current;
    private float age;
    private bool showing;
    private float requestedAlpha;

    private readonly struct Discovery
    {
        public readonly string Message;
        public readonly SheepQuality Quality;

        public Discovery(string name, SheepQuality quality)
        {
            Message = $"恭喜你第一次和{name}成为种群！";
            Quality = quality;
        }
    }

    public static SheepDiscoveryToastView Create(Transform parent, Sprite panelSprite)
    {
        Image panel = MvpUiFactory.CreateImage("SheepDiscoveryNotice", parent,
            panelSprite != null ? Color.white : MvpUiFactory.Paper);
        panel.sprite = panelSprite;
        panel.raycastTarget = false;
        // Keep the existing wolf HUD (16..96) and event banner (110..200) unobstructed.
        MvpUiFactory.Anchor(panel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -210f), new Vector2(520f, 90f));
        SheepDiscoveryToastView view = panel.gameObject.AddComponent<SheepDiscoveryToastView>();
        view.group = panel.gameObject.AddComponent<CanvasGroup>();
        view.group.alpha = 0f;
        view.group.blocksRaycasts = false;
        view.group.interactable = false;
        view.label = MvpUiFactory.CreateText("Message", panel.transform,
            string.Empty, 24f, TextAlignmentOptions.Center);
        MvpUiFactory.Stretch(view.label.rectTransform, 14f);
        view.label.enableAutoSizing = true;
        view.label.fontSizeMin = 18f;
        view.label.fontSizeMax = 24f;
        view.label.richText = false;
        return view;
    }

    public void Show(string typeId, string typeName, SheepQuality quality)
    {
        if (string.IsNullOrWhiteSpace(typeId) || string.IsNullOrWhiteSpace(typeName)
            || !announcedTypes.Add(typeId))
            return;

        pending.Enqueue(new Discovery(typeName, quality));
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

    public static Color GetTextColor(SheepQuality quality)
    {
        // Dark enough to remain readable on the same pale paper sprite at every quality.
        switch (quality)
        {
            case SheepQuality.Green: return new Color32(48, 108, 42, 255);
            case SheepQuality.Blue: return new Color32(37, 91, 151, 255);
            case SheepQuality.Purple: return new Color32(116, 63, 155, 255);
            case SheepQuality.Gold: return new Color32(142, 95, 9, 255);
            case SheepQuality.EasterEgg: return new Color32(169, 51, 112, 255);
            default: return MvpUiFactory.Ink;
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
            label.text = current.Message;
            label.color = GetTextColor(current.Quality);
            age = 0f;
            showing = true;
        }

        age += deltaTime;
        float duration = GetHoldDuration(current.Quality) + FadeDuration * 2f;
        requestedAlpha = Mathf.Min(Mathf.Clamp01(age / FadeDuration),
            Mathf.Clamp01((duration - age) / FadeDuration));
        if (age >= duration)
        {
            showing = false;
            requestedAlpha = 0f;
        }
    }
}
