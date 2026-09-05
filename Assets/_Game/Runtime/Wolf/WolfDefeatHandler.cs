using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 正式关卡里的狼群失败结算：最后一只羊被狼撞到/叼走时，
/// 停止狼群节奏、锁住操作，用 ResultPanel 预制体显示 DEFEAT，并提供返回标题。
/// </summary>
[DisallowMultipleComponent]
public sealed class WolfDefeatHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlockController flock;
    [SerializeField] private FlockMovementController flockMovement;
    [SerializeField] private WolfEventDirector director;
    [SerializeField] private PauseManager pauseManager;

    [Header("Result UI")]
    [Tooltip("包含 ResultPanelView 的预制体；留空则用一个简易占位面板。")]
    [SerializeField] private ResultPanelView resultPanelPrefab;
    [Tooltip("结算面板挂到哪个 Canvas 下；留空则自动找场景里的第一个 Canvas。")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private string defeatDescription = "最后一只羊被狼叼走了……";

    private bool isDefeated;
    private int sheepLost;
    private float startTime;

    private void Awake()
    {
        if (flock != null && flockMovement == null)
        {
            flockMovement = flock.GetComponent<FlockMovementController>();
        }

        if (pauseManager == null)
        {
            pauseManager = FindAnyObjectByType<PauseManager>();
        }

        startTime = Time.time;
    }

    private void OnEnable()
    {
        if (flock != null)
        {
            flock.MemberCountChanged += HandleMemberCountChanged;
        }

        if (director != null)
        {
            director.WolfReleased += HandleWolfReleased;
        }
    }

    private void OnDisable()
    {
        if (flock != null)
        {
            flock.MemberCountChanged -= HandleMemberCountChanged;
        }

        if (director != null)
        {
            director.WolfReleased -= HandleWolfReleased;
        }
    }

    private void HandleWolfReleased(Wolf wolf)
    {
        wolf.Attacked += HandleWolfAttacked;
    }

    private void HandleWolfAttacked(Wolf wolf, WolfAttackResult result)
    {
        if (result.CapturedSheep != null)
        {
            sheepLost++;
        }
    }

    private void HandleMemberCountChanged(int memberCount)
    {
        if (memberCount <= 0 && !isDefeated)
        {
            TriggerDefeat();
        }
    }

    private void TriggerDefeat()
    {
        isDefeated = true;

        if (director != null)
        {
            director.Stop();
        }

        if (flockMovement != null)
        {
            flockMovement.SetControlEnabled(false);
        }

        if (pauseManager != null)
        {
            pauseManager.SetResultLocked(true);
        }

        ShowResultPanel();
        Time.timeScale = 0f;
        Debug.Log("Defeat: the last sheep was taken by the wolves.", this);
    }

    private void ShowResultPanel()
    {
        if (canvas == null)
        {
            canvas = FindAnyObjectByType<Canvas>();
        }

        int recruited = flock != null ? flock.RecruitedCount : 0;
        float elapsed = Time.time - startTime;

        if (resultPanelPrefab != null && canvas != null)
        {
            ResultPanelView panel = Instantiate(resultPanelPrefab, canvas.transform);
            panel.transform.SetAsLastSibling();
            panel.ReturnTitleRequested += ReturnToTitle;
            panel.ShowDefeat(defeatDescription, 0, 0, recruited, sheepLost, elapsed);
            return;
        }

        if (canvas != null)
        {
            CreatePlaceholderPanel(canvas.transform, recruited, elapsed);
        }
        else
        {
            Debug.LogWarning("WolfDefeatHandler: no Canvas found to show the defeat panel.", this);
        }
    }

    private void CreatePlaceholderPanel(Transform parent, int recruited, float elapsed)
    {
        UnityEngine.UI.Image dimmer = MvpUiFactory.CreateImage("WolfDefeatPanel", parent, new Color(0f, 0f, 0f, 0.7f));
        MvpUiFactory.Stretch(dimmer.rectTransform);

        TMPro.TMP_Text title = MvpUiFactory.CreateText("Title", dimmer.rectTransform, "DEFEAT", 64f, TMPro.TextAlignmentOptions.Center);
        title.color = new Color(1f, 0.35f, 0.3f, 1f);
        MvpUiFactory.Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(600f, 90f));

        TMPro.TMP_Text body = MvpUiFactory.CreateText(
            "Body", dimmer.rectTransform,
            $"{defeatDescription}\n招募 {recruited} 只 · 被叼走 {sheepLost} 只 · 坚持 {elapsed:0}s",
            28f, TMPro.TextAlignmentOptions.Center);
        body.color = MvpUiFactory.Paper;
        MvpUiFactory.Anchor(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(700f, 90f));

        UnityEngine.UI.Button button = MvpUiFactory.CreateButton("Btn_Title", dimmer.rectTransform, "返回标题", ReturnToTitle, new Vector2(240f, 64f));
        MvpUiFactory.Anchor(button.image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -90f), new Vector2(240f, 64f));
    }

    private static void ReturnToTitle()
    {
        Time.timeScale = 1f;

        if (SceneLoader.Instance != null)
        {
            SceneLoader.Instance.LoadMainMenu();
        }
        else
        {
            SceneManager.LoadScene("MainMenu");
        }
    }
}
