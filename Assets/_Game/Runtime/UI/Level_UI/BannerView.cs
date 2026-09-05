using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BannerView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text bannerText;
    [SerializeField] private Image bannerIcon;

    private Coroutine hideCoroutine;


    private void Awake()
    {
        gameObject.SetActive(false);
    }


    public void Show(string message, Sprite icon = null)
    {
        if (bannerText != null)
            bannerText.text = message;

        if (bannerIcon != null)
        {
            bannerIcon.sprite = icon;
            bannerIcon.enabled = icon != null;
        }

        gameObject.SetActive(true);
    }


    public void ShowTemporary(
        string message,
        float duration = 3f,
        Sprite icon = null)
    {
        Show(message, icon);

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);

        hideCoroutine =
            StartCoroutine(HideAfterDelay(duration));
    }


    public void Hide()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        gameObject.SetActive(false);
    }


    private IEnumerator HideAfterDelay(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);

        Hide();

        hideCoroutine = null;
    }
}
