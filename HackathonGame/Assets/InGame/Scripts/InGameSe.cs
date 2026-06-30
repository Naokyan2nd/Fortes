using UnityEngine;

/// <summary>
/// インゲーム SE の静的エントリポイント。シーン未配線でも呼び出し可能。
/// </summary>
public static class InGameSe
{
    /// <summary>SE を再生する。</summary>
    public static void Play(string key, float volumeScale = 1f, float? pitchOverride = null)
    {
        SoundManager manager = SoundManager.EnsureInstance();
        if (manager == null)
        {
            return;
        }

        manager.PlaySe(key, volumeScale, pitchOverride);
    }

    /// <summary>Catalog エントリの再生用ピッチを取得する。</summary>
    public static bool TryGetPlaybackPitch(string key, out float pitch)
    {
        SoundManager manager = SoundManager.EnsureInstance();
        if (manager == null)
        {
            pitch = 1f;
            return false;
        }

        return manager.TryGetSePlaybackPitch(key, out pitch);
    }
}
