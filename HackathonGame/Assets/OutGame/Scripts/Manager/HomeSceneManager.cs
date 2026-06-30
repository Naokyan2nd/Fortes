using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public class HomeSceneManager : MonoBehaviour
{
    [SerializeField] private Button backToTitleButton;      // タイトルへ戻るボタン
    [SerializeField] private Button toDistanceDebugButton;  // 距離測定デバッグ画面へボタン
    [SerializeField] private Button toScanButton;             // スキャン画面へボタン
    [SerializeField] private Button toOutfitButton;        // 戦闘前装備画面へボタン
    [SerializeField] private Button toCraftButton;         // アイテム合成画面へボタン
    [Tooltip("Child of ToScanButton. Auto-finds object named Countdown when unset.")]
    [SerializeField] private TMP_Text battleCountdownText;

    [Header("Distance Display (TMP)")]
    [SerializeField] private TMP_Text distanceDisplayText;
    [SerializeField] private TMP_Text noiseAmountDisplayText;
    [SerializeField] private float distanceUiRefreshIntervalSeconds = 0.5f;

    [Header("NoisesAmount (bound: DistanceTravelled + gauge fill + text)")]
    [Tooltip("Walk distance per 1 on NoisesAmount. MainScene victory subtracts this many meters.")]
    [SerializeField] private float gaugeMetersPerCurrent = 5000f;
    [Tooltip("Denominator on NoisesAmount (e.g. 4/12 → 12).")]
    [SerializeField] private float gaugeMax = 12f;
    [SerializeField] private bool gaugeShowMaxInText = true;
    [Tooltip("Home Canvas > NoisesAmount. If empty, uses Gauge Value Text below.")]
    [SerializeField] private TMP_Text noisesAmountText;
    [Tooltip("Home Canvas > DistanceTravelled.")]
    [SerializeField] private TMP_Text totalDistanceDisplayText;
    [Header("Play Distance Popup")]
    [Tooltip("Home Canvas > Popup. Shown when the daily play window starts.")]
    [SerializeField] private GameObject playDistancePopupRoot;
    [SerializeField] private Button playDistancePopupExitButton;
    [SerializeField] private UIRectScaleInAnimator playDistancePopupScaleInAnimator;
    [SerializeField] private float playDistancePopupScaleInDuration = 0.35f;
    [SerializeField] [Range(1f, 6f)] private float playDistancePopupScaleInEaseOutPower = 3f;

    [Header("First Day After Popup")]
    [Tooltip("Home Canvas > FirstDayAfterPopup. Shown once after the first play-distance popup Exit.")]
    [SerializeField] private GameObject firstDayAfterPopupRoot;
    [SerializeField] private Button firstDayAfterPopupBackgroundButton;

    [Header("Distance Gauge On Battle (after popup)")]
    [Tooltip("Home Canvas > DistanceGaugeOnBattle (not under ToScanButton). Auto-finds by name when unset.")]
    [SerializeField] private RectTransform distanceGaugeOnBattleRect;
    [SerializeField] private UIRectSlideInEntryAnimator distanceGaugeOnBattleSlideInAnimator;
    [SerializeField] private float distanceGaugeOnBattleSlideInOffsetX = 1400f;
    [SerializeField] private float distanceGaugeOnBattleSlideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] private float distanceGaugeOnBattleSlideInEaseOutPower = 3f;

    [SerializeField] private Image gaugeFillImage;
    [Tooltip("Optional alias for NoisesAmount text when noisesAmountText is unset.")]
    [SerializeField] private TMP_Text gaugeValueText;

    [Header("Daily Distance Schedule")]
    [Tooltip("On: first launch grants First Play Given Distance; daily rollover syncs DistanceTravelled to the real distance recorded at the previous rollover.")]
    [SerializeField] private bool useDailyDistanceSchedule;
    [SerializeField] private int schedulePlayStartHour = 10;
    [SerializeField] private int schedulePlayStartMinute = 30;
    [Tooltip("First launch only. Starter DistanceTravelled (default 20000 m = 5 noises at 5000 m each).")]
    [SerializeField] private float firstPlayGivenDistanceMeters = 20000f;
    [Tooltip("Use Simulated Time below instead of device clock (Play mode testing).")]
    [SerializeField] private bool scheduleUseDebugTime;
    [SerializeField] private int scheduleDebugYear = 2026;
    [SerializeField] private int scheduleDebugMonth = 5;
    [SerializeField] private int scheduleDebugDay = 27;
    [SerializeField] private int scheduleDebugHour = 10;
    [SerializeField] private int scheduleDebugMinute = 30;
    [FormerlySerializedAs("scheduleDebugPhaseIsPlay")]
    [SerializeField] private bool scheduleDebugRolloverAppliedToday;
    [SerializeField] private float scheduleDebugPlayMeters;
    [FormerlySerializedAs("scheduleDebugPendingMeters")]
    [SerializeField] private float scheduleDebugStoredPreviousDayMeters;
    [SerializeField] private float scheduleDebugBackgroundAccumulatedMeters;
    [SerializeField] private float scheduleDebugRecordedRealPreviousDayMeters;

    [Header("Gauge Entry Animation")]
    [SerializeField] private float gaugeEntryAnimationDuration = 1.2f;
    [Tooltip("Ease-out strength. Higher = faster at the start and slower crawl near the target value.")]
    [SerializeField] [Range(1f, 10f)] private float gaugeEntryEaseOutPower = 4f;

    [Header("Nav Button Slide-In (Stage → Outfit → Craft, overlapping)")]
    [Tooltip("How far off-screen (to the right) each button starts before sliding in.")]
    [SerializeField] private float navButtonSlideInOffsetX = 1400f;
    [Tooltip("Stage slide duration (fastest).")]
    [SerializeField] private float navButtonStageSlideInDuration = 0.45f;
    [Tooltip("Seconds after Stage starts before Outfit begins sliding.")]
    [SerializeField] private float navButtonOutfitSlideStartDelay = 0.08f;
    [Tooltip("Seconds after Stage starts before Craft begins sliding.")]
    [SerializeField] private float navButtonCraftSlideStartDelay = 0.16f;
    [Tooltip("Outfit slide duration = Stage duration × this (>1 = slower than Stage).")]
    [SerializeField] private float navButtonOutfitSlideDurationScale = 1.15f;
    [Tooltip("Craft slide duration = Stage duration × this (> Outfit scale recommended).")]
    [SerializeField] private float navButtonCraftSlideDurationScale = 1.3f;
    [Tooltip("Ease-out on slide-in. Higher = faster approach, slower stop at rest position.")]
    [SerializeField] [Range(1f, 10f)] private float navButtonSlideInEaseOutPower = 3f;

    [Header("UI Button Click Sound (Home / Craft / Outfit / Result / Scan / Stage)")]
    [Tooltip("Shared click SFX for all Buttons in out-of-home nav scenes. Leave empty to use Resources/OutGameUiButtonClick.")]
    [SerializeField] private AudioClip uiButtonClickClip;
    [SerializeField] [Range(0f, 1f)] private float uiButtonClickVolume = 1f;

    [Header("Level Gauge Secret Debug Tap")]
    [Tooltip("On: rapidly tap LevelGauge to open DistanceDebugScene.")]
    [SerializeField] private bool enableLevelGaugeSecretDistanceDebugTap;
    [SerializeField] private int levelGaugeSecretTapCountRequired = 10;
    [SerializeField] private float levelGaugeSecretTapWindowSeconds = 2f;

    [Header("Level Gauge Slide-In (from left)")]
    [Tooltip("Home > LevelGauge. Auto-resolved from ExpSlotPivot parent when unset.")]
    [SerializeField] private RectTransform levelGaugeRect;
    [SerializeField] private float levelGaugeSlideInOffsetX = 1400f;
    [SerializeField] private float levelGaugeSlideInDuration = 0.6f;
    [SerializeField] [Range(1f, 10f)] private float levelGaugeSlideInEaseOutPower = 3f;
    [Tooltip("During LevelGauge slide-in, reveal EmptySlots one-by-one around the ring (FilledSlots stay hidden until gauge count-up).")]
    [SerializeField] private bool revealEmptySlotsDuringLevelGaugeSlide = true;
    [Tooltip("How long to light up all EmptySlots (independent of slide duration; increase if slots appear too fast).")]
    [SerializeField] private float levelGaugeEmptySlotRevealDuration = 1.5f;
    [Tooltip("Seconds after slide starts before the first EmptySlot appears.")]
    [SerializeField] private float levelGaugeEmptySlotRevealStartDelay = 0f;
    [Tooltip("Ease-out for EmptySlot reveal. Higher = more slots appear late (clearer one-by-one feel).")]
    [SerializeField] [Range(1f, 10f)] private float levelGaugeEmptySlotRevealEaseOutPower = 2.5f;

    [Header("Player Level & Experience")]
    [Tooltip("Optional. Max exp per level (Lv1–5). Applied to PlayerLevelManager on Home enter.")]
    [SerializeField] private PlayerLevelConfig playerLevelConfig;
    [Tooltip("On: use Manual Level/Exp below. Off: use saved progress from PlayerLevelManager (battle rewards, etc.).")]
    [SerializeField] private bool useManualLevelProgressForDebug;
    [SerializeField] private TMP_Text levelDisplayText;
    [SerializeField] private TMP_Text expDisplayText;
    [SerializeField] [Range(1, PlayerLevelManager.MaxLevel)] private int manualLevel = 1;
    [SerializeField] private int manualCurrentExp;

    [Header("Combat Stats (Attack / Max HP)")]
    [Tooltip("On: MainScene & Stage Ready UI use Manual Attack / Max HP below (ignores level + equipment totals).")]
    [SerializeField] private bool useManualCombatStatsForDebug;
    [SerializeField] [Min(0)] private int manualCombatAttack = 10;
    [SerializeField] [Min(1)] private int manualCombatMaxHp = 100;

    [Header("Stage Victory Rewards (per clear)")]
    [Tooltip("SuperRare / Rare / Normal 各ステージクリア時に付与する報酬数。MainScene 勝利時に適用。")]
    [SerializeField] private StageVictoryRewardSettings stageVictoryRewards = new();

    [Header("Current Battle Rewards (inventory)")]
    [Tooltip("On: use manual counts below instead of saved inventory.")]
    [SerializeField] private bool useManualBattleRewardsForDebug;
    [SerializeField] [Min(0)] private int manualSuperRareRewardCount;
    [SerializeField] [Min(0)] private int manualRareRewardCount;
    [SerializeField] [Min(0)] private int manualNormalRewardCount;

    [Header("Experience Bar (rotate around pivot)")]
    [Tooltip("Home > LevelGauge > ExpBarPivot (center of ring). Bar is a child of this object.")]
    [SerializeField] private RectTransform expBarPivot;
    [SerializeField] private ExpBarOrbitGizmo expBarOrbitGizmo;
    [SerializeField] private bool showExpBarOrbitGizmo = true;
    [Tooltip("Z rotation when current exp is 0.")]
    [SerializeField] private float expBarAngleAtZero = 40f;
    [Tooltip("Z rotation when current exp equals required exp.")]
    [SerializeField] private float expBarAngleAtFull = 175f;

    [Header("Experience Slot Ring")]
    [Tooltip("LevelGauge > ExpSlotPivot. Duplicate EmptySlot (+ child Slot if nested). Templates are hidden at runtime.")]
    [SerializeField] private RectTransform expSlotPivot;
    [SerializeField] private RectTransform emptySlotTemplate;
    [SerializeField] private RectTransform filledSlotTemplate;
    [SerializeField] private int expSlotCount = 27;
    [SerializeField] private float expSlotAngleStart = 112f;
    [SerializeField] private float expSlotAngleEnd = 238f;

    [Header("Interactive UI Rotation")]
    [SerializeField] private RectTransform rotatingUi;
    [SerializeField] private Canvas rotatingUiCanvas;
    [SerializeField] private float rotateDegreesPerSecond = 45f;
    [SerializeField] private bool rotateClockwise = true;
    [SerializeField] private float releaseSpeedPerWoundDegree = 2f;
    [SerializeField] private float maxReleaseAngularVelocity = 540f;
    [SerializeField] private float returnToBaseSpeed = 80f;
    [SerializeField] private float minDragDeltaDegrees = 0.25f;

    [Header("CD Entry (turntable from bottom-right)")]
    [SerializeField] private bool animateCdEntryOnHome = true;
    [Tooltip("Extra offset from rest position (+X = right, -Y = down).")]
    [SerializeField] private Vector2 cdEntryStartOffset = new(900f, -700f);
    [SerializeField] private float cdEntryDuration = 1.1f;
    [Tooltip("Total Z rotation (degrees) during entry. Sign follows Rotate Clockwise when enabled.")]
    [SerializeField] private float cdEntrySpinDegrees = 420f;
    [SerializeField] [Range(1f, 10f)] private float cdEntryEaseOutPower = 3f;

    [Header("Album (equipped CD visuals)")]
    [Tooltip("Home > Album. Shows StarterCD or BetterCD from equipped CD. Auto-resolved by name when unset.")]
    [SerializeField] private Transform homeAlbumRoot;
    [SerializeField] private bool animateAlbumEntryOnHome = true;
    [SerializeField] private UIRectSlideInEntryAnimator homeAlbumSlideInAnimator;
    [SerializeField] private float albumSlideInOffsetY = 800f;
    [SerializeField] private float albumSlideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] private float albumSlideInEaseOutPower = 3f;

    [Header("Character (outfit visuals)")]
    [Tooltip("Home > Character. Auto-resolved by name when unset.")]
    [SerializeField] private Transform homeCharacterRoot;
    [Tooltip("Active variant Image for fade-in. Auto-resolved from equipped Top/Bottom when unset.")]
    [SerializeField] private Image homeCharacterImage;
    [SerializeField] private bool animateCharacterFadeInOnHome = true;
    [SerializeField] private float characterFadeInDuration = 1.2f;
    [SerializeField] private float characterFadeInStartDelay = 0.2f;

    [Header("Home BGM (sync with rotating CD)")]
    [Tooltip("Pitch when the CD spins at the configured base speed.")]
    [SerializeField] private float pitchAtBaseSpin = 1f;
    [SerializeField] private float maxBgmPitch = 2.5f;
    [Tooltip("Spin speed (deg/s) at or below this pauses the track.")]
    [SerializeField] private float pauseSpinSpeedThreshold = 4f;
    [SerializeField] private float pitchSmoothTime = 0.12f;

    private float angularVelocity;
    private float dragAngularVelocityDegreesPerSecond;
    private float homeBgmPitch;
    private float homeBgmPitchVelocity;
    private float woundDegrees;
    private float lastPointerAngleDegrees;
    private bool isTouchingRotatingUi;
    private float nextDistanceUiRefreshTime;
    private float expBarPivotEulerX;
    private float expBarPivotEulerY;
    private bool expBarPivotBaseEulerCached;
    private readonly List<ExpSlotUnitInstance> expSlotUnits = new();
    private bool expSlotRingBuilt;
    private int expSlotRingBuiltCount;
    private float expSlotRingBuiltAngleStart;
    private float expSlotRingBuiltAngleEnd;
    private ExpSlotRingLayoutParameters expSlotRingCachedLayout;
    private bool expSlotRingLayoutCached;
    private bool isGaugeEntryAnimating;
    private Coroutine gaugeEntryAnimationCoroutine;
    private Coroutine experienceEntryAnimationCoroutine;
    private Coroutine popupDistanceEntryCoroutine;
    private Coroutine popupScaleEntryCoroutine;
    private Coroutine distanceGaugeOnBattleSlideCoroutine;
    private bool distanceGaugeOnBattlePendingSlideIn;
    private Coroutine homeEntryCoroutine;
    private Coroutine homeEntryNavSlideCoroutine;
    private Coroutine homeEntryLevelGaugeSlideCoroutine;
    private Coroutine homeEntryCdSlideCoroutine;
    private Coroutine homeEntryAlbumSlideCoroutine;
    private Coroutine homeEntryCharacterFadeCoroutine;
    private bool isRotatingUiEntryAnimating;
    private bool homeCharacterColorCached;
    private Color homeCharacterRestColor;
    private Transform homeCharacterRootResolved;
    private bool rotatingUiRestCached;
    private Vector2 rotatingUiRestAnchoredPosition;
    private float rotatingUiRestEulerZ;
    private bool navButtonRestPositionsCached;
    private Vector2 toScanButtonRestPosition;
    private Vector2 toOutfitButtonRestPosition;
    private Vector2 toCraftButtonRestPosition;
    private CanvasGroup toScanButtonCanvasGroup;
    private Transform toScanDistanceGaugeRoot;
    private Vector3 toScanButtonRestLocalScale = Vector3.one;
    private bool toScanButtonRestLocalScaleCached;
    private bool levelGaugeRestPositionCached;
    private Vector2 levelGaugeRestPosition;
    private HomeLevelGaugeSecretDebugTap levelGaugeSecretDebugTap;

    struct ExpSlotUnitInstance
    {
        public GameObject Root;
        public GameObject Empty;
        public GameObject Filled;
    }

    bool _navButtonClickHandling;
    bool _deferHudGaugeEntryUntilPopupDismissed;
    bool _playDistancePopupGateActive;
    string _playDistancePopupRefreshedForPendingDate;
    bool _firstDayAfterPopupGateActive;
    int _firstDayAfterPopupVisiblePanelIndex;
    GameObject[] _firstDayAfterPopupPanels;

    const string PlayDistancePopupShownDatePrefsKey = "OutGame_PlayDistancePopup_ShownDate";
    const string FirstDayAfterPopupCompletedPrefsKey = "OutGame_FirstDayAfterPopup_Completed";
    const float HomeNavButtonBlockedBackdropAlpha = 0.18f;

    static bool s_homeDistanceHandledThisPlaySession;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetHomeDistancePlaySessionFlag()
    {
        s_homeDistanceHandledThisPlaySession = false;
    }

    void Awake()
    {
        CacheExpBarPivotBaseEuler();
        EnsureNavButtonPressFeedback();
        CacheNavButtonRestPositions();
        CacheToScanButtonRestLocalScale();
        RefreshToScanButtonState();
        ResolveLevelGaugeRect();
        CacheLevelGaugeRestPosition();
        BindLevelGaugeSecretDebugTap();
        EnsureExperienceDisplayReferences();
        CacheRotatingUiRestState();
        ApplyHomeCharacterOutfitVisual();
        ResolveHomeCharacterImage();
        CacheHomeCharacterRestColor();
        EnsureHomeAlbumSlideInAnimator();
        PrepareHomeAlbumForEntry();
        EnsureNoisesAmountTextReference();
        EnsurePlayDistancePopupReferences();
        EnsureFirstDayAfterPopupReferences();
        ApplyUiButtonClickSoundSettings();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
#endif
    }

    void ApplyUiButtonClickSoundSettings()
    {
        OutGameUiButtonClickSound.Configure(uiButtonClickClip, uiButtonClickVolume);
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
#endif

        TearDownExpSlotRing();
        StopGaugeEntryAnimation();
        StopHomeEntrySequence();
    }

    void CacheExpBarPivotBaseEuler()
    {
        if (expBarPivot == null)
        {
            return;
        }

        Vector3 euler = expBarPivot.localEulerAngles;
        expBarPivotEulerX = euler.x;
        expBarPivotEulerY = euler.y;
        expBarPivotBaseEulerCached = true;
    }

    void Start()
    {
        BindHomeNavigationButtons();
        BindPlayDistancePopupUi();
        BindFirstDayAfterPopupUi();
        EnsureBattleCountdownReference();
        EnsureToScanButtonCanvasGroup();

        if (rotatingUi != null)
        {
            angularVelocity = GetBaseAngularVelocity();
        }

        SyncDailyDistanceScheduleConfig();
        RefreshDistanceLabels();
        RefreshToScanButtonState();
        if (!ShouldSuppressHudGaugeRefresh())
        {
            RefreshExperienceDisplay();
        }
    }

    void BindHomeNavigationButtons()
    {
        if (backToTitleButton != null)
        {
            backToTitleButton.onClick.RemoveListener(OnBackToTitleClicked);
            backToTitleButton.onClick.AddListener(OnBackToTitleClicked);
        }

        if (toDistanceDebugButton != null)
        {
            toDistanceDebugButton.onClick.RemoveListener(OnToDistanceDebugClicked);
            toDistanceDebugButton.onClick.AddListener(OnToDistanceDebugClicked);
        }

        if (toScanButton != null)
        {
            toScanButton.onClick.RemoveAllListeners();
            toScanButton.onClick.AddListener(OnToScanButtonClicked);
        }

        if (toOutfitButton != null)
        {
            toOutfitButton.onClick.RemoveAllListeners();
            toOutfitButton.onClick.AddListener(() =>
                OnNavButtonClicked(toOutfitButton, () =>
                {
                    OutfitSceneReturnContext.MarkFromHome();
                    SceneTransferManager.Instance.LoadNewScene(SceneNames.Outfit);
                }));
        }

        if (toCraftButton != null)
        {
            toCraftButton.onClick.RemoveAllListeners();
            toCraftButton.onClick.AddListener(() =>
                OnNavButtonClicked(toCraftButton, () =>
                    SceneTransferManager.Instance.LoadNewScene(SceneNames.Craft)));
        }

        EnsureNavButtonClickSound();
    }

    void EnsureNavButtonClickSound()
    {
        UIButtonClickSound.EnsureOnButton(toScanButton);
        UIButtonClickSound.EnsureOnButton(toOutfitButton);
        UIButtonClickSound.EnsureOnButton(toCraftButton);
    }

    void OnBackToTitleClicked()
    {
        if (IsHomeNavigationBlockedByPlayDistancePopup())
        {
            return;
        }

        SceneTransferManager.Instance.GoBack();
    }

    void OnToDistanceDebugClicked()
    {
        if (IsHomeNavigationBlockedByPlayDistancePopup())
        {
            return;
        }

        SceneTransferManager.Instance.LoadNewScene(SceneNames.DistanceDebug);
    }

    void OnToScanButtonClicked()
    {
        if (IsHomeNavigationBlockedByPlayDistancePopup() || !IsToScanAllowedByNoiseCount())
        {
            return;
        }

        string scanOrStageScene = OutGameDailyScanVisit.HasVisitedScanToday()
            ? SceneNames.Stage
            : SceneNames.Scan;
        OnNavButtonClicked(toScanButton, () =>
            SceneTransferManager.Instance.LoadNewScene(scanOrStageScene));
    }

    bool IsHomeNavigationBlockedByPlayDistancePopup()
    {
        return _playDistancePopupGateActive
            || IsPlayDistancePopupOpen()
            || _firstDayAfterPopupGateActive
            || IsFirstDayAfterPopupOpen();
    }

    void ApplyPlayDistancePopupHomeInputBlock(bool block)
    {
        if (block)
        {
            ApplyHomeNavButtonBlockedPresentation(backToTitleButton, true);
            ApplyHomeNavButtonBlockedPresentation(toDistanceDebugButton, true);
            ApplyHomeNavButtonBlockedPresentation(toOutfitButton, true);
            ApplyHomeNavButtonBlockedPresentation(toCraftButton, true);

            SetHomeButtonInteractable(backToTitleButton, false);
            SetHomeButtonInteractable(toDistanceDebugButton, false);
            SetHomeButtonInteractable(toOutfitButton, false);
            SetHomeButtonInteractable(toCraftButton, false);
        }
        else
        {
            RestoreHomeNavButtonVisuals();

            SetHomeButtonInteractable(backToTitleButton, true);
            SetHomeButtonInteractable(toDistanceDebugButton, true);
            SetHomeButtonInteractable(toOutfitButton, true);
            SetHomeButtonInteractable(toCraftButton, true);
        }

        if (toScanButton != null)
        {
            toScanButton.interactable = !block;
            EnsureToScanButtonCanvasGroup();
            if (toScanButtonCanvasGroup != null)
            {
                toScanButtonCanvasGroup.interactable = !block;
                toScanButtonCanvasGroup.blocksRaycasts = !block;
            }

            ApplyToScanButtonContentPresentation(scanAllowed: !block);
            if (block)
            {
                ForceNavButtonNoColorTint(toScanButton);
            }
            else
            {
                toScanButton.GetComponent<UIButtonPressFeedback>()
                    ?.RecacheRestVisualFromCurrent(preferButtonNormalColor: true);
            }
        }

        SetLevelGaugeRaycastBlocked(block);
    }

    void ApplyHomeNavButtonBlockedPresentation(Button button, bool blocked)
    {
        if (button == null)
        {
            return;
        }

        if (blocked)
        {
            ApplyNavButtonDimmedPresentation(button);
            return;
        }

        RestoreHomeNavButtonVisual(button);
    }

    static void ForceNavButtonNoColorTint(Button button)
    {
        if (button != null)
        {
            button.transition = Selectable.Transition.None;
        }
    }

    static void ApplyNavButtonNormalPresentation(Button button)
    {
        if (button == null)
        {
            return;
        }

        ForceNavButtonNoColorTint(button);

        Image[] images = button.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.sprite == null)
            {
                continue;
            }

            image.color = new Color(1f, 1f, 1f, 1f);
        }

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            text.color = new Color(1f, 1f, 1f, 1f);
            text.alpha = 1f;
        }

        UIButtonPressFeedback.RestoreNormalVisual(button);
    }

    static bool NavButtonLooksDimmed(Button button)
    {
        if (button == null)
        {
            return false;
        }

        Image[] images = button.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.sprite == null)
            {
                continue;
            }

            if (image.color.a < 0.99f)
            {
                return true;
            }
        }

        return false;
    }

    void RefreshSecondaryHomeNavButtonVisuals()
    {
        if (IsHomeNavigationBlockedByPlayDistancePopup())
        {
            return;
        }

        RefreshNavButtonVisualIfDimmed(toOutfitButton);
        RefreshNavButtonVisualIfDimmed(toCraftButton);
    }

    static void RefreshNavButtonVisualIfDimmed(Button button)
    {
        if (button == null || !NavButtonLooksDimmed(button))
        {
            return;
        }

        bool wasInteractable = button.interactable;
        ApplyNavButtonNormalPresentation(button);
        button.interactable = wasInteractable;
    }

    static void ApplyNavButtonDimmedPresentation(Button button)
    {
        if (button == null)
        {
            return;
        }

        ForceNavButtonNoColorTint(button);

        Image[] images = button.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.sprite == null)
            {
                continue;
            }

            image.color = new Color(1f, 1f, 1f, HomeNavButtonBlockedBackdropAlpha);
        }

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            text.color = new Color(1f, 1f, 1f, 1f);
            text.alpha = 1f;
        }
    }

    static void SetHomeButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    static void RestoreHomeNavButtonVisual(Button button)
    {
        if (button == null)
        {
            return;
        }

        ApplyNavButtonNormalPresentation(button);
        button.interactable = true;
    }

    void RestoreHomeNavButtonVisuals()
    {
        RestoreHomeNavButtonVisual(backToTitleButton);
        RestoreHomeNavButtonVisual(toDistanceDebugButton);
        RestoreHomeNavButtonVisual(toOutfitButton);
        RestoreHomeNavButtonVisual(toCraftButton);
    }

    void SetLevelGaugeRaycastBlocked(bool blocked)
    {
        if (levelGaugeRect == null)
        {
            return;
        }

        Image levelGaugeImage = levelGaugeRect.GetComponent<Image>();
        if (levelGaugeImage != null)
        {
            levelGaugeImage.raycastTarget = !blocked;
        }
    }

    void BindLevelGaugeSecretDebugTap()
    {
        ResolveLevelGaugeRect();
        if (levelGaugeRect == null)
        {
            return;
        }

        if (levelGaugeSecretDebugTap == null)
        {
            levelGaugeSecretDebugTap = levelGaugeRect.GetComponent<HomeLevelGaugeSecretDebugTap>();
            if (levelGaugeSecretDebugTap == null)
            {
                levelGaugeSecretDebugTap = levelGaugeRect.gameObject.AddComponent<HomeLevelGaugeSecretDebugTap>();
            }
        }

        EnsureLevelGaugeReceivesPointerClicks();

        levelGaugeSecretDebugTap.Configure(
            enableLevelGaugeSecretDistanceDebugTap,
            levelGaugeSecretTapCountRequired,
            levelGaugeSecretTapWindowSeconds,
            OnLevelGaugeSecretDistanceDebugTapTriggered);
    }

    void EnsureLevelGaugeReceivesPointerClicks()
    {
        if (levelGaugeRect == null)
        {
            return;
        }

        Image levelGaugeImage = levelGaugeRect.GetComponent<Image>();
        if (levelGaugeImage != null)
        {
            levelGaugeImage.raycastTarget = true;
        }
    }

    void OnLevelGaugeSecretDistanceDebugTapTriggered()
    {
        if (!enableLevelGaugeSecretDistanceDebugTap)
        {
            return;
        }

        OnToDistanceDebugClicked();
    }

    /// <summary>Inspector toggle change while playing.</summary>
    public void RefreshLevelGaugeSecretDebugTapBinding()
    {
        BindLevelGaugeSecretDebugTap();
    }

    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EquippedCdBgmManager.Instance?.SetHomePitchControlActive(true);

        CacheExpBarPivotBaseEuler();
        BindLevelGaugeSecretDebugTap();
        RefreshDistanceLabels();
        RefreshToScanButtonState();
        ApplyHomeCharacterOutfitVisual();

        if (OutfitLoadoutManager.Instance != null)
        {
            OutfitLoadoutManager.Instance.OnLoadoutChanged -= OnOutfitLoadoutChanged;
            OutfitLoadoutManager.Instance.OnLoadoutChanged += OnOutfitLoadoutChanged;
        }

        if (PlayerLevelManager.Instance != null)
        {
            if (playerLevelConfig != null)
            {
                PlayerLevelManager.Instance.ApplyConfig(playerLevelConfig);
            }

            PlayerLevelManager.Instance.OnProgressChanged -= OnPlayerLevelProgressChanged;
            PlayerLevelManager.Instance.OnProgressChanged += OnPlayerLevelProgressChanged;
            SyncManualProgressToPlayerLevelManager();
        }

        if (PlayerBattleRewardManager.Instance != null)
        {
            PlayerBattleRewardManager.Instance.ApplyVictoryRewardSettings(stageVictoryRewards);
            PlayerBattleRewardManager.Instance.OnRewardsChanged -= OnBattleRewardsChanged;
            PlayerBattleRewardManager.Instance.OnRewardsChanged += OnBattleRewardsChanged;
            if (!useManualBattleRewardsForDebug)
            {
                PullBattleRewardsFromManagerToInspector();
            }

            SyncManualBattleRewardsToManager();
        }

        SyncCombatStatsDebugOverride();
        HomeDailyDistanceSchedule.DailyRolloverApplied += OnDailyDistanceRolloverApplied;
        SyncDailyDistanceScheduleConfig();
        ConfigurePlayDistancePopupForScheduleOnEnter();
        TryPresentPlayDistancePopupOnHomeEnter();
        TryResumeFirstDayAfterPopupOnHomeEnter();

        if (!s_homeDistanceHandledThisPlaySession)
        {
            ResetDistanceBindingFromInspectorForPlayEnter();
            s_homeDistanceHandledThisPlaySession = true;
        }
        else
        {
            SyncDistanceGaugeSession();
        }

        StartHomeEntrySequence();
    }

    void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        HomeDailyDistanceSchedule.DailyRolloverApplied -= OnDailyDistanceRolloverApplied;
        SyncDistanceGaugeSession();

        if (OutfitLoadoutManager.Instance != null)
        {
            OutfitLoadoutManager.Instance.OnLoadoutChanged -= OnOutfitLoadoutChanged;
        }

        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnProgressChanged -= OnPlayerLevelProgressChanged;
        }

        if (PlayerBattleRewardManager.Instance != null)
        {
            PlayerBattleRewardManager.Instance.OnRewardsChanged -= OnBattleRewardsChanged;
        }

        EquippedCdBgmManager.Instance?.SetHomePitchControlActive(false);

        StopGaugeEntryAnimation();
        StopPopupDistanceEntryAnimation();
        StopDistanceGaugeOnBattleSlideIn();
        StopHomeEntrySequence();
        UnbindPlayDistancePopupUi();
        UnbindFirstDayAfterPopupUi();
        _playDistancePopupRefreshedForPendingDate = null;
        _playDistancePopupGateActive = false;
        _firstDayAfterPopupGateActive = false;
    }

    void OnOutfitLoadoutChanged(ItemType type)
    {
        if (type == ItemType.Weapon || type == ItemType.Top || type == ItemType.Bottom)
        {
            SyncCombatStatsDebugOverride();
        }

        if (type != ItemType.Top && type != ItemType.Bottom && type != ItemType.CD)
        {
            return;
        }

        ApplyHomeCharacterOutfitVisual();
        ResolveHomeCharacterImage();
        CacheHomeCharacterRestColor();
    }

    void OnPlayerLevelProgressChanged()
    {
        if (useManualLevelProgressForDebug)
        {
            return;
        }

        RefreshExperienceDisplay();
    }

    void Update()
    {
        RotateContinuousUi();
        UpdateHomeBgm();

        if (useDailyDistanceSchedule)
        {
            SyncDailyDistanceScheduleConfig();
            UpdateSchedulePlayDistancePopup();
        }

        if (Time.time < nextDistanceUiRefreshTime)
        {
            return;
        }

        nextDistanceUiRefreshTime = Time.time + distanceUiRefreshIntervalSeconds;
        if (!useDailyDistanceSchedule)
        {
            SyncDailyDistanceScheduleConfig();
        }

        RefreshDistanceDisplay();
        RefreshToScanButtonState();
        RefreshBattleCountdownDisplay();
        RefreshScheduleDebugInspectorFields();
    }

    bool IsFirstDayAfterPopupBlockingCdSpin()
    {
        return _firstDayAfterPopupGateActive || IsFirstDayAfterPopupOpen();
    }

    void RotateContinuousUi()
    {
        if (rotatingUi == null || isRotatingUiEntryAnimating)
        {
            return;
        }

        if (IsHomeNavigationBlockedByPlayDistancePopup())
        {
            if (isTouchingRotatingUi)
            {
                EndRotatingUiTouch();
            }

            angularVelocity = 0f;
            return;
        }

        if (UpdateRotatingUiFromPointer())
        {
            return;
        }

        float baseVelocity = GetBaseAngularVelocity();
        angularVelocity = Mathf.MoveTowards(angularVelocity, baseVelocity, returnToBaseSpeed * Time.deltaTime);
        rotatingUi.Rotate(0f, 0f, angularVelocity * Time.deltaTime);
    }

    /// <returns>タッチ中なら true（自転は停止、手動ドラッグのみ）</returns>
    bool UpdateRotatingUiFromPointer()
    {
        if (isRotatingUiEntryAnimating)
        {
            return false;
        }

        if (!TryGetPrimaryPointer(out Vector2 screenPosition, out bool isPressed, out bool beganPress))
        {
            EndRotatingUiTouch();
            return false;
        }

        if (beganPress && IsPointerOverRotatingUi(screenPosition))
        {
            BeginRotatingUiTouch(screenPosition);
        }

        if (!isPressed)
        {
            EndRotatingUiTouch();
            return false;
        }

        if (!isTouchingRotatingUi)
        {
            return false;
        }

        ApplyRotatingUiDrag(screenPosition);
        return true;
    }

    void BeginRotatingUiTouch(Vector2 screenPosition)
    {
        isTouchingRotatingUi = true;
        woundDegrees = 0f;
        angularVelocity = 0f;
        dragAngularVelocityDegreesPerSecond = 0f;
        lastPointerAngleDegrees = GetPointerAngleDegrees(screenPosition);
    }

    void EndRotatingUiTouch()
    {
        if (!isTouchingRotatingUi)
        {
            return;
        }

        isTouchingRotatingUi = false;
        dragAngularVelocityDegreesPerSecond = 0f;

        float baseVelocity = GetBaseAngularVelocity();
        float windSign = Mathf.Sign(baseVelocity);
        float releaseBonus = woundDegrees * releaseSpeedPerWoundDegree * windSign;
        angularVelocity = Mathf.Clamp(baseVelocity + releaseBonus, -maxReleaseAngularVelocity, maxReleaseAngularVelocity);
        woundDegrees = 0f;
    }

    void ApplyRotatingUiDrag(Vector2 screenPosition)
    {
        float angleDegrees = GetPointerAngleDegrees(screenPosition);
        float deltaDegrees = Mathf.DeltaAngle(lastPointerAngleDegrees, angleDegrees);
        lastPointerAngleDegrees = angleDegrees;

        if (Mathf.Abs(deltaDegrees) < minDragDeltaDegrees)
        {
            return;
        }

        rotatingUi.Rotate(0f, 0f, deltaDegrees);

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        dragAngularVelocityDegreesPerSecond = deltaDegrees / deltaTime;

        float windSign = Mathf.Sign(GetBaseAngularVelocity());
        float woundDelta = deltaDegrees * windSign;
        if (woundDelta > 0f)
        {
            woundDegrees += woundDelta;
        }
        else
        {
            woundDegrees = Mathf.Max(0f, woundDegrees + woundDelta);
        }
    }

    float GetBaseAngularVelocity()
    {
        float sign = rotateClockwise ? -1f : 1f;
        return sign * rotateDegreesPerSecond;
    }

    void UpdateHomeBgm()
    {
        if (!Application.isPlaying || EquippedCdBgmManager.Instance == null)
        {
            return;
        }

        if (IsFirstDayAfterPopupBlockingCdSpin())
        {
            homeBgmPitch = 0f;
            homeBgmPitchVelocity = 0f;
            EquippedCdBgmManager.Instance.ApplyHomeTurntablePitch(0f, pauseWhenIdle: true);
            return;
        }

        float baseSpinSpeed = Mathf.Max(Mathf.Abs(GetBaseAngularVelocity()), 1f);
        float spinSpeed = isTouchingRotatingUi
            ? Mathf.Abs(dragAngularVelocityDegreesPerSecond)
            : Mathf.Abs(angularVelocity);

        float targetPitch = spinSpeed <= pauseSpinSpeedThreshold
            ? 0f
            : Mathf.Clamp(spinSpeed / baseSpinSpeed * pitchAtBaseSpin, 0f, maxBgmPitch);

        if (targetPitch <= 0.001f)
        {
            homeBgmPitch = 0f;
            homeBgmPitchVelocity = 0f;
            EquippedCdBgmManager.Instance.ApplyHomeTurntablePitch(0f, pauseWhenIdle: true);
            return;
        }

        homeBgmPitch = Mathf.SmoothDamp(homeBgmPitch, targetPitch, ref homeBgmPitchVelocity, pitchSmoothTime);
        EquippedCdBgmManager.Instance.ApplyHomeTurntablePitch(homeBgmPitch, pauseWhenIdle: false);
    }

    Camera GetRotatingUiEventCamera()
    {
        if (rotatingUiCanvas != null && rotatingUiCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return rotatingUiCanvas.worldCamera;
        }

        return null;
    }

    Vector2 GetRotatingUiScreenCenter()
    {
        return RectTransformUtility.WorldToScreenPoint(GetRotatingUiEventCamera(), rotatingUi.position);
    }

    bool IsPointerOverRotatingUi(Vector2 screenPosition)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(
            rotatingUi,
            screenPosition,
            GetRotatingUiEventCamera());
    }

    float GetPointerAngleDegrees(Vector2 screenPosition)
    {
        Vector2 direction = screenPosition - GetRotatingUiScreenCenter();
        if (direction.sqrMagnitude < 64f)
        {
            return lastPointerAngleDegrees;
        }

        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    static bool TryGetPrimaryPointer(out Vector2 screenPosition, out bool isPressed, out bool beganPress)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            screenPosition = touch.position;
            isPressed = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            beganPress = touch.phase == TouchPhase.Began;
            return true;
        }

        screenPosition = Input.mousePosition;
        isPressed = Input.GetMouseButton(0);
        beganPress = Input.GetMouseButtonDown(0);
        return true;
    }

    void RefreshDistanceDisplay()
    {
        SyncDailyDistanceScheduleConfig();
        RefreshDistanceLabels();

        if (!ShouldSuppressHudGaugeRefresh())
        {
            RefreshGaugeDisplay();
        }
    }

    void SyncDailyDistanceScheduleConfig()
    {
        int year = Mathf.Clamp(scheduleDebugYear, 2000, 2100);
        int month = Mathf.Clamp(scheduleDebugMonth, 1, 12);
        int day = Mathf.Clamp(scheduleDebugDay, 1, DateTime.DaysInMonth(year, month));
        var debugTime = new DateTime(
            year,
            month,
            day,
            Mathf.Clamp(scheduleDebugHour, 0, 23),
            Mathf.Clamp(scheduleDebugMinute, 0, 59),
            0);

        HomeDailyDistanceSchedule.Configure(
            useDailyDistanceSchedule,
            schedulePlayStartHour,
            schedulePlayStartMinute,
            scheduleUseDebugTime && Application.isPlaying,
            debugTime,
            firstPlayGivenDistanceMeters);

        OutGameScanNoiseRevealCount.SyncHomeGaugeBinding(gaugeMetersPerCurrent, gaugeMax);
    }

    bool ShouldShowDistanceGaugeOnBattle()
    {
        return true;
    }

    bool IsFirstPlayHomeSession()
    {
        if (!useDailyDistanceSchedule)
        {
            return false;
        }

        return HomeDailyDistanceSchedule.HasGrantedFirstPlayDistance
            && !HomeDailyDistanceSchedule.HasEverAppliedRollover();
    }

    bool ShouldSlideInDistanceGaugeOnBattleAfterPopup()
    {
        return ShouldShowDistanceGaugeOnBattle() && !IsFirstPlayHomeSession();
    }

    void RefreshScheduleDebugInspectorFields()
    {
        if (!Application.isPlaying || !useDailyDistanceSchedule)
        {
            return;
        }

        scheduleDebugRolloverAppliedToday = HomeDailyDistanceSchedule.HasAppliedRolloverToday();
        scheduleDebugPlayMeters = LocationManager.GetTotalDistanceMeters();
        scheduleDebugBackgroundAccumulatedMeters = LocationManager.GetBackgroundAccumulatedMeters();
        scheduleDebugStoredPreviousDayMeters = HomeDailyDistanceSchedule.StoredPreviousDayDistanceMeters;
        scheduleDebugRecordedRealPreviousDayMeters =
            HomeDailyDistanceSchedule.RecordedRealPreviousDayDistanceMeters;
    }

    void RefreshToScanButtonState()
    {
        if (toScanButton == null)
        {
            return;
        }

        if (IsHomeNavigationBlockedByPlayDistancePopup())
        {
            ApplyPlayDistancePopupHomeInputBlock(true);
            return;
        }

        EnsureBattleCountdownReference();
        EnsureToScanButtonCanvasGroup();
        CacheToScanButtonRestLocalScale();

        if (navButtonRestPositionsCached && homeEntryNavSlideCoroutine == null)
        {
            SetNavButtonAnchoredPosition(toScanButton, toScanButtonRestPosition);
        }

        toScanButton.transform.localScale = toScanButtonRestLocalScale;
        toScanButton.gameObject.SetActive(true);

        bool scanAllowed = IsToScanAllowedByNoiseCount();
        toScanButton.interactable = scanAllowed;

        if (toScanButtonCanvasGroup != null)
        {
            toScanButtonCanvasGroup.alpha = 1f;
            toScanButtonCanvasGroup.interactable = scanAllowed;
            toScanButtonCanvasGroup.blocksRaycasts = scanAllowed;
        }

        ApplyToScanButtonContentPresentation(scanAllowed);
        SyncDistanceGaugeOnBattleOffScreenPlacement();

        if (scanAllowed)
        {
            toScanButton.GetComponent<UIButtonPressFeedback>()?.RecacheRestVisualFromCurrent(preferButtonNormalColor: true);
        }
        else
        {
            ForceNavButtonNoColorTint(toScanButton);
        }

        RefreshSecondaryHomeNavButtonVisuals();
        RefreshBattleCountdownDisplay();
    }

    void CacheToScanButtonRestLocalScale()
    {
        if (toScanButtonRestLocalScaleCached || toScanButton == null)
        {
            return;
        }

        toScanButtonRestLocalScale = toScanButton.transform.localScale;
        toScanButtonRestLocalScaleCached = true;
    }

    /// <summary>
    /// Scene defaults tint Ghost / gauge to ~(0.06, 0.18) which is effectively invisible.
    /// Brighten the ToScan button hierarchy; scan is always available (countdown is informational only).
    /// </summary>
    void ApplyToScanButtonContentPresentation(bool scanAllowed)
    {
        if (toScanButton == null)
        {
            return;
        }

        float backdropAlpha = scanAllowed ? 1f : HomeNavButtonBlockedBackdropAlpha;

        EnsureToScanDistanceGaugeRoot();

        bool showDistanceGaugeOnBattle = ShouldShowDistanceGaugeOnBattle();
        if (toScanDistanceGaugeRoot != null)
        {
            toScanDistanceGaugeRoot.gameObject.SetActive(showDistanceGaugeOnBattle);
        }

        if (showDistanceGaugeOnBattle)
        {
            ApplyDistanceGaugeOnBattleContentPresentation(scanAllowed);
        }

        Image[] images = toScanButton.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.sprite == null)
            {
                continue;
            }

            if (!showDistanceGaugeOnBattle
                && toScanDistanceGaugeRoot != null
                && image.transform.IsChildOf(toScanDistanceGaugeRoot))
            {
                continue;
            }

            bool isButtonBackdrop = image.gameObject == toScanButton.gameObject
                || (toScanDistanceGaugeRoot != null && image.gameObject == toScanDistanceGaugeRoot.gameObject);
            float alpha = isButtonBackdrop ? backdropAlpha : 1f;
            image.color = new Color(1f, 1f, 1f, alpha);
        }

        TMP_Text[] texts = toScanButton.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            if (!showDistanceGaugeOnBattle
                && toScanDistanceGaugeRoot != null
                && text.transform.IsChildOf(toScanDistanceGaugeRoot))
            {
                continue;
            }

            text.color = new Color(1f, 1f, 1f, 1f);
            text.alpha = 1f;
        }

        if (battleCountdownText != null)
        {
            battleCountdownText.alpha = 1f;
        }
    }

    /// <summary>
    /// DistanceGaugeOnBattle is no longer under ToScanButton; mirror the same alpha rules locally.
    /// </summary>
    void ApplyDistanceGaugeOnBattleContentPresentation(bool scanAllowed)
    {
        EnsureToScanDistanceGaugeRoot();
        if (toScanDistanceGaugeRoot == null)
        {
            return;
        }

        float backdropAlpha = scanAllowed ? 1f : HomeNavButtonBlockedBackdropAlpha;

        Image[] images = toScanDistanceGaugeRoot.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.sprite == null)
            {
                continue;
            }

            bool isPanelBackdrop = image.gameObject == toScanDistanceGaugeRoot.gameObject;
            float alpha = isPanelBackdrop ? backdropAlpha : 1f;
            image.color = new Color(1f, 1f, 1f, alpha);
        }

        TMP_Text[] texts = toScanDistanceGaugeRoot.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            text.color = new Color(1f, 1f, 1f, 1f);
            text.alpha = 1f;
        }
    }

    void EnsureToScanButtonCanvasGroup()
    {
        if (toScanButton == null)
        {
            return;
        }

        if (toScanButtonCanvasGroup == null)
        {
            toScanButtonCanvasGroup = toScanButton.GetComponent<CanvasGroup>();
            if (toScanButtonCanvasGroup == null)
            {
                toScanButtonCanvasGroup = toScanButton.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    void EnsureBattleCountdownReference()
    {
        if (battleCountdownText != null)
        {
            return;
        }

        if (toScanButton != null)
        {
            Transform countdownTransform = toScanButton.transform.Find("Countdown");
            if (countdownTransform != null)
            {
                battleCountdownText = countdownTransform.GetComponent<TMP_Text>();
                if (battleCountdownText != null)
                {
                    return;
                }
            }
        }

        GameObject countdownObject = FindSceneObjectIncludingInactive("Countdown");
        if (countdownObject != null)
        {
            battleCountdownText = countdownObject.GetComponent<TMP_Text>();
        }
    }

    void RefreshBattleCountdownDisplay()
    {
        EnsureBattleCountdownReference();
        if (battleCountdownText == null)
        {
            return;
        }

        if (!useDailyDistanceSchedule)
        {
            battleCountdownText.gameObject.SetActive(false);
            return;
        }

        if (!HomeDailyDistanceSchedule.TryGetCountdownToNextRollover(out TimeSpan remaining))
        {
            battleCountdownText.gameObject.SetActive(false);
            return;
        }

        battleCountdownText.gameObject.SetActive(true);
        battleCountdownText.raycastTarget = false;
        battleCountdownText.text = FormatRolloverCountdown(remaining);
    }

    static string FormatRolloverCountdown(TimeSpan remaining)
    {
        remaining = remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        int totalHours = Mathf.FloorToInt((float)remaining.TotalHours);
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:D2}:{1:D2}:{2:D2}",
            totalHours,
            remaining.Minutes,
            remaining.Seconds);
    }

    void RefreshDistanceLabels()
    {
        float meters = GetBoundGaugeDistanceMeters();
        int noiseFormed = LocationManager.GetThousandMeterBonusCount();

        if (distanceDisplayText != null)
        {
            distanceDisplayText.text = $"Distance Moved : {meters:F1} m";
        }

        if (noiseAmountDisplayText != null)
        {
            noiseAmountDisplayText.text = $"Noise Formed : {noiseFormed}";
        }
    }

    float GetBoundGaugeDistanceMeters()
    {
        return OutGameScanNoiseRevealCount.GetResolvedTotalDistanceMeters();
    }

    int GetBoundNoiseCount()
    {
        return OutGameScanNoiseRevealCount.ComputeNoiseCountFromDistance(
            GetBoundGaugeDistanceMeters(),
            gaugeMetersPerCurrent,
            gaugeMax);
    }

    bool IsToScanAllowedByNoiseCount()
    {
        return GetBoundNoiseCount() > 0;
    }

    float GetBoundGaugeFillRatio()
    {
        return OutGameScanNoiseRevealCount.GetGaugeFillRatio(
            GetBoundGaugeDistanceMeters(),
            gaugeMetersPerCurrent,
            gaugeMax);
    }

    void EnsureNoisesAmountTextReference()
    {
        if (noisesAmountText == null && gaugeValueText != null)
        {
            noisesAmountText = gaugeValueText;
        }
    }

    TMP_Text ResolveNoisesAmountText()
    {
        if (noisesAmountText != null)
        {
            return noisesAmountText;
        }

        return gaugeValueText;
    }

    void SyncCombatStatsDebugOverride()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        PlayerCombatStatsResolver.SetDebugOverride(
            useManualCombatStatsForDebug,
            manualCombatAttack,
            manualCombatMaxHp);
    }

    /// <summary>Inspector / Editor: push manual combat stats override to runtime resolver.</summary>
    public void RefreshCombatStatsBinding()
    {
        SyncCombatStatsDebugOverride();
    }

    void OnBattleRewardsChanged()
    {
        if (!useManualBattleRewardsForDebug)
        {
            PullBattleRewardsFromManagerToInspector();
        }
    }

    void SyncManualBattleRewardsToManager()
    {
        if (!Application.isPlaying || !useManualBattleRewardsForDebug)
        {
            return;
        }

        if (PlayerBattleRewardManager.Instance == null)
        {
            return;
        }

        PlayerBattleRewardManager manager = PlayerBattleRewardManager.Instance;
        int superRare = Mathf.Max(0, manualSuperRareRewardCount);
        int rare = Mathf.Max(0, manualRareRewardCount);
        int normal = Mathf.Max(0, manualNormalRewardCount);
        if (manager.SuperRareCount == superRare
            && manager.RareCount == rare
            && manager.NormalCount == normal)
        {
            return;
        }

        manager.SetCounts(superRare, rare, normal);
    }

    void PullBattleRewardsFromManagerToInspector()
    {
        if (PlayerBattleRewardManager.Instance == null)
        {
            return;
        }

        PlayerBattleRewardManager manager = PlayerBattleRewardManager.Instance;
        manualSuperRareRewardCount = manager.SuperRareCount;
        manualRareRewardCount = manager.RareCount;
        manualNormalRewardCount = manager.NormalCount;
    }

    /// <summary>Inspector / Editor: push victory table + manual inventory to runtime.</summary>
    public void RefreshBattleRewardsBinding()
    {
        if (PlayerBattleRewardManager.Instance != null)
        {
            PlayerBattleRewardManager.Instance.ApplyVictoryRewardSettings(stageVictoryRewards);
        }

        SyncManualBattleRewardsToManager();
    }

    /// <summary>Inspector / Editor: copy saved inventory into manual fields.</summary>
    public void CopySavedBattleRewardsToManual()
    {
        useManualBattleRewardsForDebug = false;
        PullBattleRewardsFromManagerToInspector();
        useManualBattleRewardsForDebug = true;
        RefreshBattleRewardsBinding();
    }

    /// <summary>Inspector / Editor: copy level + equipment totals into manual combat fields.</summary>
    public void CopyResolvedCombatStatsToManual()
    {
        useManualCombatStatsForDebug = false;
        SyncCombatStatsDebugOverride();

        PlayerCombatStats stats = PlayerCombatStatsResolver.ResolveCurrent();
        manualCombatAttack = stats.Attack;
        manualCombatMaxHp = stats.MaxHp;
        useManualCombatStatsForDebug = true;
        SyncCombatStatsDebugOverride();
    }

    /// <summary>Inspector 修改 → 共享 gauge 规则 + Scan/Stage。</summary>
    void PushInspectorDistanceToSession()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SyncDailyDistanceScheduleConfig();
        OutGameScanNoiseRevealCount.SyncHomeGaugeBinding(gaugeMetersPerCurrent, gaugeMax);
    }

    /// <summary>本次 Play 首次进入 Home：清 Stage 击败记录并刷新 UI。</summary>
    void ResetDistanceBindingFromInspectorForPlayEnter()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SyncDailyDistanceScheduleConfig();
        OutGameScanNoiseRevealCount.SyncHomeGaugeBinding(gaugeMetersPerCurrent, gaugeMax);
        StageDefeatedNoiseRegistry.ClearAll();

        if (!ShouldSuppressHudGaugeRefresh())
        {
            RefreshGaugeDisplay();
        }

        RefreshDistanceLabels();
    }

    void SyncDistanceGaugeSession()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        PushInspectorDistanceToSession();

        if (!ShouldSuppressHudGaugeRefresh())
        {
            RefreshGaugeDisplay();
        }
    }

    /// <summary>Inspector 预览：同步每日时段配置（Editor 用）。</summary>
    public void SyncDailyDistanceScheduleConfigForEditor()
    {
        SyncDailyDistanceScheduleConfig();
        RefreshScheduleDebugInspectorFields();
        RefreshToScanButtonState();
    }

    /// <summary>Inspector 数值变更时：推到游戏并刷新 UI（编辑模式 / Play 均可用）。</summary>
    public void ApplyInspectorDistanceToGame()
    {
        if (Application.isPlaying)
        {
            PushInspectorDistanceToSession();
        }

        EnsureNoisesAmountTextReference();
        RefreshDistanceLabels();
        RefreshToScanButtonState();
        if (!ShouldSuppressHudGaugeRefresh())
        {
            RefreshGaugeDisplay();
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            MarkGaugeUiDirtyInEditor();
        }
#endif
    }

