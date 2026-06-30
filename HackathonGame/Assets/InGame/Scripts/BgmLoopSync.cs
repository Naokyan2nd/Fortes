using System;
using UnityEngine;

/// <summary>
/// ループ再生中の BGM の位相・ループ長を扱う。
/// </summary>
public static class BgmLoopSync
{
    private const float PhaseEpsilonSeconds = 0.001f;

    /// <summary>
    /// 再生中 BGM の次ループ先頭（折り返し）の DSP 時刻を返す。
    /// </summary>
    /// <remarks>QTE 即時開始では未使用。将来のビート待ち用に残す。</remarks>
    [Obsolete("QTE は即時スクラッチ戻しを使用。新規コードでは TryRewindPlayingLoopToStart を利用すること。")]
    public static bool TryGetNextLoopStartDsp(AudioSource bgm, out double nextLoopStartDsp)
    {
        nextLoopStartDsp = 0d;
        if (!TryGetLoopLengthSeconds(bgm, out float loopSec))
        {
            return false;
        }

        float phaseSec = bgm.time % loopSec;
        float waitSec = phaseSec < PhaseEpsilonSeconds ? loopSec : loopSec - phaseSec;
        nextLoopStartDsp = AudioSettings.dspTime + waitSec;
        return true;
    }

    /// <summary>ループ BGM の1周秒数（pitch 補正込み）を返す。</summary>
    public static bool TryGetLoopLengthSeconds(AudioSource bgm, out float loopSec)
    {
        loopSec = 0f;
        AudioClip clip = GetLoopClip(bgm);
        if (bgm == null || clip == null)
        {
            return false;
        }

        loopSec = clip.length / Mathf.Max(bgm.pitch, 0.01f);
        return loopSec > 0f;
    }

    /// <summary>
    /// 再生中のループ BGM をループ先頭へシークする（再生は止めない）。
    /// QTE スクラッチ演出では使用しない（time シークは「ぷつっ」切れの原因になる）。
    /// </summary>
    public static bool TrySnapToLoopStart(AudioSource bgm)
    {
        if (bgm == null || !bgm.isPlaying)
        {
            return false;
        }

        if (GetLoopClip(bgm) == null)
        {
            return false;
        }

        bgm.loop = true;
        bgm.time = 0f;
        return true;
    }

    /// <summary>互換用。<see cref="TrySnapToLoopStart"/> を呼ぶ。</summary>
    [Obsolete("TrySnapToLoopStart を使用すること（Play による切り替えは行わない）。")]
    public static bool TryRewindPlayingLoopToStart(AudioSource bgm) => TrySnapToLoopStart(bgm);

    /// <summary>AudioSource からループ長算出用の Clip を取得（Unity 6 resource 対応）。</summary>
    public static AudioClip GetLoopClip(AudioSource source)
    {
        if (source == null)
        {
            return null;
        }

        if (source.clip != null)
        {
            return source.clip;
        }

        return source.resource as AudioClip;
    }
}
