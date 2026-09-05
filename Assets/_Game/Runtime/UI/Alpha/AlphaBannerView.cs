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

    private readonly Queue<string> pending = new Queue<string>();
    private CanvasGroup group;
    private float timer;
    private int state; // 0 idle, 1 fade in, 2 hold, 3 fade out

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (group == null)
            group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        if (bannerView != null)
        {
            bannerView.SetIcon(null);
            bannerView.Show();
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
        if (string.IsNullOrWhiteSpace(message))
            return;

        pending.Enqueue(message);
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;

        switch (state)
        {
            case 0:
                if (pending.Count > 0)
                {
                    string message = pending.Dequeue();
                    if (bannerView != null)
                        bannerView.SetText(message);
                    else
                        label.text = message;
                    timer = 0f;
                    state = 1;
                }
                break;

            case 1:
                timer += deltaTime;
                group.alpha = Mathf.Clamp01(timer / fadeDuration);
                if (timer >= fadeDuration) { timer = 0f; state = 2; }
                break;

            case 2:
                timer += deltaTime;
                if (timer >= holdDuration || pending.Count > 0) { timer = 0f; state = 3; }
                break;

            case 3:
                timer += deltaTime;
                group.alpha = 1f - Mathf.Clamp01(timer / fadeDuration);
                if (timer >= fadeDuration) { group.alpha = 0f; state = 0; }
                break;
        }
    }
}
