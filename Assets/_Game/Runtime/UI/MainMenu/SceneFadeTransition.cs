using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SceneFadeTransition : MonoBehaviour
{
    private const string TransitionObjectName = "Scene Fade Transition";

    private static SceneFadeTransition activeTransition;

    private readonly HashSet<EventSystem> disabledEventSystems = new();

    private CanvasGroup fadeGroup;
    private float previousTimeScale;


    public static bool Begin(Action loadScene, float duration)
    {
        if (loadScene == null || activeTransition != null)
            return false;

        GameObject transitionObject = new GameObject(
            TransitionObjectName,
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(SceneFadeTransition));
        DontDestroyOnLoad(transitionObject);

        activeTransition = transitionObject.GetComponent<SceneFadeTransition>();
        activeTransition.BuildOverlay();
        activeTransition.StartCoroutine(
            activeTransition.TransitionRoutine(loadScene, Mathf.Max(0.05f, duration)));

        return true;
    }


    private void BuildOverlay()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        fadeGroup = GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.interactable = true;
        fadeGroup.blocksRaycasts = true;

        GameObject blocker = new GameObject(
            "Fade Blocker",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        blocker.transform.SetParent(transform, false);

        RectTransform blockerRect = blocker.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.anchoredPosition = Vector2.zero;
        blockerRect.sizeDelta = Vector2.zero;

        Image blockerImage = blocker.GetComponent<Image>();
        blockerImage.color = Color.black;
        blockerImage.raycastTarget = true;
    }


    private IEnumerator TransitionRoutine(Action loadScene, float duration)
    {
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        LockEventSystems();

        yield return Fade(0f, 1f, duration);

        try
        {
            loadScene.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            RestoreInput();
            Destroy(gameObject);
            yield break;
        }

        // SceneLoader resets timeScale during the synchronous load, so lock it again
        // before the new scene receives an interactive frame.
        Time.timeScale = 0f;
        LockEventSystems();

        yield return Fade(1f, 0f, duration);

        RestoreInput();
        Destroy(gameObject);
    }


    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        fadeGroup.alpha = from;

        while (elapsed < duration)
        {
            LockEventSystems();
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            fadeGroup.alpha = Mathf.Lerp(from, to, easedProgress);
            yield return null;
        }

        fadeGroup.alpha = to;
    }


    private void LockEventSystems()
    {
        EventSystem[] eventSystems = FindObjectsByType<EventSystem>();

        foreach (EventSystem eventSystem in eventSystems)
        {
            if (!eventSystem.enabled)
                continue;

            eventSystem.enabled = false;
            disabledEventSystems.Add(eventSystem);
        }
    }


    private void RestoreInput()
    {
        foreach (EventSystem eventSystem in disabledEventSystems)
        {
            if (eventSystem != null)
                eventSystem.enabled = true;
        }

        disabledEventSystems.Clear();
        Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        activeTransition = null;
    }


    private void OnDestroy()
    {
        if (activeTransition == this)
            RestoreInput();
    }
}
