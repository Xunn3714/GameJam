using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class BannerView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text bannerText;
    [FormerlySerializedAs("icon")]
    [SerializeField] private Image bannerIcon;

    private Coroutine hideCoroutine;

    private void Awake()
    {
        // 不要关闭整个 GameObject。
        // AlphaBannerView 需要保持激活才能运行队列和淡入淡出。

        if (bannerIcon != null)
        {
            bannerIcon.enabled = false;
        }
    }

    public void Show(
        string message,
        Sprite icon = null)
    {
        if (bannerText != null)
        {
            bannerText.text = message;
        }

        if (bannerIcon != null)
        {
            bannerIcon.sprite = icon;
            bannerIcon.enabled = icon != null;
        }
    }

    public void SetText(string message)
    {
        if (bannerText != null)
            bannerText.text = message;
    }

    public void SetIcon(Sprite icon)
    {
        if (bannerIcon == null)
            return;

        bannerIcon.sprite = icon;
        bannerIcon.enabled = icon != null;
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void ShowTemporary(
        string message,
        float duration = 3f,
        Sprite icon = null)
    {
        Show(
            message,
            icon
        );

        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
        }

        hideCoroutine =
            StartCoroutine(
                HideAfterDelay(duration)
            );
    }

    public void Hide()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        if (bannerIcon != null)
        {
            bannerIcon.enabled = false;
        }

        if (bannerText != null)
        {
            bannerText.text = "";
        }
    }

    private IEnumerator HideAfterDelay(
        float duration)
    {
        yield return new WaitForSecondsRealtime(
            duration
        );

        hideCoroutine = null;

        Hide();
    }
}
