/// <summary>
/// WhiteCharacter / BlackCharacter Animator Controller の Trigger 名。
/// isRecovering は将来 AP 専用演出用（現状未使用）。
/// </summary>
public static class PlayerAnimatorTriggers
{
    public const string Playing = "isPlaying";
    public const string Attacking = "isAttacking";
    public const string ReceivingDamage = "isReceivingDamage";
    public const string Healing = "isHealing";
    public const string Recovering = "isRecovering";
    public const string Lost = "isLost";
}
