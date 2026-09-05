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
    [SerializeField] private FenceObstacle[] fences = Array.Empty<FenceObstacle>();
    [SerializeField] private GameObject signsRoot;
    [SerializeField] private Rect penRect = new Rect(-9f, -5f, 18f, 10f);
    [SerializeField, Min(0f)] private float signFadeDuration = 1.5f;
    [Tooltip("羊群贴着围栏但数量不够时给的提示，{0}=当前，{1}=需要。")]
    [SerializeField] private string notEnoughHint = "羊群还不够（{0}/{1}），再去碰几只羊";
    [SerializeField] private string readyHint = "羊群够了！按 E 撞开栅栏";

    private bool isOpen;

    public bool IsOpen => isOpen;
    public Rect PenRect => penRect;
    public IReadOnlyList<FenceObstacle> Fences => fences;

    public event Action Opened;
    public event Action<string> HintRequested;

    public void Configure(Rect rect, FenceObstacle[] penFences, GameObject signs)
    {
        penRect = rect;
        fences = penFences ?? Array.Empty<FenceObstacle>();
        signsRoot = signs;
    }

    private void OnEnable()
    {
        foreach (FenceObstacle fence in fences)
        {
            if (fence == null)
                continue;

            fence.StateChanged += HandleFenceStateChanged;
            if (fence.Breakable != null)
                fence.Breakable.Broken += HandleFenceBroken;
        }
    }

    private void OnDisable()
    {
        foreach (FenceObstacle fence in fences)
        {
            if (fence == null)
                continue;

            fence.StateChanged -= HandleFenceStateChanged;
            if (fence.Breakable != null)
                fence.Breakable.Broken -= HandleFenceBroken;
        }
    }

    private void HandleFenceStateChanged(FenceObstacle fence)
    {
        if (isOpen || !fence.IsFlockInRange)
            return;

        HintRequested?.Invoke(fence.CanBreak
            ? readyHint
            : string.Format(notEnoughHint, fence.CurrentFlockCount, fence.RequiredFlockCount));
    }

    private void HandleFenceBroken(BreakableObstacle obstacle)
    {
        if (isOpen)
            return;

        isOpen = true;
        Opened?.Invoke();
        if (signsRoot != null)
            StartCoroutine(FadeSigns());
    }

    private IEnumerator FadeSigns()
    {
        SpriteRenderer[] sprites = signsRoot.GetComponentsInChildren<SpriteRenderer>(true);
        TMP_Text[] texts = signsRoot.GetComponentsInChildren<TMP_Text>(true);
        Color[] spriteColors = new Color[sprites.Length];
        Color[] textColors = new Color[texts.Length];
        for (int i = 0; i < sprites.Length; i++) spriteColors[i] = sprites[i].color;
        for (int i = 0; i < texts.Length; i++) textColors[i] = texts[i].color;

        float elapsed = 0f;
        while (elapsed < signFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / signFadeDuration);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null)
                    sprites[i].color = new Color(spriteColors[i].r, spriteColors[i].g, spriteColors[i].b, spriteColors[i].a * alpha);
            }
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                    texts[i].color = new Color(textColors[i].r, textColors[i].g, textColors[i].b, textColors[i].a * alpha);
            }
            yield return null;
        }

        signsRoot.SetActive(false);
    }
}
