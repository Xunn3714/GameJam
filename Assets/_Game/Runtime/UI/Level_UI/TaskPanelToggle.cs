using UnityEngine;
using UnityEngine.UI;

public class TaskPanelToggle : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject taskPanel;
    [SerializeField] private Button taskButton;
    [SerializeField] private Button closeButton;

    [Header("Initial State")]
    [SerializeField] private bool openOnStart = true;


    /// 当前任务面板是否打开。
    /// PauseManager 暂停时会临时隐藏整个任务系统，
    /// 但不会通过 ESC 改变这个展开状态。
    public bool IsOpen
    {
        get
        {
            return taskPanel != null &&
                   taskPanel.activeSelf;
        }
    }


    private void Awake()
    {
        // 自动绑定按钮
        if (taskButton != null)
        {
            taskButton.onClick.AddListener(OpenTaskPanel);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseTaskPanel);
        }


        // 初始状态
        if (openOnStart)
        {
            OpenTaskPanel();
        }
        else
        {
            CloseTaskPanel();
        }
    }


    /// 打开任务面板。
    /// 打开以后左上角任务按钮隐藏。
    public void OpenTaskPanel()
    {
        if (taskPanel != null)
        {
            taskPanel.SetActive(true);
        }

        if (taskButton != null)
        {
            taskButton.gameObject.SetActive(false);
        }
    }


    /// 关闭任务面板。
    /// 关闭以后重新显示左上角任务按钮。
    public void CloseTaskPanel()
    {
        if (taskPanel != null)
        {
            taskPanel.SetActive(false);
        }

        if (taskButton != null)
        {
            taskButton.gameObject.SetActive(true);
        }
    }


    private void OnDestroy()
    {
        // 清理监听
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
