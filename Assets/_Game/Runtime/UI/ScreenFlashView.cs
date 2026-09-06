using UnityEngine;
using UnityEngine.UI;

/// <summary>全屏闪白。真结局每次落地都闪一下。</summary>
[DisallowMultipleComponent]
public sealed class ScreenFlashView : MonoBehaviour
{
    private Image image;
    private float age = -1f;
    private float duration = 0.35f;
    private float peakAlpha = 0.9f;

    public static ScreenFlashView Create(Transform canvasParent)
    {
        GameObject root = new GameObject("ScreenFlash", typeof(RectTransform));
        root.transform.SetParent(canvasParent, false);
        ScreenFlashView view = root.AddComponent<ScreenFlashView>();
        view.image = MvpUiFactory.CreateImage("Flash", root.transform, new Color(1f, 1f, 1f, 0f));
        MvpUiFactory.Stretch(view.image.rectTransform);
        view.image.raycastTarget = false;
        root.transform.SetAsLastSibling();
        return view;
    }

    public void Flash(float flashDuration = 0.35f, float alpha = 0.9f)
    {
        duration = Mathf.Max(0.05f, flashDuration);
        peakAlpha = Mathf.Clamp01(alpha);
        age = 0f;
        transform.SetAsLastSibling();
    }

    private void Update()
    {
        if (image == null || age < 0f)
            return;

        // 结算时 timeScale = 0，所以走 unscaled。
        age += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(age / duration);
        Color color = image.color;
        color.a = Mathf.Lerp(peakAlpha, 0f, progress);
        image.color = color;
        if (progress >= 1f)
            age = -1f;
    }
}
