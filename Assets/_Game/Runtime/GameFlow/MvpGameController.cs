using UnityEngine;

[DisallowMultipleComponent]
public sealed class MvpGameController : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private FlockController flockController;

    [Header("UI")]
    [SerializeField] private MvpHudView hudView;
    [SerializeField] private JoinToastView joinToastView;

    private RecruitSheepTask recruitTask;

    private void Awake()
    {
        // 当前任务固定寻找 5 只羊。
        recruitTask = new RecruitSheepTask(5);

        // 初始化 HUD。
        UpdateHud();
    }

    private void OnEnable()
    {
        if (flockController != null)
        {
            flockController.SheepRecruited += HandleSheepRecruited;
            flockController.MemberCountChanged += HandleMemberCountChanged;
        }
    }

    private void Start()
    {
        UpdateHud();
    }

    private void OnDisable()
    {
        if (flockController != null)
        {
            flockController.SheepRecruited -= HandleSheepRecruited;
            flockController.MemberCountChanged -= HandleMemberCountChanged;
        }
    }

    private void HandleMemberCountChanged(int memberCount)
    {
        UpdateHud();
    }

    private void HandleSheepRecruited(
        RecruitableSheep sheep,
        int recruitedCount)
    {
        if (recruitTask == null)
            return;

        // 任务进度 +1。
        recruitTask.RecordRecruit();

        // 更新左上角 HUD。
        UpdateHud();

        // 显示这只羊的随机名字。
        ShowJoinToast(sheep);

        Debug.Log(
            $"任务进度：{recruitTask.Progress}/{recruitTask.Target}"
        );
    }

    private void UpdateHud()
    {
        if (hudView == null || recruitTask == null)
            return;

        hudView.UpdateProgress(
            recruitTask.Progress,
            recruitTask.Target,
            flockController != null ? flockController.MemberCount : 0
        );
    }

    private void ShowJoinToast(RecruitableSheep sheep)
    {
        if (joinToastView == null || sheep == null)
            return;

        SheepIdentity identity =
            sheep.GetComponent<SheepIdentity>();

        if (identity == null)
        {
            Debug.LogWarning(
                $"{sheep.name} 没有 SheepIdentity。",
                sheep
            );

            return;
        }

        string sheepName = identity.DisplayName;

        if (string.IsNullOrWhiteSpace(sheepName))
        {
            Debug.LogWarning(
                $"{sheep.name} 尚未分配随机名字。",
                sheep
            );

            return;
        }

        joinToastView.Show(sheepName);
    }
}
