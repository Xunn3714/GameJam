using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class MvpUiFactory
{
    public static readonly Color Paper = new(0.96f, 0.91f, 0.78f, 1f);
    public static readonly Color Ink = new(0.18f, 0.18f, 0.15f, 1f);
    public static readonly Color Accent = new(0.63f, 0.76f, 0.42f, 1f);

    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject instance = new(name, typeof(RectTransform));
        RectTransform rect = instance.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    public static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TMP_Text CreateText(
        string name,
        Transform parent,
        string text,
        float fontSize,
        TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = Ink;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        MvpTmpUiFont.Apply(label);
        return label;
    }

    public static Button CreateButton(
        string name,
        Transform parent,
        string text,
        UnityAction onClick,
        Vector2 size)
    {
        Image image = CreateImage(name, parent, new Color(0.88f, 0.84f, 0.68f, 1f));
        RectTransform rect = image.rectTransform;
        rect.sizeDelta = size;

        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.96f, 0.75f, 1f);
        colors.pressedColor = new Color(0.78f, 0.82f, 0.58f, 1f);
        button.colors = colors;
        if (onClick != null)
            button.onClick.AddListener(onClick);

        TMP_Text label = CreateText("Label", rect, text, 26f, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public static void Anchor(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = (anchorMin + anchorMax) * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
