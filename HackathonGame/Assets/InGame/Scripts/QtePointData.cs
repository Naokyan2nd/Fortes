using System;
using UnityEngine;

/// <summary>
/// 1ヒット分の Taiko QTE タイミング（ジングル開始から判定ライン中央到達までの秒数）。
/// </summary>
[Serializable]
public sealed class QtePointData
{
    [SerializeField]
    [Tooltip("ジングル clip 先頭（t=0）から音符が判定ライン中央に到達する秒数（MIDI 秒）。")]
    private float _timingInSeconds;

    /// <summary>
    /// ジングル再生開始から判定ライン中央到達までの秒数。
    /// </summary>
    public float TimingInSeconds => _timingInSeconds;
}
