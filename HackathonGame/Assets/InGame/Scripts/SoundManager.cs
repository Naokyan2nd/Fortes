using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// インゲーム音声（BGM 2ch + SE 5ch）を管理するシングルトン。
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class SoundManager : MonoBehaviour
{
    private const string DefaultCatalogResourceName = "DefaultInGameSeCatalog";
    private const int BgmChannelCount = 2;
    private const int SeChannelCount = 5;

    private static SoundManager _instance;

    [SerializeField]
    private InGameSeCatalogSO _catalog;

    [SerializeField]
    private AudioClip _initialBgmClip;

    [SerializeField]
    private AudioResource _initialBgmResource;

    [SerializeField]
    [Range(0f, 1f)]
    private float _bgmVolume = 1f;

    [SerializeField]
    private bool _playBgmOnAwake = true;

    [SerializeField]
    [Range(0f, 1f)]
    private float _seVolume = 1f;

    [SerializeField]
    private bool _muteAll;

    [SerializeField]
    private float _bgmCrossfadeSeconds = 0.4f;

    private AudioSource[] _bgmSources;
    private AudioSource[] _seSources;
    private int _activeBgmIndex;
    private int _seRoundRobinIndex;

    private readonly HashSet<string> _warnedMissingKeys = new HashSet<string>();
    private bool _warnedMissingCatalog;
    private bool _warnedDisabled;

    public static SoundManager Instance => _instance;

    /// <summary>現在のループ BGM / QTE 用に操作する BGM チャンネル。</summary>
    public AudioSource ActiveBgmSource
    {
        get
        {
            EnsureAudioSources();
            return _bgmSources != null && _bgmSources.Length > 0 ? _bgmSources[_activeBgmIndex] : null;
        }
    }

    /// <summary>
    /// 実際に聞こえている BGM チャンネルを返す（2ch クロスフェード中の音量で判定）。
    /// </summary>
    public AudioSource GetAudibleBgmSource(float minVolume = 0.05f)
    {
        EnsureAudioSources();
        if (_bgmSources == null || _bgmSources.Length == 0)
        {
            return null;
        }

        AudioSource best = null;
        float bestVolume = minVolume;
        for (int i = 0; i < _bgmSources.Length; i++)
        {
            AudioSource candidate = _bgmSources[i];
            if (candidate == null || !candidate.isPlaying)
            {
                continue;
            }

            if (candidate.volume >= bestVolume)
            {
                bestVolume = candidate.volume;
                best = candidate;
            }
        }

        return best != null ? best : ActiveBgmSource;
    }

    /// <summary>以降の BGM 操作を指定 ch に向ける（QTE 開始時など）。</summary>
    public void FocusBgmChannel(AudioSource source)
    {
        if (source == null || _bgmSources == null)
        {
            return;
        }

        for (int i = 0; i < _bgmSources.Length; i++)
        {
            if (_bgmSources[i] == source)
            {
                _activeBgmIndex = i;
                return;
            }
        }
    }

    public static SoundManager EnsureInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        SoundManager existing = Object.FindFirstObjectByType<SoundManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        GameObject go = new GameObject("SoundManager");
        SoundManager manager = go.AddComponent<SoundManager>();
        manager.InitializeRuntime(null);
        return manager;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSources();
        EnsureCatalog();

        if (_playBgmOnAwake && HasInitialBgm() && !ShouldDeferInitialBgmForTutorialOpeningScene())
        {
            PlayBgmImmediate(_initialBgmClip, _initialBgmResource, loop: true);
        }
    }

    /// <summary>チュートリアルオープニング待ちなどで BGM を止める。</summary>
    public void StopInitialBgmImmediate()
    {
        EnsureAudioSources();
        StopAllBgmChannels();
    }

    private static bool ShouldDeferInitialBgmForTutorialOpeningScene()
    {
        return SceneManager.GetActiveScene().name == SceneNames.InGameTutorial;
    }

    /// <summary>ランタイム生成時に Catalog を注入する。</summary>
    public void InitializeRuntime(InGameSeCatalogSO catalog)
    {
        if (_catalog == null)
        {
            _catalog = catalog;
        }

        EnsureAudioSources();
        EnsureCatalog();
    }

    /// <summary>SE を再生する。未設定時は初回のみ警告。</summary>
    public void PlaySe(string key, float volumeScale = 1f, float? pitchOverride = null)
    {
        if (_muteAll)
        {
            WarnDisabledOnce();
            return;
        }

        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        EnsureAudioSources();
        EnsureCatalog();

        InGameSeEntry entry = default;
        if (_catalog == null)
        {
            WarnMissingCatalogOnce();
        }
        else if (!_catalog.TryGetEntry(key, out entry))
        {
            WarnMissingClipOnce(key);
            return;
        }

        if (entry.Clip == null)
        {
            if (_catalog != null)
            {
                WarnMissingClipOnce(key);
            }

            return;
        }

        AudioSource source = PickSeSource();
        if (source == null)
        {
            return;
        }

        float scale = Mathf.Max(0f, volumeScale);
        source.clip = entry.Clip;
        source.volume = _seVolume * entry.PlaybackVolume * scale;
        float pitch = pitchOverride ?? entry.PlaybackPitch;
        source.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
        source.Play();
    }

    /// <summary>Catalog エントリの再生用ピッチを取得する。未設定時は false。</summary>
    public bool TryGetSePlaybackPitch(string key, out float pitch)
    {
        pitch = 1f;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        EnsureCatalog();
        if (_catalog == null || !_catalog.TryGetEntry(key, out InGameSeEntry entry) || entry.Clip == null)
        {
            return false;
        }

        pitch = entry.PlaybackPitch;
        return true;
    }

    /// <summary>BGM をクロスフェードで切り替える。</summary>
    public void PlayBgm(AudioClip clip, bool loop = true, float fadeSeconds = -1f)
    {
        PlayBgm(clip, null, loop, fadeSeconds);
    }

    /// <summary>BGM をクロスフェードで切り替える（Unity 6 AudioResource 対応）。</summary>
    public void PlayBgm(AudioClip clip, AudioResource resource, bool loop = true, float fadeSeconds = -1f)
    {
        if (clip == null && resource == null)
        {
            return;
        }

        EnsureAudioSources();

        AudioSource active = ActiveBgmSource;
        if (active != null && active.isPlaying && BgmMatches(active, clip, resource) && active.loop == loop)
        {
            return;
        }

        float fade = fadeSeconds >= 0f ? fadeSeconds : _bgmCrossfadeSeconds;
        if (active == null || !active.isPlaying || fade <= 0f)
        {
            PlayBgmImmediate(clip, resource, loop);
            return;
        }

        CrossfadeBgmAsync(clip, resource, loop, fade, preserveFromVolume: false, destroyCancellationToken).Forget();
    }

    /// <summary>アクティブ BGM チャンネルの音量をフェードする。</summary>
    public async UniTask FadeBgmVolumeAsync(float endVolume, float seconds, CancellationToken token)
    {
        AudioSource active = ActiveBgmSource;
        if (active == null)
        {
            return;
        }

        Tweener fadeTween = active.DOFade(endVolume, seconds);
        await WaitTweenUniTaskAsync(fadeTween, token);
    }

    /// <summary>アクティブ BGM をフェードアウトして全 BGM チャンネルを停止する（リザルト遷移用）。</summary>
    public async UniTask FadeOutAndStopActiveBgmAsync(float seconds, CancellationToken token)
    {
        EnsureAudioSources();

        AudioSource active = ActiveBgmSource;
        if (active != null && active.isPlaying && seconds > 0f)
        {
            Tweener fadeTween = active.DOFade(0f, seconds).SetUpdate(true);
            await WaitTweenUniTaskAsync(fadeTween, token);
        }

        StopAllBgmChannels();
    }

    /// <summary>
    /// 初期 BGM が未再生なら開始する（DontDestroyOnLoad 再入場時の復帰用）。
    /// </summary>
    public void EnsureInitialBgmPlaying(bool fadeIn = true)
    {
        if (!HasInitialBgm())
        {
            return;
        }

        EnsureAudioSources();

        AudioSource active = ActiveBgmSource;
        if (active != null
            && active.isPlaying
            && BgmMatches(active, _initialBgmClip, _initialBgmResource))
        {
            if (active.volume < _bgmVolume * 0.99f)
            {
                active.volume = _bgmVolume;
            }

            if (Mathf.Abs(active.pitch - 1f) > 0.001f)
            {
                active.pitch = 1f;
            }

            return;
        }

        if (!fadeIn)
        {
            PlayBgmImmediate(_initialBgmClip, _initialBgmResource, loop: true);
            return;
        }

        PlayBgm(_initialBgmClip, _initialBgmResource, loop: true, fadeSeconds: _bgmCrossfadeSeconds);
    }

    private const string BgmScratchTweenId = "BgmScratchPitch";

    /// <summary>
    /// pitch の下げ・往復（スクラッチ風）のみ。ループ先頭への time シークは行わない。
    /// </summary>
    public async UniTask PlayBgmLoopScratchAsync(
        AudioSource source,
        float pitchMin,
        float pitchPeak,
        float scratchSeconds,
        CancellationToken token)
    {
        if (source == null || scratchSeconds <= 0f)
        {
#if UNITY_EDITOR
            Debug.Log("[SoundManager] BGM scratch skipped: source null or scratchSeconds <= 0.");
#endif
            return;
        }

        if (!source.isPlaying)
        {
#if UNITY_EDITOR
            Debug.Log("[SoundManager] BGM scratch skipped: source is not playing.");
#endif
            return;
        }

        if (BgmLoopSync.GetLoopClip(source) == null)
        {
#if UNITY_EDITOR
            Debug.Log("[SoundManager] BGM scratch skipped: loop clip not resolved on source.");
#endif
            return;
        }

        DOTween.Kill(source, complete: false, id: BgmScratchTweenId);

        float low = Mathf.Clamp(pitchMin, 0.01f, 3f);
        float peak = Mathf.Clamp(pitchPeak, low, 3f);
        float phaseDown = scratchSeconds * 0.25f;
        float wiggleStep = scratchSeconds * 0.125f;
        float phaseUp = scratchSeconds * 0.25f;

        Sequence scratchSeq = DOTween.Sequence()
            .SetId(BgmScratchTweenId)
            .SetUpdate(UpdateType.Normal, isIndependentUpdate: true);
        scratchSeq.Append(source.DOPitch(low, phaseDown).SetEase(Ease.InQuad));
        scratchSeq.Append(source.DOPitch(peak, wiggleStep).SetEase(Ease.OutQuad));
        scratchSeq.Append(source.DOPitch(low, wiggleStep).SetEase(Ease.InQuad));
        scratchSeq.Append(source.DOPitch(peak, wiggleStep).SetEase(Ease.OutQuad));
        scratchSeq.Append(source.DOPitch(low, wiggleStep).SetEase(Ease.InQuad));
        scratchSeq.Append(source.DOPitch(1f, phaseUp).SetEase(Ease.OutCubic));

        if (!scratchSeq.IsActive())
        {
#if UNITY_EDITOR
            Debug.Log("[SoundManager] BGM scratch skipped: DOTween sequence is not active.");
#endif
            return;
        }

        try
        {
            await WaitTweenUniTaskAsync(scratchSeq, token);
        }
        finally
        {
            if (source != null)
            {
                source.pitch = 1f;
            }
        }
    }

    /// <summary>
    /// QTE 終了後に保存済みループ BGM へクロスフェードで復帰する。
    /// アクティブ ch の現在音量（QTE 中フェード後など）を維持する。
    /// </summary>
    public async UniTask RestoreLoopBgmAfterQteAsync(
        AudioClip clip,
        AudioResource resource,
        bool wasPlaying,
        CancellationToken token,
        float fadeSeconds = -1f)
    {
        if (!wasPlaying || (clip == null && resource == null))
        {
            ActiveBgmSource?.Stop();
            return;
        }

        EnsureAudioSources();
        AudioSource from = _bgmSources[_activeBgmIndex];
        if (from != null && from.isPlaying && BgmMatches(from, clip, resource) && from.loop)
        {
            return;
        }

        float fade = fadeSeconds >= 0f ? fadeSeconds : _bgmCrossfadeSeconds;
        if (fade <= 0f)
        {
            RestoreLoopBgmAfterQteImmediate(clip, resource, wasPlaying: true);
            return;
        }

        await CrossfadeBgmAsync(clip, resource, loop: true, fade, preserveFromVolume: true, token);
    }

    /// <summary>QTE 中断時など、即時にループ BGM をアクティブ ch で再開する。</summary>
    public void RestoreLoopBgmAfterQteImmediate(AudioClip clip, AudioResource resource, bool wasPlaying)
    {
        if (!wasPlaying || (clip == null && resource == null))
        {
            ActiveBgmSource?.Stop();
            return;
        }

        EnsureAudioSources();
        AudioSource active = ActiveBgmSource;
        if (active == null)
        {
            return;
        }

        float preserveVolume = active.volume;
        for (int i = 0; i < _bgmSources.Length; i++)
        {
            if (i != _activeBgmIndex)
            {
                _bgmSources[i].Stop();
            }
        }

        ApplyBgmToSource(active, clip, resource, loop: true);
        active.volume = preserveVolume;
        active.Play();
    }

    private static bool BgmMatches(AudioSource source, AudioClip clip, AudioResource resource)
    {
        if (resource != null && source.resource == resource)
        {
            return true;
        }

        AudioClip activeClip = BgmLoopSync.GetLoopClip(source);
        return clip != null && activeClip == clip;
    }

    private bool HasInitialBgm()
    {
        return _initialBgmClip != null || _initialBgmResource != null;
    }

    private void PlayBgmImmediate(AudioClip clip, AudioResource resource, bool loop)
    {
        EnsureAudioSources();
        AudioSource active = ActiveBgmSource;
        if (active == null)
        {
            return;
        }

        for (int i = 0; i < _bgmSources.Length; i++)
        {
            if (i == _activeBgmIndex)
            {
                continue;
            }

            _bgmSources[i].Stop();
        }

        ApplyBgmToSource(active, clip, resource, loop);
        active.volume = _bgmVolume;
        active.Play();
    }

    private void StopAllBgmChannels()
    {
        if (_bgmSources == null)
        {
            return;
        }

        for (int i = 0; i < _bgmSources.Length; i++)
        {
            AudioSource source = _bgmSources[i];
            if (source == null)
            {
                continue;
            }

            DOTween.Kill(source, complete: false, id: BgmScratchTweenId);
            DOTween.Kill(source);
            source.Stop();
            source.volume = _bgmVolume;
            source.pitch = 1f;
        }
    }

    private static void ApplyBgmToSource(AudioSource source, AudioClip clip, AudioResource resource, bool loop)
    {
        source.Stop();
        source.loop = loop;
        source.time = 0f;

        if (resource != null)
        {
            source.resource = resource;
        }

        if (clip != null)
        {
            source.clip = clip;
        }
    }

    private const float SilentSourceRestoreFadeSeconds = 0.15f;

    private async UniTask CrossfadeBgmAsync(
        AudioClip clip,
        AudioResource resource,
        bool loop,
        float fadeSeconds,
        bool preserveFromVolume,
        CancellationToken token)
    {
        int nextIndex = (_activeBgmIndex + 1) % BgmChannelCount;
        AudioSource from = _bgmSources[_activeBgmIndex];
        AudioSource to = _bgmSources[nextIndex];

        float fromStart = from.volume;
        float targetVolume = preserveFromVolume ? fromStart : _bgmVolume;
        if (targetVolume <= 0.001f)
        {
            targetVolume = _bgmVolume;
        }

        ApplyBgmToSource(to, clip, resource, loop);

        if (!from.isPlaying)
        {
            float quickFade = Mathf.Min(fadeSeconds, SilentSourceRestoreFadeSeconds);
            to.volume = 0f;
            to.Play();
            await WaitTweenUniTaskAsync(to.DOFade(targetVolume, quickFade), token);
            from.Stop();
            from.volume = fromStart;
            _activeBgmIndex = nextIndex;
            return;
        }

        to.volume = 0f;
        to.Play();

        Tweener fadeOut = from.DOFade(0f, fadeSeconds);
        Tweener fadeIn = to.DOFade(targetVolume, fadeSeconds);

        await UniTask.WhenAll(
            WaitTweenUniTaskAsync(fadeOut, token),
            WaitTweenUniTaskAsync(fadeIn, token));

        from.Stop();
        from.volume = fromStart;
        _activeBgmIndex = nextIndex;
    }

    private AudioSource PickSeSource()
    {
        if (_seSources == null || _seSources.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < _seSources.Length; i++)
        {
            if (!_seSources[i].isPlaying)
            {
                return _seSources[i];
            }
        }

        AudioSource picked = _seSources[_seRoundRobinIndex % _seSources.Length];
        _seRoundRobinIndex++;
        picked.Stop();
        return picked;
    }

    private void EnsureAudioSources()
    {
        if (_bgmSources != null && _bgmSources.Length == BgmChannelCount && _bgmSources[0] != null
            && _seSources != null && _seSources.Length == SeChannelCount && _seSources[0] != null)
        {
            return;
        }

        _bgmSources = new AudioSource[BgmChannelCount];
        for (int i = 0; i < BgmChannelCount; i++)
        {
            _bgmSources[i] = gameObject.AddComponent<AudioSource>();
            ConfigureSource(_bgmSources[i], isBgm: true);
        }

        _seSources = new AudioSource[SeChannelCount];
        for (int i = 0; i < SeChannelCount; i++)
        {
            _seSources[i] = gameObject.AddComponent<AudioSource>();
            ConfigureSource(_seSources[i], isBgm: false);
        }
    }

    private static void ConfigureSource(AudioSource source, bool isBgm)
    {
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.loop = isBgm;
    }

    private void EnsureCatalog()
    {
        if (_catalog == null)
        {
            _catalog = Resources.Load<InGameSeCatalogSO>(DefaultCatalogResourceName);
        }
    }

    private static async UniTask WaitTweenUniTaskAsync(Tween tween, CancellationToken token)
    {
        if (tween == null || !tween.IsActive())
        {
            return;
        }

        UniTaskCompletionSource completionSource = new UniTaskCompletionSource();
        tween.OnComplete(() => completionSource.TrySetResult());
        tween.OnKill(() => completionSource.TrySetResult());
        await completionSource.Task.AttachExternalCancellation(token);
    }

    private void WarnMissingClipOnce(string key)
    {
        if (_warnedMissingKeys.Add(key))
        {
            Debug.LogWarning($"[InGameSe] 未設定: {key}（Catalog に AudioClip を割り当ててください）", this);
        }
    }

    private void WarnMissingCatalogOnce()
    {
        if (_warnedMissingCatalog)
        {
            return;
        }

        _warnedMissingCatalog = true;
        Debug.LogWarning(
            "[InGameSe] InGameSeCatalogSO が未設定です（InGame > Create Default SE Catalog を実行してください）。",
            this);
    }

    private void WarnDisabledOnce()
    {
        if (_warnedDisabled)
        {
            return;
        }

        _warnedDisabled = true;
        Debug.LogWarning("[InGameSe] Mute All が有効です。SE は再生されません。", this);
    }
}
