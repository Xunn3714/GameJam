using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 屏幕上方的提示横幅：
/// 排队显示“阶段升级 / 狼群来袭 / 出口解锁 / 特殊羊加入”等短消息，
/// 自动淡入淡出。
///
/// 如果指定 BannerView，
/// 则复用现有 BannerSystem 的视觉内容；
/// 否则自动创建基础 Background + Label。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class AlphaBannerView : MonoBehaviour
{
    [Tooltip("指定后改用仓库里的 BannerSystem 预制体（BannerView）来显示，不再自建元素。")]
    [SerializeField] private BannerView bannerView;

    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;

    [SerializeField, Min(0.1f)]
    private float holdDuration = 2.2f;

    [SerializeField, Min(0.05f)]
    private float fadeDuration = 0.35f;

    private readonly Queue<string> pending =
        new Queue<string>();

    private CanvasGroup group;

    private float timer;

    // 0 = idle
    // 1 = fade in
    // 2 = hold
    // 3 = fade out
    private int state;


    private void Awake()
    {
        group = GetComponent<CanvasGroup>();

        if (group == null)
        {
            group =
                gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;


        // =========================
        // 使用现有 BannerView
        // =========================

        if (bannerView != null)
        {
            // BannerView 自己负责文字与图标。
            // 此处不再调用旧版 SetIcon / SetText。
            return;
        }


        // =========================
        // 没有 BannerView 时
        // 自动创建简单 UI
        // =========================

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


    /// <summary>
    /// 将一条 Banner 消息加入显示队列。
    /// </summary>
    public void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        pending.Enqueue(message);
    }


    private void Update()
    {
        float deltaTime =
            Time.unscaledDeltaTime;


        switch (state)
        {
            // =========================
            // Idle
            // =========================

            case 0:
                {
                    if (pending.Count <= 0)
                        break;

                    string message =
                        pending.Dequeue();


                    if (bannerView != null)
                    {
                        // 新版 BannerView API：
                        // Show(message, icon)
                        bannerView.Show(
                            message,
                            null
                        );
                    }
                    else if (label != null)
                    {
                        label.text =
                            message;
                    }


                    timer = 0f;
                    state = 1;

                    break;
                }


            // =========================
            // Fade In
            // =========================

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


            // =========================
            // Hold
            // =========================

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


            // =========================
            // Fade Out
            // =========================

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
