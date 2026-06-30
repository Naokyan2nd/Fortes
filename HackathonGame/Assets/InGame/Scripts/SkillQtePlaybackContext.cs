using UnityEngine;

/// <summary>
/// QTE 開始時に抽選されたジングルバリアントとスキル共通設定の読み取り専用コンテキスト。
/// </summary>
public sealed class SkillQtePlaybackContext
{
    public SkillQtePlaybackContext(SkillDataSO skill, SkillQteJingleVariant variant)
    {
        Skill = skill;
        Variant = variant;
    }

    /// <summary>元スキル（Category / SkillId 等）。</summary>
    public SkillDataSO Skill { get; }

    /// <summary>抽選されたジングルバリアント。</summary>
    public SkillQteJingleVariant Variant { get; }

    /// <summary>スキルID。</summary>
    public string SkillId => Skill != null ? Skill.SkillId : "?";

    /// <summary>スキルカテゴリ。</summary>
    public SkillCategory Category => Skill != null ? Skill.Category : SkillCategory.Attack;

    /// <summary>全音符共通のスクロール時間（画面外 → 判定ライン中央）。</summary>
    public float NoteScrollDurationSeconds =>
        Variant != null ? Variant.NoteScrollDurationSeconds : 1f;

    /// <summary>QTE ポイント配列。</summary>
    public QtePointData[] QteTimings => Variant?.QteTimings;

    /// <summary>再生 clip に対する QTE タイムライン補正（秒）。</summary>
    public float JingleTimelineOffsetSeconds =>
        Variant != null ? Variant.JingleTimelineOffsetSeconds : 0f;

    /// <summary>QTE 再生用クリップ。</summary>
    public AudioClip GetQtePlaybackClip() => Variant?.GetQtePlaybackClip();
}
