using UnityEngine;

/// <summary>
/// Shared UI button click SFX for out-of-game nav scenes.
/// Clip / volume are assigned on <see cref="HomeSceneManager"/>; falls back to Resources/OutGameUiButtonClick.
/// </summary>
public static class OutGameUiButtonClickSound
{
    const string ResourcesClipPath = "OutGameUiButtonClick";
#if UNITY_EDITOR
    const string EditorClipPath = "Assets/OutGame/Audio/ボタン.mp3";
#endif

    static AudioClip s_clip;
    static AudioSource s_source;
    static float s_volume = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_clip = null;
        s_source = null;
        s_volume = 1f;
    }

    /// <summary>Called from HomeSceneManager Inspector settings (applies to all out-of-home nav scenes).</summary>
    public static void Configure(AudioClip clip, float volume)
    {
        s_volume = Mathf.Clamp01(volume);
        if (clip != null)
        {
            s_clip = clip;
        }

        EnsureAudio();
    }

    public static void Play(float volumeScale = 1f)
    {
        EnsureAudio();
        if (s_clip == null || s_source == null)
        {
            return;
        }

        float scale = Mathf.Clamp01(s_volume * Mathf.Max(0f, volumeScale));
        if (scale <= 0f)
        {
            return;
        }

        s_source.PlayOneShot(s_clip, scale);
    }

    static void EnsureAudio()
    {
        if (s_source == null)
        {
            var host = new GameObject(nameof(OutGameUiButtonClickSound));
            Object.DontDestroyOnLoad(host);
            s_source = host.AddComponent<AudioSource>();
            s_source.playOnAwake = false;
            s_source.loop = false;
            s_source.spatialBlend = 0f;
        }

        if (s_clip != null)
        {
            return;
        }

        s_clip = Resources.Load<AudioClip>(ResourcesClipPath);

#if UNITY_EDITOR
        if (s_clip == null)
        {
            s_clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(EditorClipPath);
        }
#endif
    }
}
