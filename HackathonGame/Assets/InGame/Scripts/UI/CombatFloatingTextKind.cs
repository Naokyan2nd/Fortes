/// <summary>
/// 戦闘中 FloatingText の表示種別（呼び出し元の識別用）。
/// </summary>
public enum CombatFloatingTextKind
{
    DamageToPlayer = 0,
    DamageToEnemy,
    HealHp,
    /// <summary>QTE 確定倍率（プレイヤー上の ×表示）。</summary>
    QteMultiplier,
}
