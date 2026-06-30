using UnityEngine;

/// <summary>
/// Enemy Animator Controller の Trigger 名。
/// </summary>
public static class EnemyAnimatorTriggers
{
    public const string Attacking = "isAttacking";
    public const string ReceivingDamage = "isReceivingDamage";

    /// <summary>攻撃トリガーを持つ Animator か（PSB 用コントローラ判定）。</summary>
    public static bool HasAttackTrigger(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return false;
        }

        for (int i = 0; i < animator.parameterCount; i++)
        {
            AnimatorControllerParameter parameter = animator.GetParameter(i);
            if (parameter.name == Attacking
                && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                return true;
            }
        }

        return false;
    }
}
