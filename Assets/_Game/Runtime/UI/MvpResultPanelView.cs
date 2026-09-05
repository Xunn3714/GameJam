using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MvpResultPanelView : MonoBehaviour
{
    private TMP_Text bodyText;
    private Button returnButton;

    public static MvpResultPanelView CreatePlaceholder(Transform parent)
    {
        Image dimmer = MvpUiFactory.CreateImage(
            "MvpResultPanel",
            parent,
            new Color(0f, 0f, 0f, 0.68f));
        MvpUiFactory.Stretch(dimmer.rectTransform);

        Image window = MvpUiFactory.CreateImage("Window", dimmer.transform, MvpUiFactory.Paper);
        MvpUiFactory.Anchor(
            window.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(760f, 560f));

        TMP_Text title = MvpUiFactory.CreateText(
            "Title", window.transform, "羊群集合完毕！", 46f, TextAlignmentOptions.Center);
        MvpUiFactory.Anchor(
            title.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -62f),
            new Vector2(650f, 80f));

        TMP_Text body = MvpUiFactory.CreateText(
            "Stats", window.transform, string.Empty, 28f, TextAlignmentOptions.TopLeft);
        MvpUiFactory.Anchor(
            body.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -5f),
            new Vector2(620f, 300f));

        Button button = MvpUiFactory.CreateButton(
            "ReturnToTitleButton", window.transform, "返回标题", null, new Vector2(320f, 72f));
        MvpUiFactory.Anchor(
            button.GetComponent<RectTransform>(),
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 62f),
            new Vector2(320f, 72f));

        MvpResultPanelView view = dimmer.gameObject.AddComponent<MvpResultPanelView>();
        view.bodyText = body;
        view.returnButton = button;
        dimmer.gameObject.SetActive(false);
        return view;
    }

    public void Show(MvpResultSnapshot result, Action onReturnToTitle)
    {
        if (result == null)
            return;

        TimeSpan elapsed = TimeSpan.FromSeconds(result.ElapsedSeconds);
        StringBuilder names = new();
        for (int i = 0; i < result.MemberNames.Count; i++)
        {
            if (i > 0) names.Append("、");
            names.Append(result.MemberNames[i]);
        }

        bodyText.text =
            $"当前羊数：{result.CurrentFlockCount}\n" +
            $"成功招募：{result.RecruitedTotal}\n" +
            $"当前分数：{result.CurrentScore}\n" +
            $"拉屎次数：{result.PoopUses}\n" +
            $"游戏用时：{elapsed.Minutes:00}:{elapsed.Seconds:00}\n\n" +
            $"本局同伴：{names}";

        returnButton.onClick.RemoveAllListeners();
        returnButton.onClick.AddListener(() => onReturnToTitle?.Invoke());
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        returnButton.Select();
    }
}
