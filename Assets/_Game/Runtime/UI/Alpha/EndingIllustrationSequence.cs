using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 在胜利结算面板之前播放结局插画。复用开场插画的逐块淡入节奏，
/// 但严格按照结局图自身的长方形分镜裁切：伪结局两块，真结局三块。
/// </summary>
[DisallowMultipleComponent]
public sealed class EndingIllustrationSequence : MonoBehaviour
{
    public const string FakeEndingResourcePath = "FakeEnding";
    public const string TrueEndingResourcePath = "TrueEnding";

    // UV 坐标以左下角为原点；数值对应两张 1920px 原图里的黑色分镜边界。
    private static readonly Rect[] FakeEndingPanels =
    {
        new Rect(0.0146f, 0.0502f, 0.3000f, 0.8891f),
        new Rect(0.3359f, 0.0502f, 0.6453f, 0.8891f),
    };

    private static readonly Rect[] TrueEndingPanels =
    {
        new Rect(0.0146f, 0.5787f, 0.3000f, 0.3954f),
        new Rect(0.3359f, 0.5787f, 0.6453f, 0.3954f),
        new Rect(0.0146f, 0.0241f, 0.9708f, 0.5231f),
    };

    [SerializeField, Range(0.5f, 1f)] private float illustrationScale = 0.94f;
    [SerializeField, Min(0.05f)] private float fragmentFadeDuration = 0.35f;
    [SerializeField, Min(0f)] private float delayBetweenFragments = 0.25f;
    [SerializeField, Min(0f)] private float completedHoldDuration = 2.2f;
    [SerializeField, Min(0.05f)] private float fadeOutDuration = 0.5f;

    private readonly GameObject[] fragments = new GameObject[3];
    private CanvasGroup rootGroup;
    private int activeFragmentCount;

    public static EndingIllustrationSequence Create(Transform canvasParent)
    {
        GameObject root = new GameObject(
            "EndingIllustrationSequence",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image));
        root.transform.SetParent(canvasParent, false);
        MvpUiFactory.Stretch(root.GetComponent<RectTransform>());

        Image background = root.GetComponent<Image>();
        background.color = Color.black;
        background.raycastTarget = true;

        EndingIllustrationSequence sequence = root.AddComponent<EndingIllustrationSequence>();
        sequence.rootGroup = root.GetComponent<CanvasGroup>();
        sequence.BuildFragments();
        root.SetActive(false);
        return sequence;
    }

    public IEnumerator Play(bool trueEnding)
    {
        string resourcePath = trueEnding ? TrueEndingResourcePath : FakeEndingResourcePath;
        Texture2D illustration = Resources.Load<Texture2D>(resourcePath);
        if (illustration == null)
        {
            Debug.LogError($"Ending illustration is missing at Resources/{resourcePath}.", this);
            yield break;
        }

        if (rootGroup == null)
            rootGroup = GetComponent<CanvasGroup>();

        ConfigureFragments(illustration, trueEnding ? TrueEndingPanels : FakeEndingPanels);
        rootGroup.alpha = 1f;
        rootGroup.interactable = true;
        rootGroup.blocksRaycasts = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        for (int index = 0; index < activeFragmentCount; index++)
        {
            GameObject fragment = fragments[index];
            CanvasGroup group = fragment.GetComponent<CanvasGroup>();
            fragment.SetActive(true);
            yield return Fade(group, 0f, 1f, fragmentFadeDuration);

            if (index < activeFragmentCount - 1 && delayBetweenFragments > 0f)
                yield return WaitUnscaled(delayBetweenFragments);
        }

        if (completedHoldDuration > 0f)
            yield return WaitUnscaled(completedHoldDuration);

        yield return Fade(rootGroup, 1f, 0f, fadeOutDuration);
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;
        gameObject.SetActive(false);
    }

    private void BuildFragments()
    {
        for (int index = 0; index < fragments.Length; index++)
        {
            GameObject fragment = new GameObject(
                $"Fragment_{index + 1:00}",
                typeof(RectTransform),
                typeof(CanvasGroup));
            fragment.transform.SetParent(transform, false);
            MvpUiFactory.Stretch(fragment.GetComponent<RectTransform>());

            GameObject artwork = new GameObject(
                "Artwork",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(EndingPanelGraphic));
            artwork.transform.SetParent(fragment.transform, false);
            RectTransform artworkRect = artwork.GetComponent<RectTransform>();
            float inset = (1f - Mathf.Clamp(illustrationScale, 0.5f, 1f)) * 0.5f;
            artworkRect.anchorMin = new Vector2(inset, inset);
            artworkRect.anchorMax = new Vector2(1f - inset, 1f - inset);
            artworkRect.anchoredPosition = Vector2.zero;
            artworkRect.sizeDelta = Vector2.zero;

            fragments[index] = fragment;
        }
    }

    private void ConfigureFragments(Texture illustration, Rect[] panelRects)
    {
        activeFragmentCount = Mathf.Min(fragments.Length, panelRects != null ? panelRects.Length : 0);
        for (int index = 0; index < fragments.Length; index++)
        {
            GameObject fragment = fragments[index];
            EndingPanelGraphic graphic = fragment.GetComponentInChildren<EndingPanelGraphic>(true);
            if (index < activeFragmentCount)
                graphic.Configure(illustration, panelRects[index]);

            CanvasGroup group = fragment.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            fragment.SetActive(false);
        }
    }

    private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        float elapsed = 0f;
        group.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            group.alpha = Mathf.Lerp(from, to, progress);
            yield return null;
        }

        group.alpha = to;
    }

    private static IEnumerator WaitUnscaled(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
