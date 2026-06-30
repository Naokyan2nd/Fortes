using System;
using UnityEngine;

/// <summary>
/// SuperRare / Rare / Normal の三種報酬の付与量または所持数。
/// </summary>
[Serializable]
public struct BattleRewardCounts
{
    [Min(0)] public int superRare;
    [Min(0)] public int rare;
    [Min(0)] public int normal;

    public BattleRewardCounts(int superRare, int rare, int normal)
    {
        this.superRare = Mathf.Max(0, superRare);
        this.rare = Mathf.Max(0, rare);
        this.normal = Mathf.Max(0, normal);
    }

    public bool IsEmpty => superRare <= 0 && rare <= 0 && normal <= 0;

    public static BattleRewardCounts operator +(BattleRewardCounts a, BattleRewardCounts b)
    {
        return new BattleRewardCounts(
            a.superRare + b.superRare,
            a.rare + b.rare,
            a.normal + b.normal);
    }

    public override string ToString()
    {
        return $"SR={superRare}, R={rare}, N={normal}";
    }
}
