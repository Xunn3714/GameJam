using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BannerView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text bannerText;

    public void SetText(string text)
    {
        if (bannerText != null)
        {
            bannerText.text = text;
        }
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon == null)
        {
            return;
        }

        icon.sprite = sprite;
        icon.gameObject.SetActive(sprite != null);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
