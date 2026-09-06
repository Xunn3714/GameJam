using System.Collections;
using UnityEngine;

public class CreditsAutoScroll : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform content;
    [SerializeField] private RectTransform firstCreditImage;
    [SerializeField] private RectTransform secondCreditImage;

    [Header("Auto Scroll")]
    [Tooltip("打开制作人员页面后，停留多久开始滚动")]
    [SerializeField, Min(0f)]
    private float startDelay = 1f;

    [Tooltip("向上滚动速度，单位：像素/秒")]
    [SerializeField, Min(1f)]
    private float scrollSpeed = 100f;

    private Coroutine scrollCoroutine;
    private float loopDistance;


    private void OnEnable()
    {
        StartAutoScroll();
    }


    private void OnDisable()
    {
        StopAutoScroll();
    }


    private void StartAutoScroll()
    {
        StopAutoScroll();

        if (content == null ||
            firstCreditImage == null ||
            secondCreditImage == null)
        {
            Debug.LogWarning(
                "CreditsAutoScroll: Content 或 CreditImage 没有绑定。",
                this
            );

            return;
        }

        scrollCoroutine = StartCoroutine(SetupAndScroll());
    }


    private void StopAutoScroll()
    {
        if (scrollCoroutine != null)
        {
            StopCoroutine(scrollCoroutine);
            scrollCoroutine = null;
        }
    }


    private IEnumerator SetupAndScroll()
    {
        // 等待 Aspect Ratio Fitter 根据全屏宽度计算图片高度
        yield return null;
        yield return null;

        Canvas.ForceUpdateCanvases();

        // ==========================================
        // 1. 第一张图固定在 Content 顶部
        // ==========================================
        Vector2 firstPos = firstCreditImage.anchoredPosition;
        firstPos.x = 0f;
        firstPos.y = 0f;
        firstCreditImage.anchoredPosition = firstPos;


        // ==========================================
        // 2. 获取全屏 Credit 图真正的高度
        // ==========================================
        loopDistance = firstCreditImage.rect.height;

        if (loopDistance <= 1f)
        {
            Debug.LogWarning(
                "CreditsAutoScroll: CreditImage 高度异常。",
                this
            );

            yield break;
        }


        // ==========================================
        // 3. 第二张图自动紧贴在第一张下面
        // ==========================================
        Vector2 secondPos = secondCreditImage.anchoredPosition;
        secondPos.x = 0f;
        secondPos.y = -loopDistance;
        secondCreditImage.anchoredPosition = secondPos;


        // Content 高度设置为两张图总高度
        content.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            loopDistance * 2f
        );


        // ==========================================
        // 4. 每次打开制作人员页都从顶部开始
        // ==========================================
        Vector2 contentPos = content.anchoredPosition;
        contentPos.y = 0f;
        content.anchoredPosition = contentPos;


        // 顶部停留
        float timer = 0f;

        while (timer < startDelay)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }


        // ==========================================
        // 5. 无限无缝滚动
        // ==========================================
        while (true)
        {
            Vector2 position = content.anchoredPosition;

            position.y +=
                scrollSpeed *
                Time.unscaledDeltaTime;


            // A 完整滚出去时，
            // 当前看到的是完全一样的 B。
            //
            // 此时把 Content 悄悄退回一个图片高度，
            // B 会瞬间替换成 A，但视觉完全相同。
            if (position.y >= loopDistance)
            {
                position.y -= loopDistance;
            }


            content.anchoredPosition = position;

            yield return null;
        }
    }


    public void RestartScroll()
    {
        if (!gameObject.activeInHierarchy)
            return;

        StartAutoScroll();
    }
}
