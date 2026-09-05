using System;
using System.Collections.Generic;

public enum SpecialSheepRunStatus
{
    Available = 0,
    Active = 1,
    Collected = 2,
}

/// <summary>记录一局内每种特殊羊是否可生成、正在等待或已经获得。</summary>
public sealed class SpecialSheepRunState
{
    private readonly Dictionary<string, SpecialSheepRunStatus> states = new(StringComparer.Ordinal);

    public bool IsAvailable(string typeId)
    {
        return !string.IsNullOrWhiteSpace(typeId)
            && (!states.TryGetValue(typeId.Trim(), out SpecialSheepRunStatus state)
                || state == SpecialSheepRunStatus.Available);
    }

    public SpecialSheepRunStatus GetStatus(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return SpecialSheepRunStatus.Available;
        return states.TryGetValue(typeId.Trim(), out SpecialSheepRunStatus state)
            ? state
            : SpecialSheepRunStatus.Available;
    }

    public bool TryActivate(string typeId)
    {
        if (!IsAvailable(typeId))
            return false;

        states[typeId.Trim()] = SpecialSheepRunStatus.Active;
        return true;
    }

    public void MarkCollected(string typeId)
    {
        if (!string.IsNullOrWhiteSpace(typeId))
            states[typeId.Trim()] = SpecialSheepRunStatus.Collected;
    }

    public bool ReleaseIfActive(string typeId)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            return false;

        string normalized = typeId.Trim();
        if (!states.TryGetValue(normalized, out SpecialSheepRunStatus state)
            || state != SpecialSheepRunStatus.Active)
        {
            return false;
        }

        states.Remove(normalized);
        return true;
    }

    public void Reset()
    {
        states.Clear();
    }
}
