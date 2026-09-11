using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Alpha 的右上角羊群状态和世界位置破坏加分跳字。
/// 羊群数量是主信息，破坏得分以略小的第二张纸签展示。
/// 短时间内位置相近的多次破坏会合并成一个跳字，但统计仍逐件记录。
/// </summary>
[DisallowMultipleComponent]
public sealed class DestructionScoreHudView : MonoBehaviour
{
    private sealed class Popup
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public TMP_Text Label;
        public Vector2 StartPosition;
        public float Age;
        public int Score;
    }

    private const float PopupLifetime = 0.72f;
    private const float MergeWindow = 0.12f;
    private const float MergeDistance = 120f;

    private readonly List<Popup> popups = new List<Popup>();
    private RectTransform canvasRect;
    private RectTransform popupLayer;
    private Camera worldCamera;
    private CanvasGroup hudGroup;
    private TMP_Text flockText;
    private TMP_Text scoreText;
    private float flockPunch;
    private float scorePunch;
    private bool gameplayVisible = true;

    public int DisplayedFlockCount { get; private set; }
    public int DisplayedScore { get; private set; }
    public int ActivePopupCount => popups.Count;

    public static DestructionScoreHudView Create(
        Transform canvasParent,
        Camera camera,
        Sprite paperStripSprite = null)
    {
        if (canvasParent == null)
            return null;

        RectTransform layer = MvpUiFactory.CreateRect("DestructionScorePopups", canvasParent);
        MvpUiFactory.Stretch(layer);

        Image panel = MvpUiFactory.CreateImage(
            "FlockStatusHud",
            canvasParent,
            paperStripSprite != null ? Color.white : MvpUiFactory.Paper);
        MvpUiFactory.Anchor(
            panel.rectTransform,
            Vector2.one,
            Vector2.one,
            new Vector2(-18f, -16f),
            new Vector2(400f, 70f));
        panel.raycastTarget = false;
        panel.sprite = paperStripSprite;
        panel.preserveAspect = false;

        if (paperStripSprite == null)
        {
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.27f, 0.25f, 0.15f, 0.42f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        TMP_Text flockCount = MvpUiFactory.CreateText(
            "FlockCount",
            panel.transform,
            "羊群　<size=145%><b>0</b></size><size=80%> 只</size>",
            26f,
            TextAlignmentOptions.Center);
        MvpUiFactory.Stretch(flockCount.rectTransform, 12f);
        flockCount.color = new Color(0.24f, 0.31f, 0.13f, 1f);
        flockCount.fontStyle = FontStyles.Normal;
        flockCount.enableWordWrapping = false;

        Image scorePanel = MvpUiFactory.CreateImage(
            "ScorePanel",
            panel.transform,
            paperStripSprite != null
                ? new Color(0.92f, 0.95f, 0.84f, 0.98f)
                : new Color(0.88f, 0.90f, 0.74f, 0.96f));
        MvpUiFactory.Anchor(
            scorePanel.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, -7f),
            new Vector2(290f, 46f));
        scorePanel.rectTransform.pivot = new Vector2(0.5f, 1f);
        scorePanel.sprite = paperStripSprite;
        scorePanel.preserveAspect = false;
        scorePanel.raycastTarget = false;

        TMP_Text scoreLabel = MvpUiFactory.CreateText(
            "Score",
            scorePanel.transform,
            "破坏得分　0",
            20f,
            TextAlignmentOptions.Center);
        MvpUiFactory.Stretch(scoreLabel.rectTransform, 8f);
        scoreLabel.color = new Color(0.29f, 0.30f, 0.18f, 0.84f);
        scoreLabel.fontStyle = FontStyles.Normal;
        scoreLabel.enableWordWrapping = false;

        DestructionScoreHudView view = panel.gameObject.AddComponent<DestructionScoreHudView>();
        view.canvasRect = canvasParent as RectTransform;
        view.popupLayer = layer;
        view.worldCamera = camera;
        view.hudGroup = panel.gameObject.AddComponent<CanvasGroup>();
        view.flockText = flockCount;
        view.scoreText = scoreLabel;
        return view;
    }

    public void ShowGain(Vector3 worldPosition, int score, int totalScore)
    {
        int awarded = Mathf.Max(1, score);
        SetScore(totalScore);

        if (canvasRect == null || popupLayer == null)
            return;

        Camera camera = worldCamera != null ? worldCamera : Camera.main;
        if (camera == null)
            return;

        Vector2 screenPosition = camera.WorldToScreenPoint(worldPosition);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPosition,
                null,
                out Vector2 localPosition))
        {
            return;
        }

        Popup mergeTarget = FindMergeTarget(localPosition);
        if (mergeTarget != null)
        {
            mergeTarget.Score += awarded;
            mergeTarget.Label.text = $"+{mergeTarget.Score}";
            mergeTarget.Age = 0f;
            mergeTarget.StartPosition = (mergeTarget.StartPosition + localPosition) * 0.5f;
            mergeTarget.Rect.anchoredPosition = mergeTarget.StartPosition;
            mergeTarget.Rect.localScale = Vector3.one * 1.18f;
            return;
        }

        TMP_Text label = MvpUiFactory.CreateText(
            "DestructionGain",
            popupLayer,
            $"+{awarded}",
            FontSizeFor(awarded),
            TextAlignmentOptions.Center);
        MvpUiFactory.Anchor(
            label.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            localPosition,
            new Vector2(220f, 80f));
        label.color = ColorFor(awarded);
        label.fontStyle = FontStyles.Bold;
        label.enableWordWrapping = false;

        Popup popup = new Popup
        {
            Rect = label.rectTransform,
            Group = label.gameObject.AddComponent<CanvasGroup>(),
            Label = label,
            StartPosition = localPosition,
            Score = awarded
        };
        popup.Rect.localScale = Vector3.one * 0.72f;
        popups.Add(popup);
    }

    public void SetScore(int score)
    {
        int normalized = Mathf.Max(0, score);
        bool changed = normalized != DisplayedScore;
        DisplayedScore = normalized;
        if (scoreText != null)
            scoreText.text = $"破坏得分　{DisplayedScore:N0}";
        if (changed)
            scorePunch = 1f;
    }

    public void SetFlockCount(int memberCount)
    {
        int normalized = Mathf.Max(0, memberCount);
        bool changed = normalized != DisplayedFlockCount;
        DisplayedFlockCount = normalized;
        if (flockText != null)
            flockText.text =
                $"羊群　<size=145%><b>{DisplayedFlockCount:N0}</b></size><size=80%> 只</size>";
        if (changed)
            flockPunch = 1f;
    }

    public void SetGameplayVisible(bool visible)
    {
        gameplayVisible = visible;
        RefreshVisibility();
    }

    private void Update()
    {
        RefreshVisibility();
        if (!gameplayVisible || Time.timeScale <= 0f)
            return;

        float deltaTime = Time.deltaTime;
        flockPunch = Mathf.MoveTowards(flockPunch, 0f, deltaTime * 4.5f);
        scorePunch = Mathf.MoveTowards(scorePunch, 0f, deltaTime * 6f);
        if (flockText != null)
            flockText.rectTransform.localScale =
                Vector3.one * (1f + Mathf.Sin(flockPunch * Mathf.PI) * 0.15f);
        if (scoreText != null)
            scoreText.rectTransform.localScale =
                Vector3.one * (1f + Mathf.Sin(scorePunch * Mathf.PI) * 0.05f);

        for (int index = popups.Count - 1; index >= 0; index--)
        {
            Popup popup = popups[index];
            popup.Age += deltaTime;
            float progress = Mathf.Clamp01(popup.Age / PopupLifetime);
            float enter = Mathf.Clamp01(progress / 0.18f);
            popup.Rect.anchoredPosition = popup.StartPosition
                + Vector2.up * Mathf.Lerp(0f, 72f, 1f - Mathf.Pow(1f - progress, 2f));
            popup.Rect.localScale = Vector3.one * Mathf.Lerp(0.72f, 1f, enter);
            popup.Group.alpha = 1f - Mathf.Clamp01((progress - 0.55f) / 0.45f);
            if (progress < 1f)
                continue;

            Destroy(popup.Rect.gameObject);
            popups.RemoveAt(index);
        }
    }

    private Popup FindMergeTarget(Vector2 position)
    {
        float maxDistanceSqr = MergeDistance * MergeDistance;
        for (int index = popups.Count - 1; index >= 0; index--)
        {
            Popup popup = popups[index];
            if (popup.Age <= MergeWindow
                && (popup.StartPosition - position).sqrMagnitude <= maxDistanceSqr)
            {
                return popup;
            }
        }
        return null;
    }

    private void RefreshVisibility()
    {
        float alpha = gameplayVisible && Time.timeScale > 0f ? 1f : 0f;
        if (hudGroup != null)
            hudGroup.alpha = alpha;
        if (popupLayer != null)
            popupLayer.gameObject.SetActive(alpha > 0f);
    }

    private static float FontSizeFor(int score)
    {
        if (score >= 50) return 38f;
        if (score >= 6) return 30f;
        return 24f;
    }

    private static Color ColorFor(int score)
    {
        if (score >= 50) return new Color32(255, 75, 60, 255);
        if (score >= 6) return new Color32(255, 155, 45, 255);
        return new Color32(255, 225, 70, 255);
    }
}
