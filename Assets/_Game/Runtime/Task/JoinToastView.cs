using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class JoinToastView : MonoBehaviour
{
    [SerializeField] private TMP_Text messageText;
    [SerializeField, Min(0f)] private float displayDuration = 1.5f;
    [SerializeField, Min(0.3f)] private float stackDisplayDuration = 1.15f;

    private const int Capacity = 3;
    private const float RowHeight = 90f;
    private const float RowSpacing = 98f;
    private const float TransitionDuration = 0.12f;

    private sealed class Toast
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public TMP_Text Label;
        public float Age;
        public string Replacement;
        public float ReplacementAlpha;
        public Vector3 ReplacementScale;
    }

    private Coroutine hideCoroutine;
    private readonly List<Toast> visible = new List<Toast>(Capacity);
    private Toast[] slots;
    private CanvasGroup stackGroup;

    private void Awake()
    {
        MvpTmpUiFont.Apply(messageText);
        HideImmediate();
    }

    public void Show(string sheepName)
    {
        if (slots != null)
        {
            ShowStacked(sheepName);
            return;
        }

        if (messageText == null)
            return;

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        messageText.text = $"“{sheepName}”加入了族群！";
        messageText.gameObject.SetActive(true);

        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    // Alpha opts in explicitly; archived scenes retain their existing single-line toast.
    public void ConfigureStack(Sprite panelSprite)
    {
        if (slots != null)
            return;

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
        HideImmediate();

        RectTransform root = (RectTransform)transform;
        MvpUiFactory.Anchor(root, Vector2.one, Vector2.one,
            new Vector2(-24f, -24f), new Vector2(520f, RowSpacing * Capacity));
        stackGroup = gameObject.AddComponent<CanvasGroup>();
        stackGroup.blocksRaycasts = false;
        stackGroup.interactable = false;
        slots = new Toast[Capacity];

        for (int index = 0; index < Capacity; index++)
        {
            Image panel = MvpUiFactory.CreateImage("JoinNotice" + index, root,
                panelSprite != null ? Color.white : MvpUiFactory.Paper);
            panel.sprite = panelSprite;
            panel.raycastTarget = false;
            MvpUiFactory.Anchor(panel.rectTransform, Vector2.one, Vector2.one,
                Vector2.zero, new Vector2(520f, RowHeight));
            TMP_Text label = MvpUiFactory.CreateText("Message", panel.transform,
                string.Empty, 24f, TextAlignmentOptions.Center);
            MvpUiFactory.Stretch(label.rectTransform, 18f);
            label.enableAutoSizing = true;
            label.fontSizeMin = 18f;
            label.fontSizeMax = 24f;
            label.richText = false;
            slots[index] = new Toast
            {
                Rect = panel.rectTransform,
                Group = panel.gameObject.AddComponent<CanvasGroup>(),
                Label = label
            };
            panel.gameObject.SetActive(false);
        }
    }

    private void ShowStacked(string sheepName)
    {
        if (string.IsNullOrWhiteSpace(sheepName))
            return;

        string message = $"“{sheepName}”加入了族群！";
        if (visible.Count == Capacity)
        {
            // Reuse the oldest slot after squeezing it out: never create a fourth panel.
            Toast oldest = visible[0];
            visible.RemoveAt(0);
            if (oldest.Replacement == null && oldest.Group.alpha <= 0.01f)
            {
                BeginToast(oldest, message, Capacity - 1);
                visible.Add(oldest);
                return;
            }
            // A continuous stream must not keep restarting an in-progress exit forever.
            if (oldest.Replacement == null)
            {
                oldest.Age = 0f;
                oldest.ReplacementAlpha = oldest.Group.alpha;
                oldest.ReplacementScale = oldest.Rect.localScale;
            }
            oldest.Replacement = message;
            visible.Add(oldest);
            return;
        }

        foreach (Toast slot in slots)
        {
            if (visible.Contains(slot))
                continue;
            BeginToast(slot, message, visible.Count);
            visible.Add(slot);
            return;
        }
    }

    private static void BeginToast(Toast toast, string message, int index)
    {
        toast.Label.text = message;
        toast.Replacement = null;
        toast.Age = 0f;
        toast.Group.alpha = 0f;
        toast.Rect.localScale = Vector3.one;
        toast.Rect.anchoredPosition = new Vector2(0f, -index * RowSpacing);
        toast.Rect.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (slots == null)
            return;

        bool paused = Time.timeScale <= 0f;
        stackGroup.alpha = paused ? 0f : 1f;
        if (!paused)
            Advance(Time.deltaTime);
    }

    private void Advance(float deltaTime)
    {
        for (int index = 0; index < visible.Count; index++)
        {
            Toast toast = visible[index];
            toast.Age += deltaTime;
            if (toast.Replacement != null)
            {
                float progress = Mathf.Clamp01(toast.Age / TransitionDuration);
                toast.Group.alpha = toast.ReplacementAlpha * (1f - progress);
                toast.Rect.localScale = Vector3.Lerp(toast.ReplacementScale,
                    new Vector3(1f, 0.1f, 1f), progress);
                if (progress >= 1f)
                    BeginToast(toast, toast.Replacement, index);
                continue;
            }

            if (toast.Age >= stackDisplayDuration)
            {
                toast.Rect.gameObject.SetActive(false);
                visible.RemoveAt(index--);
                continue;
            }

            float enter = Mathf.Clamp01(toast.Age / TransitionDuration);
            float leave = Mathf.Clamp01((stackDisplayDuration - toast.Age) / TransitionDuration);
            toast.Group.alpha = Mathf.Min(enter, leave);
            float y = Mathf.Lerp(toast.Rect.anchoredPosition.y, -index * RowSpacing,
                1f - Mathf.Exp(-22f * deltaTime));
            toast.Rect.anchoredPosition = new Vector2(32f * (1f - enter), y);
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);

        HideImmediate();
        hideCoroutine = null;
    }

    private void HideImmediate()
    {
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
    }
}
