using System.Collections.Generic;
using UnityEngine;

/// <summary>1 ノート分の乗算倍率（サマリー演出用）。</summary>
public readonly struct QtePerNoteMultiplier
{
    public readonly QteJudgment Judgment;
    public readonly float Multiplier;
    public readonly bool ContributesToProduct;

    public QtePerNoteMultiplier(QteJudgment judgment, float multiplier, bool contributesToProduct)
    {
        Judgment = judgment;
        Multiplier = multiplier;
        ContributesToProduct = contributesToProduct;
    }
}

/// <summary>
/// QTE 判定からダメージ・回復量を算出する。
/// </summary>
public static class QteOutcomeCalculator
{
    /// <summary>全ノートが Perfect なら true（ノート0は false）。</summary>
    public static bool IsAllPerfect(IReadOnlyList<QteJudgment> judgments)
    {
        if (judgments == null || judgments.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < judgments.Count; i++)
        {
            if (judgments[i] != QteJudgment.Perfect)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 各ノート倍率の積。Miss は Perfect を打ち消す（1 Miss につき 1 Perfect 無効）ほか、
    /// ノートごとに QteMissMultiplier を乗算。Good は打ち消しに使わない。
    /// </summary>
    public static float ComputeProductMultiplier(
        IReadOnlyList<QteJudgment> judgments,
        BattleSettingsSO settings)
    {
        return ComputeProductMultiplier(judgments, settings, null);
    }

    /// <summary>
    /// 乗算倍率。isResolved が null のときは全インデックス確定済みとして扱う。
    /// </summary>
    public static float ComputeProductMultiplier(
        IReadOnlyList<QteJudgment> judgments,
        BattleSettingsSO settings,
        IReadOnlyList<bool> isResolved)
    {
        if (judgments == null || judgments.Count == 0 || settings == null)
        {
            return 0f;
        }

        int perfectCount = 0;
        int missCount = 0;
        for (int i = 0; i < judgments.Count; i++)
        {
            if (isResolved != null && (i >= isResolved.Count || !isResolved[i]))
            {
                continue;
            }

            if (judgments[i] == QteJudgment.Perfect)
            {
                perfectCount++;
            }
            else if (judgments[i] == QteJudgment.Miss)
            {
                missCount++;
            }
        }

        int perfectBudget = Mathf.Max(0, perfectCount - missCount);

        float product = 1f;
        int perfectLeft = perfectBudget;
        float perfectMult = settings.QtePerfectMultiplier;
        float goodMult = settings.QteGoodMultiplier;
        float missMult = settings.QteMissMultiplier;

        for (int i = 0; i < judgments.Count; i++)
        {
            if (isResolved != null && (i >= isResolved.Count || !isResolved[i]))
            {
                continue;
            }

            switch (judgments[i])
            {
                case QteJudgment.Good:
                    product *= goodMult;
                    break;
                case QteJudgment.Miss:
                    product *= missMult;
                    break;
                case QteJudgment.Perfect:
                    if (perfectLeft > 0)
                    {
                        product *= perfectMult;
                        perfectLeft--;
                    }

                    break;
            }
        }

        return product;
    }

    /// <summary>攻撃ダメージ（基礎値はレベル＋装備の攻撃力、QTE 倍率のみ乗算）。</summary>
    public static int ComputeAttackDamage(
        int baseAttack,
        IReadOnlyList<QteJudgment> judgments,
        BattleSettingsSO settings)
    {
        float product = ComputeProductMultiplier(judgments, settings);
        return Mathf.FloorToInt(Mathf.Max(0, baseAttack) * product);
    }

    /// <summary>回復量（全 Perfect 時は追加倍率）。</summary>
    public static int ComputeHealAmount(
        SkillDataSO skill,
        IReadOnlyList<QteJudgment> judgments,
        BattleSettingsSO settings)
    {
        if (skill == null)
        {
            return 0;
        }

        float product = ComputeProductMultiplier(judgments, settings);
        if (IsAllPerfect(judgments) && settings != null)
        {
            product *= settings.AllPerfectHealBonusMultiplier;
        }

        return Mathf.FloorToInt(skill.HealPower * product);
    }

    /// <summary>判定順の各ノート倍率（計算ロジックと同一）。</summary>
    public static List<QtePerNoteMultiplier> BuildPerNoteMultipliers(
        IReadOnlyList<QteJudgment> judgments,
        BattleSettingsSO settings)
    {
        return BuildPerNoteMultipliers(judgments, settings, null);
    }

    /// <summary>判定順の各ノート倍率。isResolved が null のときは全インデックス確定済み。</summary>
    public static List<QtePerNoteMultiplier> BuildPerNoteMultipliers(
        IReadOnlyList<QteJudgment> judgments,
        BattleSettingsSO settings,
        IReadOnlyList<bool> isResolved)
    {
        List<QtePerNoteMultiplier> steps = new List<QtePerNoteMultiplier>();
        if (judgments == null || judgments.Count == 0 || settings == null)
        {
            return steps;
        }

        for (int i = 0; i < judgments.Count; i++)
        {
            steps.Add(new QtePerNoteMultiplier(QteJudgment.Miss, 0f, false));
        }

        int perfectCount = 0;
        int missCount = 0;
        for (int i = 0; i < judgments.Count; i++)
        {
            if (isResolved != null && (i >= isResolved.Count || !isResolved[i]))
            {
                continue;
            }

            if (judgments[i] == QteJudgment.Perfect)
            {
                perfectCount++;
            }
            else if (judgments[i] == QteJudgment.Miss)
            {
                missCount++;
            }
        }

        int perfectBudget = Mathf.Max(0, perfectCount - missCount);
        int perfectLeft = perfectBudget;
        float perfectMult = settings.QtePerfectMultiplier;
        float goodMult = settings.QteGoodMultiplier;
        float missMult = settings.QteMissMultiplier;
        bool missContributes = missMult > 0.0001f;

        for (int i = 0; i < judgments.Count; i++)
        {
            if (isResolved != null && (i >= isResolved.Count || !isResolved[i]))
            {
                continue;
            }

            switch (judgments[i])
            {
                case QteJudgment.Good:
                    steps[i] = new QtePerNoteMultiplier(QteJudgment.Good, goodMult, true);
                    break;
                case QteJudgment.Miss:
                    steps[i] = new QtePerNoteMultiplier(QteJudgment.Miss, missMult, missContributes);
                    break;
                case QteJudgment.Perfect:
                    if (perfectLeft > 0)
                    {
                        steps[i] = new QtePerNoteMultiplier(QteJudgment.Perfect, perfectMult, true);
                        perfectLeft--;
                    }
                    else
                    {
                        steps[i] = new QtePerNoteMultiplier(QteJudgment.Perfect, perfectMult, false);
                    }

                    break;
            }
        }

        return steps;
    }

    /// <summary>サマリー演出用: Miss により打ち消される Perfect 枠数。</summary>
    public static int CountCanceledPerfects(IReadOnlyList<QteJudgment> judgments)
    {
        if (judgments == null || judgments.Count == 0)
        {
            return 0;
        }

        int perfectCount = 0;
        int missCount = 0;
        for (int i = 0; i < judgments.Count; i++)
        {
            if (judgments[i] == QteJudgment.Perfect)
            {
                perfectCount++;
            }
            else if (judgments[i] == QteJudgment.Miss)
            {
                missCount++;
            }
        }

        return Mathf.Min(missCount, perfectCount);
    }

    /// <summary>確定倍率の表示用文字列（ライブ倍率 UI・CombatFloatingText 共通）。</summary>
    public static string FormatProductMultiplierDisplay(float value)
    {
        if (value <= 0.0001f)
        {
            return "×0";
        }

        return $"×{value:0.##}";
    }
}
