using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class IllustrationIntroController : MonoBehaviour
{
    [Header("Fragments")]
    [SerializeField] private GameObject fragment01;
    [SerializeField] private GameObject fragment02;
    [SerializeField] private GameObject fragment03;

    [Header("Continue UI")]
    [SerializeField] private GameObject continueHint;
    [SerializeField] private GameObject continueButton;

    [Header("Fade")]
    [SerializeField] private Image fadeImage;

    [Header("Timing")]
    [SerializeField] private float fragmentFadeDuration = 0.3f;
    [SerializeField] private float completeLockDuration = 0.6f;
    [SerializeField] private float sceneFadeDuration = 0.5f;

    private int currentStep = 0;

    private bool isAnimating = false;
    private bool canEnterGame = false;
    private bool isLoadingGame = false;


    private void Awake()
    {
        // 初始隐藏三个碎片
        SetFragmentInitialState(fragment01);
        SetFragmentInitialState(fragment02);
        SetFragmentInitialState(fragment03);

        if (continueHint != null)
            continueHint.SetActive(false);

        if (continueButton != null)
            continueButton.SetActive(false);

        if (fadeImage != null)
        {
            fadeImage.gameObject.SetActive(true);

            Color color = fadeImage.color;
            color.a = 0f;
            fadeImage.color = color;

            fadeImage.raycastTarget = false;
        }
    }


    private void Update()
    {
        if (isLoadingGame)
            return;

        if (!WasAdvancePressed())
            return;

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

        isAnimating = true;

        fragment.SetActive(true);

        CanvasGroup canvasGroup =
            GetOrAddCanvasGroup(fragment);

        canvasGroup.alpha = 0f;

        float timer = 0f;

        while (timer < fragmentFadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / fragmentFadeDuration
                );

            canvasGroup.alpha = t;

            yield return null;
        }

        canvasGroup.alpha = 1f;

        isAnimating = false;
    }


    private IEnumerator RevealFinalFragment()
    {
        if (fragment03 == null)
            yield break;

        isAnimating = true;

        fragment03.SetActive(true);

        CanvasGroup canvasGroup =
            GetOrAddCanvasGroup(fragment03);

        canvasGroup.alpha = 0f;

        float timer = 0f;

        while (timer < fragmentFadeDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    timer / fragmentFadeDuration
                );

            canvasGroup.alpha = t;

            yield return null;
        }

        canvasGroup.alpha = 1f;


        // 第三块出现后短暂锁输入，
        // 防止玩家连续点击直接进入游戏。
        timer = 0f;

        while (timer < completeLockDuration)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }


        if (continueHint != null)
            continueHint.SetActive(true);

        if (continueButton != null)
            continueButton.SetActive(true);


        canEnterGame = true;
        isAnimating = false;
    }


    // ============================================================
    // ENTER GAME
    // ============================================================

    private IEnumerator EnterGameRoutine()
    {
        if (isLoadingGame)
            yield break;

        isLoadingGame = true;


        // 淡黑
        if (fadeImage != null)
        {
            fadeImage.raycastTarget = true;

            float timer = 0f;

            Color color =
                fadeImage.color;

            while (timer < sceneFadeDuration)
            {
                timer += Time.unscaledDeltaTime;

                float t =
                    Mathf.Clamp01(
                        timer / sceneFadeDuration
                    );

                color.a = t;
                fadeImage.color = color;

                yield return null;
            }

            color.a = 1f;
            fadeImage.color = color;
        }


        // 使用项目现有 SceneLoader
        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadGameplayScene();
            yield break;
        }


        Debug.LogError(
            "IllustrationIntroController: SceneLoader Instance not found.",
            this
        );

        isLoadingGame = false;
    }


    // ============================================================
    // HELPERS
    // ============================================================

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
}
