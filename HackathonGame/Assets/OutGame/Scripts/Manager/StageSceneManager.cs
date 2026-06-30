using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StageSceneManager : MonoBehaviour
{
    [Header("CD Turntable")]
    [SerializeField] UITurntableDragRotator cdTurntableRotator;
    [SerializeField] RectTransform cdRect;
    [SerializeField] UIRectSlideInEntryAnimator cdSlideInAnimator;
    [SerializeField] float cdSlideInOffsetX = 1400f;
    [SerializeField] float cdSlideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] float cdSlideInEaseOutPower = 3f;

    [Header("Noises slide-in (scene start)")]
    [SerializeField] RectTransform noisesRect;
    [SerializeField] UIRectSlideInEntryAnimator noisesSlideInAnimator;
    [SerializeField] float noisesSlideInOffsetY = 800f;
    [SerializeField] float noisesSlideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] float noisesSlideInEaseOutPower = 3f;

    [Header("Noises visibility (Home NoisesAmount)")]
    [SerializeField] StageNoisesAmountVisibility noisesAmountVisibility;
    [Tooltip("When enabled, uses revealCountOverride instead of Home NoisesAmount.")]
    [SerializeField] bool overrideNoisesRevealCount;
    [SerializeField] int noisesRevealCountOverride = 4;

    [Header("Noises scroll")]
    [SerializeField] UITurntableDrivenVerticalScroll noisesVerticalScroll;
    [SerializeField] float noisesScrollPixelsPerDegree = 10f;

    [Header("Noises slot focus")]
    [SerializeField] StageNoiseSlotFocus noisesSlotFocus;
    [Tooltip("Vertical gap opened above (+) and below (-) the focused noise item.")]
    [SerializeField] float noisesSiblingSpreadOffset = 10f;
    [Tooltip("How close a noise item must be to the slot line before it enlarges.")]
    [SerializeField] float noisesSlotAdsorptionThreshold = 120f;
    [Tooltip("Duration of the enlarge / shrink focus animation.")]
    [SerializeField] float noisesFocusAnimDuration = 0.2f;
    [SerializeField] [Range(1f, 6f)] float noisesFocusAnimEaseOutPower = 3f;
    [Tooltip("Brightness multiplier for Noises items outside the enlarged slot (1 = unchanged).")]
    [SerializeField] [Range(0.1f, 1f)] float noisesUnfocusedBrightness = 0.45f;
    [Tooltip("Duration of the snap-to-slot animation on release.")]
    [SerializeField] float noisesSnapDuration = 0.28f;
    [SerializeField] [Range(1f, 6f)] float noisesSnapEaseOutPower = 3f;
    [Tooltip("Resistance when scrolling past the top/bottom slot limits.")]
    [SerializeField] [Range(0.05f, 0.5f)] float noisesScrollEdgeRubberBandStrength = 0.22f;
    [SerializeField] float noisesScrollEdgeMaxOvershoot = 48f;
    [SerializeField] float noisesScrollBoundSpringDuration = 0.2f;
    [SerializeField] [Range(1f, 6f)] float noisesScrollBoundSpringEaseOutPower = 3f;
    [Tooltip("Played when a Noises item begins enlarging in the slot during turntable scroll.")]
    [SerializeField] AudioClip noiseSlotEnlargeClip;
    [SerializeField] [Range(0f, 1f)] float noiseSlotEnlargeVolume = 1f;

    [Header("Stage status slide-in (scene start)")]
    [SerializeField] float stageStatusSlideInOffsetY = 800f;
    [SerializeField] float stageStatusSlideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] float stageStatusSlideInEaseOutPower = 3f;

    [Header("Stage status (left panel)")]
    [SerializeField] StageStatusFocusSync stageStatusFocusSync;

    readonly List<UIRectSlideInEntryAnimator> _stageStatusSlideInAnimators = new();

    [Header("Ready / Battle buttons")]
    [SerializeField] Button toReadyButton;
    [SerializeField] Button toBattleButton;
    [SerializeField] Button toOutfitButton;
    [SerializeField] Button backToStageButton;
    [SerializeField] Button backToHomeButton;
    [SerializeField] UIStageReadyBattleButtonTransition readyBattleButtonTransition;
    [SerializeField] float readyBattleSwapDuration = 0.38f;
    [SerializeField] [Range(1f, 6f)] float readyBattleSwapEaseOutPower = 3f;
    [SerializeField] float readyBattleSwapSlideOffsetX = 48f;
    [SerializeField] [Range(0.5f, 1f)] float readyBattleSwapScaleFrom = 0.9f;

    [Header("Ready banner fly-in")]
    [SerializeField] RectTransform readyBannerRect;
    [SerializeField] UIStageReadyBannerFlyIn readyBannerFlyIn;
    [SerializeField] Vector2 readyBannerFlyInFromPosition = new(1785f, 742f);
    [SerializeField] float readyBannerFlyInDuration = 0.52f;
    [SerializeField] [Range(1f, 6f)] float readyBannerFlyInEaseOutPower = 3f;
    [SerializeField] bool readyBannerFadeInDuringFly = true;

    [Header("Back to home slide-in (scene start)")]
    [SerializeField] UIButtonSlideInEntryAnimator backToHomeButtonSlideInAnimator;
    [SerializeField] float backToHomeSlideInOffsetX = 1400f;
    [SerializeField] float backToHomeSlideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] float backToHomeSlideInEaseOutPower = 3f;

    [Header("To ready slide-in (scene start)")]
    [SerializeField] UIButtonSlideInEntryAnimator toReadyButtonSlideInAnimator;
    [SerializeField] float toReadySlideInOffsetX = 1400f;
    [SerializeField] float toReadySlideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] float toReadySlideInEaseOutPower = 3f;

    [Header("Back to stage (hidden until ToReady)")]
    [SerializeField] RectTransform backToStageRect;
    [SerializeField] UIStageReadyBannerFlyIn backToStageFlyIn;
    [SerializeField] Vector2 backToStageFlyInFromPosition = new(930f, 720f);
    [Tooltip("Ignored when BackToStageButton is found; flies to that object's scene anchored position.")]
    [SerializeField] Vector2 backToStageFlyInToPosition = new(-1110f, 368f);
    [SerializeField] float backToStageFlyInDuration = 0.52f;
    [SerializeField] [Range(1f, 6f)] float backToStageFlyInEaseOutPower = 3f;
    [SerializeField] bool backToStageFadeInDuringFly = true;

    [Header("Scan wave (on ToReady, same as ScanScene)")]
    [SerializeField] StageScanWaveAnimator stageScanWaveAnimator;
    [SerializeField] Image stageScanWaveImage;
    [SerializeField] Image stageMapDarkImage;
    [SerializeField] float stageScanWaveDuration = 0.5f;
    [SerializeField] [Range(0f, 0.5f)] float stageScanWaveRevealEnd = 0.5f;
    [SerializeField] [Range(1f, 6f)] float stageScanWaveEaseOutPower = 2.5f;
    [Tooltip("Keep MapDark visible before/after the ToReady scan (ScanScene keeps it hidden until scan).")]
    [SerializeField] bool stageHideMapDarkUntilScan = false;
    [SerializeField] RectTransform readyStatusRoot;

    [Header("Ready status stats display")]
    [SerializeField] PlayerLevelConfig playerLevelConfig;
    [SerializeField] StageReadyStatusStatsDisplay readyStatusStatsDisplay;

    [Header("Ready equipment display")]
    [Tooltip("Shows Top/Bottom/CD Starter or Better children under Equipment.")]
    [SerializeField] Transform equipmentRoot;

    bool _backToHomeNavInProgress;
    AudioSource _noiseSlotEnlargeAudioSource;

    void Awake()
    {
        EnsureCdTurntableRotator();
        EnsureCdSlideInAnimator();
        EnsureNoisesVerticalScroll();
        EnsureNoisesSlotFocus();
        EnsureNoisesAmountVisibility();
        EnsureNoisesSlideInAnimator();
        EnsureStageStatusFocusSync();
        EnsureStageStatusSlideInAnimators();
        EnsureStageScanWaveAnimator();
        EnsureReadyStatusStatsDisplay();
        EnsureReadyBannerFlyIn();
        EnsureBackToStageFlyIn();
        EnsureReadyBattleButtonTransition();
        EnsureBackToHomeSlideInAnimator();
        EnsureToReadySlideInAnimator();
        EnsureEquipmentRoot();
    }

    void OnEnable()
    {
        if (OutfitLoadoutManager.Instance != null)
        {
            OutfitLoadoutManager.Instance.OnLoadoutChanged += OnOutfitLoadoutChanged;
        }
    }

    void OnDisable()
    {
        UnbindNoisesSlotFocusSound();

        if (OutfitLoadoutManager.Instance != null)
        {
            OutfitLoadoutManager.Instance.OnLoadoutChanged -= OnOutfitLoadoutChanged;
        }
    }

    void Start()
    {
        BindBackToHomeButtonClick();
        ApplyPlayerLevelConfig();
        PlayBackToHomeSlideIn();
        ApplyStageEquipmentVisual();
        RefreshReadyStatusStats();
        RefreshNoisesListLayout();

        bool resumeReadyState = StageSceneReadyResume.ConsumeResumeReadyState(
            out string statusKey,
            out string focusedNoiseName);

        if (readyBattleButtonTransition != null)
        {
            readyBattleButtonTransition.BindReadyButtonClick();
            readyBattleButtonTransition.BindBackToStageButtonClick();
            readyBattleButtonTransition.BindOutfitButtonClick();
            readyBattleButtonTransition.BindBattleButtonClick();

            if (resumeReadyState)
            {
                readyBattleButtonTransition.RestoreReadyStateAfterOutfit();
                RestoreStageStatusAfterOutfit(statusKey, focusedNoiseName);
                readyBattleButtonTransition.RestoreReadyBattleNoiseCommit(focusedNoiseName);
                ApplyStageEquipmentVisual();
                RefreshReadyStatusStats();
                RefreshNoisesListLayout();
            }
        }

        if (!resumeReadyState)
        {
            PlayToReadySlideIn();
            PlayCdSlideIn();
            PlayNoisesSlideIn();
            PlayStageStatusSlideIn();
        }
        else
        {
            if (cdSlideInAnimator != null)
            {
                cdSlideInAnimator.SnapToRest();
            }

            if (noisesSlideInAnimator != null)
            {
                noisesSlideInAnimator.SnapToRest();
            }

            SnapStageStatusPanelsToRest();
        }
    }

    void OnOutfitLoadoutChanged(ItemType type)
    {
        if (type != ItemType.Top && type != ItemType.Bottom && type != ItemType.CD && type != ItemType.Weapon)
        {
            return;
        }

        ApplyStageEquipmentVisual();
        RefreshReadyStatusStats();
    }

    void ApplyPlayerLevelConfig()
    {
        if (playerLevelConfig == null || PlayerLevelManager.Instance == null)
        {
            return;
        }

        PlayerLevelManager.Instance.ApplyConfig(playerLevelConfig);
    }

    void RefreshReadyStatusStats()
    {
        EnsureReadyStatusStatsDisplay();
        if (readyStatusStatsDisplay != null)
        {
            readyStatusStatsDisplay.Refresh();
        }
    }

    void EnsureReadyStatusStatsDisplay()
    {
        if (readyStatusStatsDisplay == null)
        {
            readyStatusStatsDisplay = GetComponent<StageReadyStatusStatsDisplay>();
            if (readyStatusStatsDisplay == null)
            {
                readyStatusStatsDisplay = gameObject.AddComponent<StageReadyStatusStatsDisplay>();
            }
        }

        if (readyStatusRoot == null)
        {
            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject != null)
            {
                Transform[] transforms = canvasObject.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    Transform candidate = transforms[i];
                    if (candidate != null && candidate.name == "ReadyStatus")
                    {
                        readyStatusRoot = candidate as RectTransform;
                        break;
                    }
                }
            }
        }

        readyStatusStatsDisplay.Configure(readyStatusRoot);
    }

    void ApplyStageEquipmentVisual()
    {
        if (equipmentRoot == null)
        {
            return;
        }

        if (OutfitLoadoutManager.Instance == null)
        {
            return;
        }

        ApplyEquipmentSlotVariant(equipmentRoot, ItemType.Top);
        ApplyEquipmentSlotVariant(equipmentRoot, ItemType.Bottom);
        ApplyEquipmentSlotVariant(equipmentRoot, ItemType.CD);
    }

    static void ApplyEquipmentSlotVariant(Transform equipmentRoot, ItemType type)
    {
        OutfitItemVisualHelper.ApplyEquipmentSlotVariant(equipmentRoot, type);
    }

    void EnsureEquipmentRoot()
    {
        if (equipmentRoot != null)
        {
            return;
        }

        Transform searchRoot = readyStatusRoot;
        if (searchRoot == null)
        {
            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject != null)
            {
                searchRoot = canvasObject.transform;
            }
        }

        if (searchRoot == null)
        {
            return;
        }

        Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == "Equipment")
            {
                equipmentRoot = candidate;
                return;
            }
        }
    }

    void RestoreStageStatusAfterOutfit(string statusKey, string focusedNoiseName)
    {
        if (!string.IsNullOrEmpty(focusedNoiseName) && noisesSlotFocus != null)
        {
            noisesSlotFocus.RestoreFocusedChildByName(focusedNoiseName);
        }

        if (stageStatusFocusSync == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(statusKey))
        {
            stageStatusFocusSync.ApplyStatusKey(statusKey);
        }
        else if (noisesSlotFocus != null && noisesSlotFocus.FocusedChild != null)
        {
            stageStatusFocusSync.ApplyStatusKey(
                StageStatusFocusSync.ResolveStatusKeyFromNoiseName(noisesSlotFocus.FocusedChild.name));
        }
    }

    void BindBackToHomeButtonClick()
    {
        if (backToHomeButton == null)
        {
            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject != null)
            {
                backToHomeButton = FindStageSceneButton(canvasObject.transform, "BackToHomeButton");
            }
        }

        if (backToHomeButton == null)
        {
            return;
        }

        backToHomeButton.onClick.RemoveListener(OnBackToHomeClicked);
        backToHomeButton.onClick.AddListener(OnBackToHomeClicked);
    }

    void EnsureBackToHomeSlideInAnimator()
    {
        if (backToHomeButton == null)
        {
            Transform canvasRoot = null;
            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject != null)
            {
                canvasRoot = canvasObject.transform;
            }

            backToHomeButton = FindStageSceneButton(canvasRoot, "BackToHomeButton");
        }

        if (backToHomeButton == null)
        {
            return;
        }

        if (backToHomeButtonSlideInAnimator == null)
        {
            backToHomeButtonSlideInAnimator = backToHomeButton.GetComponent<UIButtonSlideInEntryAnimator>();
        }

        if (backToHomeButtonSlideInAnimator == null)
        {
            backToHomeButtonSlideInAnimator = backToHomeButton.gameObject.AddComponent<UIButtonSlideInEntryAnimator>();
        }

        backToHomeButtonSlideInAnimator.SetTarget(backToHomeButton, "BackToHomeButton");
        backToHomeButtonSlideInAnimator.ConfigureSlideFromLeft(
            backToHomeSlideInOffsetX,
            backToHomeSlideInDuration,
            backToHomeSlideInEaseOutPower,
            fromLeft: true);
        backToHomeButtonSlideInAnimator.PrepareOffScreenLeft();
    }

    void PlayBackToHomeSlideIn()
    {
        if (backToHomeButtonSlideInAnimator == null)
        {
            return;
        }

        StartCoroutine(backToHomeButtonSlideInAnimator.PlaySlideIn());
    }

    void EnsureToReadySlideInAnimator()
    {
        if (toReadyButton == null)
        {
            Transform canvasRoot = null;
            GameObject canvasObject = GameObject.Find("Canvas");
            if (canvasObject != null)
            {
                canvasRoot = canvasObject.transform;
            }

            toReadyButton = FindStageSceneButton(canvasRoot, "ToReadyButton");
        }

        if (toReadyButton == null)
        {
            return;
        }

        if (toReadyButtonSlideInAnimator == null)
        {
            toReadyButtonSlideInAnimator = toReadyButton.GetComponent<UIButtonSlideInEntryAnimator>();
        }

        if (toReadyButtonSlideInAnimator == null)
        {
            toReadyButtonSlideInAnimator = toReadyButton.gameObject.AddComponent<UIButtonSlideInEntryAnimator>();
        }

        toReadyButtonSlideInAnimator.SetTarget(toReadyButton, "ToReadyButton");
        toReadyButtonSlideInAnimator.ConfigureSlideFromLeft(
            toReadySlideInOffsetX,
            toReadySlideInDuration,
            toReadySlideInEaseOutPower,
            fromLeft: false);
        toReadyButtonSlideInAnimator.PrepareOffScreenRight();
    }

    void PlayToReadySlideIn()
    {
        if (toReadyButtonSlideInAnimator == null || toReadyButton == null || !toReadyButton.gameObject.activeInHierarchy)
        {
            return;
        }

        StartCoroutine(PlayToReadySlideInRoutine());
    }

    IEnumerator PlayToReadySlideInRoutine()
    {
        yield return toReadyButtonSlideInAnimator.PlaySlideIn();
        UIButtonPressFeedback.RestoreNormalVisual(toReadyButton);
    }

    void OnBackToHomeClicked()
    {
        if (_backToHomeNavInProgress)
        {
            return;
        }

        StartCoroutine(HandleBackToHomeClick());
    }

    IEnumerator HandleBackToHomeClick()
    {
        _backToHomeNavInProgress = true;
        backToHomeButton.interactable = false;

        UIButtonPressFeedback pressFeedback = backToHomeButton.GetComponent<UIButtonPressFeedback>();
        if (pressFeedback != null)
        {
            yield return pressFeedback.PlayClickConfirm();
        }

        StageSceneReadyResume.Clear();
        SceneTransferManager.Instance.ClearHistory();
        SceneTransferManager.Instance.LoadNewScene(SceneNames.Home, saveToHistory: false);
        _backToHomeNavInProgress = false;
    }

    void EnsureNoisesAmountVisibility()
    {
        GameObject noisesObject = GameObject.Find("Noises");
        if (noisesObject == null)
        {
            return;
        }

        RectTransform noisesRect = noisesObject.GetComponent<RectTransform>();
        if (noisesRect == null)
        {
            return;
        }

        if (noisesAmountVisibility == null)
        {
            noisesAmountVisibility = GetComponent<StageNoisesAmountVisibility>();
            if (noisesAmountVisibility == null)
            {
                noisesAmountVisibility = gameObject.AddComponent<StageNoisesAmountVisibility>();
            }
        }

        if (noisesSlotFocus == null)
        {
            noisesSlotFocus = GetComponent<StageNoiseSlotFocus>();
        }

        noisesAmountVisibility.Configure(
            noisesRect,
            overrideNoisesRevealCount,
            noisesRevealCountOverride,
            noisesSlotFocus);
    }

    void RefreshNoisesListLayout()
    {
        if (noisesAmountVisibility == null)
        {
            EnsureNoisesAmountVisibility();
        }

        noisesAmountVisibility?.Apply();
    }

    void EnsureCdTurntableRotator()
    {
        if (cdTurntableRotator != null)
        {
            return;
        }

        GameObject cdObject = GameObject.Find("CD");
        if (cdObject == null)
        {
            return;
        }

        cdTurntableRotator = cdObject.GetComponent<UITurntableDragRotator>();
        if (cdTurntableRotator == null)
        {
            cdTurntableRotator = cdObject.AddComponent<UITurntableDragRotator>();
        }
    }

    void EnsureCdSlideInAnimator()
    {
        if (cdRect == null)
        {
            GameObject cdObject = GameObject.Find("CD");
            if (cdObject != null)
            {
                cdRect = cdObject.GetComponent<RectTransform>();
            }
        }

        if (cdRect == null)
        {
            return;
        }

        if (cdSlideInAnimator == null)
        {
            cdSlideInAnimator = cdRect.GetComponent<UIRectSlideInEntryAnimator>();
        }

        if (cdSlideInAnimator == null)
        {
            cdSlideInAnimator = cdRect.gameObject.AddComponent<UIRectSlideInEntryAnimator>();
        }

        cdSlideInAnimator.SetTarget(cdRect, "CD");
        cdSlideInAnimator.Configure(cdSlideInOffsetX, cdSlideInDuration, cdSlideInEaseOutPower);

        if (!StageSceneReadyResume.ResumeReadyStateOnNextLoad)
        {
            cdSlideInAnimator.PrepareOffScreenRight();
        }
    }

    void PlayCdSlideIn()
    {
        if (cdSlideInAnimator == null)
        {
            return;
        }

        StartCoroutine(cdSlideInAnimator.PlaySlideIn());
    }

    void EnsureNoisesVerticalScroll()
    {
        RectTransform noisesRect = null;
        GameObject noisesObject = GameObject.Find("Noises");
        if (noisesObject != null)
        {
            noisesRect = noisesObject.GetComponent<RectTransform>();
        }

        if (cdTurntableRotator == null || noisesRect == null)
        {
            return;
        }

        if (noisesVerticalScroll == null)
        {
            noisesVerticalScroll = GetComponent<UITurntableDrivenVerticalScroll>();
            if (noisesVerticalScroll == null)
            {
                noisesVerticalScroll = gameObject.AddComponent<UITurntableDrivenVerticalScroll>();
            }
        }

        noisesVerticalScroll.Configure(cdTurntableRotator, noisesRect, noisesScrollPixelsPerDegree);
    }

    void EnsureNoisesSlotFocus()
    {
        GameObject noisesObject = GameObject.Find("Noises");
        if (noisesObject == null)
        {
            return;
        }

        RectTransform noisesRect = noisesObject.GetComponent<RectTransform>();
        if (noisesRect == null)
        {
            return;
        }

        if (noisesSlotFocus == null)
        {
            noisesSlotFocus = GetComponent<StageNoiseSlotFocus>();
            if (noisesSlotFocus == null)
            {
                noisesSlotFocus = gameObject.AddComponent<StageNoiseSlotFocus>();
            }
        }

        noisesSlotFocus.Configure(
            noisesRect,
            "SuperRare",
            noisesSiblingSpreadOffset,
            noisesSlotAdsorptionThreshold,
            cdTurntableRotator,
            noisesVerticalScroll,
            noisesSnapDuration,
            noisesSnapEaseOutPower,
            noisesFocusAnimDuration,
            noisesFocusAnimEaseOutPower,
            noisesScrollEdgeRubberBandStrength,
            noisesScrollEdgeMaxOvershoot,
            noisesScrollBoundSpringDuration,
            noisesScrollBoundSpringEaseOutPower,
            noisesUnfocusedBrightness);

        if (noisesVerticalScroll != null && noisesSlotFocus != null)
        {
            noisesVerticalScroll.SetScrollBounds(noisesSlotFocus);
        }

        BindNoisesSlotFocusSound();
    }

    void BindNoisesSlotFocusSound()
    {
        if (noisesSlotFocus == null)
        {
            return;
        }

        noisesSlotFocus.NoiseEnlarged -= OnNoiseSlotEnlarged;
        noisesSlotFocus.NoiseEnlarged += OnNoiseSlotEnlarged;
    }

    void UnbindNoisesSlotFocusSound()
    {
        if (noisesSlotFocus == null)
        {
            return;
        }

        noisesSlotFocus.NoiseEnlarged -= OnNoiseSlotEnlarged;
    }

    void OnNoiseSlotEnlarged(RectTransform noiseChild)
    {
        PlayNoiseSlotEnlargeSound();
    }

    void PlayNoiseSlotEnlargeSound()
    {
        if (noiseSlotEnlargeClip == null)
        {
            return;
        }

        if (_noiseSlotEnlargeAudioSource == null)
        {
            _noiseSlotEnlargeAudioSource = gameObject.AddComponent<AudioSource>();
            _noiseSlotEnlargeAudioSource.playOnAwake = false;
            _noiseSlotEnlargeAudioSource.loop = false;
            _noiseSlotEnlargeAudioSource.spatialBlend = 0f;
        }

        _noiseSlotEnlargeAudioSource.PlayOneShot(
            noiseSlotEnlargeClip,
            Mathf.Clamp01(noiseSlotEnlargeVolume));
    }

    void EnsureNoisesSlideInAnimator()
    {
        if (noisesRect == null)
        {
            GameObject noisesObject = GameObject.Find("Noises");
            if (noisesObject != null)
            {
                noisesRect = noisesObject.GetComponent<RectTransform>();
            }
        }

        if (noisesRect == null)
        {
            return;
        }

        if (noisesSlideInAnimator == null)
        {
            noisesSlideInAnimator = noisesRect.GetComponent<UIRectSlideInEntryAnimator>();
        }

        if (noisesSlideInAnimator == null)
        {
            noisesSlideInAnimator = noisesRect.gameObject.AddComponent<UIRectSlideInEntryAnimator>();
        }

        noisesSlideInAnimator.SetTarget(noisesRect, "Noises");
        noisesSlideInAnimator.ConfigureFromBottom(
            noisesSlideInOffsetY,
            noisesSlideInDuration,
            noisesSlideInEaseOutPower);

        if (!StageSceneReadyResume.ResumeReadyStateOnNextLoad)
        {
            noisesSlideInAnimator.PrepareOffScreenBottom();
        }
    }

    void PlayNoisesSlideIn()
    {
        if (noisesSlideInAnimator == null)
        {
            return;
        }

        StartCoroutine(noisesSlideInAnimator.PlaySlideIn());
    }

    void EnsureStageStatusFocusSync()
    {
        if (noisesSlotFocus == null)
        {
            return;
        }

        if (stageStatusFocusSync == null)
        {
            stageStatusFocusSync = GetComponent<StageStatusFocusSync>();
            if (stageStatusFocusSync == null)
            {
                stageStatusFocusSync = gameObject.AddComponent<StageStatusFocusSync>();
            }
        }

        Transform searchRoot = null;
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            searchRoot = canvasObject.transform;
        }

        stageStatusFocusSync.Configure(noisesSlotFocus, searchRoot);
    }

    void EnsureStageStatusSlideInAnimators()
    {
        _stageStatusSlideInAnimators.Clear();

        Transform canvasRoot = null;
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            canvasRoot = canvasObject.transform;
        }

        if (canvasRoot == null)
        {
            return;
        }

        const string stageStatusPrefix = "StageStatus";
        for (int i = 0; i < canvasRoot.childCount; i++)
        {
            Transform child = canvasRoot.GetChild(i);
            if (child == null
                || !child.name.StartsWith(stageStatusPrefix, System.StringComparison.Ordinal)
                || child.name.Length <= stageStatusPrefix.Length)
            {
                continue;
            }

            RectTransform panelRect = child as RectTransform;
            if (panelRect == null)
            {
                continue;
            }

            UIRectSlideInEntryAnimator slideInAnimator = panelRect.GetComponent<UIRectSlideInEntryAnimator>();
            if (slideInAnimator == null)
            {
                slideInAnimator = panelRect.gameObject.AddComponent<UIRectSlideInEntryAnimator>();
            }

            slideInAnimator.SetTarget(panelRect, child.name);
            slideInAnimator.ConfigureFromBottom(
                stageStatusSlideInOffsetY,
                stageStatusSlideInDuration,
                stageStatusSlideInEaseOutPower);

            if (!StageSceneReadyResume.ResumeReadyStateOnNextLoad)
            {
                slideInAnimator.PrepareOffScreenBottom();
            }

            _stageStatusSlideInAnimators.Add(slideInAnimator);
        }
    }

    void PlayStageStatusSlideIn()
    {
        for (int i = 0; i < _stageStatusSlideInAnimators.Count; i++)
        {
            UIRectSlideInEntryAnimator slideInAnimator = _stageStatusSlideInAnimators[i];
            if (slideInAnimator != null)
            {
                StartCoroutine(slideInAnimator.PlaySlideIn());
            }
        }
    }

    void SnapStageStatusPanelsToRest()
    {
        for (int i = 0; i < _stageStatusSlideInAnimators.Count; i++)
        {
            UIRectSlideInEntryAnimator slideInAnimator = _stageStatusSlideInAnimators[i];
            if (slideInAnimator != null)
            {
                slideInAnimator.SnapToRest();
            }
        }
    }

    void EnsureReadyBattleButtonTransition()
    {
        if (readyBattleButtonTransition == null)
        {
            readyBattleButtonTransition = GetComponent<UIStageReadyBattleButtonTransition>();
            if (readyBattleButtonTransition == null)
            {
                readyBattleButtonTransition = gameObject.AddComponent<UIStageReadyBattleButtonTransition>();
            }
        }

        Transform canvasRoot = null;
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            canvasRoot = canvasObject.transform;
        }

        if (toReadyButton == null)
        {
            toReadyButton = FindStageSceneButton(canvasRoot, "ToReadyButton");
        }

        if (toBattleButton == null)
        {
            toBattleButton = FindStageSceneButton(canvasRoot, "ToBattleButton");
        }

        if (toOutfitButton == null)
        {
            toOutfitButton = FindStageSceneButton(canvasRoot, "ToOutfitButton");
        }

        if (backToStageButton == null)
        {
            backToStageButton = FindStageSceneButton(canvasRoot, "BackToStageButton");
        }

        if (backToHomeButton == null)
        {
            backToHomeButton = FindStageSceneButton(canvasRoot, "BackToHomeButton");
        }

        LinkReadyFlowToButtonTransition();
        if (readyBattleButtonTransition != null)
        {
            readyBattleButtonTransition.EnsureButtonReferences();
        }
    }

    void EnsureStageScanWaveAnimator()
    {
        Transform canvasRoot = null;
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            canvasRoot = canvasObject.transform;
        }

        if (stageScanWaveImage == null)
        {
            stageScanWaveImage = FindStageSceneImage(canvasRoot, "ScanWave");
        }

        if (stageMapDarkImage == null)
        {
            stageMapDarkImage = FindStageSceneImage(canvasRoot, "MapDark");
        }

        if (readyStatusRoot == null && canvasRoot != null)
        {
            Transform[] transforms = canvasRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == "ReadyStatus")
                {
                    readyStatusRoot = candidate as RectTransform;
                    break;
                }
            }
        }

        if (stageScanWaveAnimator == null)
        {
            stageScanWaveAnimator = GetComponent<StageScanWaveAnimator>();
            if (stageScanWaveAnimator == null)
            {
                stageScanWaveAnimator = gameObject.AddComponent<StageScanWaveAnimator>();
            }
        }

        stageScanWaveAnimator.Configure(
            stageScanWaveImage,
            stageMapDarkImage,
            stageScanWaveDuration,
            stageScanWaveRevealEnd,
            stageScanWaveEaseOutPower,
            stageHideMapDarkUntilScan,
            readyStatusRoot,
            hideReadyStatusIdle: true);
    }

    void EnsureReadyBannerFlyIn()
    {
        Transform canvasRoot = null;
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            canvasRoot = canvasObject.transform;
        }

        if (readyBannerRect == null && canvasRoot != null)
        {
            Transform[] transforms = canvasRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == "ReadyBanner")
                {
                    readyBannerRect = candidate as RectTransform;
                    break;
                }
            }
        }

        if (readyBannerFlyIn == null)
        {
            readyBannerFlyIn = GetComponent<UIStageReadyBannerFlyIn>();
            if (readyBannerFlyIn == null)
            {
                readyBannerFlyIn = gameObject.AddComponent<UIStageReadyBannerFlyIn>();
            }
        }

        readyBannerFlyIn.Configure(
            readyBannerRect,
            readyBannerFlyInFromPosition,
            readyBannerFlyInDuration,
            readyBannerFlyInEaseOutPower,
            readyBannerFadeInDuringFly);

        readyBannerFlyIn.EnsureBannerReference();
        readyBannerFlyIn.PrepareHidden();
        LinkReadyFlowToButtonTransition();
    }

    void EnsureBackToStageFlyIn()
    {
        Transform canvasRoot = null;
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject != null)
        {
            canvasRoot = canvasObject.transform;
        }

        if (backToStageRect == null && canvasRoot != null)
        {
            Transform[] transforms = canvasRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == "BackToStageButton")
                {
                    backToStageRect = candidate as RectTransform;
                    break;
                }
            }
        }

        if (backToStageFlyIn == null)
        {
            UIStageReadyBannerFlyIn[] flyIns = GetComponents<UIStageReadyBannerFlyIn>();
            for (int i = 0; i < flyIns.Length; i++)
            {
                if (flyIns[i] != null && flyIns[i] != readyBannerFlyIn)
                {
                    backToStageFlyIn = flyIns[i];
                    break;
                }
            }

            if (backToStageFlyIn == null)
            {
                backToStageFlyIn = gameObject.AddComponent<UIStageReadyBannerFlyIn>();
            }
        }

        Vector2 backToStageRestPosition = backToStageFlyInToPosition;
        if (backToStageRect != null)
        {
            backToStageRestPosition = backToStageRect.anchoredPosition;
        }

        backToStageFlyIn.Configure(
            backToStageRect,
            backToStageFlyInFromPosition,
            backToStageRestPosition,
            backToStageFlyInDuration,
            backToStageFlyInEaseOutPower,
            backToStageFadeInDuringFly,
            "BackToStageButton");

        backToStageFlyIn.EnsureBannerReference();
        backToStageFlyIn.PrepareHidden();
        LinkReadyFlowToButtonTransition();
    }

    void LinkReadyFlowToButtonTransition()
    {
        if (readyBattleButtonTransition == null)
        {
            return;
        }

        readyBattleButtonTransition.Configure(
            toReadyButton,
            toBattleButton,
            toOutfitButton,
            backToStageButton,
            readyBattleSwapDuration,
            readyBattleSwapEaseOutPower,
            readyBattleSwapSlideOffsetX,
            readyBattleSwapScaleFrom,
            readyBannerFlyIn,
            backToStageFlyIn,
            stageScanWaveAnimator,
            cdTurntableRotator,
            noisesVerticalScroll,
            noisesSlotFocus);
    }

    static Image FindStageSceneImage(Transform searchRoot, string objectName)
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

    static Button FindStageSceneButton(Transform searchRoot, string objectName)
    {
        if (searchRoot == null)
        {
            return null;
        }

        Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate.name != objectName)
            {
                continue;
            }

            Button button = candidate.GetComponent<Button>();
            if (button != null)
            {
                return button;
            }

            Image image = candidate.GetComponent<Image>();
            if (image == null)
            {
                return null;
            }

            button = candidate.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            if (candidate.GetComponent<UIButtonPressFeedback>() == null)
            {
                candidate.gameObject.AddComponent<UIButtonPressFeedback>();
            }

            return button;
        }

        return null;
    }
}
