using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameJam.Game.UI
{
    /// <summary>Owns the main, start, and settings panels for the generated game menu.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuUIController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private PopupDialog popupDialog;

        [Header("Effects")]
        [SerializeField] private MenuBackgroundMotion backgroundMotion;
        [SerializeField] private CameraShake2D cameraShake;
        [SerializeField] private AudioSource bgmAudioSource;

        [Header("Settings")]
        [SerializeField] private Text bgmButtonText;
        [SerializeField] private string bgmEnabledText = "BGM：开";
        [SerializeField] private string bgmDisabledText = "BGM：关";

        [Header("Popup")]
        [SerializeField] private PopupContent demonstrationPopup = new PopupContent(
            "游戏提示", "这个弹窗的标题、正文与每一个选项文本都可以在 Inspector 中修改。", "知道了", "稍后再说");

        [Header("Gameplay Hook")]
        [SerializeField] private UnityEvent onStartGame = new UnityEvent();

        private void Awake()
        {
            ShowMain();
            RefreshBgmLabel();
        }

        public void Configure(
            GameObject main,
            GameObject start,
            GameObject settings,
            PopupDialog popup,
            MenuBackgroundMotion background,
            CameraShake2D shake,
            AudioSource bgmSource,
            Text bgmLabel)
        {
            mainPanel = main;
            startPanel = start;
            settingsPanel = settings;
            popupDialog = popup;
            backgroundMotion = background;
            cameraShake = shake;
            bgmAudioSource = bgmSource;
            bgmButtonText = bgmLabel;
            RefreshBgmLabel();
        }

        public void Execute(MenuButtonAction action)
        {
            PlayPressFeedback();
            switch (action)
            {
                case MenuButtonAction.ShowMain:
                    ShowMain();
                    break;
                case MenuButtonAction.ShowStart:
                    ShowStart();
                    break;
                case MenuButtonAction.ShowSettings:
                    ShowSettings();
                    break;
                case MenuButtonAction.ToggleBgm:
                    ToggleBgm();
                    break;
                case MenuButtonAction.ShowPopup:
                    ShowDemonstrationPopup();
                    break;
                case MenuButtonAction.StartGame:
                    StartGame();
                    break;
                case MenuButtonAction.ClosePopup:
                    popupDialog?.Close();
                    break;
            }
        }

        public void ShowMain()
        {
            SetPanelActive(mainPanel, true);
            SetPanelActive(startPanel, false);
            SetPanelActive(settingsPanel, false);
        }

        public void ShowStart()
        {
            SetPanelActive(mainPanel, false);
            SetPanelActive(startPanel, true);
            SetPanelActive(settingsPanel, false);
        }

        public void ShowSettings()
        {
            SetPanelActive(mainPanel, false);
            SetPanelActive(startPanel, false);
            SetPanelActive(settingsPanel, true);
        }

        public void ToggleBgm()
        {
            if (bgmAudioSource == null)
            {
                return;
            }

            var enableBgm = bgmAudioSource.mute;
            bgmAudioSource.mute = !enableBgm;
            if (enableBgm && bgmAudioSource.clip != null && !bgmAudioSource.isPlaying)
            {
                bgmAudioSource.Play();
            }

            RefreshBgmLabel();
        }

        public void ShowDemonstrationPopup()
        {
            popupDialog?.Show(demonstrationPopup);
        }

        public void StartGame()
        {
            popupDialog?.Close();
            SetPanelActive(mainPanel, false);
            SetPanelActive(startPanel, false);
            SetPanelActive(settingsPanel, false);
            onStartGame?.Invoke();
        }

        private void RefreshBgmLabel()
        {
            if (bgmButtonText == null)
            {
                return;
            }

            bgmButtonText.text = bgmAudioSource == null || !bgmAudioSource.mute
                ? bgmEnabledText
                : bgmDisabledText;
        }

        private void PlayPressFeedback()
        {
            cameraShake?.Shake(0.08f, 0.025f);
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }
    }
}
