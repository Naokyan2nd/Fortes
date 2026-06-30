using System;
using UnityEngine;

/// <summary>
/// レベル 1〜5 それぞれの「このレベルで貯める最大経験値」（満タンで次レベルへ）。
/// </summary>
[Serializable]
public sealed class PlayerLevelExpTable
{
    public const int MaxLevel = 5;

    [Header("Level 1 — max exp to reach Lv 2")]
    [SerializeField] [Min(0)] private int level1MaxExp = 100;

    [Header("Level 2 — max exp to reach Lv 3")]
    [SerializeField] [Min(0)] private int level2MaxExp = 200;

    [Header("Level 3 — max exp to reach Lv 4")]
    [SerializeField] [Min(0)] private int level3MaxExp = 350;

    [Header("Level 4 — max exp to reach Lv 5")]
    [SerializeField] [Min(0)] private int level4MaxExp = 500;

    [Header("Level 5 — max level (display cap, 0 = no cap)")]
    [SerializeField] [Min(0)] private int level5MaxExp;

    public int GetExpRequiredForLevel(int level)
    {
        return level switch
        {
            1 => Mathf.Max(0, level1MaxExp),
            2 => Mathf.Max(0, level2MaxExp),
            3 => Mathf.Max(0, level3MaxExp),
            4 => Mathf.Max(0, level4MaxExp),
            5 => Mathf.Max(0, level5MaxExp),
            _ => 0
        };
    }

    public void SetExpRequiredForLevel(int level, int maxExp)
    {
        maxExp = Mathf.Max(0, maxExp);
        switch (level)
        {
            case 1: level1MaxExp = maxExp; break;
            case 2: level2MaxExp = maxExp; break;
            case 3: level3MaxExp = maxExp; break;
            case 4: level4MaxExp = maxExp; break;
            case 5: level5MaxExp = maxExp; break;
        }
    }

    public int[] ToArray()
    {
        return new[]
        {
            GetExpRequiredForLevel(1),
            GetExpRequiredForLevel(2),
            GetExpRequiredForLevel(3),
            GetExpRequiredForLevel(4),
            GetExpRequiredForLevel(5)
        };
    }
}