#if UNITY_EDITOR
    void MarkGaugeUiDirtyInEditor()
    {
        UnityEditor.EditorUtility.SetDirty(this);
        if (totalDistanceDisplayText != null)
        {
            UnityEditor.EditorUtility.SetDirty(totalDistanceDisplayText);
        }

        TMP_Text noisesLabel = ResolveNoisesAmountText();
        if (noisesLabel != null)
        {
            UnityEditor.EditorUtility.SetDirty(noisesLabel);
        }

        if (gaugeFillImage != null)
        {
            UnityEditor.EditorUtility.SetDirty(gaugeFillImage);
        }

        if (gameObject.scene.IsValid())
        {
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }
#endif

    static string FormatDistanceMeters(float meters)
    {
        long displayMeters = (long)Mathf.Floor(Mathf.Max(0f, meters));
        return string.Format(CultureInfo.InvariantCulture, "{0:N0}", displayMeters);
    }

    void EnsurePlayDistancePopupReferences()
    {
        if (playDistancePopupRoot == null)
        {
            GameObject popupObject = GameObject.Find("Popup");
            if (popupObject == null)
            {
                popupObject = FindSceneObjectIncludingInactive("Popup");
            }

            if (popupObject != null)
            {
                playDistancePopupRoot = popupObject;
            }
        }

        if (playDistancePopupExitButton == null && playDistancePopupRoot != null)
        {
            Transform exitTransform = playDistancePopupRoot.transform.Find("ExitButton");
            if (exitTransform != null)
            {
                playDistancePopupExitButton = exitTransform.GetComponent<Button>();
            }
        }

        if (totalDistanceDisplayText == null && playDistancePopupRoot != null)
        {
            Transform distanceTransform = playDistancePopupRoot.transform.Find("DistanceTravelled");
            if (distanceTransform != null)
            {
                totalDistanceDisplayText = distanceTransform.GetComponent<TMP_Text>();
            }
        }

        EnsurePlayDistancePopupScaleInAnimator();
    }

    void EnsurePlayDistancePopupScaleInAnimator()
    {
        if (playDistancePopupRoot == null)
        {
            return;
        }

        RectTransform rect = playDistancePopupRoot.transform as RectTransform;
        if (rect == null)
        {
            return;
        }

        if (playDistancePopupScaleInAnimator == null)
        {
            playDistancePopupScaleInAnimator = rect.GetComponent<UIRectScaleInAnimator>();
        }

        if (playDistancePopupScaleInAnimator == null)
        {
            playDistancePopupScaleInAnimator = playDistancePopupRoot.AddComponent<UIRectScaleInAnimator>();
        }

        playDistancePopupScaleInAnimator.SetTarget(rect, "Popup");
        playDistancePopupScaleInAnimator.Configure(
            playDistancePopupScaleInDuration,
            playDistancePopupScaleInEaseOutPower);
    }

    void BindPlayDistancePopupUi()
    {
        EnsurePlayDistancePopupReferences();

        if (playDistancePopupExitButton != null)
        {
            EnsureButtonPressFeedback(playDistancePopupExitButton);
            playDistancePopupExitButton.onClick.RemoveListener(OnPlayDistancePopupExitClicked);
            playDistancePopupExitButton.onClick.AddListener(OnPlayDistancePopupExitClicked);
        }
    }

    void UnbindPlayDistancePopupUi()
    {
        if (playDistancePopupExitButton != null)
        {
            playDistancePopupExitButton.onClick.RemoveListener(OnPlayDistancePopupExitClicked);
        }
    }

    void ConfigurePlayDistancePopupForScheduleOnEnter()
    {
        EnsurePlayDistancePopupReferences();

        if (!useDailyDistanceSchedule)
        {
            PrepareGaugesForEntryAfterNavButtons();
            return;
        }

        HidePlayDistancePopup();
        ResetPopupDistanceDisplayToZero();

        if (HomeDailyDistanceSchedule.IsPlayDistancePopupPendingForToday())
        {
            ClearPlayDistancePopupShownDate();
        }

        SyncPlayDistancePopupGateForEnter();
        _deferHudGaugeEntryUntilPopupDismissed = _playDistancePopupGateActive;
        distanceGaugeOnBattlePendingSlideIn = _deferHudGaugeEntryUntilPopupDismissed
            && ShouldSlideInDistanceGaugeOnBattleAfterPopup();
        if (distanceGaugeOnBattlePendingSlideIn)
        {
            PrepareDistanceGaugeOnBattleOffScreen();
        }

        PrepareGaugesForEntryAfterNavButtons();
    }

    void SyncPlayDistancePopupGateForEnter()
    {
        _playDistancePopupGateActive = useDailyDistanceSchedule && ShouldOfferPlayDistancePopupToday();
        if (_playDistancePopupGateActive)
        {
            ApplyPlayDistancePopupHomeInputBlock(true);
        }
    }

    void TryPresentPlayDistancePopupOnHomeEnter()
    {
        if (!_playDistancePopupGateActive)
        {
            return;
        }

        TryOpenPlayDistancePopupWithAnimation();
    }

    void ReleasePlayDistancePopupGate()
    {
        _playDistancePopupGateActive = false;
        ApplyPlayDistancePopupHomeInputBlock(false);
        RefreshToScanButtonState();
        RefreshSecondaryHomeNavButtonVisuals();
    }

    bool ShouldStartBattleDistanceGaugeEntryOnHomeEntry()
    {
        return !useDailyDistanceSchedule || !_deferHudGaugeEntryUntilPopupDismissed;
    }

    bool ShouldSuppressBattleDistanceGaugeRefresh()
    {
        return isGaugeEntryAnimating
            || distanceGaugeOnBattlePendingSlideIn
            || distanceGaugeOnBattleSlideCoroutine != null
            || _deferHudGaugeEntryUntilPopupDismissed;
    }

    bool ShouldSuppressHudGaugeRefresh()
    {
        return ShouldSuppressBattleDistanceGaugeRefresh();
    }

    bool ShouldSuppressExperienceDisplayRefresh()
    {
        return experienceEntryAnimationCoroutine != null;
    }

    bool ShouldOfferPlayDistancePopupToday()
    {
        if (!useDailyDistanceSchedule || HasShownPlayDistancePopupToday())
        {
            return false;
        }

        if (HomeDailyDistanceSchedule.IsPlayDistancePopupPendingForToday())
        {
            return true;
        }

        if (HomeDailyDistanceSchedule.HasAppliedRolloverToday())
        {
            return true;
        }

        // Day 1: first play grant, before the account's first rollover.
        return HomeDailyDistanceSchedule.HasGrantedFirstPlayDistance
            && !HomeDailyDistanceSchedule.HasEverAppliedRollover();
    }

    bool ShouldDeferBattleGaugeUntilPopupDismissed()
    {
        return ShouldOfferPlayDistancePopupToday();
    }

    void PrepareBattleGaugeForPopupEntry()
    {
        if (gaugeEntryAnimationCoroutine != null)
        {
            StopCoroutine(gaugeEntryAnimationCoroutine);
            gaugeEntryAnimationCoroutine = null;
        }

        isGaugeEntryAnimating = false;
        ApplyGaugeDisplayVisuals(0f, 0, 0f, updateDistanceText: false);
    }

    static string GetPlayDistancePopupDateKey(DateTime? optionalNow = null)
    {
        DateTime now = optionalNow ?? HomeDailyDistanceSchedule.GetNow();
        return now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    static bool HasShownPlayDistancePopupToday()
    {
        string shownDate = PlayerPrefs.GetString(PlayDistancePopupShownDatePrefsKey, string.Empty);
        return shownDate == GetPlayDistancePopupDateKey();
    }

    static void MarkPlayDistancePopupShownToday()
    {
        PlayerPrefs.SetString(PlayDistancePopupShownDatePrefsKey, GetPlayDistancePopupDateKey());
        HomeDailyDistanceSchedule.ClearPlayDistancePopupPendingForToday();
        PlayerPrefs.Save();
    }

    static void ClearPlayDistancePopupShownDate()
    {
        if (!PlayerPrefs.HasKey(PlayDistancePopupShownDatePrefsKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(PlayDistancePopupShownDatePrefsKey);
        PlayerPrefs.Save();
    }

    void UpdateSchedulePlayDistancePopup()
    {
        if (!useDailyDistanceSchedule || !Application.isPlaying)
        {
            return;
        }

        if (!ShouldOfferPlayDistancePopupToday())
        {
            return;
        }

        if (IsPlayDistancePopupOpen() || IsPlayDistancePopupEntryAnimating())
        {
            return;
        }

        TryOpenPlayDistancePopupWithAnimation();
    }

    bool IsPlayDistancePopupEntryAnimating()
    {
        return popupScaleEntryCoroutine != null || popupDistanceEntryCoroutine != null;
    }

    static GameObject FindSceneObjectIncludingInactive(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (transform == null || transform.name != objectName)
            {
                continue;
            }

            GameObject candidate = transform.gameObject;
            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
            {
                continue;
            }

            if ((transform.hideFlags & HideFlags.HideInHierarchy) != 0)
            {
                continue;
            }

            return transform.gameObject;
        }

        return null;
    }

    void OnDailyDistanceRolloverApplied()
    {
        // Rollover popup is separate from day-1 popup; allow showing again even on the same calendar day.
        ClearPlayDistancePopupShownDate();
        _playDistancePopupGateActive = ShouldOfferPlayDistancePopupToday();
        _deferHudGaugeEntryUntilPopupDismissed = _playDistancePopupGateActive;

        if (_playDistancePopupGateActive)
        {
            ApplyPlayDistancePopupHomeInputBlock(true);
            PrepareBattleGaugeForPopupEntry();
        }

        if (!TryRefreshOpenPlayDistancePopupForRollover())
        {
            TryOpenPlayDistancePopupWithAnimation();
        }

        RefreshToScanButtonState();
        RefreshDistanceLabels();
    }

    bool IsPlayDistancePopupOpen()
    {
        return playDistancePopupRoot != null && playDistancePopupRoot.activeSelf;
    }

    /// <summary>
    /// When rollover happens while yesterday's popup is still open, re-run distance entry for the new day.
    /// </summary>
    bool TryRefreshOpenPlayDistancePopupForRollover()
    {
        if (!IsPlayDistancePopupOpen()
            || !HomeDailyDistanceSchedule.IsPlayDistancePopupPendingForToday())
        {
            return false;
        }

        string todayKey = GetPlayDistancePopupDateKey();
        if (_playDistancePopupRefreshedForPendingDate == todayKey)
        {
            return true;
        }

        RefreshOpenPlayDistancePopupForNewScheduleDay();
        _playDistancePopupRefreshedForPendingDate = todayKey;
        return true;
    }

    void RefreshOpenPlayDistancePopupForNewScheduleDay()
    {
        EnsurePlayDistancePopupReferences();
        if (playDistancePopupRoot == null)
        {
            return;
        }

        _deferHudGaugeEntryUntilPopupDismissed = true;
        PrepareBattleGaugeForPopupEntry();
        distanceGaugeOnBattlePendingSlideIn = ShouldSlideInDistanceGaugeOnBattleAfterPopup();
        if (distanceGaugeOnBattlePendingSlideIn)
        {
            PrepareDistanceGaugeOnBattleOffScreen();
        }

        playDistancePopupScaleInAnimator?.SnapToRest();
        StartPopupDistanceContentAnimation();
    }

    void TryOpenPlayDistancePopupWithAnimation()
    {
        if (!ShouldOfferPlayDistancePopupToday())
        {
            return;
        }

        EnsurePlayDistancePopupReferences();
        if (playDistancePopupRoot == null)
        {
            Debug.LogWarning(
                "HomeSceneManager: Play distance popup root not found. Assign Popup on HomeSceneManager or add a root named Popup.");
            ReleasePlayDistancePopupGate();
            return;
        }

        if (IsPlayDistancePopupOpen() || IsPlayDistancePopupEntryAnimating())
        {
            return;
        }

        _deferHudGaugeEntryUntilPopupDismissed = true;
        PrepareBattleGaugeForPopupEntry();
        distanceGaugeOnBattlePendingSlideIn = ShouldSlideInDistanceGaugeOnBattleAfterPopup();
        if (distanceGaugeOnBattlePendingSlideIn)
        {
            PrepareDistanceGaugeOnBattleOffScreen();
        }

        _playDistancePopupGateActive = true;
        ApplyPlayDistancePopupHomeInputBlock(true);
        ShowPlayDistancePopup();
        StartPopupDistanceEntryAnimation();
    }

    void ShowPlayDistancePopup()
    {
        EnsurePlayDistancePopupReferences();

        if (playDistancePopupRoot != null)
        {
            playDistancePopupRoot.SetActive(true);
            playDistancePopupRoot.transform.SetAsLastSibling();
            EnsurePlayDistancePopupScaleInAnimator();

            RectTransform popupRect = playDistancePopupRoot.transform as RectTransform;
            if (popupRect != null)
            {
                popupRect.localScale = Vector3.zero;
            }
        }
    }

    void HidePlayDistancePopup()
    {
        playDistancePopupScaleInAnimator?.SnapToRest();

        if (playDistancePopupRoot != null)
        {
            playDistancePopupRoot.SetActive(false);
        }
    }

    void OnPlayDistancePopupExitClicked()
    {
        MarkPlayDistancePopupShownToday();
        _playDistancePopupRefreshedForPendingDate = null;
        HidePlayDistancePopup();
        ReleasePlayDistancePopupGate();

        if (ShouldOfferFirstDayAfterPopup())
        {
            TryPresentFirstDayAfterPopup();
        }
        else
        {
            _deferHudGaugeEntryUntilPopupDismissed = false;
            StartPostPopupDistanceGaugeOnBattleSequence();
        }
    }

    static bool HasCompletedFirstDayAfterPopup()
    {
        return PlayerPrefs.GetInt(FirstDayAfterPopupCompletedPrefsKey, 0) == 1;
    }

    static void MarkFirstDayAfterPopupCompleted()
    {
        PlayerPrefs.SetInt(FirstDayAfterPopupCompletedPrefsKey, 1);
        PlayerPrefs.Save();
    }

    bool ShouldOfferFirstDayAfterPopup()
    {
        if (HasCompletedFirstDayAfterPopup() || !useDailyDistanceSchedule)
        {
            return false;
        }

        return HomeDailyDistanceSchedule.HasGrantedFirstPlayDistance
            && !HomeDailyDistanceSchedule.HasEverAppliedRollover();
    }

    bool IsFirstDayAfterPopupOpen()
    {
        return firstDayAfterPopupRoot != null && firstDayAfterPopupRoot.activeInHierarchy;
    }

    void TryResumeFirstDayAfterPopupOnHomeEnter()
    {
        if (_playDistancePopupGateActive || IsPlayDistancePopupOpen())
        {
            return;
        }

        if (!ShouldOfferFirstDayAfterPopup() || !HasShownPlayDistancePopupToday())
        {
            return;
        }

        TryPresentFirstDayAfterPopup();
    }

    void EnsureFirstDayAfterPopupReferences()
    {
        if (firstDayAfterPopupRoot == null)
        {
            GameObject popupObject = FindSceneObjectIncludingInactive("FirstDayAfterPopup");
            if (popupObject != null)
            {
                firstDayAfterPopupRoot = popupObject;
            }
        }

        if (firstDayAfterPopupBackgroundButton == null && firstDayAfterPopupRoot != null)
        {
            Transform backgroundTransform = firstDayAfterPopupRoot.transform.Find("Background");
            if (backgroundTransform != null)
            {
                firstDayAfterPopupBackgroundButton = backgroundTransform.GetComponent<Button>();
                if (firstDayAfterPopupBackgroundButton == null)
                {
                    firstDayAfterPopupBackgroundButton = backgroundTransform.gameObject.AddComponent<Button>();
                }

                firstDayAfterPopupBackgroundButton.transition = Selectable.Transition.None;
            }
        }

        EnsureFirstDayAfterPopupPanelCache();
    }

    void EnsureFirstDayAfterPopupPanelCache()
    {
        if (_firstDayAfterPopupPanels != null && _firstDayAfterPopupPanels.Length == 5)
        {
            return;
        }

        if (firstDayAfterPopupRoot == null)
        {
            return;
        }

        _firstDayAfterPopupPanels = new GameObject[5];
        for (int panelIndex = 1; panelIndex <= 5; panelIndex++)
        {
            Transform panelTransform = firstDayAfterPopupRoot.transform.Find($"Panel{panelIndex}");
            if (panelTransform != null)
            {
                _firstDayAfterPopupPanels[panelIndex - 1] = panelTransform.gameObject;
            }
        }
    }

    void BindFirstDayAfterPopupUi()
    {
        EnsureFirstDayAfterPopupReferences();

        if (firstDayAfterPopupBackgroundButton != null)
        {
            firstDayAfterPopupBackgroundButton.onClick.RemoveListener(OnFirstDayAfterPopupBackgroundClicked);
            firstDayAfterPopupBackgroundButton.onClick.AddListener(OnFirstDayAfterPopupBackgroundClicked);
        }
    }

    void UnbindFirstDayAfterPopupUi()
    {
        if (firstDayAfterPopupBackgroundButton != null)
        {
            firstDayAfterPopupBackgroundButton.onClick.RemoveListener(OnFirstDayAfterPopupBackgroundClicked);
        }
    }

    void TryPresentFirstDayAfterPopup()
    {
        EnsureFirstDayAfterPopupReferences();
        if (firstDayAfterPopupRoot == null)
        {
            return;
        }

        _firstDayAfterPopupGateActive = true;
        _firstDayAfterPopupVisiblePanelIndex = 1;
        _deferHudGaugeEntryUntilPopupDismissed = true;
        distanceGaugeOnBattlePendingSlideIn = ShouldSlideInDistanceGaugeOnBattleAfterPopup();
        if (distanceGaugeOnBattlePendingSlideIn)
        {
            PrepareDistanceGaugeOnBattleOffScreen();
        }

        ApplyPlayDistancePopupHomeInputBlock(true);
        ConfigureFirstDayAfterPopupPanelRaycasts(false);
        firstDayAfterPopupRoot.SetActive(true);
        firstDayAfterPopupRoot.transform.SetAsLastSibling();
        SetFirstDayAfterPopupPanelVisible(_firstDayAfterPopupVisiblePanelIndex);
    }

    void ConfigureFirstDayAfterPopupPanelRaycasts(bool panelsBlockRaycasts)
    {
        EnsureFirstDayAfterPopupPanelCache();
        if (_firstDayAfterPopupPanels == null)
        {
            return;
        }

        for (int i = 0; i < _firstDayAfterPopupPanels.Length; i++)
        {
            SetFirstDayAfterPopupPanelRaycasts(_firstDayAfterPopupPanels[i], panelsBlockRaycasts);
        }
    }

    static void SetFirstDayAfterPopupPanelRaycasts(GameObject panel, bool blockRaycasts)
    {
        if (panel == null)
        {
            return;
        }

        Graphic[] graphics = panel.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            graphics[i].raycastTarget = blockRaycasts;
        }
    }

    void SetFirstDayAfterPopupPanelVisible(int panelIndex)
    {
        EnsureFirstDayAfterPopupPanelCache();
        if (_firstDayAfterPopupPanels == null)
        {
            return;
        }

        for (int i = 0; i < _firstDayAfterPopupPanels.Length; i++)
        {
            GameObject panel = _firstDayAfterPopupPanels[i];
            if (panel != null)
            {
                panel.SetActive(i == panelIndex - 1);
            }
        }
    }

    void OnFirstDayAfterPopupBackgroundClicked()
    {
        if (!IsFirstDayAfterPopupOpen())
        {
            return;
        }

        if (_firstDayAfterPopupVisiblePanelIndex < 5)
        {
            _firstDayAfterPopupVisiblePanelIndex++;
            SetFirstDayAfterPopupPanelVisible(_firstDayAfterPopupVisiblePanelIndex);
            return;
        }

        MarkFirstDayAfterPopupCompleted();
        DismissFirstDayAfterPopup();
    }

    void DismissFirstDayAfterPopup()
    {
        _firstDayAfterPopupGateActive = false;
        _deferHudGaugeEntryUntilPopupDismissed = false;
        HideFirstDayAfterPopup();
        ApplyPlayDistancePopupHomeInputBlock(false);
        RefreshToScanButtonState();
        RefreshSecondaryHomeNavButtonVisuals();
        StartPostPopupDistanceGaugeOnBattleSequence();
    }

    void HideFirstDayAfterPopup()
    {
        if (firstDayAfterPopupRoot != null)
        {
            firstDayAfterPopupRoot.SetActive(false);
        }
    }

    void EnsureToScanDistanceGaugeRoot()
    {
        if (toScanDistanceGaugeRoot != null)
        {
            return;
        }

        if (distanceGaugeOnBattleRect != null)
        {
            toScanDistanceGaugeRoot = distanceGaugeOnBattleRect;
            return;
        }

        GameObject gaugeObject = GameObject.Find("DistanceGaugeOnBattle");
        if (gaugeObject == null)
        {
            gaugeObject = FindSceneObjectIncludingInactive("DistanceGaugeOnBattle");
        }

        if (gaugeObject != null)
        {
            toScanDistanceGaugeRoot = gaugeObject.transform;
            distanceGaugeOnBattleRect = gaugeObject.transform as RectTransform;
        }
    }

    RectTransform ResolveDistanceGaugeOnBattleRect()
    {
        EnsureToScanDistanceGaugeRoot();
        return toScanDistanceGaugeRoot != null
            ? toScanDistanceGaugeRoot as RectTransform
            : null;
    }

    void EnsureDistanceGaugeOnBattleSlideInAnimator()
    {
        RectTransform gaugeRect = ResolveDistanceGaugeOnBattleRect();
        if (gaugeRect == null)
        {
            return;
        }

        if (distanceGaugeOnBattleSlideInAnimator == null)
        {
            distanceGaugeOnBattleSlideInAnimator = gaugeRect.GetComponent<UIRectSlideInEntryAnimator>();
        }

        if (distanceGaugeOnBattleSlideInAnimator == null)
        {
            distanceGaugeOnBattleSlideInAnimator = gaugeRect.gameObject.AddComponent<UIRectSlideInEntryAnimator>();
        }

        distanceGaugeOnBattleSlideInAnimator.SetTarget(gaugeRect, "DistanceGaugeOnBattle");
        distanceGaugeOnBattleSlideInAnimator.Configure(
            distanceGaugeOnBattleSlideInOffsetX,
            distanceGaugeOnBattleSlideInDuration,
            distanceGaugeOnBattleSlideInEaseOutPower);
    }

    void PrepareDistanceGaugeOnBattleOffScreen()
    {
        if (!ShouldShowDistanceGaugeOnBattle())
        {
            return;
        }

        EnsureToScanDistanceGaugeRoot();
        if (toScanDistanceGaugeRoot != null)
        {
            toScanDistanceGaugeRoot.gameObject.SetActive(true);
        }

        EnsureDistanceGaugeOnBattleSlideInAnimator();
        if (distanceGaugeOnBattleSlideInAnimator == null)
        {
            return;
        }

        distanceGaugeOnBattleSlideInAnimator.SnapToRest();
        distanceGaugeOnBattleSlideInAnimator.PrepareOffScreenRight();
    }

    void SyncDistanceGaugeOnBattleOffScreenPlacement()
    {
        if (!distanceGaugeOnBattlePendingSlideIn || distanceGaugeOnBattleSlideCoroutine != null)
        {
            return;
        }

        PrepareDistanceGaugeOnBattleOffScreen();
    }

    void StartPostPopupDistanceGaugeOnBattleSequence()
    {
        if (distanceGaugeOnBattleSlideCoroutine != null)
        {
            StopCoroutine(distanceGaugeOnBattleSlideCoroutine);
            distanceGaugeOnBattleSlideCoroutine = null;
        }

        if (!useDailyDistanceSchedule)
        {
            StartBattleDistanceGaugeEntryAnimations(animatePopupDistanceText: false);
            return;
        }

        distanceGaugeOnBattleSlideCoroutine = StartCoroutine(PostPopupDistanceGaugeOnBattleSequence());
    }

    void StopDistanceGaugeOnBattleSlideIn(bool snapToRest = true)
    {
        if (distanceGaugeOnBattleSlideCoroutine != null)
        {
            StopCoroutine(distanceGaugeOnBattleSlideCoroutine);
            distanceGaugeOnBattleSlideCoroutine = null;
        }

        if (snapToRest)
        {
            distanceGaugeOnBattleSlideInAnimator?.SnapToRest();
        }
    }

    IEnumerator PostPopupDistanceGaugeOnBattleSequence()
    {
        if (ShouldShowDistanceGaugeOnBattle())
        {
            if (ShouldSlideInDistanceGaugeOnBattleAfterPopup())
            {
                yield return SlideDistanceGaugeOnBattleIn();
            }
            else
            {
                ShowDistanceGaugeOnBattleAtRest();
            }
        }
        else
        {
            EnsureToScanDistanceGaugeRoot();
            if (toScanDistanceGaugeRoot != null)
            {
                toScanDistanceGaugeRoot.gameObject.SetActive(false);
            }

            distanceGaugeOnBattleSlideInAnimator?.SnapToRest();
        }

        distanceGaugeOnBattlePendingSlideIn = false;
        StartBattleDistanceGaugeEntryAnimations(animatePopupDistanceText: false);
        distanceGaugeOnBattleSlideCoroutine = null;
    }

    void ShowDistanceGaugeOnBattleAtRest()
    {
        EnsureToScanDistanceGaugeRoot();
        if (toScanDistanceGaugeRoot != null)
        {
            toScanDistanceGaugeRoot.gameObject.SetActive(true);
        }

        EnsureDistanceGaugeOnBattleSlideInAnimator();
        distanceGaugeOnBattleSlideInAnimator?.SnapToRest();
    }

    IEnumerator SlideDistanceGaugeOnBattleIn()
    {
        EnsureToScanDistanceGaugeRoot();
        if (toScanDistanceGaugeRoot != null)
        {
            toScanDistanceGaugeRoot.gameObject.SetActive(true);
        }

        EnsureDistanceGaugeOnBattleSlideInAnimator();
        if (distanceGaugeOnBattleSlideInAnimator == null)
        {
            yield break;
        }

        yield return distanceGaugeOnBattleSlideInAnimator.PlaySlideIn();
    }

    void RefreshGaugeDisplay()
    {
        float meters = GetBoundGaugeDistanceMeters();
        ApplyGaugeDisplayVisuals(GetBoundGaugeFillRatio(), GetBoundNoiseCount(), meters);
    }

    void ApplyGaugeDisplayVisuals(float fillRatio, int noisesAmount, float displayMeters, bool updateDistanceText = true)
    {
        int displayMax = Mathf.Max(0, Mathf.FloorToInt(gaugeMax));
        fillRatio = Mathf.Clamp01(fillRatio);
        noisesAmount = Mathf.Clamp(noisesAmount, 0, displayMax);

        if (gaugeFillImage != null)
        {
            gaugeFillImage.fillAmount = fillRatio;
        }

        TMP_Text noisesAmountLabel = ResolveNoisesAmountText();
        if (noisesAmountLabel != null)
        {
            noisesAmountLabel.text = gaugeShowMaxInText
                ? $"{noisesAmount} / {displayMax}"
                : $"{noisesAmount}";
        }

        if (updateDistanceText && totalDistanceDisplayText != null)
        {
            totalDistanceDisplayText.text = FormatDistanceMeters(displayMeters);
        }
    }

    void ResetPopupDistanceDisplayToZero()
    {
        if (totalDistanceDisplayText != null)
        {
            totalDistanceDisplayText.text = FormatDistanceMeters(0f);
        }
    }

    void PrepareGaugesForEntryAfterNavButtons()
    {
        bool deferBattleGaugeForPopup = useDailyDistanceSchedule && _deferHudGaugeEntryUntilPopupDismissed;
        bool prepareFullHudEntry = ShouldStartBattleDistanceGaugeEntryOnHomeEntry() && !deferBattleGaugeForPopup;

        if (prepareFullHudEntry)
        {
            isGaugeEntryAnimating = true;
        }

        ResolveExperienceDisplayValues(out int level, out _, out int required);

        if (deferBattleGaugeForPopup)
        {
            ApplyGaugeDisplayVisuals(0f, 0, 0f, updateDistanceText: false);
        }
        else if (prepareFullHudEntry)
        {
            ApplyGaugeDisplayVisuals(0f, 0, 0f, updateDistanceText: !useDailyDistanceSchedule);
            ApplyExperienceDisplayVisuals(level, 0, required, 0f);
            ApplyExperienceBarFillRatio(0f);
        }

        if (Application.isPlaying)
        {
            EnsureExpSlotRingBuilt();
            if (prepareFullHudEntry)
            {
                ApplyExpSlotRingFillRatio(0f);
            }

            if (revealEmptySlotsDuringLevelGaugeSlide)
            {
                SetExpSlotRingEmptyRevealCount(0);
            }
        }
    }

    void TryStartHomeEntryGaugeAnimations()
    {
        StartExperienceEntryAnimations();
        if (ShouldStartBattleDistanceGaugeEntryOnHomeEntry())
        {
            StartBattleDistanceGaugeEntryAnimations(animatePopupDistanceText: !useDailyDistanceSchedule);
        }
    }

    void StartExperienceEntryAnimations()
    {
        if (experienceEntryAnimationCoroutine != null)
        {
            StopCoroutine(experienceEntryAnimationCoroutine);
            experienceEntryAnimationCoroutine = null;
        }

        experienceEntryAnimationCoroutine = StartCoroutine(AnimateExperienceGaugesOnEntry());
    }

    void StartBattleDistanceGaugeEntryAnimations(bool animatePopupDistanceText = true)
    {
        isGaugeEntryAnimating = true;

        if (gaugeEntryAnimationCoroutine != null)
        {
            StopCoroutine(gaugeEntryAnimationCoroutine);
            gaugeEntryAnimationCoroutine = null;
        }

        gaugeEntryAnimationCoroutine = StartCoroutine(AnimateBattleDistanceGaugesOnEntry(animatePopupDistanceText));
    }

    void StartGaugeEntryAnimations(bool animatePopupDistanceText = true)
    {
        StartExperienceEntryAnimations();
        StartBattleDistanceGaugeEntryAnimations(animatePopupDistanceText);
    }

    void StopGaugeEntryAnimation()
    {
        if (gaugeEntryAnimationCoroutine != null)
        {
            StopCoroutine(gaugeEntryAnimationCoroutine);
            gaugeEntryAnimationCoroutine = null;
        }

        if (experienceEntryAnimationCoroutine != null)
        {
            StopCoroutine(experienceEntryAnimationCoroutine);
            experienceEntryAnimationCoroutine = null;
        }

        isGaugeEntryAnimating = false;
    }

    void StartPopupDistanceEntryAnimation()
    {
        StopPopupDistanceEntryCoroutines();
        popupDistanceEntryCoroutine = StartCoroutine(AnimatePopupDistanceOnEntry());
        popupScaleEntryCoroutine = StartCoroutine(AnimatePopupScaleOnEntry());
    }

    void StartPopupDistanceContentAnimation()
    {
        StopPopupDistanceEntryCoroutines();
        popupDistanceEntryCoroutine = StartCoroutine(AnimatePopupDistanceOnEntry());
    }

    void StopPopupDistanceEntryCoroutines()
    {
        if (popupDistanceEntryCoroutine != null)
        {
            StopCoroutine(popupDistanceEntryCoroutine);
            popupDistanceEntryCoroutine = null;
        }

        if (popupScaleEntryCoroutine != null)
        {
            StopCoroutine(popupScaleEntryCoroutine);
            popupScaleEntryCoroutine = null;
        }
    }

    void StopPopupDistanceEntryAnimation()
    {
        StopPopupDistanceEntryCoroutines();
        playDistancePopupScaleInAnimator?.SnapToRest();
    }

    IEnumerator AnimatePopupScaleOnEntry()
    {
        if (playDistancePopupScaleInAnimator == null)
        {
            yield break;
        }

        yield return playDistancePopupScaleInAnimator.PlayScaleIn();
        popupScaleEntryCoroutine = null;
    }

    IEnumerator AnimatePopupDistanceOnEntry()
    {
        if (totalDistanceDisplayText == null)
        {
            yield break;
        }

        float targetMeters = GetBoundGaugeDistanceMeters();
        float duration = Mathf.Max(0f, gaugeEntryAnimationDuration);
        if (duration <= 0f)
        {
            totalDistanceDisplayText.text = FormatDistanceMeters(targetMeters);
            yield break;
        }

        ResetPopupDistanceDisplayToZero();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EvaluateGaugeEntryAnimation(t);
            float animatedMeters = targetMeters * eased;
            totalDistanceDisplayText.text = FormatDistanceMeters(animatedMeters);
            yield return null;
        }

        totalDistanceDisplayText.text = FormatDistanceMeters(targetMeters);
        popupDistanceEntryCoroutine = null;
    }

    IEnumerator AnimateExperienceGaugesOnEntry()
    {
        ResolveExperienceDisplayValues(out int targetLevel, out int targetExpCurrent, out int targetExpRequired);
        targetExpCurrent = Mathf.Max(0, targetExpCurrent);
        targetExpRequired = Mathf.Max(0, targetExpRequired);

        EnsureExpSlotRingBuilt();

        float duration = Mathf.Max(0f, gaugeEntryAnimationDuration);
        if (duration <= 0f)
        {
            RefreshExperienceDisplay();
            experienceEntryAnimationCoroutine = null;
            yield break;
        }

        ApplyExperienceDisplayVisuals(targetLevel, 0, targetExpRequired, 0f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EvaluateGaugeEntryAnimation(t);
            int animatedExpCurrent = Mathf.RoundToInt(targetExpCurrent * eased);
            float animatedExpFillRatio = targetExpRequired > 0
                ? Mathf.Clamp01((targetExpCurrent * eased) / targetExpRequired)
                : targetExpCurrent > 0 ? eased : 0f;

            ApplyExperienceDisplayVisuals(
                targetLevel,
                animatedExpCurrent,
                targetExpRequired,
                animatedExpFillRatio);

            yield return null;
        }

        experienceEntryAnimationCoroutine = null;
        float targetExpFillRatio = GetExperienceFillRatio(targetExpCurrent, targetExpRequired);
        ApplyExperienceDisplayVisuals(
            targetLevel,
            targetExpCurrent,
            targetExpRequired,
            targetExpFillRatio);
    }

    IEnumerator AnimateBattleDistanceGaugesOnEntry(bool animatePopupDistanceText = true)
    {
        float targetMeters = GetBoundGaugeDistanceMeters();
        int targetNoisesAmount = GetBoundNoiseCount();
        float targetGaugeFillRatio = GetBoundGaugeFillRatio();
        float targetGaugeProgressUnits = gaugeMetersPerCurrent > 0f
            ? targetMeters / gaugeMetersPerCurrent
            : 0f;

        float duration = Mathf.Max(0f, gaugeEntryAnimationDuration);
        if (duration <= 0f)
        {
            RefreshGaugeDisplay();
            isGaugeEntryAnimating = false;
            gaugeEntryAnimationCoroutine = null;
            yield break;
        }

        isGaugeEntryAnimating = true;
        ApplyGaugeDisplayVisuals(0f, 0, 0f, animatePopupDistanceText);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EvaluateGaugeEntryAnimation(t);

            float progressUnits = targetGaugeProgressUnits * eased;
            float animatedFillRatio = gaugeMax > 0f
                ? Mathf.Clamp01(progressUnits / gaugeMax)
                : 0f;
            float animatedMeters = targetMeters * eased;
            int animatedNoisesAmount = Mathf.RoundToInt(targetNoisesAmount * eased);

            ApplyGaugeDisplayVisuals(
                animatedFillRatio,
                animatedNoisesAmount,
                animatedMeters,
                animatePopupDistanceText);

            yield return null;
        }

        isGaugeEntryAnimating = false;
        gaugeEntryAnimationCoroutine = null;
        ApplyGaugeDisplayVisuals(
            targetGaugeFillRatio,
            targetNoisesAmount,
            targetMeters,
            animatePopupDistanceText);

        if (!animatePopupDistanceText)
        {
            ResetPopupDistanceDisplayToZero();
        }
    }

    /// <summary>Ease-out: changes quickly at first, slows as displayed values approach the target.</summary>
    float EvaluateGaugeEntryAnimation(float normalizedTime)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        float power = Mathf.Max(1f, gaugeEntryEaseOutPower);
        return 1f - Mathf.Pow(1f - normalizedTime, power);
    }

    void EnsureNavButtonPressFeedback()
    {
        EnsureButtonPressFeedback(toScanButton);
        EnsureButtonPressFeedback(toOutfitButton);
        EnsureButtonPressFeedback(toCraftButton);
    }

    static void EnsureButtonPressFeedback(Button button)
    {
        if (button == null)
        {
            return;
        }

        if (button.GetComponent<UIButtonPressFeedback>() == null)
        {
            button.gameObject.AddComponent<UIButtonPressFeedback>();
        }
    }

    void OnNavButtonClicked(Button button, Action navigate)
    {
        if (_navButtonClickHandling || IsHomeNavigationBlockedByPlayDistancePopup())
        {
            return;
        }

        StartCoroutine(HandleNavButtonClick(button, navigate));
    }

    IEnumerator HandleNavButtonClick(Button button, Action navigate)
    {
        _navButtonClickHandling = true;

        UIButtonPressFeedback pressFeedback = button.GetComponent<UIButtonPressFeedback>();
        if (pressFeedback != null)
        {
            yield return pressFeedback.PlayClickConfirm();
        }

        button.interactable = false;
        _navButtonClickHandling = false;
        navigate();
    }

    void CacheNavButtonRestPositions()
    {
        if (navButtonRestPositionsCached)
        {
            return;
        }

        bool hasScan = TryCacheButtonRestPosition(toScanButton, ref toScanButtonRestPosition);
        bool hasOutfit = TryCacheButtonRestPosition(toOutfitButton, ref toOutfitButtonRestPosition);
        bool hasCraft = TryCacheButtonRestPosition(toCraftButton, ref toCraftButtonRestPosition);
        navButtonRestPositionsCached = hasScan && hasOutfit && hasCraft;
    }

    static bool TryCacheButtonRestPosition(Button button, ref Vector2 restPosition)
    {
        RectTransform rect = GetButtonRectTransform(button);
        if (rect == null)
        {
            return false;
        }

        restPosition = rect.anchoredPosition;
        return true;
    }

    static RectTransform GetButtonRectTransform(Button button)
    {
        return button != null ? button.transform as RectTransform : null;
    }

    void StartHomeEntrySequence()
    {
        StopHomeEntrySequence();
        homeEntryCoroutine = StartCoroutine(PlayHomeEntrySequence());
    }

    void StopHomeEntrySequence()
    {
        if (homeEntryCoroutine != null)
        {
            StopCoroutine(homeEntryCoroutine);
            homeEntryCoroutine = null;
        }

        if (homeEntryNavSlideCoroutine != null)
        {
            StopCoroutine(homeEntryNavSlideCoroutine);
            homeEntryNavSlideCoroutine = null;
        }

        if (homeEntryLevelGaugeSlideCoroutine != null)
        {
            StopCoroutine(homeEntryLevelGaugeSlideCoroutine);
            homeEntryLevelGaugeSlideCoroutine = null;
        }

        if (homeEntryCdSlideCoroutine != null)
        {
            StopCoroutine(homeEntryCdSlideCoroutine);
            homeEntryCdSlideCoroutine = null;
        }

        if (homeEntryAlbumSlideCoroutine != null)
        {
            StopCoroutine(homeEntryAlbumSlideCoroutine);
            homeEntryAlbumSlideCoroutine = null;
        }

        if (homeEntryCharacterFadeCoroutine != null)
        {
            StopCoroutine(homeEntryCharacterFadeCoroutine);
            homeEntryCharacterFadeCoroutine = null;
        }

        isRotatingUiEntryAnimating = false;
        SnapNavButtonsToRestPositions();
        SnapLevelGaugeToRestPosition();
        SnapRotatingUiToRestState();
        SnapHomeAlbumToRestPosition();
        SnapHomeCharacterToRestColor();
    }

    IEnumerator PlayHomeEntrySequence()
    {
        CacheNavButtonRestPositions();
        ResolveLevelGaugeRect();
        CacheLevelGaugeRestPosition();
        CacheRotatingUiRestState();
        PrepareRotatingUiForEntry();
        ApplyHomeCharacterOutfitVisual();
        ResolveHomeCharacterImage();
        CacheHomeCharacterRestColor();
        PrepareHomeCharacterForFadeIn();

        IEnumerator navSlide = navButtonRestPositionsCached ? AnimateNavButtonsSlideIn() : null;
        IEnumerator levelGaugeSlide = levelGaugeRestPositionCached ? AnimateLevelGaugeSlideIn() : null;
        IEnumerator cdSlide = ShouldAnimateCdEntryOnHome() ? AnimateCdTurntableEntry() : null;
        IEnumerator albumSlide = ShouldAnimateAlbumEntryOnHome() ? AnimateAlbumSlideIn() : null;
        IEnumerator characterFade = ShouldAnimateCharacterFadeInOnHome() ? AnimateHomeCharacterFadeIn() : null;

        if (navSlide == null)
        {
            SnapNavButtonsToRestPositions();
        }

        if (levelGaugeSlide == null)
        {
            SnapLevelGaugeToRestPosition();
        }

        if (cdSlide == null)
        {
            SnapRotatingUiToRestState();
        }

        if (albumSlide == null)
        {
            SnapHomeAlbumToRestPosition();
        }

        if (characterFade == null)
        {
            SnapHomeCharacterToRestColor();
        }

        bool deferGaugeEntryUntilLevelGaugeSlideDone = levelGaugeSlide != null
            && revealEmptySlotsDuringLevelGaugeSlide;
        if (!deferGaugeEntryUntilLevelGaugeSlideDone)
        {
            TryStartHomeEntryGaugeAnimations();
        }

        bool navSlideDone = navSlide == null;
        bool levelGaugeSlideDone = levelGaugeSlide == null;
        bool cdSlideDone = cdSlide == null;
        bool albumSlideDone = albumSlide == null;
        bool characterFadeDone = characterFade == null;

        if (navSlide != null)
        {
            homeEntryNavSlideCoroutine = StartCoroutine(RunCoroutineWithCompletion(navSlide, () => navSlideDone = true));
        }

        if (levelGaugeSlide != null)
        {
            homeEntryLevelGaugeSlideCoroutine = StartCoroutine(
                RunCoroutineWithCompletion(levelGaugeSlide, () => levelGaugeSlideDone = true));
        }

        if (cdSlide != null)
        {
            homeEntryCdSlideCoroutine = StartCoroutine(RunCoroutineWithCompletion(cdSlide, () => cdSlideDone = true));
        }

        if (albumSlide != null)
        {
            homeEntryAlbumSlideCoroutine = StartCoroutine(
                RunCoroutineWithCompletion(albumSlide, () => albumSlideDone = true));
        }

        if (characterFade != null)
        {
            homeEntryCharacterFadeCoroutine = StartCoroutine(
                RunCoroutineWithCompletion(characterFade, () => characterFadeDone = true));
        }

        while (!navSlideDone || !levelGaugeSlideDone || !cdSlideDone || !albumSlideDone || !characterFadeDone)
        {
            yield return null;
        }

        if (deferGaugeEntryUntilLevelGaugeSlideDone)
        {
            TryStartHomeEntryGaugeAnimations();
        }

        homeEntryNavSlideCoroutine = null;
        homeEntryLevelGaugeSlideCoroutine = null;
        homeEntryCdSlideCoroutine = null;
        homeEntryAlbumSlideCoroutine = null;
        homeEntryCharacterFadeCoroutine = null;
        homeEntryCoroutine = null;
    }

    bool ShouldAnimateCharacterFadeInOnHome()
    {
        return animateCharacterFadeInOnHome && homeCharacterImage != null && homeCharacterColorCached;
    }

    void ApplyHomeCharacterOutfitVisual()
    {
        Transform root = ResolveHomeCharacterRoot();
        if (root == null)
        {
            return;
        }

        OutfitItemVisualHelper.ApplyHomeCharacterVariant(root);
        InvalidateHomeCharacterImageCache();
        ApplyHomeAlbumVisual();
    }

    void ApplyHomeAlbumVisual()
    {
        Transform root = ResolveHomeAlbumRoot();
        if (root == null)
        {
            return;
        }

        OutfitItemVisualHelper.ApplyHomeAlbumVariant(root);
    }

    Transform ResolveHomeAlbumRoot()
    {
        if (homeAlbumRoot != null)
        {
            return homeAlbumRoot;
        }

        GameObject albumObject = GameObject.Find("Album");
        return albumObject != null ? albumObject.transform : null;
    }

    RectTransform ResolveHomeAlbumRect()
    {
        return ResolveHomeAlbumRoot() as RectTransform;
    }

    void EnsureHomeAlbumSlideInAnimator()
    {
        RectTransform albumRect = ResolveHomeAlbumRect();
        if (albumRect == null)
        {
            return;
        }

        if (homeAlbumSlideInAnimator == null)
        {
            homeAlbumSlideInAnimator = albumRect.GetComponent<UIRectSlideInEntryAnimator>();
        }

        if (homeAlbumSlideInAnimator == null)
        {
            homeAlbumSlideInAnimator = albumRect.gameObject.AddComponent<UIRectSlideInEntryAnimator>();
        }

        homeAlbumSlideInAnimator.SetTarget(albumRect, "Album");
        homeAlbumSlideInAnimator.ConfigureFromBottom(
            albumSlideInOffsetY,
            albumSlideInDuration,
            albumSlideInEaseOutPower);
    }

    bool ShouldAnimateAlbumEntryOnHome()
    {
        return animateAlbumEntryOnHome && ResolveHomeAlbumRect() != null;
    }

    void PrepareHomeAlbumForEntry()
    {
        if (!ShouldAnimateAlbumEntryOnHome())
        {
            return;
        }

        EnsureHomeAlbumSlideInAnimator();
        if (homeAlbumSlideInAnimator != null)
        {
            homeAlbumSlideInAnimator.PrepareOffScreenBottom();
        }
    }

    void SnapHomeAlbumToRestPosition()
    {
        if (homeAlbumSlideInAnimator != null)
        {
            homeAlbumSlideInAnimator.SnapToRest();
        }
    }

    IEnumerator AnimateAlbumSlideIn()
    {
        EnsureHomeAlbumSlideInAnimator();
        if (homeAlbumSlideInAnimator == null)
        {
            yield break;
        }

        homeAlbumSlideInAnimator.PrepareOffScreenBottom();
        yield return homeAlbumSlideInAnimator.PlaySlideIn();
    }

    Transform ResolveHomeCharacterRoot()
    {
        if (homeCharacterRoot != null)
        {
            homeCharacterRootResolved = homeCharacterRoot;
            return homeCharacterRootResolved;
        }

        if (homeCharacterRootResolved != null)
        {
            return homeCharacterRootResolved;
        }

        GameObject characterObject = GameObject.Find("Character");
        if (characterObject != null)
        {
            homeCharacterRootResolved = characterObject.transform;
        }

        return homeCharacterRootResolved;
    }

    void InvalidateHomeCharacterImageCache()
    {
        homeCharacterImage = null;
        homeCharacterColorCached = false;
    }

    void ResolveHomeCharacterImage()
    {
        if (homeCharacterImage != null)
        {
            return;
        }

        Transform root = ResolveHomeCharacterRoot();
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!child.gameObject.activeSelf)
            {
                continue;
            }

            Image image = child.GetComponent<Image>();
            if (image != null)
            {
                homeCharacterImage = image;
                return;
            }
        }
    }

    void CacheHomeCharacterRestColor()
    {
        if (homeCharacterColorCached || homeCharacterImage == null)
        {
            return;
        }

        homeCharacterRestColor = homeCharacterImage.color;
        homeCharacterColorCached = true;
    }

    void SetHomeCharacterAlpha(float alpha)
    {
        if (homeCharacterImage == null || !homeCharacterColorCached)
        {
            return;
        }

        Color color = homeCharacterRestColor;
        color.a = Mathf.Clamp01(alpha) * homeCharacterRestColor.a;
        homeCharacterImage.color = color;
    }

    void PrepareHomeCharacterForFadeIn()
    {
        if (!ShouldAnimateCharacterFadeInOnHome())
        {
            return;
        }

        SetHomeCharacterAlpha(0f);
    }

    void SnapHomeCharacterToRestColor()
    {
        if (!homeCharacterColorCached || homeCharacterImage == null)
        {
            return;
        }

        homeCharacterImage.color = homeCharacterRestColor;
    }

    IEnumerator AnimateHomeCharacterFadeIn()
    {
        if (homeCharacterImage == null || !homeCharacterColorCached)
        {
            yield break;
        }

        float delay = Mathf.Max(0f, characterFadeInStartDelay);
        float duration = Mathf.Max(0f, characterFadeInDuration);
        float targetAlpha = homeCharacterRestColor.a;

        PrepareHomeCharacterForFadeIn();

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (duration <= 0f)
        {
            SnapHomeCharacterToRestColor();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetHomeCharacterAlpha(Mathf.Lerp(0f, targetAlpha, t));
            yield return null;
        }

        SnapHomeCharacterToRestColor();
    }

    bool ShouldAnimateCdEntryOnHome()
    {
        return animateCdEntryOnHome && rotatingUi != null && rotatingUiRestCached;
    }

    void CacheRotatingUiRestState()
    {
        if (rotatingUiRestCached || rotatingUi == null)
        {
            return;
        }

        rotatingUiRestAnchoredPosition = rotatingUi.anchoredPosition;
        rotatingUiRestEulerZ = rotatingUi.localEulerAngles.z;
        rotatingUiRestCached = true;
    }

    void SnapRotatingUiToRestState()
    {
        if (!rotatingUiRestCached || rotatingUi == null)
        {
            isRotatingUiEntryAnimating = false;
            return;
        }

        isRotatingUiEntryAnimating = false;
        rotatingUi.anchoredPosition = rotatingUiRestAnchoredPosition;
        Vector3 euler = rotatingUi.localEulerAngles;
        euler.z = rotatingUiRestEulerZ;
        rotatingUi.localEulerAngles = euler;
        angularVelocity = GetBaseAngularVelocity();
    }

    void PrepareRotatingUiForEntry()
    {
        if (!ShouldAnimateCdEntryOnHome())
        {
            return;
        }

        isRotatingUiEntryAnimating = true;
        isTouchingRotatingUi = false;
        angularVelocity = 0f;
        dragAngularVelocityDegreesPerSecond = 0f;

        float spinSign = rotateClockwise ? -1f : 1f;
        float startEulerZ = rotatingUiRestEulerZ - cdEntrySpinDegrees * spinSign;

        rotatingUi.anchoredPosition = rotatingUiRestAnchoredPosition + cdEntryStartOffset;
        Vector3 euler = rotatingUi.localEulerAngles;
        euler.z = startEulerZ;
        rotatingUi.localEulerAngles = euler;
    }

    IEnumerator AnimateCdTurntableEntry()
    {
        if (rotatingUi == null || !rotatingUiRestCached)
        {
            yield break;
        }

        float duration = Mathf.Max(0f, cdEntryDuration);
        Vector2 startPosition = rotatingUiRestAnchoredPosition + cdEntryStartOffset;
        Vector2 restPosition = rotatingUiRestAnchoredPosition;
        float spinSign = rotateClockwise ? -1f : 1f;
        float startEulerZ = rotatingUiRestEulerZ - cdEntrySpinDegrees * spinSign;

        PrepareRotatingUiForEntry();

        if (duration <= 0f)
        {
            SnapRotatingUiToRestState();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateSlideInEase(Mathf.Clamp01(elapsed / duration), cdEntryEaseOutPower);
            rotatingUi.anchoredPosition = Vector2.LerpUnclamped(startPosition, restPosition, eased);

            Vector3 euler = rotatingUi.localEulerAngles;
            euler.z = Mathf.LerpAngle(startEulerZ, rotatingUiRestEulerZ, eased);
            rotatingUi.localEulerAngles = euler;

            yield return null;
        }

        SnapRotatingUiToRestState();
    }

    static IEnumerator RunCoroutineWithCompletion(IEnumerator routine, System.Action onComplete)
    {
        yield return routine;
        onComplete?.Invoke();
    }

    void SnapNavButtonsToRestPositions()
    {
        if (navButtonRestPositionsCached)
        {
            SetNavButtonAnchoredPosition(toScanButton, toScanButtonRestPosition);
            SetNavButtonAnchoredPosition(toOutfitButton, toOutfitButtonRestPosition);
            SetNavButtonAnchoredPosition(toCraftButton, toCraftButtonRestPosition);
        }

        RefreshToScanButtonState();
        if (!IsHomeNavigationBlockedByPlayDistancePopup())
        {
            RestoreHomeNavButtonVisual(toOutfitButton);
            RestoreHomeNavButtonVisual(toCraftButton);
        }
    }

    static void SetNavButtonAnchoredPosition(Button button, Vector2 anchoredPosition)
    {
        RectTransform rect = GetButtonRectTransform(button);
        if (rect != null)
        {
            rect.anchoredPosition = anchoredPosition;
        }
    }

    void SetToScanButtonNavInteractable(bool interactable)
    {
        if (toScanButton == null)
        {
            return;
        }

        if (IsHomeNavigationBlockedByPlayDistancePopup())
        {
            ApplyPlayDistancePopupHomeInputBlock(true);
            return;
        }

        if (interactable)
        {
            RefreshToScanButtonState();
            return;
        }

        EnsureToScanButtonCanvasGroup();
        toScanButton.interactable = false;

        if (toScanButtonCanvasGroup != null)
        {
            toScanButtonCanvasGroup.alpha = 1f;
            toScanButtonCanvasGroup.interactable = false;
            toScanButtonCanvasGroup.blocksRaycasts = true;
        }
    }

    void SetNavButtonInteractable(Button button, bool interactable)
    {
        if (button == null)
        {
            return;
        }

        ForceNavButtonNoColorTint(button);
        button.interactable = interactable;

        if (!interactable || IsHomeNavigationBlockedByPlayDistancePopup())
        {
            return;
        }

        ApplyNavButtonNormalPresentation(button);
    }

    IEnumerator AnimateNavButtonsSlideIn()
    {
        float offsetX = Mathf.Max(0f, navButtonSlideInOffsetX);
        float stageDuration = Mathf.Max(0f, navButtonStageSlideInDuration);
        float outfitDuration = stageDuration * Mathf.Max(1f, navButtonOutfitSlideDurationScale);
        float craftDuration = stageDuration * Mathf.Max(1f, navButtonCraftSlideDurationScale);
        float outfitStartDelay = Mathf.Max(0f, navButtonOutfitSlideStartDelay);
        float craftStartDelay = Mathf.Max(outfitStartDelay, navButtonCraftSlideStartDelay);

        PlaceNavButtonOffScreenRight(toScanButton, toScanButtonRestPosition, offsetX);
        PlaceNavButtonOffScreenRight(toOutfitButton, toOutfitButtonRestPosition, offsetX);
        PlaceNavButtonOffScreenRight(toCraftButton, toCraftButtonRestPosition, offsetX);
        SetToScanButtonNavInteractable(false);
        SetNavButtonInteractable(toOutfitButton, false);
        SetNavButtonInteractable(toCraftButton, false);

        if (stageDuration <= 0f && outfitDuration <= 0f && craftDuration <= 0f)
        {
            SnapNavButtonsToRestPositions();
            yield break;
        }

        bool stageSlideDone = false;
        bool outfitSlideDone = false;
        bool craftSlideDone = false;

        StartCoroutine(RunCoroutineWithCompletion(
            DelayedSlideNavButtonIn(toScanButton, toScanButtonRestPosition, 0f, stageDuration),
            () => stageSlideDone = true));
        StartCoroutine(RunCoroutineWithCompletion(
            DelayedSlideNavButtonIn(toOutfitButton, toOutfitButtonRestPosition, outfitStartDelay, outfitDuration),
            () => outfitSlideDone = true));
        StartCoroutine(RunCoroutineWithCompletion(
            DelayedSlideNavButtonIn(toCraftButton, toCraftButtonRestPosition, craftStartDelay, craftDuration),
            () => craftSlideDone = true));

        while (!stageSlideDone || !outfitSlideDone || !craftSlideDone)
        {
            yield return null;
        }

        RefreshToScanButtonState();
        if (!IsHomeNavigationBlockedByPlayDistancePopup())
        {
            RestoreHomeNavButtonVisual(toOutfitButton);
            RestoreHomeNavButtonVisual(toCraftButton);
        }
    }

    IEnumerator DelayedSlideNavButtonIn(Button button, Vector2 restPosition, float startDelay, float duration)
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSeconds(startDelay);
        }

        if (duration > 0f)
        {
            yield return SlideNavButtonIn(button, restPosition, duration);
            yield break;
        }

        SetNavButtonAnchoredPosition(button, restPosition);
        if (button == toScanButton)
        {
            RefreshToScanButtonState();
        }
        else
        {
            RestoreHomeNavButtonVisual(button);
        }
    }

    IEnumerator AnimateLevelGaugeSlideIn()
    {
        if (levelGaugeRect == null)
        {
            yield break;
        }

        EnsureExpSlotRingBuilt();
        if (revealEmptySlotsDuringLevelGaugeSlide)
        {
            SetExpSlotRingEmptyRevealCount(0);
        }

        float offsetX = Mathf.Max(0f, levelGaugeSlideInOffsetX);
        float slideDuration = Mathf.Max(0f, levelGaugeSlideInDuration);
        float revealDuration = Mathf.Max(0f, levelGaugeEmptySlotRevealDuration);
        float revealStartDelay = Mathf.Max(0f, levelGaugeEmptySlotRevealStartDelay);
        Vector2 restPosition = levelGaugeRestPosition;
        Vector2 startPosition = restPosition + Vector2.left * offsetX;
        int slotRevealTotal = expSlotUnits.Count;
        bool animateReveal = revealEmptySlotsDuringLevelGaugeSlide && slotRevealTotal > 0;
        float sequenceDuration = slideDuration;
        if (animateReveal)
        {
            sequenceDuration = Mathf.Max(sequenceDuration, revealStartDelay + revealDuration);
        }

        levelGaugeRect.anchoredPosition = startPosition;

        if (sequenceDuration <= 0f)
        {
            levelGaugeRect.anchoredPosition = restPosition;
            if (animateReveal)
            {
                SetExpSlotRingEmptyRevealCount(slotRevealTotal);
            }

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < sequenceDuration)
        {
            elapsed += Time.deltaTime;

            if (slideDuration > 0f)
            {
                float slideT = Mathf.Clamp01(elapsed / slideDuration);
                float slideEased = EvaluateSlideInEase(slideT, levelGaugeSlideInEaseOutPower);
                levelGaugeRect.anchoredPosition = Vector2.LerpUnclamped(startPosition, restPosition, slideEased);
            }
            else
            {
                levelGaugeRect.anchoredPosition = restPosition;
            }

            if (animateReveal && revealDuration > 0f)
            {
                float revealElapsed = elapsed - revealStartDelay;
                float revealT = Mathf.Clamp01(revealElapsed / revealDuration);
                float revealEased = EvaluateSlideInEase(revealT, levelGaugeEmptySlotRevealEaseOutPower);
                int revealCount = Mathf.Clamp(Mathf.FloorToInt(revealEased * slotRevealTotal), 0, slotRevealTotal);
                SetExpSlotRingEmptyRevealCount(revealCount);
            }
            else if (animateReveal && elapsed >= revealStartDelay)
            {
                SetExpSlotRingEmptyRevealCount(slotRevealTotal);
            }

            yield return null;
        }

        levelGaugeRect.anchoredPosition = restPosition;
        if (animateReveal)
        {
            SetExpSlotRingEmptyRevealCount(slotRevealTotal);
        }
    }

    void EnsureExperienceDisplayReferences()
    {
        if (levelDisplayText == null)
        {
            TMP_Text levelText = FindExperienceUiText("Level");
            if (levelText != null)
            {
                levelDisplayText = levelText;
            }
        }

        if (expDisplayText == null)
        {
            TMP_Text experienceText = FindExperienceUiText("Experience");
            if (experienceText != null)
            {
                expDisplayText = experienceText;
            }
        }
    }

    static TMP_Text FindExperienceUiText(string objectName)
    {
        GameObject textObject = GameObject.Find(objectName);
        return textObject != null ? textObject.GetComponent<TMP_Text>() : null;
    }

    void ResolveLevelGaugeRect()
    {
        if (levelGaugeRect != null)
        {
            return;
        }

        if (expSlotPivot != null && expSlotPivot.parent is RectTransform slotParent)
        {
            levelGaugeRect = slotParent;
            return;
        }

        if (expBarPivot != null && expBarPivot.parent is RectTransform barParent)
        {
            levelGaugeRect = barParent;
        }
    }

    void CacheLevelGaugeRestPosition()
    {
        if (levelGaugeRestPositionCached || levelGaugeRect == null)
        {
            return;
        }

        levelGaugeRestPosition = levelGaugeRect.anchoredPosition;
        levelGaugeRestPositionCached = true;
    }

    void SnapLevelGaugeToRestPosition()
    {
        if (!levelGaugeRestPositionCached || levelGaugeRect == null)
        {
            return;
        }

        levelGaugeRect.anchoredPosition = levelGaugeRestPosition;
        if (expSlotRingBuilt)
        {
            SetExpSlotRingEmptyRevealCount(expSlotUnits.Count);
        }
    }

    /// <summary>Reveal empty slots 0..count-1 around the ring. Does not touch FilledSlots (count-up owns those).</summary>
    void SetExpSlotRingEmptyRevealCount(int revealedCount)
    {
        if (!expSlotRingBuilt)
        {
            return;
        }

        revealedCount = Mathf.Clamp(revealedCount, 0, expSlotUnits.Count);
        bool filledNestedInEmpty = IsFilledSlotNestedInEmpty();

        for (int i = 0; i < expSlotUnits.Count; i++)
        {
            ExpSlotUnitInstance unit = ResolveExpSlotUnitReferences(expSlotUnits[i]);
            expSlotUnits[i] = unit;
            ApplyExpSlotUnitEmptyRevealState(unit, i < revealedCount, filledNestedInEmpty);
        }
    }

    static void ApplyExpSlotUnitEmptyRevealState(ExpSlotUnitInstance unit, bool revealEmpty, bool filledNestedInEmpty)
    {
        if (unit.Empty == null)
        {
            return;
        }

        if (!revealEmpty)
        {
            if (unit.Filled != null && unit.Filled.activeSelf)
            {
                unit.Filled.SetActive(false);
            }

            unit.Empty.SetActive(false);
            return;
        }

        if (unit.Filled != null && unit.Filled.activeSelf)
        {
            unit.Filled.SetActive(false);
        }

        if (filledNestedInEmpty || (unit.Filled != null && unit.Filled.transform.IsChildOf(unit.Empty.transform)))
        {
            unit.Empty.SetActive(true);

            Image emptyImage = unit.Empty.GetComponent<Image>();
            if (emptyImage != null && !emptyImage.enabled)
            {
                emptyImage.enabled = true;
            }

            return;
        }

        unit.Empty.SetActive(true);
    }

    static void PlaceNavButtonOffScreenRight(Button button, Vector2 restPosition, float offsetX)
    {
        SetNavButtonAnchoredPosition(button, restPosition + Vector2.right * offsetX);
    }

    IEnumerator SlideNavButtonIn(Button button, Vector2 restPosition, float duration)
    {
        RectTransform rect = GetButtonRectTransform(button);
        if (rect == null)
        {
            yield break;
        }

        Vector2 startPosition = rect.anchoredPosition;
        yield return AnimateRectSlide(rect, startPosition, restPosition, duration, navButtonSlideInEaseOutPower);
        if (button == toScanButton)
        {
            RefreshToScanButtonState();
        }
        else if (!IsHomeNavigationBlockedByPlayDistancePopup())
        {
            RestoreHomeNavButtonVisual(button);
        }

        yield break;
    }

    static IEnumerator AnimateRectSlide(
        RectTransform rect,
        Vector2 startPosition,
        Vector2 restPosition,
        float duration,
        float easeOutPower)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateSlideInEase(Mathf.Clamp01(elapsed / duration), easeOutPower);
            rect.anchoredPosition = Vector2.LerpUnclamped(startPosition, restPosition, eased);
            yield return null;
        }

        rect.anchoredPosition = restPosition;
    }

    static float EvaluateSlideInEase(float normalizedTime, float easeOutPower)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        float power = Mathf.Max(1f, easeOutPower);
        return 1f - Mathf.Pow(1f - normalizedTime, power);
    }

    public void SetGaugeMax(float max)
    {
        gaugeMax = max;
        RefreshGaugeDisplay();
    }

    void RefreshExperienceDisplay()
    {
        if (ShouldSuppressExperienceDisplayRefresh())
        {
            return;
        }

        SyncManualProgressToPlayerLevelManager();
        ResolveExperienceDisplayValues(out int level, out int current, out int required);
        ApplyExperienceDisplayVisuals(level, current, required);
    }

    /// <summary>
    /// Manual 调试值写入 PlayerLevelManager，Result 等其它场景才能读到同一经验。
    /// </summary>
    void SyncManualProgressToPlayerLevelManager()
    {
        if (!Application.isPlaying || !useManualLevelProgressForDebug)
        {
            return;
        }

        if (PlayerLevelManager.Instance == null)
        {
            return;
        }

        int level = Mathf.Clamp(manualLevel, 1, PlayerLevelManager.MaxLevel);
        int currentExp = Mathf.Max(0, manualCurrentExp);
        PlayerLevelManager manager = PlayerLevelManager.Instance;
        if (manager.CurrentLevel == level && manager.CurrentLevelExp == currentExp)
        {
            return;
        }

        manager.SetProgress(level, currentExp);
    }

    void ResolveExperienceDisplayValues(out int level, out int current, out int required)
    {
        if (PlayerLevelManager.Instance != null)
        {
            level = PlayerLevelManager.Instance.CurrentLevel;
            current = PlayerLevelManager.Instance.CurrentLevelExp;
            required = PlayerLevelManager.Instance.GetExpRequiredForCurrentLevel();
            return;
        }

        level = Mathf.Clamp(manualLevel, 1, PlayerLevelManager.MaxLevel);
        current = Mathf.Max(0, manualCurrentExp);
        required = GetMaxExpForLevel(level);
    }

    int GetMaxExpForLevel(int level)
    {
        level = Mathf.Clamp(level, 1, PlayerLevelManager.MaxLevel);
        if (playerLevelConfig != null)
        {
            return playerLevelConfig.GetExpRequiredForLevel(level);
        }

        if (PlayerLevelManager.Instance != null)
        {
            return PlayerLevelManager.Instance.GetExpRequiredForLevel(level);
        }

        return PlayerLevelManager.GetDefaultExpRequiredForLevel(level);
    }

    static string FormatExperienceProgressText(int currentExp, int maxExp)
    {
        currentExp = Mathf.Max(0, currentExp);
        if (maxExp > 0)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} / {1}", currentExp, maxExp);
        }

        return string.Format(CultureInfo.InvariantCulture, "{0} / MAX", currentExp);
    }

    static float GetExperienceFillRatio(int current, int required)
    {
        if (required > 0)
        {
            return Mathf.Clamp01((float)current / required);
        }

        return current > 0 ? 1f : 0f;
    }

    void ApplyExperienceDisplayVisuals(int level, int current, int required, float? experienceFillRatioOverride = null)
    {
        level = Mathf.Clamp(level, 1, PlayerLevelManager.MaxLevel);
        current = Mathf.Max(0, current);
        required = Mathf.Max(0, required);

        if (levelDisplayText != null)
        {
            levelDisplayText.text = level.ToString(CultureInfo.InvariantCulture);
        }

        if (expDisplayText != null)
        {
            expDisplayText.text = FormatExperienceProgressText(current, required);
        }

        float fillRatio = experienceFillRatioOverride ?? GetExperienceFillRatio(current, required);
        ApplyExperienceBarFillRatio(fillRatio);

        if (!Application.isPlaying)
        {
            return;
        }

        EnsureExpSlotRingBuilt();
        ApplyExpSlotRingFillRatio(fillRatio);
    }

    void EnsureExpSlotRingBuilt()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (expSlotPivot == null || emptySlotTemplate == null || filledSlotTemplate == null)
        {
            return;
        }

        if (!TryComputeExpSlotRingLayoutParameters(emptySlotTemplate, filledSlotTemplate, out ExpSlotRingLayoutParameters layout))
        {
            return;
        }

        bool needRebuild = !expSlotRingBuilt
            || expSlotRingBuiltCount != expSlotCount
            || !AreExpSlotUnitsValid();

        if (needRebuild)
        {
            BuildExpSlotRing(layout);
            return;
        }

        bool needRelayout = !Mathf.Approximately(expSlotRingBuiltAngleStart, expSlotAngleStart)
            || !Mathf.Approximately(expSlotRingBuiltAngleEnd, expSlotAngleEnd)
            || !expSlotRingLayoutCached
            || !RingLayoutParametersEqual(expSlotRingCachedLayout, layout);

        if (needRelayout)
        {
            RefreshExpSlotRingLayout(layout);
        }
    }

    void BuildExpSlotRing(ExpSlotRingLayoutParameters layout)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ClearGeneratedExpSlots();

        if (expSlotPivot == null || emptySlotTemplate == null || filledSlotTemplate == null || expSlotCount <= 0)
        {
            expSlotRingBuilt = false;
            return;
        }

        bool filledNestedInEmpty = IsFilledSlotNestedInEmpty();
        emptySlotTemplate.gameObject.SetActive(false);
        if (!filledNestedInEmpty && filledSlotTemplate != null)
        {
            filledSlotTemplate.gameObject.SetActive(false);
        }

        for (int i = 0; i < expSlotCount; i++)
        {
            float t = expSlotCount > 1 ? i / (float)(expSlotCount - 1) : 0f;
            float angleDeg = Mathf.Lerp(expSlotAngleStart, expSlotAngleEnd, t);
            Vector2 unitAnchoredPosition = PolarToAnchoredPosition(layout.Radius, angleDeg);
            float unitEulerZ = angleDeg + layout.UnitRootRotationOffset;

            GameObject unitRoot = new GameObject($"ExpSlotUnit_{i}", typeof(RectTransform));
            unitRoot.transform.SetParent(expSlotPivot, false);
            ApplyUnitRootLayout(unitRoot.GetComponent<RectTransform>(), unitAnchoredPosition, unitEulerZ);

            GameObject emptyInstance = Instantiate(emptySlotTemplate.gameObject, unitRoot.transform);
            emptyInstance.name = "EmptySlot";
            ApplyUnitChildLayout(emptyInstance.GetComponent<RectTransform>(), Vector2.zero, 0f, layout.EmptyLayout);
            emptyInstance.SetActive(true);

            GameObject filledInstance = CreateFilledSlotInstance(
                unitRoot.transform,
                emptyInstance,
                layout,
                filledNestedInEmpty);
            if (filledInstance != null)
            {
                filledInstance.SetActive(false);
            }

            expSlotUnits.Add(new ExpSlotUnitInstance
            {
                Root = unitRoot,
                Empty = emptyInstance,
                Filled = filledInstance
            });
        }

        expSlotRingBuilt = true;
        expSlotRingBuiltCount = expSlotCount;
        CacheExpSlotRingLayoutState(layout);

        ResolveExperienceDisplayValues(out _, out int previewCurrent, out int previewRequired);
        float initialFillRatio = experienceEntryAnimationCoroutine != null
            ? 0f
            : GetExperienceFillRatio(previewCurrent, previewRequired);
        ApplyExpSlotRingFillRatio(initialFillRatio);
    }

    GameObject CreateFilledSlotInstance(
        Transform unitRoot,
        GameObject emptyInstance,
        ExpSlotRingLayoutParameters layout,
        bool filledNestedInEmpty)
    {
        if (filledSlotTemplate == null)
        {
            return null;
        }

        if (filledNestedInEmpty)
        {
            return FindFilledSlotInEmptyClone(emptyInstance);
        }

        GameObject filledInstance = Instantiate(filledSlotTemplate.gameObject, unitRoot);
        filledInstance.name = filledSlotTemplate.gameObject.name;
        ApplyUnitChildLayout(
            filledInstance.GetComponent<RectTransform>(),
            layout.FilledLocalOffset,
            layout.FilledLocalEulerZ,
            layout.FilledLayout);

        return filledInstance;
    }

    static bool IsFilledSlotNestedInEmpty(RectTransform emptyTemplate, RectTransform filledTemplate)
    {
        return emptyTemplate != null
            && filledTemplate != null
            && filledTemplate.IsChildOf(emptyTemplate);
    }

    bool IsFilledSlotNestedInEmpty()
    {
        return IsFilledSlotNestedInEmpty(emptySlotTemplate, filledSlotTemplate);
    }

    GameObject FindFilledSlotInEmptyClone(GameObject emptyInstance)
    {
        if (emptyInstance == null)
        {
            return null;
        }

        if (filledSlotTemplate != null)
        {
            Transform filledTransform = emptyInstance.transform.Find(filledSlotTemplate.name);
            if (filledTransform != null)
            {
                return filledTransform.gameObject;
            }
        }

        Image[] images = emptyInstance.GetComponentsInChildren<Image>(true);
        if (images.Length < 2)
        {
            Image emptyImage = emptyInstance.GetComponent<Image>();
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i] != emptyImage)
                {
                    return images[i].gameObject;
                }
            }

            return null;
        }

        Image rootImage = emptyInstance.GetComponent<Image>();
        for (int i = images.Length - 1; i >= 0; i--)
        {
            if (images[i] != null && images[i] != rootImage)
            {
                return images[i].gameObject;
            }
        }

        return null;
    }

    void RefreshExpSlotRingLayout(ExpSlotRingLayoutParameters layout)
    {
        for (int i = 0; i < expSlotUnits.Count; i++)
        {
            ExpSlotUnitInstance unit = expSlotUnits[i];
            if (unit.Root == null)
            {
                continue;
            }

            float t = expSlotCount > 1 ? i / (float)(expSlotCount - 1) : 0f;
            float angleDeg = Mathf.Lerp(expSlotAngleStart, expSlotAngleEnd, t);
            Vector2 unitAnchoredPosition = PolarToAnchoredPosition(layout.Radius, angleDeg);
            float unitEulerZ = angleDeg + layout.UnitRootRotationOffset;

            ApplyUnitRootLayout(unit.Root.GetComponent<RectTransform>(), unitAnchoredPosition, unitEulerZ);

            if (unit.Empty != null)
            {
                ApplyUnitChildLayout(unit.Empty.GetComponent<RectTransform>(), Vector2.zero, 0f, layout.EmptyLayout);
            }

            if (unit.Filled != null && !IsFilledSlotNestedInEmpty())
            {
                ApplyUnitChildLayout(
                    unit.Filled.GetComponent<RectTransform>(),
                    layout.FilledLocalOffset,
                    layout.FilledLocalEulerZ,
                    layout.FilledLayout);
            }
        }

        CacheExpSlotRingLayoutState(layout);
    }

    void CacheExpSlotRingLayoutState(ExpSlotRingLayoutParameters layout)
    {
        expSlotRingCachedLayout = layout;
        expSlotRingLayoutCached = true;
        expSlotRingBuiltAngleStart = expSlotAngleStart;
        expSlotRingBuiltAngleEnd = expSlotAngleEnd;
    }

    bool AreExpSlotUnitsValid()
    {
        if (expSlotUnits.Count != expSlotCount)
        {
            return false;
        }

        for (int i = 0; i < expSlotUnits.Count; i++)
        {
            ExpSlotUnitInstance unit = ResolveExpSlotUnitReferences(expSlotUnits[i]);
            expSlotUnits[i] = unit;

            if (unit.Root == null || unit.Empty == null)
            {
                return false;
            }

            if (IsFilledSlotNestedInEmpty() && unit.Filled == null)
            {
                return false;
            }

            if (!IsFilledSlotNestedInEmpty() && unit.Filled == null)
            {
                return false;
            }

            if (unit.Root.transform.parent != expSlotPivot)
            {
                return false;
            }
        }

        return true;
    }

    static bool TryComputeExpSlotRingLayoutParameters(
        RectTransform emptyTemplate,
        RectTransform filledTemplate,
        out ExpSlotRingLayoutParameters parameters)
    {
        parameters = default;

        if (emptyTemplate == null || filledTemplate == null)
        {
            return false;
        }

        float radius = emptyTemplate.anchoredPosition.magnitude;
        if (radius < 1f)
        {
            radius = filledTemplate.anchoredPosition.magnitude;
        }

        float templateAngle = Mathf.Atan2(emptyTemplate.anchoredPosition.y, emptyTemplate.anchoredPosition.x) * Mathf.Rad2Deg;
        float anchorEulerZ = emptyTemplate.localEulerAngles.z;
        Vector2 filledLocalOffset;
        float filledLocalEulerZ;

        if (IsFilledSlotNestedInEmpty(emptyTemplate, filledTemplate))
        {
            filledLocalOffset = filledTemplate.anchoredPosition;
            filledLocalEulerZ = filledTemplate.localEulerAngles.z;
        }
        else
        {
            Vector2 filledOffsetInPivot = filledTemplate.anchoredPosition - emptyTemplate.anchoredPosition;
            filledLocalOffset = RotateAnchoredOffset(-anchorEulerZ, filledOffsetInPivot);
            filledLocalEulerZ = Mathf.DeltaAngle(anchorEulerZ, filledTemplate.localEulerAngles.z);
        }

        parameters = new ExpSlotRingLayoutParameters
        {
            Radius = radius,
            UnitRootRotationOffset = anchorEulerZ - templateAngle,
            FilledLocalOffset = filledLocalOffset,
            FilledLocalEulerZ = filledLocalEulerZ,
            EmptyLayout = CaptureSlotTemplateLayout(emptyTemplate),
            FilledLayout = CaptureSlotTemplateLayout(filledTemplate)
        };

        return true;
    }

    static bool RingLayoutParametersEqual(ExpSlotRingLayoutParameters a, ExpSlotRingLayoutParameters b)
    {
        return Mathf.Approximately(a.Radius, b.Radius)
            && Mathf.Approximately(a.UnitRootRotationOffset, b.UnitRootRotationOffset)
            && a.FilledLocalOffset == b.FilledLocalOffset
            && Mathf.Approximately(a.FilledLocalEulerZ, b.FilledLocalEulerZ)
            && SlotTemplateLayoutsEqual(a.EmptyLayout, b.EmptyLayout)
            && SlotTemplateLayoutsEqual(a.FilledLayout, b.FilledLayout);
    }

    static bool SlotTemplateLayoutsEqual(SlotTemplateLayout a, SlotTemplateLayout b)
    {
        return a.sizeDelta == b.sizeDelta && a.pivot == b.pivot && a.localScale == b.localScale;
    }

    struct ExpSlotRingLayoutParameters
    {
        public float Radius;
        public float UnitRootRotationOffset;
        public Vector2 FilledLocalOffset;
        public float FilledLocalEulerZ;
        public SlotTemplateLayout EmptyLayout;
        public SlotTemplateLayout FilledLayout;
    }

    readonly struct SlotTemplateLayout
    {
        public readonly Vector2 sizeDelta;
        public readonly Vector2 pivot;
        public readonly Vector3 localScale;

        public SlotTemplateLayout(Vector2 sizeDelta, Vector2 pivot, Vector3 localScale)
        {
            this.sizeDelta = sizeDelta;
            this.pivot = pivot;
            this.localScale = localScale;
        }
    }

    static SlotTemplateLayout CaptureSlotTemplateLayout(RectTransform template)
    {
        return new SlotTemplateLayout(template.sizeDelta, template.pivot, template.localScale);
    }

    static void ApplyUnitRootLayout(RectTransform rect, Vector2 anchoredPosition, float eulerZ)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
        rect.localEulerAngles = new Vector3(0f, 0f, eulerZ);
    }

    static void ApplyUnitChildLayout(RectTransform rect, Vector2 localAnchoredPosition, float localEulerZ, SlotTemplateLayout layout)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = layout.pivot;
        rect.anchoredPosition = localAnchoredPosition;
        rect.sizeDelta = layout.sizeDelta;
        rect.localScale = layout.localScale;
        rect.localEulerAngles = new Vector3(0f, 0f, localEulerZ);
    }

    static Vector2 RotateAnchoredOffset(float degrees, Vector2 offset)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(cos * offset.x - sin * offset.y, sin * offset.x + cos * offset.y);
    }

    static Vector2 PolarToAnchoredPosition(float radius, float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
    }

    void RefreshExpSlotRing(int current, int required)
    {
        ApplyExpSlotRingFillRatio(GetExperienceFillRatio(current, required));
    }

    /// <summary>
    /// FilledSlots follow the same fillRatio as the exp bar. Slot i lights when fillRatio passes i/N
    /// (not floor), so the first slot appears as soon as the bar starts moving.
    /// </summary>
    void ApplyExpSlotRingFillRatio(float fillRatio)
    {
        if (!expSlotRingBuilt)
        {
            return;
        }

        fillRatio = Mathf.Clamp01(fillRatio);
        int slotCount = expSlotUnits.Count;
        bool filledNestedInEmpty = IsFilledSlotNestedInEmpty();
        bool fillAll = fillRatio >= 1f - 0.0001f;

        for (int i = 0; i < slotCount; i++)
        {
            float slotThreshold = slotCount > 0 ? (float)i / slotCount : 1f;
            bool isFilled = fillAll || fillRatio > slotThreshold;

            ExpSlotUnitInstance unit = ResolveExpSlotUnitReferences(expSlotUnits[i]);
            expSlotUnits[i] = unit;
            ApplyExpSlotUnitFillState(unit, isFilled, filledNestedInEmpty);
        }
    }

    ExpSlotUnitInstance ResolveExpSlotUnitReferences(ExpSlotUnitInstance unit)
    {
        if (unit.Root == null)
        {
            return unit;
        }

        if (unit.Empty == null)
        {
            Transform emptyTransform = unit.Root.transform.Find("EmptySlot");
            if (emptyTransform != null)
            {
                unit.Empty = emptyTransform.gameObject;
            }
        }

        if (unit.Filled == null)
        {
            unit.Filled = FindFilledSlotInEmptyClone(unit.Empty);
        }

        if (unit.Filled == null && filledSlotTemplate != null)
        {
            Transform filledTransform = unit.Root.transform.Find(filledSlotTemplate.name);
            if (filledTransform != null)
            {
                unit.Filled = filledTransform.gameObject;
            }
        }

        return unit;
    }

    static void ApplyExpSlotUnitFillState(ExpSlotUnitInstance unit, bool isFilled, bool filledNestedInEmpty)
    {
        if (unit.Filled != null && unit.Filled.activeSelf != isFilled)
        {
            unit.Filled.SetActive(isFilled);
        }

        if (unit.Empty == null)
        {
            return;
        }

        if (filledNestedInEmpty || (unit.Filled != null && unit.Filled.transform.IsChildOf(unit.Empty.transform)))
        {
            if (!unit.Empty.activeSelf)
            {
                unit.Empty.SetActive(true);
            }

            Image emptyImage = unit.Empty.GetComponent<Image>();
            if (emptyImage != null && emptyImage.enabled == isFilled)
            {
                emptyImage.enabled = !isFilled;
            }

            return;
        }

        bool emptyShouldBeActive = !isFilled;
        if (unit.Empty.activeSelf != emptyShouldBeActive)
        {
            unit.Empty.SetActive(emptyShouldBeActive);
        }
    }

    static bool IsGeneratedExpSlotUnitName(string objectName)
    {
        return objectName.StartsWith("ExpSlotUnit_") || objectName.StartsWith("ExpSlotRing_");
    }

    void TearDownExpSlotRing()
    {
        ClearGeneratedExpSlots();
    }

    void ClearGeneratedExpSlots()
    {
        if (expSlotPivot == null)
        {
            expSlotUnits.Clear();
            expSlotRingBuilt = false;
            return;
        }

        for (int i = expSlotPivot.childCount - 1; i >= 0; i--)
        {
            Transform child = expSlotPivot.GetChild(i);
            if (!IsGeneratedExpSlotUnitName(child.name))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        expSlotUnits.Clear();
        expSlotRingBuilt = false;
        expSlotRingLayoutCached = false;

        if (emptySlotTemplate != null)
        {
            emptySlotTemplate.gameObject.SetActive(true);
        }

        if (filledSlotTemplate != null && !IsFilledSlotNestedInEmpty())
        {
            filledSlotTemplate.gameObject.SetActive(true);
        }
    }

    void RefreshExperienceBarRotation(int current, int required)
    {
        ApplyExperienceBarFillRatio(GetExperienceFillRatio(current, required));
    }

    void ApplyExperienceBarFillRatio(float fillRatio)
    {
        if (expBarPivot == null)
        {
            return;
        }

        fillRatio = Mathf.Clamp01(fillRatio);
        float z = Mathf.Lerp(expBarAngleAtZero, expBarAngleAtFull, fillRatio);

        if (Application.isPlaying)
        {
            if (!expBarPivotBaseEulerCached)
            {
                CacheExpBarPivotBaseEuler();
            }

            expBarPivot.localEulerAngles = new Vector3(expBarPivotEulerX, expBarPivotEulerY, z);
            return;
        }

        Vector3 euler = expBarPivot.localEulerAngles;
        euler.z = z;
        expBarPivot.localEulerAngles = euler;
    }

    public void SetExperienceValues(int current)
    {
        manualCurrentExp = Mathf.Max(0, current);
        RefreshExperienceDisplay();
    }

    public void SetLevelProgressForDebug(int level, int currentExp)
    {
        useManualLevelProgressForDebug = true;
        manualLevel = Mathf.Clamp(level, 1, PlayerLevelManager.MaxLevel);
        manualCurrentExp = Mathf.Max(0, currentExp);
        SyncManualProgressToPlayerLevelManager();
        RefreshExperienceDisplay();
    }

    void SyncExpBarOrbitGizmo()
    {
        if (expBarOrbitGizmo == null && expBarPivot != null)
        {
            expBarOrbitGizmo = expBarPivot.GetComponent<ExpBarOrbitGizmo>();
        }

        if (expBarOrbitGizmo != null)
        {
            expBarOrbitGizmo.ShowOrbitCircle = showExpBarOrbitGizmo;
        }
    }

#if UNITY_EDITOR
    void OnEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
        {
            TearDownExpSlotRing();
        }
    }

    void OnValidate()
    {
        EnsureNoisesAmountTextReference();
        EnsureExperienceDisplayReferences();
        SyncExpBarOrbitGizmo();
        SyncManualProgressToPlayerLevelManager();
        SyncManualBattleRewardsToManager();
        if (Application.isPlaying && PlayerBattleRewardManager.Instance != null)
        {
            PlayerBattleRewardManager.Instance.ApplyVictoryRewardSettings(stageVictoryRewards);
        }

        SyncCombatStatsDebugOverride();
        if (Application.isPlaying)
        {
            ApplyUiButtonClickSoundSettings();
            SyncDistanceGaugeSession();
            SyncDailyDistanceScheduleConfig();
            UpdateSchedulePlayDistancePopup();
            RefreshDistanceDisplay();
        }
        else
        {
            ApplyInspectorDistanceToGame();
        }

        RefreshExperienceDisplay();
    }
#endif
}
