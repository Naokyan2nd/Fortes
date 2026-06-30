using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScanSceneManager : MonoBehaviour
{
    [SerializeField] private Button backToHomeButton;       // ホームへ戻るボタン
    [SerializeField] private Button toStageButton;          // ステージ画面へボタン

    [Header("Stage / Scan BGM")]
    [Tooltip("ScanScene・StageScene、および Stage から開いた OutfitScene で再生する BGM。")]
    [SerializeField] private AudioClip stageAndScanMusic;

    [Header("Scan Wave (match StageScene)")]
    [SerializeField] private StageScanWaveAnimator stageScanWaveAnimator;
    [SerializeField] private Image scanWaveImage;
    [SerializeField] private Image mapDarkImage;
    [SerializeField] private float scanWaveDuration = 0.5f;
    [SerializeField] [Range(0f, 0.5f)] private float scanWaveRevealEnd = 0.5f;
    [SerializeField] [Range(1f, 6f)] private float scanWaveEaseOutPower = 2.5f;
    [Tooltip("When true, MapDark is hidden until PlayScan so NoisesOnMap / ScanButton entry stay visible.")]
    [SerializeField] private bool hideMapDarkUntilScan = true;
    [SerializeField] private Button scanButton;

    [Header("Scan Button Slide-In")]
    [SerializeField] private UIButtonSlideInEntryAnimator scanButtonSlideInAnimator;

    [Header("Scan Button Press")]
    [SerializeField] private UIButtonPressFeedback scanButtonPressFeedback;

    [Header("Unscanned Noises Entry")]
    [SerializeField] private ScannedNoisesEntryAnimator unscannedNoisesEntryAnimator;
    [SerializeField] private ScannedNoisesEntryAnimator scannedNoisesEntryAnimator;
    [SerializeField] private NoiseScanFlipRevealAnimator noiseScanFlipRevealAnimator;
    [Tooltip("Delay between each unscanned pop-in START. Smaller than Reveal Duration feels more continuous.")]
    [SerializeField] private float unscannedNoisesRevealStartInterval = 0.06f;
    [SerializeField] private float unscannedNoisesRevealDuration = 0.2f;
    [SerializeField] [Range(0.5f, 1f)] private float unscannedNoisesScaleFrom = 0.88f;
    [SerializeField] [Range(1f, 6f)] private float unscannedNoisesEaseOutPower = 3f;
    [SerializeField] private float noiseFlipStartInterval = 0.2f;
    [SerializeField] private float noiseFlipDuration = 0.35f;
    [SerializeField] [Range(1f, 6f)] private float noiseFlipEaseOutPower = 3f;

    [Header("Noise Flip SFX")]
    [SerializeField] private AudioClip noiseFlipClip;
    [SerializeField] [Range(0f, 1f)] private float noiseFlipVolume = 1f;

    [Header("Pre-Flip Anticipation Pulse")]
    [SerializeField] private bool noisePreFlipPulseEnabled = true;
    [SerializeField] private float noisePreFlipPulseDuration = 0.7f;
    [SerializeField] [Range(1f, 1.35f)] private float noisePreFlipPulseScalePeak = 1.16f;
    [SerializeField] [Range(1f, 1.2f)] private float noisePreFlipPulseYStretchPeak = 1.12f;
    [SerializeField] [Range(0.7f, 1f)] private float noisePreFlipPulseAlphaMin = 0.78f;
    [SerializeField] [Range(0f, 1f)] private float noisePreFlipPulseHighlightPeak = 0.85f;
    [SerializeField] [Range(1, 3)] private int noisePreFlipPulseCount = 2;
    [SerializeField] private float noisePreFlipPulseHoldAtPeak = 0.08f;
    [SerializeField] [Range(1f, 6f)] private float noisePreFlipPulseEaseOutPower = 3f;
    [SerializeField] [Range(0f, 3f)] private float noisePreFlipPulseOvershoot = 2.1f;

    [Header("To Stage Button Slide-In")]
    [SerializeField] private UIButtonSlideInEntryAnimator toStageButtonSlideInAnimator;

    [Header("NoisesOnMap Entry")]
    [SerializeField] private NoisesOnMapEntryAnimator noisesOnMapEntryAnimator;
    [Tooltip("When enabled, uses this count instead of the Home ghost NoisesAmount (distance gauge current).")]
    [SerializeField] private bool overrideNoisesOnMapRevealCount;
    [SerializeField] private int noisesOnMapRevealCountOverride;
    [Tooltip("Fallback gauge settings when opening ScanScene without visiting Home first.")]
    [SerializeField] private float scanNoiseCountGaugeMetersPerCurrent = 5000f;
    [SerializeField] private float scanNoiseCountGaugeMax = 12f;
    [Tooltip("Seconds after a group's parent fade STARTS before the next group begins. Smaller than Parent Fade Duration overlaps groups.")]
    [SerializeField] private float noisesGroupStartInterval = 0.15f;
    [Tooltip("Fade-in duration for the parent marker (e.g. '>').")]
    [SerializeField] private float noisesParentFadeDuration = 0.35f;
    [Tooltip("Move + fade duration for Noise children from the parent anchor.")]
    [SerializeField] private float noisesChildMoveDuration = 0.55f;
    [SerializeField] [Range(1f, 6f)] private float noisesEaseOutPower = 3f;

    private bool _isNavigating;
    private bool _scanTriggered;
    private bool _scanClickHandling;
    private AudioSource _noiseFlipAudioSource;

    void Awake()
    {
        OutGameDailyScanVisit.MarkVisitedToday();
        EnsureStageScanWaveAnimator();
        EnsureScanButtonReference();
        EnsureScanButtonPressFeedback();
        EnsureScanButtonSlideInAnimator();
        EnsureUnscannedNoisesEntryAnimator();
        EnsureScannedNoisesEntryAnimator();
        EnsureNoiseScanFlipRevealAnimator();
        EnsureToStageButtonReference();
        EnsureToStageButtonSlideInAnimator();
        EnsureNoisesOnMapEntryAnimator();
        ApplyNoisesOnMapEntrySettings();
        ApplyUnscannedNoisesEntrySettings();
        ApplyNoiseScanFlipRevealSettings();
        BindNoiseFlipSound();
        RegisterStageScanMusic();
    }

    void OnDisable()
    {
        UnbindNoiseFlipSound();
    }

    void BindNoiseFlipSound()
    {
        if (noiseScanFlipRevealAnimator == null)
        {
            return;
        }

        noiseScanFlipRevealAnimator.CardFlipStarted -= PlayNoiseFlipSound;
        noiseScanFlipRevealAnimator.CardFlipStarted += PlayNoiseFlipSound;
    }

    void UnbindNoiseFlipSound()
    {
        if (noiseScanFlipRevealAnimator == null)
        {
            return;
        }

        noiseScanFlipRevealAnimator.CardFlipStarted -= PlayNoiseFlipSound;
    }

    void PlayNoiseFlipSound()
    {
        if (noiseFlipClip == null)
        {
            return;
        }

        if (_noiseFlipAudioSource == null)
        {
            _noiseFlipAudioSource = gameObject.AddComponent<AudioSource>();
            _noiseFlipAudioSource.playOnAwake = false;
            _noiseFlipAudioSource.loop = false;
            _noiseFlipAudioSource.spatialBlend = 0f;
        }

        _noiseFlipAudioSource.PlayOneShot(
            noiseFlipClip,
            Mathf.Clamp01(noiseFlipVolume));
    }

    void RegisterStageScanMusic()
    {
        if (stageAndScanMusic == null)
        {
            return;
        }

        EquippedCdBgmManager.Instance?.SetStageScanMusic(stageAndScanMusic);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying || stageAndScanMusic == null)
        {
            return;
        }

        EquippedCdBgmManager.Instance?.SetStageScanMusic(stageAndScanMusic);
    }
