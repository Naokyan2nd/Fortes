using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 装備中 CD の BGM を DontDestroyOnLoad で再生し、シーン遷移でも途切れないようにする。
/// Stage / Scan BGM は ScanSceneManager の Inspector から登録する。
/// Main / Title / DistanceDebug / Ikeda_Sandbox では一時停止する。
/// </summary>
public class EquippedCdBgmManager : MonoBehaviour
{
    public static EquippedCdBgmManager Instance { get; private set; }

    private const string StageScanTrackKey = "stage_scan";

    [SerializeField] [Range(0f, 1f)] private float bgmVolume = 0.5f;

    private AudioSource _bgmSource;
    private string _playingTrackKey = string.Empty;
    private bool _homePitchControlActive;
    private static AudioClip _stageScanMusicClip;

    private static readonly HashSet<string> StageScanScenes = new HashSet<string>
    {
        SceneNames.Stage,
        SceneNames.Scan,
    };

    private static readonly HashSet<string> ExcludedScenes = new HashSet<string>
    {
        SceneNames.Main,
        SceneNames.InGameTutorial,
        SceneNames.Title,
        SceneNames.Result,
        SceneNames.DistanceDebug,
        SceneNames.IkedaSandbox,
    };

    public AudioSource BgmSource
    {
        get
        {
            EnsureAudioSource();
            return _bgmSource;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("Runtime_EquippedCdBgmManager");
        Instance = go.AddComponent<EquippedCdBgmManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureAudioSource();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        TrySubscribeLoadout();
    }

    private void Start()
    {
        TrySubscribeLoadout();
        RefreshForActiveScene();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeLoadout();
    }

    private void TrySubscribeLoadout()
    {
        if (OutfitLoadoutManager.Instance == null)
        {
            return;
        }

        OutfitLoadoutManager.Instance.OnLoadoutChanged -= OnLoadoutChanged;
        OutfitLoadoutManager.Instance.OnLoadoutChanged += OnLoadoutChanged;
    }

    private void UnsubscribeLoadout()
    {
        if (OutfitLoadoutManager.Instance == null)
        {
            return;
        }

        OutfitLoadoutManager.Instance.OnLoadoutChanged -= OnLoadoutChanged;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshForActiveScene();
    }

    private void OnLoadoutChanged(ItemType type)
    {
        if (type != ItemType.CD)
        {
            return;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        if (IsStageScanScene(sceneName))
        {
            return;
        }

        if (ShouldUseStageScanMusicInOutfit(sceneName))
        {
            return;
        }

        RefreshForActiveScene(forceClipRefresh: true);
    }

    public void SetStageScanMusic(AudioClip clip)
    {
        if (_stageScanMusicClip == clip)
        {
            return;
        }

        _stageScanMusicClip = clip;

        string sceneName = SceneManager.GetActiveScene().name;
        if (IsStageScanScene(sceneName) || ShouldUseStageScanMusicInOutfit(sceneName))
        {
            RefreshForActiveScene(forceClipRefresh: true);
        }
    }

    public void SetHomePitchControlActive(bool active)
    {
        _homePitchControlActive = active;

        if (!active && _bgmSource != null)
        {
            _bgmSource.pitch = 1f;
        }
    }

    /// <summary>Home の CD 回転に合わせてピッチ／一時停止を制御する。</summary>
    public void ApplyHomeTurntablePitch(float targetPitch, bool pauseWhenIdle)
    {
        if (!_homePitchControlActive)
        {
            return;
        }

        EnsureAudioSource();

        if (pauseWhenIdle || targetPitch <= 0.001f)
        {
            if (_bgmSource.isPlaying)
            {
                _bgmSource.Pause();
            }

            return;
        }

        if (!_bgmSource.isPlaying && ShouldPlayBgmInScene(SceneManager.GetActiveScene().name))
        {
            _bgmSource.UnPause();
            if (!_bgmSource.isPlaying)
            {
                _bgmSource.Play();
            }
        }
        else
        {
            _bgmSource.UnPause();
        }

        _bgmSource.pitch = targetPitch;
    }

    public void RefreshForActiveScene()
    {
        RefreshForActiveScene(forceClipRefresh: false);
    }

    private void RefreshForActiveScene(bool forceClipRefresh)
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (!ShouldPlayBgmInScene(sceneName))
        {
            PauseForExcludedScene();
            return;
        }

        if (IsStageScanScene(sceneName) || ShouldUseStageScanMusicInOutfit(sceneName))
        {
            PlayStageScanMusic(forceClipRefresh);
            return;
        }

        PlayEquippedCd(forceClipRefresh);
    }

    private void PlayStageScanMusic(bool forceClipRefresh)
    {
        AudioClip clip = ResolveStageAndScanMusic();
        if (clip == null)
        {
            return;
        }

        PlayLoopingTrack(StageScanTrackKey, clip, forceClipRefresh);
    }

    private void PlayEquippedCd(bool forceClipRefresh)
    {
        if (OutfitLoadoutManager.Instance == null)
        {
            return;
        }

        ItemData equippedCd = OutfitLoadoutManager.Instance.GetSelected(ItemType.CD);
        AudioClip clip = equippedCd != null ? equippedCd.cdBgm : null;
        if (clip == null)
        {
            return;
        }

        string trackKey = "cd:" + OutfitLoadoutManager.GetStableItemId(equippedCd);
        PlayLoopingTrack(trackKey, clip, forceClipRefresh);
    }

    private void PlayLoopingTrack(string trackKey, AudioClip clip, bool forceClipRefresh)
    {
        EnsureAudioSource();

        bool clipChanged = _bgmSource.clip != clip;
        if (!clipChanged && !forceClipRefresh && _playingTrackKey == trackKey && _bgmSource.isPlaying)
        {
            return;
        }

        if (clipChanged)
        {
            _bgmSource.clip = clip;
            _bgmSource.time = 0f;
            _playingTrackKey = trackKey;
        }
        else if (forceClipRefresh)
        {
            _playingTrackKey = trackKey;
        }

        _bgmSource.loop = true;
        _bgmSource.volume = bgmVolume;
        if (!_homePitchControlActive)
        {
            _bgmSource.pitch = 1f;
        }

        if (!_bgmSource.isPlaying)
        {
            _bgmSource.Play();
        }
        else
        {
            _bgmSource.UnPause();
        }
    }

    private void PauseForExcludedScene()
    {
        EnsureAudioSource();

        if (_bgmSource.isPlaying)
        {
            _bgmSource.Pause();
        }
    }

    private static AudioClip ResolveStageAndScanMusic()
    {
        return _stageScanMusicClip;
    }

    private static bool IsStageScanScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && StageScanScenes.Contains(sceneName);
    }

    private static bool ShouldUseStageScanMusicInOutfit(string sceneName)
    {
        return sceneName == SceneNames.Outfit
            && OutfitSceneReturnContext.Source == OutfitSceneReturnContext.SourceScene.StageReady;
    }

    private static bool ShouldPlayBgmInScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && !ExcludedScenes.Contains(sceneName);
    }

    private void EnsureAudioSource()
    {
        if (_bgmSource != null)
        {
            _bgmSource.playOnAwake = false;
            _bgmSource.spatialBlend = 0f;
            return;
        }

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.loop = true;
        _bgmSource.volume = bgmVolume;
    }
}
