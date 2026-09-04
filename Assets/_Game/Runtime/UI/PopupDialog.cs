using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GameJam.Game.UI
{
    [Serializable]
    public sealed class PopupOption
    {
        [SerializeField] private string text = "选项";
        [SerializeField] private UnityEvent onSelected = new UnityEvent();

        public PopupOption(string optionText)
        {
            text = optionText;
        }

        public string Text => text;
        public UnityEvent OnSelected => onSelected;
    }

    [Serializable]
    public sealed class PopupContent
    {
        [SerializeField] private string title = "标题";
        [TextArea(3, 8)]
        [SerializeField] private string body = "正文";
        [SerializeField] private List<PopupOption> options = new List<PopupOption>();

        public PopupContent(string popupTitle, string popupBody, params string[] optionTexts)
        {
            title = popupTitle;
            body = popupBody;
            options = new List<PopupOption>();

            foreach (var optionText in optionTexts)
            {
                options.Add(new PopupOption(optionText));
            }
        }

        public string Title => title;
        public string Body => body;
        public IReadOnlyList<PopupOption> Options => options;
    }

    /// <summary>
    /// Reusable modal dialog with Inspector-configurable title, body, and option labels/events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PopupDialog : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Transform optionContainer;
        [SerializeField] private Button optionButtonTemplate;

        [Header("Default Content")]
        [SerializeField] private PopupContent defaultContent = new PopupContent(
            "提示", "这是一个可配置的弹窗。", "确认");
        [SerializeField] private bool closeAfterSelection = true;

        private readonly List<Button> spawnedButtons = new List<Button>();
        private PopupContent displayedContent;

        public event Action<int> OptionSelected;

        public void Configure(Text title, Text body, Transform optionsRoot, Button buttonTemplate)
        {
            titleText = title;
            bodyText = body;
            optionContainer = optionsRoot;
            optionButtonTemplate = buttonTemplate;
        }

        public void SetDefaultContent(PopupContent content)
        {
            defaultContent = content;
        }

        public void ShowDefault()
        {
            Show(defaultContent);
        }

        public void Show(PopupContent content)
        {
            displayedContent = content ?? defaultContent;
            if (displayedContent == null)
            {
                return;
            }

            gameObject.SetActive(true);
            if (titleText != null)
            {
                titleText.text = displayedContent.Title;
            }

            if (bodyText != null)
            {
                bodyText.text = displayedContent.Body;
            }

            RebuildOptions();
            Canvas.ForceUpdateCanvases();
        }

        public void Close()
        {
            ClearSpawnedOptions();
            gameObject.SetActive(false);
        }

        private void RebuildOptions()
        {
            ClearSpawnedOptions();
            if (optionButtonTemplate == null || optionContainer == null)
            {
                return;
            }

            var options = displayedContent.Options;
            for (var index = 0; index < options.Count; index++)
            {
                var capturedIndex = index;
                var option = options[index];
                var button = Instantiate(optionButtonTemplate, optionContainer);
                button.gameObject.SetActive(true);

                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = option.Text;
                }

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectOption(capturedIndex));
                spawnedButtons.Add(button);
            }
        }

        private void SelectOption(int index)
        {
            var options = displayedContent == null ? null : displayedContent.Options;
            if (options == null || index < 0 || index >= options.Count)
            {
                return;
            }

            if (closeAfterSelection)
            {
                Close();
            }

            options[index].OnSelected?.Invoke();
            OptionSelected?.Invoke(index);
        }

        private void ClearSpawnedOptions()
        {
            foreach (var button in spawnedButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }

            spawnedButtons.Clear();
        }
    }
}
