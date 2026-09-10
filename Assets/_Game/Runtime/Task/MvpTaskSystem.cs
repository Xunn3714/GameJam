using System;
using System.Collections.Generic;

public enum MvpTaskMetric
{
    RecruitedTotal,
    CurrentFlockCount,
    PoopUses
}

public enum MvpTaskConditionMode
{
    All,
    Any
}

public readonly struct MvpRunMetrics
{
    public MvpRunMetrics(int recruitedTotal, int currentFlockCount, int poopUses)
    {
        RecruitedTotal = recruitedTotal;
        CurrentFlockCount = currentFlockCount;
        PoopUses = poopUses;
    }

    public int RecruitedTotal { get; }
    public int CurrentFlockCount { get; }
    public int PoopUses { get; }

    public int Read(MvpTaskMetric metric)
    {
        return metric switch
        {
            MvpTaskMetric.RecruitedTotal => RecruitedTotal,
            MvpTaskMetric.CurrentFlockCount => CurrentFlockCount,
            MvpTaskMetric.PoopUses => PoopUses,
            _ => 0
        };
    }
}

public sealed class MvpTaskCondition
{
    public MvpTaskCondition(MvpTaskMetric metric, int target)
    {
        Metric = metric;
        Target = Math.Max(0, target);
    }

    public MvpTaskMetric Metric { get; }
    public int Target { get; }

    public int Progress(MvpRunMetrics metrics) => Math.Min(metrics.Read(Metric), Target);
    public bool IsMet(MvpRunMetrics metrics) => metrics.Read(Metric) >= Target;
}

public sealed class MvpTaskDefinition
{
    public MvpTaskDefinition(
        string id,
        string title,
        bool isRequired,
        MvpTaskConditionMode conditionMode,
        params MvpTaskCondition[] conditions)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("Task id is required.", nameof(id)) : id;
        Title = string.IsNullOrWhiteSpace(title) ? id : title;
        IsRequired = isRequired;
        ConditionMode = conditionMode;
        Conditions = conditions ?? Array.Empty<MvpTaskCondition>();
    }

    public string Id { get; }
    public string Title { get; }
    public bool IsRequired { get; }
    public MvpTaskConditionMode ConditionMode { get; }
    public IReadOnlyList<MvpTaskCondition> Conditions { get; }
}

public readonly struct MvpObjectiveSnapshot
{
    public MvpObjectiveSnapshot(
        string id,
        string title,
        bool isRequired,
        bool isNew,
        bool isComplete,
        int progress,
        int target,
        bool isCounter = false)
    {
        Id = id;
        Title = title;
        IsRequired = isRequired;
        IsNew = isNew;
        IsComplete = isComplete;
        Progress = progress;
        Target = target;
        IsCounter = isCounter;
    }

    public string Id { get; }
    public string Title { get; }
    public bool IsRequired { get; }
    public bool IsNew { get; }
    public bool IsComplete { get; }
    public int Progress { get; }
    public int Target { get; }
    public bool IsCounter { get; }
}

public sealed class MvpTaskSystem
{
    private sealed class TaskState
    {
        public TaskState(MvpTaskDefinition definition)
        {
            Definition = definition;
            IsNew = true;
        }

        public MvpTaskDefinition Definition { get; }
        public bool IsComplete { get; set; }
        public bool IsNew { get; set; }
    }

    private readonly List<TaskState> states = new();
    private readonly List<MvpObjectiveSnapshot> snapshots = new();
    private bool completionRaised;

    public MvpTaskSystem(IEnumerable<MvpTaskDefinition> definitions)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);

        foreach (MvpTaskDefinition definition in definitions ?? Array.Empty<MvpTaskDefinition>())
        {
            if (definition == null || !ids.Add(definition.Id))
                continue;

            states.Add(new TaskState(definition));
        }
    }

    public event Action<IReadOnlyList<MvpObjectiveSnapshot>> Changed;
    public event Action AllRequiredCompleted;
    public IReadOnlyList<MvpObjectiveSnapshot> Snapshots => snapshots;
    public bool RequiredTasksComplete { get; private set; }

    public void Evaluate(MvpRunMetrics metrics)
    {
        foreach (TaskState state in states)
        {
            if (state.IsComplete || !EvaluateDefinition(state.Definition, metrics))
                continue;

            state.IsComplete = true;
            state.IsNew = false;
        }

        bool requiredComplete = HasRequiredTasks() && states.TrueForAll(
            state => !state.Definition.IsRequired || state.IsComplete);

        if (requiredComplete != RequiredTasksComplete)
            RequiredTasksComplete = requiredComplete;

        // Evaluation is event-driven, so publishing every committed metric
        // snapshot keeps numeric progress current even before a task completes.
        RebuildSnapshots(metrics);
        Changed?.Invoke(snapshots);

        if (requiredComplete && !completionRaised)
        {
            completionRaised = true;
            AllRequiredCompleted?.Invoke();
        }
    }

    public void MarkPresented()
    {
        bool changed = false;
        foreach (TaskState state in states)
        {
            if (!state.IsNew)
                continue;

            state.IsNew = false;
            changed = true;
        }

        if (changed)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                MvpObjectiveSnapshot old = snapshots[i];
                snapshots[i] = new MvpObjectiveSnapshot(
                    old.Id,
                    old.Title,
                    old.IsRequired,
                    false,
                    old.IsComplete,
                    old.Progress,
                    old.Target,
                    old.IsCounter);
            }
        }
    }

    private static bool EvaluateDefinition(MvpTaskDefinition definition, MvpRunMetrics metrics)
    {
        if (definition.Conditions.Count == 0)
            return false;

        if (definition.ConditionMode == MvpTaskConditionMode.Any)
        {
            foreach (MvpTaskCondition condition in definition.Conditions)
                if (condition.IsMet(metrics)) return true;

            return false;
        }

        foreach (MvpTaskCondition condition in definition.Conditions)
            if (!condition.IsMet(metrics)) return false;

        return true;
    }

    private bool HasRequiredTasks()
    {
        foreach (TaskState state in states)
            if (state.Definition.IsRequired) return true;

        return false;
    }

    private void RebuildSnapshots(MvpRunMetrics metrics)
    {
        snapshots.Clear();

        foreach (TaskState state in states)
        {
            int progress = 0;
            int target = 0;

            foreach (MvpTaskCondition condition in state.Definition.Conditions)
            {
                progress += condition.Progress(metrics);
                target += condition.Target;
            }

            snapshots.Add(new MvpObjectiveSnapshot(
                state.Definition.Id,
                state.Definition.Title,
                state.Definition.IsRequired,
                state.IsNew,
                state.IsComplete,
                progress,
                target));
        }
    }
}
