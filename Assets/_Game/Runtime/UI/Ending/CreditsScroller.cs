using UnityEngine;
using UnityEngine.UI;

public class CreditsScroller : MonoBehaviour
{
    [Header("Credits")]
    public RectTransform creditsArea;
    public RectTransform creditsContent;

    [Header("Scroll Settings")]
    public float scrollSpeed = 60f;
    public float startPadding = 100f;

    private float startY;
    private float targetY;
    private bool finished = false;


    private void Start()
    {
        if (creditsArea == null || creditsContent == null)
        {
            Debug.LogError("CreditsScroller references are missing.");
            enabled = false;
            return;
        }

        // 先刷新 Layout，确保 CreditsContent 尺寸正确
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(creditsContent);
        Canvas.ForceUpdateCanvases();

        // 这个位置就是 Credits 最终停留位置
        targetY = creditsContent.anchoredPosition.y;

        // 从画面底部之外开始
        float areaHeight = creditsArea.rect.height;
        float contentHeight = creditsContent.rect.height;

        startY = -(areaHeight + contentHeight + startPadding);

        Vector2 position = creditsContent.anchoredPosition;
        position.y = startY;

        creditsContent.anchoredPosition = position;
    }


    private void Update()
    {
        if (finished)
            return;

        Vector2 position = creditsContent.anchoredPosition;

        position.y += scrollSpeed * Time.unscaledDeltaTime;

        // 到达预先摆好的最终位置后停止
        if (position.y >= targetY)
        {
            position.y = targetY;
            finished = true;
        }

        creditsContent.anchoredPosition = position;
    }
}
