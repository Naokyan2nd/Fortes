using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Taiko QTE 用タイミング・判定帯検証。
/// </summary>
public static class QteTaikoTimingValidator
{
    private const float MinPerfectTimingSeconds = 0.05f;
    private const float MinScrollDurationSeconds = 0.1f;

    /// <summary>
    /// 音符数・中心到達シーケンスと判定帯を検証する。
    /// </summary>
    public static bool TryValidate(
        IReadOnlyList<QtePointData> points,
        float noteScrollDurationSeconds,
        QteTaikoJudgmentZones zones,
        float judgmentLineX,
        out string errorMessage)
    {
        errorMessage = null;
        if (points == null || points.Count == 0)
        {
            errorMessage = "[QteTaiko] QteTimings が空です。";
            return false;
        }

        if (points.Count < QteTaikoSettingsSO.SimultaneousNoteMin
            || points.Count > QteTaikoSettingsSO.SimultaneousNoteMax)
        {
            errorMessage =
                $"[QteTaiko] 音符数 {points.Count} が範囲外です（" +
                $"{QteTaikoSettingsSO.SimultaneousNoteMin}〜{QteTaikoSettingsSO.SimultaneousNoteMax}）。";
            return false;
        }

        if (noteScrollDurationSeconds < MinScrollDurationSeconds - 1e-5f)
        {
            errorMessage =
                $"[QteTaiko] NoteScrollDurationSeconds={noteScrollDurationSeconds:F3}s は " +
                $"{MinScrollDurationSeconds:F3}s 未満です。";
            return false;
        }

        if (zones == null || !zones.TryValidateLayout(judgmentLineX, out errorMessage))
        {
            if (string.IsNullOrEmpty(errorMessage))
            {
                errorMessage = "[QteTaiko] 判定帯が未設定です。";
            }

            return false;
        }

        for (int i = 0; i < points.Count; i++)
        {
            QtePointData point = points[i];
            if (point == null)
            {
                errorMessage = $"[QteTaiko] QteTimings[{i}] が null です。";
                return false;
            }

            float perfectSec = point.TimingInSeconds;

            if (perfectSec < MinPerfectTimingSeconds)
            {
                errorMessage =
                    $"[QteTaiko] note[{i}] TimingInSeconds={perfectSec:F3}s は " +
                    $"{MinPerfectTimingSeconds:F3}s 未満です。";
                return false;
            }

            if (perfectSec < noteScrollDurationSeconds - 1e-5f)
            {
                errorMessage =
                    $"[QteTaiko] note[{i}] TimingInSeconds ({perfectSec:F3}s) は " +
                    $"NoteScrollDurationSeconds ({noteScrollDurationSeconds:F3}s) 以上である必要があります。";
                return false;
            }
        }

        return true;
    }
}
