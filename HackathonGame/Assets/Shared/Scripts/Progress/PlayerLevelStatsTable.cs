using System;
using UnityEngine;

/// <summary>
/// レベル 1〜5 それぞれの基礎攻撃力・最大 HP（装備ボーナスは別途加算）。
/// </summary>
[Serializable]
public sealed class PlayerLevelStatsTable
{
    public const int MaxLevel = PlayerLevelExpTable.MaxLevel;

    [Header("Level 1")]
    [SerializeField] [Min(0)] private int level1Attack = 10;
    [SerializeField] [Min(1)] private int level1MaxHp = 100;

    [Header("Level 2")]
    [SerializeField] [Min(0)] private int level2Attack = 14;
    [SerializeField] [Min(1)] private int level2MaxHp = 120;

    [Header("Level 3")]
    [SerializeField] [Min(0)] private int level3Attack = 18;
    [SerializeField] [Min(1)] private int level3MaxHp = 145;

    [Header("Level 4")]
    [SerializeField] [Min(0)] private int level4Attack = 22;
    [SerializeField] [Min(1)] private int level4MaxHp = 175;

    [Header("Level 5")]
    [SerializeField] [Min(0)] private int level5Attack = 26;
    [SerializeField] [Min(1)] private int level5MaxHp = 210;

    public int GetAttackForLevel(int level)
    {
        return level switch
        {
            1 => Mathf.Max(0, level1Attack),
            2 => Mathf.Max(0, level2Attack),
            3 => Mathf.Max(0, level3Attack),
            4 => Mathf.Max(0, level4Attack),
            5 => Mathf.Max(0, level5Attack),
            _ => 0
        };
    }

    public int GetMaxHpForLevel(int level)
    {
        return level switch
        {
            1 => Mathf.Max(1, level1MaxHp),
            2 => Mathf.Max(1, level2MaxHp),
            3 => Mathf.Max(1, level3MaxHp),
            4 => Mathf.Max(1, level4MaxHp),
            5 => Mathf.Max(1, level5MaxHp),
            _ => 1
        };
    }
}
