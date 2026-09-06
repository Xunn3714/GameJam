using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IllustrationIntroController : MonoBehaviour
{
    private const string ArtworkChildName = "Artwork";

    [Header("Fragments")]
    [SerializeField] private GameObject fragment01;
    [SerializeField] private GameObject fragment02;
    [SerializeField] private GameObject fragment03;

    [Header("Illustration")]
    [SerializeField]
    private string illustrationResourcePath = "IllustrationIntro/StoryTriptych";

    [SerializeField, Range(0.5f, 1f)]
    private float illustrationScale = 0.82f;

    [Header("Continue UI")]
    [SerializeField] private GameObject continueHint;
    [SerializeField] private GameObject continueButton;

    [Header("Fade")]
    [SerializeField] private Image fadeImage;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float autoAdvanceDelay = 2f;
    [SerializeField] private float fragmentFadeDuration = 0.3f;
    [SerializeField] private float completeLockDuration = 0.6f;
    [SerializeField] private float sceneFadeDuration = 0.5f;

    private int currentStep = 0;
    private float autoAdvanceTimer = 0f;

    private bool isAnimating = false;
    private bool canEnterGame = false;
    private bool isLoadingGame = false;


    private void Awake()
    {
        Texture2D illustration = Resources.Load<Texture2D>(illustrationResourcePath);

        if (illustration == null)
        {
            Debug.LogError(
                $"IllustrationIntroController: illustration not found at Resources/{illustrationResourcePath}.",
                this);
        }
        else
        {
            ConfigureFragmentVisual(fragment01, illustration, 0);
            ConfigureFragmentVisual(fragment02, illustration, 1);
            ConfigureFragmentVisual(fragment03, illustration, 2);
        }

        // 初始隐藏三个碎片
        SetFragmentInitialState(fragment01);
        SetFragmentInitialState(fragment02);
        SetFragmentInitialState(fragment03);

        if (continueHint != null)
            continueHint.SetActive(false);

        if (continueButton != null)
            continueButton.SetActive(false);

        PrepareFadeOverlay();
    }


    private IEnumerator Start()
    {
        if (fadeImage == null)
            yield break;

        yield return FadeImageAlpha(fadeImage, 1f, 0f, sceneFadeDuration);
        SetInputLocked(false);
    }


    private void Update()
    {
        if (isLoadingGame || isAnimating)
            return;

        if (WasAdvancePressed())
        {
            Advance();
            return;
        }

        autoAdvanceTimer += Time.unscaledDeltaTime;

        if (autoAdvanceTimer >= autoAdvanceDelay)
            Advance();
    }


    // ============================================================
    // INPUT
    // ============================================================

    private bool WasAdvancePressed()
    {
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        if (Touchscreen.current != null &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }


    // ============================================================
    // STORY ADVANCE
    // ============================================================

    public void Advance()
    {
        if (isAnimating ||
            isLoadingGame)
        {
            return;
        }

        autoAdvanceTimer = 0f;

        // 三张图全部完成以后
        // 下一次点击正式进入游戏
        if (canEnterGame)
        {
            StartCoroutine(EnterGameRoutine());
            return;
        }

        switch (currentStep)
        {
            case 0:
                StartCoroutine(
                    RevealFragment(fragment01)
                );

                currentStep = 1;
                break;


            case 1:
                StartCoroutine(
                    RevealFragment(fragment02)
                );

                currentStep = 2;
                break;


            case 2:
                StartCoroutine(
                    RevealFinalFragment()
                );

                currentStep = 3;
                break;
        }
    }


    // ============================================================
    // FRAGMENT REVEAL
    // ============================================================

    private IEnumerator RevealFragment(
        GameObject fragment)
    {
        if (fragment == null)
            yield break;

        SetInputLocked(true);

        fragment.SetActive(true);

        CanvasGroup canvasGroup =
            GetOrAddCanvasGroup(fragment);

        canvasGroup.alpha = 0f;

        yield return FadeCanvasGroup(
            canvasGroup,
            0f,
            1f,
            fragmentFadeDuration);

        SetInputLocked(false);
    }


    private IEnumerator RevealFinalFragment()
    {
        if (fragment03 == null)
            yield break;

        SetInputLocked(true);

        fragment03.SetActive(true);

        CanvasGroup canvasGroup =
            GetOrAddCanvasGroup(fragment03);

        canvasGroup.alpha = 0f;

        yield return FadeCanvasGroup(
            canvasGroup,
            0f,
            1f,
            fragmentFadeDuration);


        // 第三块出现后短暂锁输入，
        // 防止玩家连续点击直接进入游戏。
        float timer = 0f;

        while (timer < completeLockDuration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }


        CanvasGroup hintGroup = ShowForFade(continueHint);
        CanvasGroup buttonGroup = ShowForFade(continueButton);

        float promptFadeDuration = Mathf.Max(0.15f, fragmentFadeDuration * 0.75f);
        timer = 0f;

        while (timer < promptFadeDuration)
        {
            timer += Time.unscaledDeltaTime;
            float t = SmoothProgress(timer, promptFadeDuration);

            if (hintGroup != null)
                hintGroup.alpha = t;

            if (buttonGroup != null)
                buttonGroup.alpha = t;

            yield return null;
        }

        SetCanvasGroupReady(hintGroup);
        SetCanvasGroupReady(buttonGroup);
        canEnterGame = true;
        SetInputLocked(false);
    }


    // ============================================================
    // ENTER GAME
    // ============================================================

    private IEnumerator EnterGameRoutine()
    {
        if (isLoadingGame)
            yield break;

        isLoadingGame = true;
        SetInputLocked(true);

        Action loadGame;

        // 使用项目现有 SceneLoader，并让渐变遮罩跨场景保留。
        if (SceneLoader.Instance != null)
            loadGame = SceneLoader.Instance.LoadGameplayScene;
        else
            loadGame = LoadFallbackGameplayScene;

        if (SceneFadeTransition.Begin(loadGame, sceneFadeDuration))
            yield break;

        Debug.LogError(
            "IllustrationIntroController: unable to start the gameplay scene transition.",
            this);

        isLoadingGame = false;
        SetInputLocked(false);
    }


    private void LoadFallbackGameplayScene()
    {
        const string fallbackSceneName = "AlphaFlockExpansion";

        if (Application.CanStreamedLevelBeLoaded(fallbackSceneName))
        {
            SceneManager.LoadScene(fallbackSceneName);
            return;
        }

        Debug.LogError(
            $"IllustrationIntroController: SceneLoader is missing and fallback scene {fallbackSceneName} cannot be loaded.",
            this);

        throw new InvalidOperationException(
            $"Fallback scene {fallbackSceneName} cannot be loaded.");
    }


    // ============================================================
    // HELPERS
    // ============================================================

    private void ConfigureFragmentVisual(
        GameObject fragment,
        Texture illustration,
        int fragmentIndex)
    {
        if (fragment == null)
            return;

        Image placeholder = fragment.GetComponent<Image>();

        if (placeholder != null)
            placeholder.enabled = false;

        Transform artworkTransform = fragment.transform.Find(ArtworkChildName);
        GameObject artwork;

        if (artworkTransform == null)
        {
            artwork = new GameObject(
                ArtworkChildName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(IllustrationFragmentGraphic));
            artwork.transform.SetParent(fragment.transform, false);
        }
        else
        {
            artwork = artworkTransform.gameObject;
        }

        RectTransform artworkRect = artwork.GetComponent<RectTransform>();
        float clampedScale = Mathf.Clamp(illustrationScale, 0.5f, 1f);
        float inset = (1f - clampedScale) * 0.5f;
        artworkRect.anchorMin = new Vector2(inset, inset);
        artworkRect.anchorMax = new Vector2(1f - inset, 1f - inset);
        artworkRect.anchoredPosition = Vector2.zero;
        artworkRect.sizeDelta = Vector2.zero;

        IllustrationFragmentGraphic graphic =
            artwork.GetComponent<IllustrationFragmentGraphic>();
        graphic.Configure(illustration, fragmentIndex);
    }

    private void SetFragmentInitialState(
        GameObject fragment)
    {
        if (fragment == null)
            return;

        CanvasGroup canvasGroup =
            GetOrAddCanvasGroup(fragment);

        canvasGroup.alpha = 0f;

        fragment.SetActive(false);
    }


    private CanvasGroup GetOrAddCanvasGroup(
        GameObject target)
    {
        CanvasGroup canvasGroup =
            target.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup =
                target.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }


    private void PrepareFadeOverlay()
    {
        if (fadeImage == null)
            return;

        fadeImage.gameObject.SetActive(true);
        SetImageAlpha(fadeImage, 1f);
        SetInputLocked(true);
    }


    private void SetInputLocked(bool locked)
    {
        isAnimating = locked;

        if (fadeImage != null)
            fadeImage.raycastTarget = locked;
    }


    private IEnumerator FadeImageAlpha(
        Image image,
        float from,
        float to,
        float duration)
    {
        float timer = 0f;
        SetImageAlpha(image, from);

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            SetImageAlpha(image, Mathf.Lerp(from, to, SmoothProgress(timer, duration)));
            yield return null;
        }

        SetImageAlpha(image, to);
    }


    private IEnumerator FadeCanvasGroup(
        CanvasGroup canvasGroup,
        float from,
        float to,
        float duration)
    {
        float timer = 0f;
        canvasGroup.alpha = from;

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, SmoothProgress(timer, duration));
            yield return null;
        }

        canvasGroup.alpha = to;
    }


    private CanvasGroup ShowForFade(GameObject target)
    {
        if (target == null)
            return null;

        CanvasGroup canvasGroup = GetOrAddCanvasGroup(target);
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        target.SetActive(true);
        return canvasGroup;
    }


    private static void SetCanvasGroupReady(CanvasGroup canvasGroup)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }


    private static float SmoothProgress(float elapsed, float duration)
    {
        if (duration <= 0f)
            return 1f;

        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
    }


    private static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }
}
