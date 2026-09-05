using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 狼群节奏 HUD：一个阶段图标 + 一句提示 + 倒计时。
/// 图标按阶段切换（羊群平静 → 狼嚎提示 → 一只狼来啦 → 狼跑路）；没配贴图时用色块占位。
/// 挂在 Canvas 下任意 RectTransform 上即可，子元素没引用时会自动创建。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class WolfEventHudView : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WolfEventDirector director;

    [Header("UI (留空则自动创建)")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text countdownText;

    [Header("Phase Icons (可选)")]
    [Tooltip("羊群平静：一只可爱的小灰灰。")]
    [SerializeField] private Sprite calmIcon;
    [Tooltip("狼嚎提示：狼爪 / 狼脚。")]
    [SerializeField] private Sprite howlIcon;
    [Tooltip("开始攻击：一只凶狠的狼头。")]
    [SerializeField] private Sprite attackIcon;
    [Tooltip("事件结束：狼跑路。")]
    [SerializeField] private Sprite retreatIcon;

    [Header("Placeholder Colors")]
    [SerializeField] private Color calmColor = new Color(0.62f, 0.62f, 0.65f, 1f);
    [SerializeField] private Color howlColor = new Color(0.95f, 0.65f, 0.2f, 1f);
    [SerializeField] private Color attackColor = new Color(0.85f, 0.2f, 0.15f, 1f);
    [SerializeField] private Color retreatColor = new Color(0.4f, 0.55f, 0.85f, 1f);

    [Header("Texts")]
    [SerializeField] private string calmText = "羊群平静";
    [SerializeField] private string howlText = "狼嚎……狼要来了！";
    [SerializeField] private string attackText = "一只狼来啦！";
    [SerializeField] private string retreatText = "攻击结束，狼跑路了";
    [Tooltip("{0} = 当前羊数，{1} = 门槛。")]
    [SerializeField] private string dormantText = "羊群还小，狼群尚未出现（{0}/{1}）";
    [SerializeField] private bool hideIconWhenDormant = true;
    [Tooltip("勾选后只在狼嚎 / 攻击 / 狼跑路期间显示，平静和蛰伏阶段整块隐藏。")]
    [SerializeField] private bool showOnlyDuringEvent;

    [Header("Howl Flash")]
    [SerializeField, Min(0f)] private float howlFlashFrequency = 3f;

    private TMP_Text placeholderGlyph;

    private void Awake()
    {
        EnsureUi();
    }

    private void OnEnable()
    {
        if (director != null)
        {
            director.PhaseChanged += HandlePhaseChanged;
            HandlePhaseChanged(director.Phase);
        }
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.PhaseChanged -= HandlePhaseChanged;
        }
    }

    public void Bind(WolfEventDirector target)
    {
        if (director != null && isActiveAndEnabled)
        {
            director.PhaseChanged -= HandlePhaseChanged;
        }

        director = target;

        if (director != null && isActiveAndEnabled)
        {
            director.PhaseChanged += HandlePhaseChanged;
            HandlePhaseChanged(director.Phase);
        }
    }

    private void Update()
    {
        if (director == null)
            return;

        if (countdownText != null)
        {
            float remaining = director.PhaseTimeRemaining;
            countdownText.text = remaining < 0f ? "" : $"{Mathf.CeilToInt(remaining)}s";
        }

        if (director.Phase == WolfEventPhase.Dormant && phaseText != null)
        {
            phaseText.text = string.Format(dormantText, director.CurrentMemberCount, director.RequiredMemberCount);
        }

        // 狼嚎阶段图标闪烁，提醒玩家。
        if (iconImage != null && director.Phase == WolfEventPhase.Howl)
        {
            float blink = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * howlFlashFrequency * Mathf.PI));
            iconImage.color = iconImage.sprite != null
                ? new Color(1f, 1f, 1f, blink)
                : new Color(howlColor.r, howlColor.g, howlColor.b, blink);
        }
    }

    private void HandlePhaseChanged(WolfEventPhase phase)
    {
        Sprite sprite;
        Color color;
        string text;
        string glyph;

        switch (phase)
        {
            case WolfEventPhase.Howl:
                sprite = howlIcon; color = howlColor; text = howlText; glyph = "爪";
                break;
            case WolfEventPhase.Attack:
                sprite = attackIcon; color = attackColor; glyph = "狼";
                // 编队攻击时显示编队名（例如"长狼包夹！"），独狼沿用原文案。
                text = director != null
                       && !string.IsNullOrEmpty(director.CurrentAttackName)
                       && director.CurrentAttackName != WolfFormation.DefaultName(WolfFormationType.Single)
                    ? director.CurrentAttackName + "！"
                    : attackText;
                break;
            case WolfEventPhase.Retreat:
                sprite = retreatIcon; color = retreatColor; text = retreatText; glyph = "逃";
                break;
            case WolfEventPhase.Dormant:
                sprite = calmIcon; color = calmColor;
                text = string.Format(dormantText, director != null ? director.CurrentMemberCount : 0,
                    director != null ? director.RequiredMemberCount : 0);
                glyph = "羊";
                break;
            default:
                sprite = calmIcon; color = calmColor; text = calmText; glyph = "羊";
                break;
        }

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.color = sprite != null ? Color.white : color;
            iconImage.gameObject.SetActive(!(hideIconWhenDormant && phase == WolfEventPhase.Dormant));
        }

        if (showOnlyDuringEvent)
        {
            bool eventActive = phase == WolfEventPhase.Howl
                || phase == WolfEventPhase.Attack
                || phase == WolfEventPhase.Retreat;
            if (iconImage != null) iconImage.gameObject.SetActive(eventActive);
            if (phaseText != null) phaseText.gameObject.SetActive(eventActive);
            if (countdownText != null) countdownText.gameObject.SetActive(eventActive);
        }

        if (placeholderGlyph != null)
        {
            placeholderGlyph.gameObject.SetActive(sprite == null);
            placeholderGlyph.text = glyph;
        }

        if (phaseText != null)
        {
            phaseText.text = text;
        }
    }

    private void EnsureUi()
    {
        RectTransform root = (RectTransform)transform;

        if (iconImage == null)
        {
            iconImage = MvpUiFactory.CreateImage("PhaseIcon", root, calmColor);
            MvpUiFactory.Anchor(
                iconImage.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(36f, -36f),
                new Vector2(56f, 56f));
            iconImage.raycastTarget = false;

            placeholderGlyph = MvpUiFactory.CreateText(
                "Glyph", iconImage.rectTransform, "羊", 30f, TextAlignmentOptions.Center);
            placeholderGlyph.color = MvpUiFactory.Paper;
            MvpUiFactory.Stretch(placeholderGlyph.rectTransform);
        }

        if (phaseText == null)
        {
            phaseText = MvpUiFactory.CreateText("PhaseText", root, calmText, 24f, TextAlignmentOptions.MidlineLeft);
            phaseText.color = MvpUiFactory.Paper;
            MvpUiFactory.Anchor(
                phaseText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(240f, -28f),
                new Vector2(360f, 34f));
        }

        if (countdownText == null)
        {
            countdownText = MvpUiFactory.CreateText("Countdown", root, "", 20f, TextAlignmentOptions.MidlineLeft);
            countdownText.color = MvpUiFactory.Paper;
            MvpUiFactory.Anchor(
                countdownText.rectTransform,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(240f, -58f),
                new Vector2(360f, 28f));
        }
    }
}
