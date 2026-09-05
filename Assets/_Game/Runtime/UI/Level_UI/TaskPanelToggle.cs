using UnityEngine;
using UnityEngine.UI;

public class TaskPanelToggle : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject taskPanel;
    [SerializeField] private Button taskButton;
    [SerializeField] private Button closeButton;

    [Header("Initial State")]
    [SerializeField] private bool openOnStart = false;


    private void Awake()
    {
        if (taskButton != null)
        {
            taskButton.onClick.AddListener(OpenTaskPanel);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseTaskPanel);
        }

        if (openOnStart)
        {
            OpenTaskPanel();
        }
        else
        {
            CloseTaskPanel();
        }
    }


    public void OpenTaskPanel()
    {
        if (taskPanel != null)
        {
            taskPanel.SetActive(true);
        }

        // 打开任务面板后，隐藏左上角任务图标
        if (taskButton != null)
        {
            taskButton.gameObject.SetActive(false);
        }
    }


    public void CloseTaskPanel()
    {
        if (taskPanel != null)
        {
            taskPanel.SetActive(false);
        }

        // 关闭任务面板后，重新显示任务图标
        if (taskButton != null)
        {
            taskButton.gameObject.SetActive(true);
        }
    }


    private void OnDestroy()
    {
        if (taskButton != null)
        {
            taskButton.onClick.RemoveListener(OpenTaskPanel);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseTaskPanel);
        }
    }
}
