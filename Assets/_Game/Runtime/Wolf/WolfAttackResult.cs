using System.Collections.Generic;

/// <summary>
/// 一次狼冲锋的结算结果，供 HUD / 调试使用。
/// </summary>
public readonly struct WolfAttackResult
{
    public WolfAttackResult(bool centerHit, IReadOnlyList<SheepMember> knockedSheep, SheepMember capturedSheep)
    {
        CenterHit = centerHit;
        KnockedSheep = knockedSheep;
        CapturedSheep = capturedSheep;
    }

    /// <summary>是否正面撞进了羊群中心。</summary>
    public bool CenterHit { get; }

    /// <summary>被撞散落到地上的羊（不含被叼走的那只）。</summary>
    public IReadOnlyList<SheepMember> KnockedSheep { get; }

    /// <summary>被狼叼走的羊；未叼走时为 null。</summary>
    public SheepMember CapturedSheep { get; }
}
