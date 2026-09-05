using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 屏幕上方的提示横幅：排队显示“阶段升级 / 狼群来袭 / 出口解锁 / 特殊羊加入”等短消息，自动淡入淡出。
/// 挂在 Canvas 下的 RectTransform 上，子元素留空会自动创建。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class AlphaBannerView : MonoBehaviour
{
    [Tooltip("指定后改用仓库里的 BannerSystem 预制体（BannerView）来显示，不再自建元素。")]
    [SerializeField] private BannerView bannerView;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField, Min(0.1f)] private float holdDuration = 2.2f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.35f;

    private readonly struct BannerMessage
    {
        public readonly string Text;
        public readonly Sprite Icon;

        public BannerMessage(string text, Sprite icon)
        {
            Text = text;
            Icon = icon;
        }
    }

    private readonly Queue<BannerMessage> pending = new Queue<BannerMessage>();
    private CanvasGroup group;
    private float timer;
    private int state; // 0 idle, 1 fade in, 2 hold, 3 fade out
    private float requestedAlpha;
    private bool suppressed;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        SetAlpha(0f);

        if (bannerView != null)
        {
            bannerView.Hide();
            return;
        }

        RectTransform root = (RectTransform)transform;
        if (background == null)
        {
            background = MvpUiFactory.CreateImage("Background", root, new Color(0.1f, 0.1f, 0.08f, 0.72f));
            MvpUiFactory.Stretch(background.rectTransform);
            background.raycastTarget = false;
        }

        if (label == null)
        {
            label = MvpUiFactory.CreateText("Label", root, "", 30f, TextAlignmentOptions.Center);
            label.color = MvpUiFactory.Paper;
            MvpUiFactory.Stretch(label.rectTransform, 12f);
        }
    }

    public void Show(string message)
    {
        Show(message, null);
    }

    public void Show(string message, Sprite icon)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        pending.Enqueue(new BannerMessage(message, icon));
    }

    /// <summary>
    /// 立即显示会被后续状态取代的提示，例如人数进度和阶段。
    /// 清掉旧状态，避免快速招募时仍按队列播放过期的 4/6、5/6。
    /// </summary>
    public void ShowLatest(string message)
    {
        ShowLatest(message, null);
    }

    public void ShowLatest(string message, Sprite icon)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        pending.Clear();
        SetMessage(new BannerMessage(message, icon));
        SetAlpha(1f);
        timer = 0f;
        state = 2;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        switch (state)
        {
            case 0:
                if (pending.Count > 0)
                {
                    BannerMessage message = pending.Dequeue();
                    SetMessage(message);
                    timer = 0f;
                    state = 1;
                }
                break;

            case 1:
                timer += deltaTime;
                SetAlpha(Mathf.Clamp01(timer / fadeDuration));
                if (timer >= fadeDuration) { timer = 0f; state = 2; }
                break;

            case 2:
                timer += deltaTime;
                if (timer >= holdDuration || pending.Count > 0) { timer = 0f; state = 3; }
                break;

            case 3:
                timer += deltaTime;
                SetAlpha(1f - Mathf.Clamp01(timer / fadeDuration));
                if (timer >= fadeDuration) { SetAlpha(0f); state = 0; }
                break;
        }
    }

    public void SetSuppressed(bool value)
    {
        suppressed = value;
        if (group != null)
            group.alpha = suppressed ? 0f : requestedAlpha;
    }

    private void SetAlpha(float value)
    {
        requestedAlpha = value;
        if (group != null)
            group.alpha = suppressed ? 0f : requestedAlpha;
    }

    private void SetMessage(BannerMessage message)
    {
        if (bannerView != null)
            bannerView.Show(message.Text, message.Icon);
        else if (label != null)
            label.text = message.Text;
    }
}
