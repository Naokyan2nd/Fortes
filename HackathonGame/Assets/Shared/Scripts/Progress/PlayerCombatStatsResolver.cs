using System;
using UnityEngine;

/// <summary>
/// レベル基礎ステータスと装備ボーナスを合算する。
/// </summary>
public static class PlayerCombatStatsResolver
{
    static bool s_useDebugOverride;
    static int s_debugAttack;
    static int s_debugMaxHp;

    public static event Action OnResolvedStatsChanged;

    public static void SetDebugOverride(bool enabled, int attack, int maxHp)
    {
        s_useDebugOverride = enabled;
        s_debugAttack = Mathf.Max(0, attack);
        s_debugMaxHp = Mathf.Max(1, maxHp);
        OnResolvedStatsChanged?.Invoke();
    }

    public static bool IsUsingDebugOverride => s_useDebugOverride;

    public static PlayerCombatStats ResolveCurrent()
    {
        if (s_useDebugOverride)
        {
            return new PlayerCombatStats(s_debugAttack, s_debugMaxHp);
        }

        int bonusAttack = 0;
        int bonusMaxHp = 0;
        OutfitLoadoutManager loadout = OutfitLoadoutManager.Instance;
        if (loadout != null)
        {
            bonusAttack += RoundItemStat(loadout.GetSelected(ItemType.Weapon), item => item.attackPower);
            bonusMaxHp += RoundItemStat(loadout.GetSelected(ItemType.Top), item => item.defense);
            bonusMaxHp += RoundItemStat(loadout.GetSelected(ItemType.Bottom), item => item.defense);
        }

        PlayerLevelManager levelManager = PlayerLevelManager.Instance;
        if (levelManager != null)
        {
            return levelManager.GetCombatStats(bonusAttack, bonusMaxHp);
        }

        return new PlayerCombatStats(bonusAttack, Mathf.Max(1, bonusMaxHp));
    }

    public static void ResolveEquipmentBonuses(out int bonusAttack, out int bonusMaxHp)
    {
        bonusAttack = 0;
        bonusMaxHp = 0;
        OutfitLoadoutManager loadout = OutfitLoadoutManager.Instance;
        if (loadout == null)
        {
            return;
        }

        bonusAttack += RoundItemStat(loadout.GetSelected(ItemType.Weapon), item => item.attackPower);
        bonusMaxHp += RoundItemStat(loadout.GetSelected(ItemType.Top), item => item.defense);
        bonusMaxHp += RoundItemStat(loadout.GetSelected(ItemType.Bottom), item => item.defense);
    }

    static int RoundItemStat(ItemData item, Func<ItemData, float> selector)
    {
        if (item == null || selector == null)
        {
            return 0;
        }

        return Mathf.Max(0, Mathf.RoundToInt(selector(item)));
    }
}