#endif

    void Start()
    {
        StartCoroutine(PlayStageEntrySequence());

        backToHomeButton.onClick.AddListener(() =>
            OnButtonClicked(backToHomeButton, () =>
                SceneTransferManager.Instance.GoBack()));

        if (toStageButton != null)
        {
            toStageButton.onClick.AddListener(() =>
                OnButtonClicked(toStageButton, () =>
                    SceneTransferManager.Instance.LoadNewScene(SceneNames.Stage)));
        }
    }

    IEnumerator PlayStageEntrySequence()
    {
        if (noisesOnMapEntryAnimator != null)
        {
            yield return noisesOnMapEntryAnimator.PlayEntry();
        }

        if (scanButtonSlideInAnimator != null)
        {
            yield return scanButtonSlideInAnimator.PlaySlideIn();
        }

        yield return WaitForScanButtonClick();

        if (stageScanWaveAnimator != null)
        {
            yield return stageScanWaveAnimator.PlayScan();
        }

        if (noiseScanFlipRevealAnimator != null)
        {
            yield return noiseScanFlipRevealAnimator.PlaySequence();
        }

        if (toStageButtonSlideInAnimator != null)
        {
            yield return toStageButtonSlideInAnimator.PlaySlideIn();
        }
    }

    IEnumerator WaitForScanButtonClick()
    {
        if (scanButton == null)
        {
            yield break;
        }

        _scanTriggered = false;
        _scanClickHandling = false;
        scanButton.onClick.AddListener(OnScanButtonClicked);

        while (!_scanTriggered)
        {
            yield return null;
        }
    }

    void OnScanButtonClicked()
    {
        if (_scanClickHandling)
        {
            return;
        }

        StartCoroutine(HandleScanButtonClick());
    }

    IEnumerator HandleScanButtonClick()
    {
        _scanClickHandling = true;
        scanButton.onClick.RemoveListener(OnScanButtonClicked);

        if (scanButtonPressFeedback != null)
        {
            yield return scanButtonPressFeedback.PlayClickConfirm();
        }

        scanButton.interactable = false;
        _scanTriggered = true;
        _scanClickHandling = false;
    }

    void EnsureStageScanWaveAnimator()
    {
        if (stageScanWaveAnimator == null)
        {
            stageScanWaveAnimator = GetComponent<StageScanWaveAnimator>();
            if (stageScanWaveAnimator == null)
            {
                stageScanWaveAnimator = gameObject.AddComponent<StageScanWaveAnimator>();
            }
        }

        Transform canvasRoot = null;
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            canvasRoot = canvasObject.transform;
        }

        if (scanWaveImage == null)
        {
            scanWaveImage = FindSceneImage(canvasRoot, "ScanWave");
        }

        if (mapDarkImage == null)
        {
            mapDarkImage = FindSceneImage(canvasRoot, "MapDark");
        }

        stageScanWaveAnimator.Configure(
            scanWaveImage,
            mapDarkImage,
            scanWaveDuration,
            scanWaveRevealEnd,
            scanWaveEaseOutPower,
            hideMapDarkUntilScan,
            readyStatus: null,
            hideReadyStatusIdle: true);
    }

    static Image FindSceneImage(Transform searchRoot, string objectName)
    {
        if (searchRoot == null)
        {
            return null;
        }

        Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
            {
                return candidate.GetComponent<Image>();
            }
        }

        return null;
    }

    void EnsureScanButtonReference()
    {
        if (scanButton != null)
        {
            return;
        }

        GameObject scanButtonObject = GameObject.Find("ScanButton");
        if (scanButtonObject != null)
        {
            scanButton = scanButtonObject.GetComponent<Button>();
        }
    }

    void EnsureScanButtonPressFeedback()
    {
        if (scanButtonPressFeedback != null)
        {
            return;
        }

        if (scanButton == null)
        {
            return;
        }

        scanButtonPressFeedback = scanButton.GetComponent<UIButtonPressFeedback>();
        if (scanButtonPressFeedback == null)
        {
            scanButtonPressFeedback = scanButton.gameObject.AddComponent<UIButtonPressFeedback>();
        }
    }

    void EnsureScanButtonSlideInAnimator()
    {
        if (scanButtonSlideInAnimator != null)
        {
            return;
        }

        UIButtonSlideInEntryAnimator[] animators = GetComponents<UIButtonSlideInEntryAnimator>();
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null && animators[i] != toStageButtonSlideInAnimator)
            {
                scanButtonSlideInAnimator = animators[i];
                return;
            }
        }

        scanButtonSlideInAnimator = gameObject.AddComponent<UIButtonSlideInEntryAnimator>();
    }

    void EnsureUnscannedNoisesEntryAnimator()
    {
        if (unscannedNoisesEntryAnimator != null)
        {
            return;
        }

        GameObject unscannedNoisesRoot = GameObject.Find("UnscannedNoises");
        if (unscannedNoisesRoot == null)
        {
            return;
        }

        unscannedNoisesEntryAnimator = unscannedNoisesRoot.GetComponent<ScannedNoisesEntryAnimator>();
        if (unscannedNoisesEntryAnimator == null)
        {
            unscannedNoisesEntryAnimator = unscannedNoisesRoot.AddComponent<ScannedNoisesEntryAnimator>();
        }
    }

    void EnsureScannedNoisesEntryAnimator()
    {
        if (scannedNoisesEntryAnimator != null)
        {
            return;
        }

        GameObject scannedNoisesRoot = GameObject.Find("ScannedNoises");
        if (scannedNoisesRoot == null)
        {
            return;
        }

        scannedNoisesEntryAnimator = scannedNoisesRoot.GetComponent<ScannedNoisesEntryAnimator>();
        if (scannedNoisesEntryAnimator == null)
        {
            scannedNoisesEntryAnimator = scannedNoisesRoot.AddComponent<ScannedNoisesEntryAnimator>();
        }
    }

    void EnsureNoiseScanFlipRevealAnimator()
    {
        if (noiseScanFlipRevealAnimator != null)
        {
            return;
        }

        noiseScanFlipRevealAnimator = GetComponent<NoiseScanFlipRevealAnimator>();
        if (noiseScanFlipRevealAnimator == null)
        {
            noiseScanFlipRevealAnimator = gameObject.AddComponent<NoiseScanFlipRevealAnimator>();
        }
    }

    void EnsureToStageButtonReference()
    {
        if (toStageButton != null)
        {
            return;
        }

        GameObject toStageButtonObject = GameObject.Find("ToStageButton");
        if (toStageButtonObject != null)
        {
            toStageButton = toStageButtonObject.GetComponent<Button>();
        }
    }

    void EnsureToStageButtonSlideInAnimator()
    {
        if (toStageButtonSlideInAnimator != null)
        {
            return;
        }

        UIButtonSlideInEntryAnimator[] animators = GetComponents<UIButtonSlideInEntryAnimator>();
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null && animators[i] != scanButtonSlideInAnimator)
            {
                toStageButtonSlideInAnimator = animators[i];
                return;
            }
        }

        toStageButtonSlideInAnimator = gameObject.AddComponent<UIButtonSlideInEntryAnimator>();
    }

    private void OnButtonClicked(Button button, Action navigate)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        button.interactable = false;
        navigate();
    }

    void EnsureNoisesOnMapEntryAnimator()
    {
        if (noisesOnMapEntryAnimator != null)
        {
            return;
        }

        GameObject noisesRoot = GameObject.Find("NoisesOnMap");
        if (noisesRoot == null)
        {
            return;
        }

        noisesOnMapEntryAnimator = noisesRoot.GetComponent<NoisesOnMapEntryAnimator>();
        if (noisesOnMapEntryAnimator == null)
        {
            noisesOnMapEntryAnimator = noisesRoot.AddComponent<NoisesOnMapEntryAnimator>();
        }
    }

    void ApplyNoisesOnMapEntrySettings()
    {
        if (noisesOnMapEntryAnimator == null)
        {
            return;
        }

        noisesOnMapEntryAnimator.ApplySettings(
            noisesGroupStartInterval,
            noisesParentFadeDuration,
            noisesChildMoveDuration,
            noisesEaseOutPower);

        int revealCount = ResolveNoisesOnMapRevealCount();
        noisesOnMapEntryAnimator.ConfigureRevealCount(revealCount);
    }

    int ResolveNoisesOnMapRevealCount()
    {
        if (overrideNoisesOnMapRevealCount)
        {
            return Mathf.Max(0, noisesOnMapRevealCountOverride);
        }

        return OutGameScanNoiseRevealCount.GetRevealCount();
    }

    void ApplyUnscannedNoisesEntrySettings()
    {
        if (unscannedNoisesEntryAnimator == null)
        {
            return;
        }

        unscannedNoisesEntryAnimator.ApplySettings(
            unscannedNoisesRevealStartInterval,
            unscannedNoisesRevealDuration,
            unscannedNoisesScaleFrom,
            unscannedNoisesEaseOutPower);

        int revealCount = ResolveNoisesOnMapRevealCount();
        unscannedNoisesEntryAnimator.ConfigureActiveElementCount(revealCount);

        if (scannedNoisesEntryAnimator != null)
        {
            scannedNoisesEntryAnimator.ApplySettings(
                unscannedNoisesRevealStartInterval,
                unscannedNoisesRevealDuration,
                unscannedNoisesScaleFrom,
                unscannedNoisesEaseOutPower);
            scannedNoisesEntryAnimator.ConfigureActiveElementCount(revealCount);
        }
    }

    void ApplyNoiseScanFlipRevealSettings()
    {
        if (noiseScanFlipRevealAnimator == null)
        {
            return;
        }

        int revealCount = ResolveNoisesOnMapRevealCount();
        noiseScanFlipRevealAnimator.ConfigureRevealCount(revealCount);

        noiseScanFlipRevealAnimator.ApplySettings(
            unscannedNoisesEntryAnimator,
            scannedNoisesEntryAnimator,
            unscannedNoisesRevealStartInterval,
            noiseFlipStartInterval,
            noiseFlipDuration,
            noiseFlipEaseOutPower,
            noisePreFlipPulseEnabled,
            noisePreFlipPulseDuration,
            noisePreFlipPulseScalePeak,
            noisePreFlipPulseYStretchPeak,
            noisePreFlipPulseAlphaMin,
            noisePreFlipPulseHighlightPeak,
            noisePreFlipPulseCount,
            noisePreFlipPulseHoldAtPeak,
            noisePreFlipPulseEaseOutPower,
            noisePreFlipPulseOvershoot);
    }
}
