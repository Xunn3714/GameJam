using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 出生羊圈：一圈教程围栏 + 地上的教程标识。任意一段围栏被撞开即视为“教程完成”，标识淡出。
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialPen : MonoBehaviour
{
    private const int DefaultRequiredFlockCount = 6;

    private enum TutorialStage
    {
        Move,
        Recruit,
        Fence,
        Complete
    }

    [SerializeField] private FenceObstacle[] fences = Array.Empty<FenceObstacle>();
    [SerializeField] private GameObject signsRoot;
    [SerializeField] private FlockController flock;
    [SerializeField] private GameObject moveSignsRoot;
    [SerializeField] private GameObject recruitSignsRoot;
    [SerializeField] private GameObject fenceSignsRoot;
    [SerializeField, Min(1)] private int requiredFlockCount = DefaultRequiredFlockCount;
    [SerializeField] private Rect penRect = new Rect(-9f, -5f, 18f, 10f);
    [SerializeField, Min(0f)] private float signFadeDuration = 1.5f;

    private bool isOpen;
    private TutorialStage stage;
    private Coroutine stageFadeCoroutine;
    private readonly Dictionary<SpriteRenderer, float> spriteBaseAlphas =
        new Dictionary<SpriteRenderer, float>();
    private readonly Dictionary<TMP_Text, float> textBaseAlphas =
        new Dictionary<TMP_Text, float>();

    public bool IsOpen => isOpen;
    public Rect PenRect => penRect;
    public IReadOnlyList<FenceObstacle> Fences => fences;
    public int RequiredFlockCount => EffectiveRequiredFlockCount;

    public event Action Opened;

    public void Configure(
        Rect rect,
        FenceObstacle[] penFences,
        GameObject signs,
        GameObject moveSigns,
        GameObject recruitSigns,
        GameObject fenceSigns)
    {
        penRect = rect;
        fences = penFences ?? Array.Empty<FenceObstacle>();
        signsRoot = signs;
        moveSignsRoot = moveSigns;
        recruitSignsRoot = recruitSigns;
        fenceSignsRoot = fenceSigns;
        CacheSignAlphas();
        RefreshTutorialStage();
    }

    public void BindFlock(FlockController controller)
    {
        UnsubscribeFromFlock();
        flock = controller;
        SubscribeToFlock();
        CacheSignAlphas();
        RefreshTutorialStage();
    }

    private void OnEnable()
    {
        foreach (FenceObstacle fence in fences)
        {
            if (fence == null)
                continue;

            fence.SetRequiredCountOverride(EffectiveRequiredFlockCount);
            if (fence.Breakable != null)
                fence.Breakable.Broken += HandleFenceBroken;
        }

        SubscribeToFlock();
        CacheSignAlphas();
        RefreshTutorialStage();
    }

    private void OnDisable()
    {
        foreach (FenceObstacle fence in fences)
        {
            if (fence == null)
                continue;

            if (fence.Breakable != null)
                fence.Breakable.Broken -= HandleFenceBroken;
        }

        UnsubscribeFromFlock();
        if (stageFadeCoroutine != null)
        {
            StopCoroutine(stageFadeCoroutine);
            stageFadeCoroutine = null;
        }
    }

    private void Update()
    {
        if (!isOpen && stage == TutorialStage.Move && flock != null && flock.IsMoving)
            ShowStage(TutorialStage.Recruit);
    }

    private void SubscribeToFlock()
    {
        if (flock == null || !isActiveAndEnabled)
            return;

        flock.MemberCountChanged -= HandleMemberCountChanged;
        flock.MemberCountChanged += HandleMemberCountChanged;
    }

    private void UnsubscribeFromFlock()
    {
        if (flock == null)
            return;

        flock.MemberCountChanged -= HandleMemberCountChanged;
    }

    private void HandleMemberCountChanged(int memberCount)
    {
        if (!isOpen && memberCount >= EffectiveRequiredFlockCount)
            ShowStage(TutorialStage.Fence);
    }

    private void RefreshTutorialStage()
    {
        if (isOpen)
        {
            if (signsRoot != null)
                signsRoot.SetActive(false);
            return;
        }

        if (flock != null && flock.MemberCount >= EffectiveRequiredFlockCount)
        {
            ShowStage(TutorialStage.Fence);
            return;
        }

        if (flock != null && flock.MemberCount > 1)
        {
            ShowStage(TutorialStage.Recruit);
            return;
        }

        ShowStage(TutorialStage.Move);
    }

    private int EffectiveRequiredFlockCount =>
        requiredFlockCount > 1 ? requiredFlockCount : DefaultRequiredFlockCount;

    private void ShowStage(TutorialStage nextStage)
    {
        stage = nextStage;
        if (!Application.isPlaying)
        {
            SetStageImmediate(nextStage);
            return;
        }

        if (IsStageAlreadyDisplayed(nextStage))
        {
            SetStageImmediate(nextStage);
            return;
        }

        if (stageFadeCoroutine != null)
            StopCoroutine(stageFadeCoroutine);
        stageFadeCoroutine = StartCoroutine(FadeToStage(nextStage));
    }

    private IEnumerator FadeToStage(TutorialStage nextStage)
    {
        float fadeDuration = Mathf.Max(0.05f, signFadeDuration);
        GameObject[] groups = { moveSignsRoot, recruitSignsRoot, fenceSignsRoot };
        float[] start = new float[groups.Length];
        bool hasVisibleGroup = false;

        for (int index = 0; index < groups.Length; index++)
        {
            start[index] = GetGroupAlpha(groups[index]);
            hasVisibleGroup |= start[index] > 0f;
        }

        if (!hasVisibleGroup)
        {
            SetStageImmediate(nextStage);
            if (nextStage == TutorialStage.Complete && signsRoot != null)
                signsRoot.SetActive(false);
            stageFadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeDuration);
            for (int index = 0; index < groups.Length; index++)
                SetGroupAlpha(groups[index], start[index] * (1f - progress));
            yield return null;
        }

        for (int index = 0; index < groups.Length; index++)
        {
            SetGroupAlpha(groups[index], 1f);
            if (groups[index] != null)
                groups[index].SetActive(false);
        }

        SetStageImmediate(nextStage);
        if (nextStage == TutorialStage.Complete && signsRoot != null)
            signsRoot.SetActive(false);
        stageFadeCoroutine = null;
    }

    private bool IsStageAlreadyDisplayed(TutorialStage targetStage)
    {
        bool moveVisible = moveSignsRoot != null && moveSignsRoot.activeSelf;
        bool recruitVisible = recruitSignsRoot != null && recruitSignsRoot.activeSelf;
        bool fenceVisible = fenceSignsRoot != null && fenceSignsRoot.activeSelf;
        return moveVisible == (targetStage == TutorialStage.Move)
            && recruitVisible == (targetStage == TutorialStage.Recruit)
            && fenceVisible == (targetStage == TutorialStage.Fence);
    }

    private void SetStageImmediate(TutorialStage nextStage)
    {
        SetGroupVisible(moveSignsRoot, nextStage == TutorialStage.Move);
        SetGroupVisible(recruitSignsRoot, nextStage == TutorialStage.Recruit);
        SetGroupVisible(fenceSignsRoot, nextStage == TutorialStage.Fence);
    }

    private void SetGroupVisible(GameObject group, bool visible)
    {
        if (group == null)
            return;

        SetGroupAlpha(group, 1f);
        group.SetActive(visible);
    }

    private void CacheSignAlphas()
    {
        if (signsRoot == null)
            return;

        foreach (SpriteRenderer sprite in signsRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sprite != null && !spriteBaseAlphas.ContainsKey(sprite))
                spriteBaseAlphas.Add(sprite, sprite.color.a);
        }

        foreach (TMP_Text text in signsRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && !textBaseAlphas.ContainsKey(text))
                textBaseAlphas.Add(text, text.color.a);
        }
    }

    private float GetGroupAlpha(GameObject group)
    {
        if (group == null || !group.activeSelf)
            return 0f;

        foreach (SpriteRenderer sprite in group.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sprite != null && spriteBaseAlphas.TryGetValue(sprite, out float baseAlpha) && baseAlpha > 0f)
                return Mathf.Clamp01(sprite.color.a / baseAlpha);
        }

        foreach (TMP_Text text in group.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && textBaseAlphas.TryGetValue(text, out float baseAlpha) && baseAlpha > 0f)
                return Mathf.Clamp01(text.color.a / baseAlpha);
        }

        return 1f;
    }

    private void SetGroupAlpha(GameObject group, float alpha)
    {
        if (group == null)
            return;

        foreach (SpriteRenderer sprite in group.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (sprite == null || !spriteBaseAlphas.TryGetValue(sprite, out float baseAlpha))
                continue;
            Color color = sprite.color;
            color.a = baseAlpha * alpha;
            sprite.color = color;
        }

        foreach (TMP_Text text in group.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null || !textBaseAlphas.TryGetValue(text, out float baseAlpha))
                continue;
            Color color = text.color;
            color.a = baseAlpha * alpha;
            text.color = color;
        }
    }

    private void HandleFenceBroken(BreakableObstacle obstacle)
    {
        if (isOpen)
            return;

        isOpen = true;
        Opened?.Invoke();
        ShowStage(TutorialStage.Complete);
    }
}
