using UnityEngine;

/// <summary>
/// レベル基礎値＋装備などのボーナスを合算した戦闘用ステータス。
/// </summary>
public readonly struct PlayerCombatStats
{
    public int Attack { get; }
    public int MaxHp { get; }

    public PlayerCombatStats(int attack, int maxHp)
    {
        Attack = Mathf.Max(0, attack);
        MaxHp = Mathf.Max(1, maxHp);
    }

    public static PlayerCombatStats Combine(
        int baseAttack,
        int baseMaxHp,
        int bonusAttack = 0,
        int bonusMaxHp = 0)
    {
        return new PlayerCombatStats(baseAttack + bonusAttack, baseMaxHp + bonusMaxHp);
    }
}
