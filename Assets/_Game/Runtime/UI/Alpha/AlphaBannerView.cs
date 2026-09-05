using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Alpha 场景顶部提示横幅。
/// 支持文字 + 可选图片，并按队列依次淡入淡出。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class AlphaBannerView : MonoBehaviour
{
    [Tooltip("如果已经有 BannerView 视觉组件，就直接复用它。")]
    [SerializeField] private BannerView bannerView;

    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;

    [SerializeField, Min(0.1f)]
    private float holdDuration = 2.2f;

    [SerializeField, Min(0.05f)]
    private float fadeDuration = 0.35f;

    private struct BannerMessage
    {
        public string Message;
        public Sprite Icon;

        public BannerMessage(string message, Sprite icon)
        {
            Message = message;
            Icon = icon;
        }
    }

    private readonly Queue<BannerMessage> pending =
        new Queue<BannerMessage>();

    private CanvasGroup group;

    private float timer;

    // 0 idle
    // 1 fade in
    // 2 hold
    // 3 fade out
    private int state;


    private void Awake()
    {
        group = GetComponent<CanvasGroup>();

        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();

        group.alpha = 0f;

        // 已经使用 BannerView 时，
        // UI 外观和图片由 BannerView 自己负责。
        if (bannerView != null)
            return;

        RectTransform root =
            (RectTransform)transform;

        if (background == null)
        {
            background =
                MvpUiFactory.CreateImage(
                    "Background",
                    root,
                    new Color(
                        0.1f,
                        0.1f,
                        0.08f,
                        0.72f
                    )
                );

            MvpUiFactory.Stretch(
                background.rectTransform
            );

            background.raycastTarget = false;
        }

        if (label == null)
        {
            label =
                MvpUiFactory.CreateText(
                    "Label",
                    root,
                    "",
                    30f,
                    TextAlignmentOptions.Center
                );

            label.color =
                MvpUiFactory.Paper;

            MvpUiFactory.Stretch(
                label.rectTransform,
                12f
            );
        }
    }


    // 原接口继续保留，避免其他地方报错
    public void Show(string message)
    {
        Show(message, null);
    }


    // 新接口：文字 + 图片
    public void Show(
        string message,
        Sprite icon)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        pending.Enqueue(
            new BannerMessage(
                message,
                icon
            )
        );
    }


    private void Update()
    {
        float deltaTime =
            Time.unscaledDeltaTime;

        switch (state)
        {
            case 0:
                {
                    if (pending.Count <= 0)
                        break;

                    BannerMessage item =
                        pending.Dequeue();

                    if (bannerView != null)
                    {
                        bannerView.Show(
                            item.Message,
                            item.Icon
                        );
                    }
                    else if (label != null)
                    {
                        label.text =
                            item.Message;
                    }

                    timer = 0f;
                    state = 1;

                    break;
                }

            case 1:
                {
                    timer += deltaTime;

                    group.alpha =
                        Mathf.Clamp01(
                            timer / fadeDuration
                        );

                    if (timer >= fadeDuration)
                    {
                        timer = 0f;
                        state = 2;
                    }

                    break;
                }

            case 2:
                {
                    timer += deltaTime;

                    if (timer >= holdDuration ||
                        pending.Count > 0)
                    {
                        timer = 0f;
                        state = 3;
                    }

                    break;
                }

            case 3:
                {
                    timer += deltaTime;

                    group.alpha =
                        1f -
                        Mathf.Clamp01(
                            timer / fadeDuration
                        );

                    if (timer >= fadeDuration)
                    {
                        group.alpha = 0f;
                        timer = 0f;
                        state = 0;
                    }

                    break;
                }
        }
    }
}
