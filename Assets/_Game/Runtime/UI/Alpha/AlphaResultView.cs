using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Alpha 场景的成功 / 失败结算页：标题、说明、按类型统计、再来一局（R）、返回标题。
/// 用 MvpUiFactory 在运行时搭建，不依赖预制体。
/// </summary>
public sealed class AlphaResultView : MonoBehaviour
{
    private static readonly Color VictoryColor = new Color(0.95f, 0.85f, 0.35f, 1f);
    private static readonly Color DefeatColor = new Color(1f, 0.38f, 0.32f, 1f);

    private Image dimmer;
    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private TMP_Text statsText;
    private bool isShowing;

    public bool IsShowing => isShowing;

    public static AlphaResultView Create(Transform canvasParent)
    {
        Image dimmer = MvpUiFactory.CreateImage("AlphaResultPanel", canvasParent, new Color(0f, 0f, 0f, 0.78f));
        MvpUiFactory.Stretch(dimmer.rectTransform);
        AlphaResultView view = dimmer.gameObject.AddComponent<AlphaResultView>();
        view.Build(dimmer);
        dimmer.gameObject.SetActive(false);
        return view;
    }

    private void Build(Image root)
    {
        dimmer = root;
        RectTransform rect = root.rectTransform;

        titleText = MvpUiFactory.CreateText("Title", rect, "", 72f, TextAlignmentOptions.Center);
        MvpUiFactory.Anchor(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(900f, 100f));

        descriptionText = MvpUiFactory.CreateText("Description", rect, "", 30f, TextAlignmentOptions.Center);
        descriptionText.color = MvpUiFactory.Paper;
        MvpUiFactory.Anchor(descriptionText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(1000f, 60f));

        statsText = MvpUiFactory.CreateText("Stats", rect, "", 24f, TextAlignmentOptions.TopLeft);
        statsText.color = MvpUiFactory.Paper;
        MvpUiFactory.Anchor(statsText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(1000f, 220f));

        Button restart = MvpUiFactory.CreateButton("Btn_Restart", rect, "再来一局 (R)", Restart, new Vector2(260f, 64f));
        MvpUiFactory.Anchor(restart.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-150f, -200f), new Vector2(260f, 64f));

        Button title = MvpUiFactory.CreateButton("Btn_Title", rect, "返回标题", ReturnToTitle, new Vector2(260f, 64f));
        MvpUiFactory.Anchor(title.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(150f, -200f), new Vector2(260f, 64f));
    }

    public void ShowVictory(string description, string stats)
    {
        Show("冲出草原！", VictoryColor, description, stats);
    }

    public void ShowDefeat(string description, string stats)
    {
        Show("全军覆没", DefeatColor, description, stats);
    }

    private void Show(string title, Color titleColor, string description, string stats)
    {
        if (isShowing)
            return;

        isShowing = true;
        titleText.text = title;
        titleText.color = titleColor;
        descriptionText.text = description;
        statsText.text = stats;
        dimmer.gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    private void Update()
    {
        if (!isShowing)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            Restart();
        }
    }

    private static void Restart()
    {
        SceneReloadUtility.ReloadActiveScene();
    }

    private static void ReturnToTitle()
    {
        Time.timeScale = 1f;
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadMainMenu();
        else
            SceneManager.LoadScene("MainMenu");
    }
}
