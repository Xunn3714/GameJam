using System;

/// <summary>正式关卡里狼的攻击方式（独狼三种 + 狼群五种）。</summary>
public enum WolfAttackType
{
    /// <summary>只会直线攻击的狼：不做躲避预判。</summary>
    StraightWolf,
    /// <summary>聪明的狼：按玩家躲避记忆预判。</summary>
    SmartWolf,
    /// <summary>一条长狼单独扫过。</summary>
    LongWolf,
    /// <summary>三条长狼并排、随机顺序依次冲。</summary>
    ParallelSequential,
    /// <summary>三条长狼并排同时冲。</summary>
    ParallelSimultaneous,
    /// <summary>长狼直角连击。</summary>
    PerpendicularChain,
    /// <summary>长狼包夹。</summary>
    LongWolfWithEscorts,
    /// <summary>五角星围猎。</summary>
    Pentagram,
}

/// <summary>攻击选择的三大类。</summary>
public enum WolfAttackCategory
{
    Single,
    Pack,
    /// <summary>本场损失最多羊的那种攻击（不含五角星）。</summary>
    MostLoss,
}

public static class WolfAttackTypes
{
    public static readonly WolfAttackType[] SingleTypes =
    {
        WolfAttackType.StraightWolf, WolfAttackType.SmartWolf, WolfAttackType.LongWolf,
    };

    public static readonly WolfAttackType[] PackTypes =
    {
        WolfAttackType.ParallelSequential, WolfAttackType.ParallelSimultaneous,
        WolfAttackType.PerpendicularChain, WolfAttackType.LongWolfWithEscorts, WolfAttackType.Pentagram,
    };

    public static bool IsPack(WolfAttackType type) => Array.IndexOf(PackTypes, type) >= 0;

    public static bool UsesLongWolf(WolfAttackType type) =>
        type != WolfAttackType.StraightWolf && type != WolfAttackType.SmartWolf;

    public static string DisplayName(WolfAttackType type)
    {
        switch (type)
        {
            case WolfAttackType.StraightWolf: return "直冲的狼";
            case WolfAttackType.SmartWolf: return "聪明的狼";
            case WolfAttackType.LongWolf: return "长狼";
            case WolfAttackType.ParallelSequential: return "长狼并排轮冲";
            case WolfAttackType.ParallelSimultaneous: return "长狼并排齐冲";
            case WolfAttackType.PerpendicularChain: return "长狼直角连击";
            case WolfAttackType.LongWolfWithEscorts: return "长狼包夹";
            case WolfAttackType.Pentagram: return "五角星围猎";
            default: return type.ToString();
        }
    }

    /// <summary>对应的编队类型（独狼普通狼不走编队，返回 Single）。</summary>
    public static WolfFormationType ToFormationType(WolfAttackType type)
    {
        switch (type)
        {
            case WolfAttackType.LongWolf: return WolfFormationType.SingleLong;
            case WolfAttackType.ParallelSequential: return WolfFormationType.ParallelSequential;
            case WolfAttackType.ParallelSimultaneous: return WolfFormationType.ParallelSimultaneous;
            case WolfAttackType.PerpendicularChain: return WolfFormationType.PerpendicularChain;
            case WolfAttackType.LongWolfWithEscorts: return WolfFormationType.LongWolfWithEscorts;
            case WolfAttackType.Pentagram: return WolfFormationType.Pentagram;
            default: return WolfFormationType.Single;
        }
    }
}
