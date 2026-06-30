using UnityEngine;

/// <summary>
/// SE Catalog の1エントリ（ラベル id + AudioClip + 音量・ピッチ）。
/// </summary>
[System.Serializable]
public struct InGameSeEntry
{
    [SerializeField]
    private string _id;

    [SerializeField]
    private AudioClip _clip;

    [SerializeField]
    [Range(0f, 2f)]
    private float _volume;

    [SerializeField]
    [Range(0.01f, 3f)]
    private float _pitch;

    public string Id => _id;

    public AudioClip Clip => _clip;

    public float Volume => _volume;

    public float Pitch => _pitch;

    /// <summary>旧 Catalog（音量・ピッチ未シリアライズ）か。</summary>
    public bool IsLegacyUnset => _volume <= 0f && _pitch <= 0f;

    /// <summary>再生用音量（未設定時は 1、0 は無音）。</summary>
    public float PlaybackVolume => IsLegacyUnset ? 1f : Mathf.Max(0f, _volume);

    /// <summary>再生用ピッチ（未設定時は 1）。</summary>
    public float PlaybackPitch
    {
        get
        {
            if (IsLegacyUnset)
            {
                return 1f;
            }

            float pitch = _pitch > 0f ? _pitch : 1f;
            return Mathf.Clamp(pitch, 0.01f, 3f);
        }
    }

    public InGameSeEntry(string id, AudioClip clip)
    {
        _id = id;
        _clip = clip;
        _volume = 1f;
        _pitch = 1f;
    }

    public InGameSeEntry(string id, AudioClip clip, float volume, float pitch)
    {
        _id = id;
        _clip = clip;
        _volume = volume;
        _pitch = pitch;
    }
}
