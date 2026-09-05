using System;
using System.Collections.Generic;
using UnityEngine;

/// 围栏：E 的整群冲刺命中时按实际冲撞力度判定。
/// 数量或力度不够时由 Blocking 层实体碰撞体截停羊群。
[DisallowMultipleComponent]
[RequireComponent(typeof(BreakableObstacle))]
public sealed class FenceObstacle : MonoBehaviour
{
    [SerializeField] private BreakableObstacle breakable;
    [Tooltip("勾选：羊群数量够了碰到就碎；不勾：只能由 E 整群冲刺撞碎。")]
    [SerializeField] private bool breakOnContact = true;
    [Tooltip("大于 0 时覆盖 ObstacleDefinition 里的门槛（例如外围围栏由关卡控制器统一配置）。")]
    [SerializeField, Min(0)] private int requiredCountOverride;

    private readonly HashSet<Collider2D> collidersInRange = new HashSet<Collider2D>();
    private FlockController flockInRange;
    private bool wasInteractable;
    private bool wasInRange;
    private int lastReportedCount = int.MinValue;

    public bool IsFlockInRange => flockInRange != null;
    public int CurrentFlockCount => flockInRange == null
        ? 0
        : (breakable != null && breakable.Definition != null
            && breakable.Definition.CountSource == ObstacleCountSource.HighestFlockCountThisRun
            ? flockInRange.HighestMemberCount
            : flockInRange.MemberCount);
    public int RequiredFlockCount => requiredCountOverride > 0
        ? requiredCountOverride
        : (breakable != null && breakable.Definition != null
            ? breakable.Definition.RequiredFlockCount
            : 1);
    public BreakableObstacle Breakable => breakable;

    public void SetRequiredCountOverride(int count)
    {
        requiredCountOverride = Mathf.Max(0, count);
        NotifyIfChanged(force: true);
    }
    public bool CanBreak => IsFlockInRange && CurrentFlockCount >= RequiredFlockCount;

    public event Action<FenceObstacle> StateChanged;

    private void Awake()
    {
        if (breakable == null) breakable = GetComponent<BreakableObstacle>();
    }

    private void OnEnable()
    {
        if (flockInRange != null)
        {
            flockInRange.MemberCountChanged += HandleMemberCountChanged;
        }
    }

    private void OnDisable()
    {
        if (flockInRange != null)
        {
            flockInRange.MemberCountChanged -= HandleMemberCountChanged;
        }
    }

    private void Update()
    {
        if (breakable == null || breakable.IsBroken || Time.timeScale == 0f)
            return;

        PruneDestroyedColliders();

        if (CanBreak && breakOnContact)
        {
            breakable.Break();
            ClearRange();
        }
    }

    /// <summary>E 整群冲刺传入本次实际力度，不要求预先停留在交互 Trigger 内。</summary>
    public bool ReceiveDashImpact(FlockController sourceFlock, float impactForce)
    {
        if (sourceFlock == null || breakable == null || breakable.IsBroken)
            return false;

        bool canBreak = FlockActionController.MeetsBreakThreshold(
            impactForce,
            RequiredFlockCount);
        sourceFlock.ReportFenceChargeImpact(hardImpact: !canBreak);
        if (!canBreak)
            return false;

        breakable.Break();
        ClearRange();
        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (breakable == null || breakable.IsBroken)
            return;

        if (!BreakableObstacle.IsFlockContact(other))
            return;

        collidersInRange.Add(other);
        RefreshFlockInRange(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (collidersInRange.Remove(other) && collidersInRange.Count == 0)
        {
            ClearRange();
        }
    }

    private void RefreshFlockInRange(Collider2D other)
    {
        FlockController flock = other.GetComponentInParent<FlockController>();
        if (flock == null)
        {
            SheepMember member = other.GetComponentInParent<SheepMember>();
            flock = member != null ? member.Flock : null;
        }

        if (flock == null || flock == flockInRange)
        {
            NotifyIfChanged();
            return;
        }

        if (flockInRange != null)
        {
            flockInRange.MemberCountChanged -= HandleMemberCountChanged;
        }

        flockInRange = flock;
        flockInRange.MemberCountChanged += HandleMemberCountChanged;
        NotifyIfChanged(force: true);
    }

    private void ClearRange()
    {
        collidersInRange.Clear();

        if (flockInRange != null)
        {
            flockInRange.MemberCountChanged -= HandleMemberCountChanged;
            flockInRange = null;
        }

        NotifyIfChanged(force: true);
    }

    private void HandleMemberCountChanged(int memberCount)
    {
        NotifyIfChanged();
    }

    private void NotifyIfChanged(bool force = false)
    {
        bool inRange = IsFlockInRange;
        int currentCount = CurrentFlockCount;
        bool interactable = CanBreak;
        if (force
            || inRange != wasInRange
            || currentCount != lastReportedCount
            || interactable != wasInteractable)
        {
            wasInRange = inRange;
            lastReportedCount = currentCount;
            wasInteractable = interactable;
            StateChanged?.Invoke(this);
        }
    }

    private void PruneDestroyedColliders()
    {
        if (collidersInRange.Count == 0)
            return;

        collidersInRange.RemoveWhere(collider => collider == null);
        if (collidersInRange.Count == 0 && flockInRange != null)
        {
            ClearRange();
        }
    }
}
