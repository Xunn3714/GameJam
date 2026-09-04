using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class FlockController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private SheepPlayerController playerController;

    [Header("Formation")]
    [SerializeField] private Transform formationRoot;
    [SerializeField] private Transform[] followSlots;

    [Header("Recruitable Sheep")]
    [SerializeField] private RecruitableSheep[] recruitableSheep;

    public int RecruitedCount { get; private set; }

    public event Action<RecruitableSheep, int> SheepRecruited;

    private void Awake()
    {
        if (playerController == null)
        {
            playerController = GetComponent<SheepPlayerController>();
        }
    }

    private void OnEnable()
    {
        if (recruitableSheep == null)
            return;

        foreach (RecruitableSheep sheep in recruitableSheep)
        {
            if (sheep != null)
            {
                sheep.Recruited += HandleSheepRecruited;
            }
        }
    }

    private void OnDisable()
    {
        if (recruitableSheep == null)
            return;

        foreach (RecruitableSheep sheep in recruitableSheep)
        {
            if (sheep != null)
            {
                sheep.Recruited -= HandleSheepRecruited;
            }
        }
    }

    private void Update()
    {
        UpdateFormationDirection();
    }

    private void HandleSheepRecruited(RecruitableSheep sheep)
    {
        if (sheep == null)
            return;

        if (followSlots == null || followSlots.Length == 0)
        {
            Debug.LogWarning("FlockController 没有配置 Follow Slots。", this);
            return;
        }

        if (RecruitedCount >= followSlots.Length)
        {
            Debug.LogWarning("没有更多可用的跟随位置。", this);
            return;
        }

        Transform slot = followSlots[RecruitedCount];

        if (slot == null)
        {
            Debug.LogWarning(
                $"Follow Slot {RecruitedCount} 没有设置。",
                this
            );
            return;
        }

        // 加入族群：
        // 直接成为对应 Slot 的子物体。
        // 从此跟随 PlayerSheep 的整体 Transform 一起移动。
        sheep.transform.SetParent(slot);

        sheep.transform.localPosition = Vector3.zero;
        sheep.transform.localRotation = Quaternion.identity;

        RecruitedCount++;

        SheepRecruited?.Invoke(sheep, RecruitedCount);

        Debug.Log(
            $"{sheep.name} joined formation Slot_{RecruitedCount:00}. " +
            $"Current Sheep Count: {RecruitedCount + 1}",
            sheep
        );
    }

    private void UpdateFormationDirection()
    {
        if (formationRoot == null || playerController == null)
            return;

        Vector2 direction = playerController.LastMoveDirection;

        if (direction.sqrMagnitude <= 0.001f)
            return;

        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        formationRoot.rotation = Quaternion.Euler(
            0f,
            0f,
            angle
        );
    }
}
