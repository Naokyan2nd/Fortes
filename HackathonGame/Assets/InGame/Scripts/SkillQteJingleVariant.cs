using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 1曲分の QTE ジングル設定（合成クリップ・スクロール・タイムライン補正・ノートタイミング）。
/// </summary>
[Serializable]
public sealed class SkillQteJingleVariant
{
    [SerializeField]
    [Tooltip("BGM+ジングル合成済みの QTE 用クリップ。")]
    private AudioClip _qteCombinedClip;

    [SerializeField]
    [Tooltip("全音符共通。画面外出現から判定ライン中央到達までの秒数。")]
    private float _noteScrollDurationSeconds = 1f;

    [SerializeField]
    [Tooltip("MIDI/HTML の t=0 と再生 clip 波形先頭の差（秒）。正の値でノートを遅らせる（clip の音が早い場合）。")]
    private float _jingleTimelineOffsetSeconds;

    [SerializeField]
    private QtePointData[] _qteTimings;

    [SerializeField, HideInInspector, FormerlySerializedAs("_jingleClip")]
    private AudioClip _legacyVariantJingleClip;

    /// <summary>BGM+ジングル合成済みの QTE 用クリップ。</summary>
    public AudioClip QteCombinedClip => _qteCombinedClip;

    /// <summary>全音符共通のスクロール時間（画面外 → 判定ライン中央）。</summary>
    public float NoteScrollDurationSeconds => _noteScrollDurationSeconds;

    /// <summary>再生 clip に対する QTE タイムライン補正（秒）。</summary>
    public float JingleTimelineOffsetSeconds => _jingleTimelineOffsetSeconds;

    /// <summary>QTE ポイント配列。</summary>
    public QtePointData[] QteTimings => _qteTimings;

    /// <summary>QTE 再生用クリップ。</summary>
    public AudioClip GetQtePlaybackClip() => _qteCombinedClip;

    /// <summary>このバリアントが QTE に使用可能か。</summary>
    public bool IsValid =>
        _qteCombinedClip != null && _qteTimings != null && _qteTimings.Length > 0;
}
