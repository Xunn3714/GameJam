using System;

public sealed class RecruitSheepTask
{
    public int Target { get; }
    public int Progress { get; private set; }

    public bool IsComplete => Progress >= Target;

    public event Action<int, int> ProgressChanged;
    public event Action Completed;

    private bool completed;

    public RecruitSheepTask(int target)
    {
        Target = target;
        Progress = 0;
    }

    public void RecordRecruit()
    {
        if (completed)
            return;

        if (Progress >= Target)
            return;

        Progress++;

        ProgressChanged?.Invoke(Progress, Target);

        if (Progress >= Target)
        {
            completed = true;
            Completed?.Invoke();
        }
    }
}
