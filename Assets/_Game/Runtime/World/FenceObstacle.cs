using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// 围栏：羊群数量达到 ObstacleDefinition.RequiredFlockCount 后，接触时自动撞碎。
/// 数量不够时靠 Blocking 层的实体碰撞体挡住羊群；也保留手动交互模式供以后使用。
[DisallowMultipleComponent]
[RequireComponent(typeof(BreakableObstacle))]
public sealed class FenceObstacle : MonoBehaviour
{
    private const string InteractActionName = "Player/Interact";

    [SerializeField] private BreakableObstacle breakable;
    [Tooltip("勾选：羊群数量够了碰到就碎；不勾：数量够了还要按交互键（E）。")]
    [SerializeField] private bool breakOnContact = true;
    [Tooltip("大于 0 时覆盖 ObstacleDefinition 里的门槛（例如外围围栏由关卡控制器统一配置）。")]
    [SerializeField, Min(0)] private int requiredCountOverride;

    private readonly HashSet<Collider2D> collidersInRange = new HashSet<Collider2D>();
    private InputAction interactAction;
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
        interactAction = InputSystem.actions?.FindAction(InteractActionName);
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

        bool interactPressed = InteractPressedThisFrame();
        if (interactPressed)
            AttemptCharge();

        if (CanBreak && breakOnContact)
        {
            breakable.Break();
            ClearRange();
        }
    }

    private void AttemptCharge()
    {
        if (flockInRange == null)
            return;

        bool canBreak = CanBreak;
        flockInRange.ReportFenceChargeImpact(hardImpact: !canBreak);
        if (!canBreak)
            return;

        breakable.Break();
        ClearRange();
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

    private bool InteractPressedThisFrame()
    {
        if (interactAction != null)
            return interactAction.WasPressedThisFrame();

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.eKey.wasPressedThisFrame;
    }
}
