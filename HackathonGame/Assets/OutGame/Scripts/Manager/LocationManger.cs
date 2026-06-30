using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Globalization;

[DefaultExecutionOrder(-100)]
public class LocationManager : MonoBehaviour
{
    public static LocationManager Instance { get; private set; }

    [Header("Foreground Smooth (app open — like Maps)")]
    [SerializeField] float foregroundSampleIntervalSeconds = 0.35f;
    [SerializeField] float foregroundMinMovementMeters = 8f;
    [SerializeField] float foregroundMaxRequiredMovementMeters = 10f;
    [SerializeField] float foregroundMaxCreditPerStepMeters = 12f;
    [SerializeField] float foregroundDistanceMaxAccuracyMeters = 65f;
    [SerializeField] float foregroundLocationStaleSeconds = 4f;
    [SerializeField] float foregroundLocationRestartCooldownSeconds = 12f;
    [SerializeField] float foregroundMaxAccuracyMeters = 25f;
    [SerializeField] int foregroundMovementConfirmSamples = 3;
    [SerializeField] float foregroundMaxJumpMeters = 100f;
    [SerializeField] float fastTravelSpeedThresholdMps = 5f;
    [SerializeField] float fastTravelMaxJumpMeters = 150f;
    [SerializeField] float displaySmoothTimeSeconds = 0.35f;
    [SerializeField] float uiRefreshIntervalSeconds = 0.15f;
    [SerializeField] float foregroundPrefsSaveIntervalSeconds = 3f;

    [Header("Background / Catch-up (after background or kill)")]
    [SerializeField] float minMovementMeters = 6f;
    [SerializeField] float maxHorizontalAccuracyMeters = 40f;
    [SerializeField] float idealHorizontalAccuracyMeters = 20f;
    [SerializeField] float stationaryMinMovementMeters = 12f;
    [SerializeField] float minSpeedForWalkingMps = 0.35f;
    [SerializeField] float accuracyMovementMultiplier = 1f;

    [Header("GPS Startup")]
    [SerializeField] float locationServiceInitTimeoutSeconds = 8f;
    [SerializeField] float gpsRefineTimeoutSeconds = 12f;
    [SerializeField] float approximateAccuracyMeters = 65f;

    [SerializeField] private Button backToHomeButton;     // ホームへ戻るボタン

    // Must match IOSLocationPlugin.mm background distance gate.
    const float NativeBackgroundMinDeltaMeters = 10f;
    const float NativeBackgroundAccuracyMultiplier = 1.5f;
    const float NativeBackgroundMaxAccuracyForMultiplier = 65f;
    const float NativeBackgroundRejectAccuracyMeters = 100f;

    private static readonly string[] TrackingPrefsKeys =
    {
        "TotalDistance",
        "LastSavedLat",
        "LastSavedLng",
        "AppLaunchCount",
        "AppKilledCount",
        "AppWasRunning",
        "SessionEndedCleanly",
        "BackgroundEnterCount",
        "BackgroundReturnCount",
        "MM_ActivatedCount",
    };

    // Must match IOSLocationPlugin.mm: "{companyName}.{productName}.{shortKey}"
    static string _prefsPrefix;

    static string PrefsPrefix
    {
        get
        {
            if (_prefsPrefix == null)
            {
                _prefsPrefix = Application.companyName + "." + Application.productName + ".";
            }

            return _prefsPrefix;
        }
    }

    static string PrefsKey(string shortKey) => PrefsPrefix + shortKey;

    static bool PrefHasKey(string shortKey) => PlayerPrefs.HasKey(PrefsKey(shortKey));

    static float PrefGetFloat(string shortKey, float defaultValue = 0f) =>
        PlayerPrefs.GetFloat(PrefsKey(shortKey), defaultValue);

    static void PrefSetFloat(string shortKey, float value) =>
        PlayerPrefs.SetFloat(PrefsKey(shortKey), value);

    static int PrefGetInt(string shortKey, int defaultValue = 0) =>
        PlayerPrefs.GetInt(PrefsKey(shortKey), defaultValue);

    static void PrefSetInt(string shortKey, int value) =>
        PlayerPrefs.SetInt(PrefsKey(shortKey), value);

    static void PrefDeleteKey(string shortKey) => PlayerPrefs.DeleteKey(PrefsKey(shortKey));

    static void MigrateLegacyTrackingPrefsIfNeeded()
    {
        bool migrated = false;

        foreach (string shortKey in TrackingPrefsKeys)
        {
            if (!PlayerPrefs.HasKey(shortKey))
            {
                continue;
            }

            string prefixed = PrefsKey(shortKey);
            if (PlayerPrefs.HasKey(prefixed))
            {
                PlayerPrefs.DeleteKey(shortKey);
                migrated = true;
                continue;
            }

            if (shortKey == "TotalDistance" || shortKey == "LastSavedLat" || shortKey == "LastSavedLng")
            {
                PlayerPrefs.SetFloat(prefixed, PlayerPrefs.GetFloat(shortKey));
            }
            else
            {
                PlayerPrefs.SetInt(prefixed, PlayerPrefs.GetInt(shortKey));
            }

            PlayerPrefs.DeleteKey(shortKey);
            migrated = true;
        }

        if (migrated)
        {
            PlayerPrefs.Save();
        }
    }

    [Header("UI Display")]
    public TMP_Text debugText;

    [Header("Buttons")]
    [Tooltip("Optional. Assign a UI Button, or wire ResetAllTrackingData() from any Button On Click.")]
    public Button resetButton;

    [Tooltip("Optional. Refreshes distance and MM counters from storage without clearing data.")]
    public Button refreshButton;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _ConfigureIOSLocationPlugin(string companyName, string productName);

    [DllImport("__Internal")]
    private static extern void _SetIOSLocationDistanceAccumulationEnabled(bool enabled);

    [DllImport("__Internal")]
    private static extern void _SetIOSLocationUseBackgroundAccumulatedBucket(bool enabled);

    [DllImport("__Internal")]
    private static extern void _StartSignificantLocationTracking();

    [DllImport("__Internal")]
    private static extern void _StartBackgroundDeferredLocation();

    [DllImport("__Internal")]
    private static extern void _StopBackgroundDeferredLocation();

    [DllImport("__Internal")]
    private static extern string _GetIOSDocumentsPath();

    [DllImport("__Internal")]
    private static extern string _GetIOSLocationAuthorizationLabel();
#endif

    private string logFilePath;
    private double totalDistance;

    private bool isTrackingForeground;
    private int foregroundMovementConfirmCount;
    private Vector2 lastForegroundPosition;
    private Vector2 smoothDisplayPosition;
    private Vector2 smoothDisplayVelocity;
    private bool hasSmoothDisplayPosition;
    private float nextForegroundSampleTime;
    private float nextUiRefreshTime;
    private float lastForegroundPrefsSaveTime;
    private float displaySpeedMps;
    private Vector2 speedReferencePosition;
    private float speedReferenceTime;
    private double speedReferenceGpsTimestamp = -1d;
    private bool hasSpeedReference;

    private int appLaunchCount;
    private int backgroundEnterCount;
    private int backgroundReturnCount;
    private int mmFileActivatedCount;
    private int mmLogFileUpdateCount;
    private int deferredLogFileUpdateCount;
    private int appKilledCount;
    private float lastReportedAccuracyMeters = -1f;
    private string lastGpsStatusMessage = "Starting";
    private string lastIosAuthLabel = "N/A";
    private NetworkReachability lastNetworkReachability = NetworkReachability.NotReachable;
    private bool isRestartingLocationService;
    private bool pendingCatchUpOnFirstGpsFix;
    private Coroutine foregroundLocationCoroutine;
    private double lastSeenGpsTimestamp = -1d;
    private double lastSeenGpsLat;
    private double lastSeenGpsLng;
    private float unchangedGpsSinceTime = -1f;
    private float nextLocationServiceRestartTime;

    private long cachedLogFileLength = -1;
    private int cachedMmLogFileUpdateCount;
    private int cachedDeferredLogFileUpdateCount;

    bool trackingStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("Runtime_LocationManager");
        Instance = go.AddComponent<LocationManager>();
        DontDestroyOnLoad(go);
    }

    /// <summary>PlayerPrefs に保存された累計距離（メートル）を返す。Home など他シーンからも利用可。</summary>
    public static float GetTotalDistanceMeters()
    {
        MigrateLegacyTrackingPrefsIfNeeded();
        return PrefGetFloat("TotalDistance");
    }

    /// <summary>累計距離に対する「1000m ごと +1」の段数（切り捨て）。</summary>
    public static int GetThousandMeterBonusCount()
    {
        return Mathf.FloorToInt(GetTotalDistanceMeters() / 1000f);
    }

    /// <summary>累計距離を減算（ノイズ撃破など）。0 未満にはしない。</summary>
    public static void SubtractTotalDistanceMeters(float meters)
    {
        if (meters <= 0f)
        {
            return;
        }

        MigrateLegacyTrackingPrefsIfNeeded();
        float next = Mathf.Max(0f, GetTotalDistanceMeters() - meters);
        PrefSetFloat("TotalDistance", next);
        PlayerPrefs.Save();

        if (Instance != null)
        {
            Instance.totalDistance = next;
        }
    }

    /// <summary>Sets saved TotalDistance without clearing GPS anchors.</summary>
    public static void SetTotalDistanceMeters(float meters)
    {
        MigrateLegacyTrackingPrefsIfNeeded();
        float next = Mathf.Max(0f, meters);
        PrefSetFloat("TotalDistance", next);
        PlayerPrefs.Save();

        if (Instance != null)
        {
            Instance.totalDistance = next;
        }
    }

    /// <summary>After schedule rollover: memory must match prefs so leftover GPS credit is not re-applied.</summary>
    public static void ForceAdoptStoredTotalDistance()
    {
        MigrateLegacyTrackingPrefsIfNeeded();
        float storedDistance = GetTotalDistanceMeters();

        if (Instance != null)
        {
            Instance.totalDistance = storedDistance;
        }
    }

    const string PrefsBackgroundAccumulatedMeters = "OutGame_BackgroundAccumulatedMeters";
    const string PrefsPlayDistanceSnapshotAtBackground = "OutGame_PlayDistanceAtBackground";

    /// <summary>GPS walk credit since last rollover (not added to DistanceTravelled while schedule is on).</summary>
    public static float GetBackgroundAccumulatedMeters()
    {
        MigrateLegacyTrackingPrefsIfNeeded();
        return Mathf.Max(0f, PrefGetFloat(PrefsBackgroundAccumulatedMeters));
    }

    public static void ClearBackgroundAccumulatedMeters()
    {
        MigrateLegacyTrackingPrefsIfNeeded();
        PrefSetFloat(PrefsBackgroundAccumulatedMeters, 0f);
        PlayerPrefs.Save();
    }

    static void AddBackgroundAccumulatedMeters(float meters)
    {
        if (meters <= 0f)
        {
            return;
        }

        MigrateLegacyTrackingPrefsIfNeeded();
        float next = GetBackgroundAccumulatedMeters() + meters;
        PrefSetFloat(PrefsBackgroundAccumulatedMeters, next);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// iOS native background GPS historically wrote walk credit into TotalDistance.
    /// Move that credit into Background GPS today and restore play DistanceTravelled.
    /// </summary>
    public static void ReconcileNativeWalkCreditsIntoBackgroundBucket()
    {
        if (!HomeDailyDistanceSchedule.IsEnabled)
        {
            return;
        }

        MigrateLegacyTrackingPrefsIfNeeded();
        if (!PrefHasKey(PrefsPlayDistanceSnapshotAtBackground))
        {
            return;
        }

        float playDistanceSnapshot = PrefGetFloat(PrefsPlayDistanceSnapshotAtBackground);
        float totalInPrefs = PrefGetFloat("TotalDistance");
        if (totalInPrefs > playDistanceSnapshot + 0.5f)
        {
            AddBackgroundAccumulatedMeters(totalInPrefs - playDistanceSnapshot);
            SetTotalDistanceMeters(playDistanceSnapshot);
        }

        PrefDeleteKey(PrefsPlayDistanceSnapshotAtBackground);
        PlayerPrefs.Save();
    }

    static void SavePlayDistanceSnapshotForBackgroundReconcile()
    {
        if (!HomeDailyDistanceSchedule.IsEnabled)
        {
            return;
        }

        MigrateLegacyTrackingPrefsIfNeeded();
        PrefSetFloat(PrefsPlayDistanceSnapshotAtBackground, GetTotalDistanceMeters());
        PlayerPrefs.Save();
    }

    /// <summary>Credits walked meters to background (schedule on) or TotalDistance (schedule off).</summary>
    public static void CreditWalkMeters(double meters)
    {
        if (meters <= 0d)
        {
            return;
        }

        if (HomeDailyDistanceSchedule.IsEnabled)
        {
            AddBackgroundAccumulatedMeters((float)meters);
            return;
        }

        if (Instance != null)
        {
            Instance.totalDistance += meters;
            PrefSetFloat("TotalDistance", (float)Instance.totalDistance);
            PlayerPrefs.Save();
            return;
        }

        SetTotalDistanceMeters(GetTotalDistanceMeters() + (float)meters);
    }

    static void PersistWalkTrackingPrefs()
    {
        MigrateLegacyTrackingPrefsIfNeeded();
        if (HomeDailyDistanceSchedule.IsEnabled)
        {
            PrefSetFloat(PrefsBackgroundAccumulatedMeters, GetBackgroundAccumulatedMeters());
        }
        else if (Instance != null)
        {
            PrefSetFloat("TotalDistance", (float)Instance.totalDistance);
        }

        PlayerPrefs.Save();
    }

    /// <summary>After daily rollover: keep TotalDistance, reset GPS anchors for fresh step credit.</summary>
    public static void ResetGpsAnchorsForNewTrackingPeriod()
    {
        MigrateLegacyTrackingPrefsIfNeeded();
        PrefDeleteKey("LastSavedLat");
        PrefDeleteKey("LastSavedLng");
        PlayerPrefs.Save();

        if (Instance == null)
        {
            return;
        }

        Instance.foregroundMovementConfirmCount = 0;
        Instance.hasSmoothDisplayPosition = false;
        Instance.smoothDisplayVelocity = Vector2.zero;
        Instance.lastSeenGpsTimestamp = -1d;
        Instance.unchangedGpsSinceTime = -1f;
        Instance.ResetSpeedReference();
    }

    /// <summary>Applies daily schedule: block native / foreground GPS credit during play window.</summary>
    public static void ApplyScheduleGpsAccumulationPolicy()
    {
        bool allow = HomeDailyDistanceSchedule.IsGpsDistanceAccumulationAllowed;
#if UNITY_IOS && !UNITY_EDITOR
        _SetIOSLocationDistanceAccumulationEnabled(allow);
        _SetIOSLocationUseBackgroundAccumulatedBucket(HomeDailyDistanceSchedule.IsEnabled);
#endif
        if (Instance != null)
        {
            Instance.ClampTotalDistanceToStorageWhenPlayLocked();
        }
    }

    /// <summary>累計距離を加算（デバッグ用）。</summary>
    public static void AddTotalDistanceMeters(float meters)
    {
        if (meters <= 0f)
        {
            return;
        }

        MigrateLegacyTrackingPrefsIfNeeded();
        float next = GetTotalDistanceMeters() + meters;
        PrefSetFloat("TotalDistance", next);
        PlayerPrefs.Save();

        if (Instance != null)
        {
            Instance.totalDistance = next;
        }
    }

    void Awake()
    {
        MigrateLegacyTrackingPrefsIfNeeded();

        if (Instance != null && Instance != this)
        {
            Instance.AbsorbDebugScenePeer(this);
            enabled = false;
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        UpdateLifecycleCounters();
    }

    void Start()
    {
        if (Instance != this)
        {
            return;
        }

        BeginTracking();
    }

    /// <summary>DistanceDebugScene のシーン配置コンポーネントから UI 参照を引き継ぐ。</summary>
    public void AbsorbDebugScenePeer(LocationManager scenePeer)
    {
        if (scenePeer == null || scenePeer == this)
        {
            return;
        }

        if (scenePeer.debugText != null)
        {
            debugText = scenePeer.debugText;
        }

        if (scenePeer.backToHomeButton != null)
        {
            backToHomeButton = scenePeer.backToHomeButton;
        }

        if (scenePeer.resetButton != null)
        {
            resetButton = scenePeer.resetButton;
        }

        if (scenePeer.refreshButton != null)
        {
            refreshButton = scenePeer.refreshButton;
        }

        BindDebugSceneButtons();
        RefreshDebugDisplay();
    }

    void BeginTracking()
    {
        if (trackingStarted)
        {
            return;
        }

        trackingStarted = true;

#if UNITY_IOS && !UNITY_EDITOR
        _ConfigureIOSLocationPlugin(Application.companyName, Application.productName);
        _StartSignificantLocationTracking();
        logFilePath = Path.Combine(_GetIOSDocumentsPath(), "positions_log.txt");
        RefreshIosAuthorizationLabel();
#else
        logFilePath = Path.Combine(Application.persistentDataPath, "positions_log.txt");
#endif

        totalDistance = PrefGetFloat("TotalDistance");
        lastNetworkReachability = Application.internetReachability;

        BindDebugSceneButtons();
        ApplyScheduleGpsAccumulationPolicy();

        if (debugText != null)
        {
            RefreshDebugDisplay();
        }

        StartForegroundLocationTracking();
    }

    void BindDebugSceneButtons()
    {
        // RemoveAllListeners() does not clear Inspector persistent calls; replace the event entirely.
        if (resetButton != null)
        {
            resetButton.onClick = new Button.ButtonClickedEvent();
            resetButton.onClick.AddListener(ResetAllTrackingData);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick = new Button.ButtonClickedEvent();
            refreshButton.onClick.AddListener(RefreshDebugDisplay);
        }

        if (backToHomeButton != null)
        {
            backToHomeButton.onClick = new Button.ButtonClickedEvent();
            backToHomeButton.onClick.AddListener(OnDistanceDebugBackToHomeClicked);
        }
    }

    void OnDistanceDebugBackToHomeClicked()
    {
        if (SceneTransferManager.Instance != null)
        {
            SceneTransferManager.Instance.GoBack();
        }
    }

    void OnEnable()
    {
        if (debugText != null && Application.isPlaying)
        {
            RefreshDebugDisplay();
        }
    }

    void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        if (resetButton != null)
        {
            resetButton.onClick.RemoveListener(ResetAllTrackingData);
        }

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshDebugDisplay);
        }

        if (backToHomeButton != null)
        {
            backToHomeButton.onClick.RemoveListener(OnDistanceDebugBackToHomeClicked);
        }

        Instance = null;
        trackingStarted = false;
    }

    public void RefreshDebugDisplay()
    {
        RefreshIosAuthorizationLabel();
        ReconcileNativeWalkCreditsIntoBackgroundBucket();
        SyncTotalDistanceFromStorage(forceFromPrefs: true);
        appLaunchCount = PrefGetInt("AppLaunchCount");
        appKilledCount = PrefGetInt("AppKilledCount");
        backgroundEnterCount = PrefGetInt("BackgroundEnterCount");
        backgroundReturnCount = PrefGetInt("BackgroundReturnCount");
        mmFileActivatedCount = PrefGetInt("MM_ActivatedCount");
        mmLogFileUpdateCount = CountMmUpdatesInLogFile();
        deferredLogFileUpdateCount = GetCachedLogCount("iOS_Background_Deferred", ref cachedDeferredLogFileUpdateCount);

        if (Input.location.status == LocationServiceStatus.Running &&
            TryGetLiveDisplayLocation(out float lat, out float lng, out float accuracy))
        {
            if (isTrackingForeground)
            {
                ApplyGpsRefinement(lat, lng, accuracy, runCatchUp: pendingCatchUpOnFirstGpsFix);
            }
            else if (TryGetCachedPosition(out float cachedLat, out float cachedLng))
            {
                BeginLiveTracking(cachedLat, cachedLng, -1f, $"Last known · {GetNetworkReachabilityLabel()}");
                ApplyGpsRefinement(lat, lng, accuracy, runCatchUp: true);
            }
        }
        else if (TryGetCachedPosition(out float cachedLat, out float cachedLng) && !isTrackingForeground)
        {
            BeginLiveTracking(cachedLat, cachedLng, -1f, $"Last known · {GetNetworkReachabilityLabel()}");
            EnsureLocationServiceRunning();
        }
        else if (Input.location.isEnabledByUser && !isRestartingLocationService)
        {
            StartForegroundLocationTracking();
        }

        UpdateUI();
    }

    public void ResetAllTrackingData()
    {
        foreach (string key in TrackingPrefsKeys)
        {
            PrefDeleteKey(key);
        }
        PlayerPrefs.Save();

        if (!string.IsNullOrEmpty(logFilePath) && File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }

        InvalidateLogCountCache();
        totalDistance = 0;
        appLaunchCount = 0;
        backgroundEnterCount = 0;
        backgroundReturnCount = 0;
        mmFileActivatedCount = 0;
        appKilledCount = 0;
        isTrackingForeground = false;
        foregroundMovementConfirmCount = 0;
        lastForegroundPosition = Vector2.zero;
        hasSmoothDisplayPosition = false;
        smoothDisplayVelocity = Vector2.zero;
        displaySpeedMps = 0f;
        nextForegroundSampleTime = 0f;
        nextUiRefreshTime = 0f;
        lastForegroundPrefsSaveTime = 0f;
        ResetSpeedReference();

        if (Input.location.isEnabledByUser)
        {
            Input.location.Stop();
        }

        StopAllCoroutines();

        PrefSetInt("AppWasRunning", 1);
        PrefSetInt("SessionEndedCleanly", 0);
        PlayerPrefs.Save();

        UpdateUI();
        StartForegroundLocationTracking();
    }

    void StartForegroundLocationTracking()
    {
        if (foregroundLocationCoroutine != null)
        {
            StopCoroutine(foregroundLocationCoroutine);
        }

        foregroundLocationCoroutine = StartCoroutine(HandleReturnToForeground());
    }

    void UpdateLifecycleCounters()
    {
        bool previousWasRunning = PrefGetInt("AppWasRunning") == 1;
        bool previousSessionEndedCleanly = PrefGetInt("SessionEndedCleanly", 1) == 1;

        appLaunchCount = PrefGetInt("AppLaunchCount") + 1;
        PrefSetInt("AppLaunchCount", appLaunchCount);

        appKilledCount = PrefGetInt("AppKilledCount");
        if (previousWasRunning && !previousSessionEndedCleanly)
        {
            appKilledCount++;
            PrefSetInt("AppKilledCount", appKilledCount);
        }

        PrefSetInt("AppWasRunning", 1);
        PrefSetInt("SessionEndedCleanly", 0);
        PlayerPrefs.Save();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
        {
            backgroundReturnCount = PrefGetInt("BackgroundReturnCount") + 1;
            PrefSetInt("BackgroundReturnCount", backgroundReturnCount);
            PrefSetInt("SessionEndedCleanly", 1);
            PlayerPrefs.Save();

#if UNITY_IOS && !UNITY_EDITOR
            _StopBackgroundDeferredLocation();
            RefreshIosAuthorizationLabel();
#endif
            ReconcileNativeWalkCreditsIntoBackgroundBucket();
            SyncTotalDistanceFromStorage(forceFromPrefs: true);
            StartForegroundLocationTracking();
        }
        else
        {
            backgroundEnterCount = PrefGetInt("BackgroundEnterCount") + 1;
            PrefSetInt("BackgroundEnterCount", backgroundEnterCount);
            PlayerPrefs.Save();

            if (TryGetLiveDisplayLocation(out float lat, out float lng, out _) ||
                TryGetCachedPosition(out lat, out lng))
            {
                SaveLastKnownPosition(lat, lng);
                AppendLog("Pre-Background-Lock", lat, lng);
            }

            SavePlayDistanceSnapshotForBackgroundReconcile();
            PersistWalkTrackingPrefs();

            Input.location.Stop();

#if UNITY_IOS && !UNITY_EDITOR
            _StartBackgroundDeferredLocation();
#endif
            isTrackingForeground = false;
            foregroundMovementConfirmCount = 0;
            hasSmoothDisplayPosition = false;
            unchangedGpsSinceTime = -1f;
            lastSeenGpsTimestamp = -1d;
            ResetSpeedReference();
            UpdateUI();
        }
    }

    void OnApplicationQuit()
    {
#if UNITY_EDITOR
        PrefSetInt("SessionEndedCleanly", 1);
        PrefSetInt("AppWasRunning", 0);
        PlayerPrefs.Save();
#endif
    }

    System.Collections.IEnumerator HandleReturnToForeground()
    {
        isRestartingLocationService = true;
        pendingCatchUpOnFirstGpsFix = true;
        isTrackingForeground = false;
        ReconcileNativeWalkCreditsIntoBackgroundBucket();
        totalDistance = PrefGetFloat("TotalDistance");
        mmFileActivatedCount = PrefGetInt("MM_ActivatedCount");

        if (!Input.location.isEnabledByUser)
        {
            lastGpsStatusMessage = "GPS disabled in Settings";
            UpdateUI("GPS Disabled in System Settings.");
            isRestartingLocationService = false;
            yield break;
        }

        yield return null;

        string networkHint = GetNetworkReachabilityLabel();

        // Maps-like: show last known position immediately (no wait for satellite fix).
        if (TryGetCachedPosition(out float cachedLat, out float cachedLng))
        {
            BeginLiveTracking(cachedLat, cachedLng, -1f, $"Last known · refining ({networkHint})");
            UpdateUI();
        }

        EnsureLocationServiceRunning();

        float initDeadline = Time.time + locationServiceInitTimeoutSeconds;
        while (Input.location.status == LocationServiceStatus.Initializing && Time.time < initDeadline)
        {
            if (!isTrackingForeground && TryGetCachedPosition(out cachedLat, out cachedLng))
            {
                BeginLiveTracking(cachedLat, cachedLng, -1f, $"Last known · refining ({networkHint})");
            }

            if (TryGetApproximateLocation(out float approxLat, out float approxLng, out float approxAccuracy))
            {
                ApplyGpsRefinement(approxLat, approxLng, approxAccuracy, runCatchUp: true);
                isRestartingLocationService = false;
                yield break;
            }

            lastGpsStatusMessage = $"Starting GPS ({networkHint})...";
            UpdateUI();
            yield return new WaitForSeconds(0.3f);
        }

        if (Input.location.status == LocationServiceStatus.Failed && !isTrackingForeground)
        {
            lastGpsStatusMessage = "Location service failed";
            UpdateUI("Location service failed. Tap Refresh to retry.");
            isRestartingLocationService = false;
            yield break;
        }

        float refineDeadline = Time.time + gpsRefineTimeoutSeconds;
        while (Time.time < refineDeadline && Input.location.status == LocationServiceStatus.Running)
        {
            if (TryGetForegroundLocation(out float lat, out float lng, out float accuracy))
            {
                ApplyGpsRefinement(lat, lng, accuracy, runCatchUp: true);
                isRestartingLocationService = false;
                yield break;
            }

            if (TryGetApproximateLocation(out float approxLat, out float approxLng, out float approxAccuracy))
            {
                if (!isTrackingForeground)
                {
                    BeginLiveTracking(approxLat, approxLng, approxAccuracy, $"Approximate · refining ({networkHint})");
                }
                else
                {
                    ApplyGpsRefinement(approxLat, approxLng, approxAccuracy, runCatchUp: true);
                }
            }

            if (isTrackingForeground)
            {
                lastGpsStatusMessage = $"Live · refining GPS ({networkHint})";
                UpdateUI();
            }

            yield return new WaitForSeconds(0.3f);
        }

        if (!isTrackingForeground && TryGetCachedPosition(out cachedLat, out cachedLng))
        {
            BeginLiveTracking(cachedLat, cachedLng, -1f, $"Last known ({networkHint})");
        }
        else if (isTrackingForeground)
        {
            lastGpsStatusMessage = $"Live · GPS still refining ({networkHint})";
        }

        isRestartingLocationService = false;
        UpdateUI();
    }

    void EnsureLocationServiceRunning()
    {
        if (Input.location.status == LocationServiceStatus.Running ||
            Input.location.status == LocationServiceStatus.Initializing)
        {
            return;
        }

        // updateDistance 0 = as often as iOS delivers fixes (lastData was staying stale at 1m).
        Input.location.Start(5f, 0f);
    }

    void BeginLiveTracking(float lat, float lng, float accuracy, string statusMessage)
    {
        isTrackingForeground = true;
        foregroundMovementConfirmCount = 0;
        unchangedGpsSinceTime = Time.time;
        lastSeenGpsTimestamp = -1d;
        SyncForegroundAnchors(lat, lng);
        NoteForegroundGpsSample(lat, lng, accuracy);
        lastReportedAccuracyMeters = accuracy;
        lastGpsStatusMessage = statusMessage;
        ResetSpeedReference();
        MarkSpeedReference(lat, lng);
        nextForegroundSampleTime = 0f;
        nextUiRefreshTime = 0f;
        lastForegroundPrefsSaveTime = Time.time;
    }

    void ApplyGpsRefinement(float lat, float lng, float accuracy, bool runCatchUp)
    {
        if (runCatchUp && pendingCatchUpOnFirstGpsFix)
        {
            ProcessForegroundCatchUp(lat, lng, accuracy);
            pendingCatchUpOnFirstGpsFix = false;
        }

        if (!isTrackingForeground)
        {
            BeginLiveTracking(lat, lng, accuracy, BuildLiveStatusMessage(accuracy));
        }
        else
        {
            // Do not reset lastForegroundPosition here — refine/approx callbacks were
            // wiping the walk anchor every ~0.3s so distance never accumulated.
            lastReportedAccuracyMeters = accuracy;
            lastGpsStatusMessage = BuildLiveStatusMessage(accuracy);
            if (!hasSmoothDisplayPosition)
            {
                SyncForegroundAnchors(lat, lng);
            }
        }
    }

    string BuildLiveStatusMessage(float accuracy)
    {
        if (accuracy <= 0f)
        {
            return "Live · last known";
        }

        if (accuracy <= idealHorizontalAccuracyMeters)
        {
            return $"Live · {displaySpeedMps:F1} m/s · {accuracy:F0}m";
        }

        if (accuracy <= approximateAccuracyMeters)
        {
            return $"Live · approx {accuracy:F0}m · refining";
        }

        return $"Live · weak {accuracy:F0}m";
    }

    bool TryGetCachedPosition(out float lat, out float lng)
    {
        lat = 0f;
        lng = 0f;

        if (!TryGetLastKnownAnchor(out double anchorLat, out double anchorLng))
        {
            return false;
        }

        lat = (float)anchorLat;
        lng = (float)anchorLng;
        return IsValidLatLng(lat, lng);
    }

    bool TryGetApproximateLocation(out float lat, out float lng, out float accuracy)
    {
        lat = 0f;
        lng = 0f;
        accuracy = 0f;

        if (Input.location.status != LocationServiceStatus.Running)
        {
            return false;
        }

        LocationInfo data = Input.location.lastData;
        if (!IsValidLatLng(data.latitude, data.longitude))
        {
            return false;
        }

        lat = data.latitude;
        lng = data.longitude;
        accuracy = data.horizontalAccuracy;
        if (accuracy <= 0f)
        {
            accuracy = approximateAccuracyMeters;
        }
        else if (accuracy > approximateAccuracyMeters)
        {
            return false;
        }

        return true;
    }

    bool TryGetLiveDisplayLocation(out float lat, out float lng, out float accuracy)
    {
        if (TryGetForegroundLocation(out lat, out lng, out accuracy))
        {
            return true;
        }

        return TryGetApproximateLocation(out lat, out lng, out accuracy);
    }

    static bool IsValidLatLng(double lat, double lng)
    {
        if (Math.Abs(lat) < 0.0001 && Math.Abs(lng) < 0.0001)
        {
            return false;
        }

        return Math.Abs(lat) <= 90.0 && Math.Abs(lng) <= 180.0;
    }

    void SyncForegroundAnchors(float lat, float lng)
    {
        lastForegroundPosition = new Vector2(lat, lng);
        smoothDisplayPosition = new Vector2(lat, lng);
        smoothDisplayVelocity = Vector2.zero;
        hasSmoothDisplayPosition = true;
    }

    string GetNetworkReachabilityLabel()
    {
        switch (Application.internetReachability)
        {
            case NetworkReachability.ReachableViaLocalAreaNetwork:
                return "WiFi";
            case NetworkReachability.ReachableViaCarrierDataNetwork:
                return "Cellular";
            default:
                return "No network";
        }
    }

    // After background/kill: compare current GPS with the last anchor (PlayerPrefs or MM log).
    void ProcessForegroundCatchUp(double currentLat, double currentLng, float accuracy)
    {
        totalDistance = PrefGetFloat("TotalDistance");
        mmFileActivatedCount = PrefGetInt("MM_ActivatedCount");

        if (!TryGetLastKnownAnchor(out double anchorLat, out double anchorLng))
        {
            SaveLastKnownPosition(currentLat, currentLng);
            SyncForegroundAnchors((float)currentLat, (float)currentLng);
            UpdateUI();
            return;
        }

        if (accuracy <= 0f || accuracy > NativeBackgroundRejectAccuracyMeters)
        {
            SaveLastKnownPosition(currentLat, currentLng);
            SyncForegroundAnchors((float)currentLat, (float)currentLng);
            UpdateUI();
            return;
        }

        double delta = CalculateDistance(anchorLat, anchorLng, currentLat, currentLng);
        float minDelta = GetNativeStyleMinMovementMeters(accuracy);
        if (delta <= minDelta)
        {
            SyncForegroundAnchors((float)currentLat, (float)currentLng);
            UpdateUI();
            return;
        }

        double credited = Math.Min(delta, minDelta + accuracy);
        credited = Math.Min(credited, foregroundMaxCreditPerStepMeters);
        if (credited < 1.0)
        {
            SyncForegroundAnchors((float)currentLat, (float)currentLng);
            UpdateUI();
            return;
        }

        if (!HomeDailyDistanceSchedule.IsGpsDistanceAccumulationAllowed)
        {
            SyncForegroundAnchors((float)currentLat, (float)currentLng);
            UpdateUI();
            return;
        }

        CreditWalkMeters(credited);
        if (HomeDailyDistanceSchedule.IsEnabled)
        {
            totalDistance = GetTotalDistanceMeters();
        }

        AppendLog("Catch-Up-Foreground", currentLat, currentLng);

        SaveLastKnownPosition(currentLat, currentLng);
        SyncForegroundAnchors((float)currentLat, (float)currentLng);
        UpdateUI();
    }

    bool TryGetLastKnownAnchor(out double lat, out double lng)
    {
        lat = 0;
        lng = 0;

        if (PrefHasKey("LastSavedLat"))
        {
            lat = PrefGetFloat("LastSavedLat");
            lng = PrefGetFloat("LastSavedLng");
            return true;
        }

        return TryGetLastLoggedPosition(out lat, out lng);
    }

    bool TryGetLastLoggedPosition(out double lat, out double lng)
    {
        lat = 0;
        lng = 0;

        if (!File.Exists(logFilePath))
        {
            return false;
        }

        string[] lines = File.ReadAllLines(logFilePath);
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            if (TryParseLogLineCoordinates(lines[i], out lat, out lng))
            {
                return true;
            }
        }

        return false;
    }

    bool TryParseLogLineCoordinates(string line, out double lat, out double lng)
    {
        lat = 0;
        lng = 0;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string[] parts = line.Trim().Split(',');
        if (parts.Length < 4)
        {
            return false;
        }

        int latIndex = parts.Length - 2;
        int lngIndex = parts.Length - 1;

        if (!double.TryParse(parts[latIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out lat))
        {
            return false;
        }

        if (!double.TryParse(parts[lngIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out lng))
        {
            return false;
        }

        return Math.Abs(lat) <= 90.0 && Math.Abs(lng) <= 180.0;
    }

    void Update()
    {
        HomeDailyDistanceSchedule.TickWhenEnabled();

        NetworkReachability reachability = Application.internetReachability;
        if (reachability != lastNetworkReachability)
        {
            lastNetworkReachability = reachability;
            if (isTrackingForeground)
            {
                lastGpsStatusMessage = $"Live · network {GetNetworkReachabilityLabel()}";
            }
        }

        if (!isTrackingForeground)
        {
            return;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            EnsureLocationServiceRunning();
            return;
        }

        TryRestartStaleForegroundLocation();
        UpdateSmoothDisplayPosition();

        if (Time.time >= nextForegroundSampleTime)
        {
            nextForegroundSampleTime = Time.time + foregroundSampleIntervalSeconds;
            ProcessForegroundSmoothSample();
        }

        if (Time.time >= nextUiRefreshTime)
        {
            nextUiRefreshTime = Time.time + uiRefreshIntervalSeconds;
            UpdateUI();
        }
    }

    void UpdateSmoothDisplayPosition()
    {
        if (!hasSmoothDisplayPosition || !TryGetLiveDisplayLocation(out float rawLat, out float rawLng, out float accuracy))
        {
            return;
        }

        lastReportedAccuracyMeters = accuracy;
        Vector2 rawPosition = new Vector2(rawLat, rawLng);
        smoothDisplayPosition = Vector2.SmoothDamp(
            smoothDisplayPosition,
            rawPosition,
            ref smoothDisplayVelocity,
            displaySmoothTimeSeconds);
    }

    void ProcessForegroundSmoothSample()
    {
        if (pendingCatchUpOnFirstGpsFix &&
            TryGetForegroundLocationForDistance(out float catchLat, out float catchLng, out float catchAccuracy))
        {
            ProcessForegroundCatchUp(catchLat, catchLng, catchAccuracy);
            pendingCatchUpOnFirstGpsFix = false;
        }

        if (!TryGetForegroundLocationForDistance(out float currentLat, out float currentLng, out float accuracy))
        {
            foregroundMovementConfirmCount = 0;

            if (TryGetApproximateLocation(out float approxLat, out float approxLng, out float approxAccuracy))
            {
                lastReportedAccuracyMeters = approxAccuracy;
                lastGpsStatusMessage = $"Live · approx {approxAccuracy:F0}m · refining";
            }

            return;
        }

        lastReportedAccuracyMeters = accuracy;
        NoteForegroundGpsSample(currentLat, currentLng, accuracy);
        displaySpeedMps = EstimateSpeedMps(currentLat, currentLng);
        lastGpsStatusMessage = BuildLiveStatusMessage(accuracy);

        double deltaFromAnchor = CalculateDistance(
            lastForegroundPosition.x,
            lastForegroundPosition.y,
            currentLat,
            currentLng);

        float allowedJumpMeters = GetForegroundMaxJumpMeters(accuracy);
        if (deltaFromAnchor > allowedJumpMeters)
        {
            // Train / sparse GPS: large but plausible jumps still credit (reference native uses full delta).
            bool fastTravelJump = displaySpeedMps >= fastTravelSpeedThresholdMps
                || (accuracy > idealHorizontalAccuracyMeters
                    && deltaFromAnchor <= fastTravelMaxJumpMeters
                    && accuracy <= foregroundDistanceMaxAccuracyMeters);

            if (fastTravelJump)
            {
                foregroundMovementConfirmCount = 0;
                ApplyForegroundMovement(currentLat, currentLng, accuracy);
                return;
            }

            foregroundMovementConfirmCount = 0;
            lastForegroundPosition = new Vector2(currentLat, currentLng);
            return;
        }

        if (!IsForegroundMovementSignificant(deltaFromAnchor, accuracy))
        {
            foregroundMovementConfirmCount = 0;
            return;
        }

        int requiredConfirmSamples = GetRequiredForegroundConfirmSamples(accuracy);
        foregroundMovementConfirmCount++;
        if (foregroundMovementConfirmCount < requiredConfirmSamples)
        {
            return;
        }

        foregroundMovementConfirmCount = 0;
        ApplyForegroundMovement(currentLat, currentLng, accuracy);
    }

    bool TryGetForegroundLocationForDistance(out float lat, out float lng, out float accuracy)
    {
        return TryGetLocation(
            out lat,
            out lng,
            out accuracy,
            requireIdealAccuracy: false,
            maxAccuracy: foregroundDistanceMaxAccuracyMeters);
    }

    bool TryGetForegroundLocation(out float lat, out float lng, out float accuracy)
    {
        return TryGetLocation(out lat, out lng, out accuracy, requireIdealAccuracy: false, maxAccuracy: foregroundMaxAccuracyMeters);
    }

    bool TryGetLocation(out float lat, out float lng, out float accuracy, bool requireIdealAccuracy = false, float maxAccuracy = -1f)
    {
        lat = 0f;
        lng = 0f;
        accuracy = 0f;

        if (Input.location.status != LocationServiceStatus.Running)
        {
            return false;
        }

        LocationInfo data = Input.location.lastData;
        if (!IsValidLatLng(data.latitude, data.longitude))
        {
            return false;
        }

        accuracy = data.horizontalAccuracy;
        float accuracyLimit = maxAccuracy > 0f ? maxAccuracy : maxHorizontalAccuracyMeters;
        if (accuracy <= 0f || accuracy > accuracyLimit)
        {
            return false;
        }

        if (requireIdealAccuracy && accuracy > idealHorizontalAccuracyMeters)
        {
            return false;
        }

        lat = data.latitude;
        lng = data.longitude;
        NoteForegroundGpsSample(lat, lng, accuracy, data.timestamp);
        return true;
    }

    int CountMmUpdatesInLogFile()
    {
        return GetCachedLogCount("iOS_Background_Significant", ref cachedMmLogFileUpdateCount);
    }

    int GetCachedLogCount(string tag, ref int cachedCount)
    {
        if (string.IsNullOrEmpty(logFilePath) || !File.Exists(logFilePath))
        {
            cachedLogFileLength = 0;
            cachedCount = 0;
            return 0;
        }

        long fileLength = new FileInfo(logFilePath).Length;
        if (fileLength == cachedLogFileLength)
        {
            return cachedCount;
        }

        cachedCount = CountLogUpdatesWithTag(tag);
        cachedLogFileLength = fileLength;
        return cachedCount;
    }

    void InvalidateLogCountCache()
    {
        cachedLogFileLength = -1;
    }

    int CountLogUpdatesWithTag(string tag)
    {
        if (string.IsNullOrEmpty(logFilePath) || !File.Exists(logFilePath))
        {
            return 0;
        }

        int count = 0;
        string[] lines = File.ReadAllLines(logFilePath);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].IndexOf(tag, StringComparison.Ordinal) >= 0)
            {
                count++;
            }
        }

        return count;
    }

    static float GetNativeStyleMinMovementMeters(float accuracy)
    {
        float minDelta = NativeBackgroundMinDeltaMeters;
        if (accuracy > 0f && accuracy <= NativeBackgroundMaxAccuracyForMultiplier)
        {
            minDelta = Mathf.Max(minDelta, accuracy * NativeBackgroundAccuracyMultiplier);
        }

        return minDelta;
    }

    static bool IsNativeStyleBackgroundMovementSignificant(double deltaMeters, float accuracy)
    {
        return deltaMeters > GetNativeStyleMinMovementMeters(accuracy);
    }

    float EstimateSpeedMps(float lat, float lng)
    {
        if (!hasSpeedReference)
        {
            return -1f;
        }

        double distanceMeters = CalculateDistance(
            speedReferencePosition.x,
            speedReferencePosition.y,
            lat,
            lng);

        float elapsedSeconds = -1f;
        if (speedReferenceGpsTimestamp > 0d &&
            Input.location.status == LocationServiceStatus.Running)
        {
            double currentGpsTimestamp = Input.location.lastData.timestamp;
            if (currentGpsTimestamp > speedReferenceGpsTimestamp + 0.05d)
            {
                elapsedSeconds = (float)(currentGpsTimestamp - speedReferenceGpsTimestamp);
            }
        }

        if (elapsedSeconds < 0.1f)
        {
            elapsedSeconds = Time.time - speedReferenceTime;
            if (elapsedSeconds < 0.1f)
            {
                return -1f;
            }
        }

        return (float)(distanceMeters / elapsedSeconds);
    }

    void MarkSpeedReference(float lat, float lng, double gpsTimestamp = -1d)
    {
        speedReferencePosition = new Vector2(lat, lng);
        speedReferenceTime = Time.time;
        speedReferenceGpsTimestamp = gpsTimestamp > 0d ? gpsTimestamp : -1d;
        hasSpeedReference = true;
    }

    void ResetSpeedReference()
    {
        hasSpeedReference = false;
        speedReferenceGpsTimestamp = -1d;
    }

    float GetForegroundMaxJumpMeters(float accuracy)
    {
        float limit = foregroundMaxJumpMeters;

        if (accuracy > idealHorizontalAccuracyMeters)
        {
            limit = Mathf.Max(limit, Mathf.Min(fastTravelMaxJumpMeters, accuracy * 4f));
        }

        if (displaySpeedMps >= fastTravelSpeedThresholdMps)
        {
            float speedBasedLimit = displaySpeedMps * Mathf.Max(foregroundSampleIntervalSeconds, 1f) * 8f;
            return Mathf.Max(fastTravelMaxJumpMeters, speedBasedLimit);
        }

        if (displaySpeedMps < 0f && accuracy > idealHorizontalAccuracyMeters)
        {
            return Mathf.Max(limit, 80f);
        }

        return limit;
    }

    int GetRequiredForegroundConfirmSamples(float accuracy)
    {
        if (displaySpeedMps >= fastTravelSpeedThresholdMps || accuracy > idealHorizontalAccuracyMeters)
        {
            return 1;
        }

        return foregroundMovementConfirmSamples;
    }

    float GetForegroundRequiredDelta(float accuracy)
    {
        float requiredDelta = foregroundMinMovementMeters;
        if (accuracy > 0f)
        {
            requiredDelta = Mathf.Max(requiredDelta, accuracy * accuracyMovementMultiplier);
            requiredDelta = Mathf.Min(requiredDelta, foregroundMaxRequiredMovementMeters);
        }

        return requiredDelta;
    }

    bool IsForegroundMovementSignificant(double deltaMeters, float accuracy)
    {
        return deltaMeters > GetForegroundRequiredDelta(accuracy);
    }

    double ComputeForegroundCreditedMeters(double rawDelta, float accuracy)
    {
        float requiredDelta = GetForegroundRequiredDelta(accuracy);
        if (rawDelta <= requiredDelta)
        {
            return 0d;
        }

        double credited = rawDelta - requiredDelta;
        double cap = requiredDelta + Mathf.Max(accuracy, 0f);
        cap = Math.Min(cap, foregroundMaxCreditPerStepMeters);
        credited = Math.Min(credited, cap);
        return credited < 1.0 ? 0d : credited;
    }

    void NoteForegroundGpsSample(float lat, float lng, float accuracy, double timestamp = -1d)
    {
        if (Input.location.status == LocationServiceStatus.Running && timestamp < 0d)
        {
            timestamp = Input.location.lastData.timestamp;
        }

        bool timestampAdvanced = timestamp > lastSeenGpsTimestamp + 0.01d;
        bool coordinatesChanged = !CoordinatesNearlyEqual(lat, lng, lastSeenGpsLat, lastSeenGpsLng);

        if (timestampAdvanced || coordinatesChanged)
        {
            lastSeenGpsTimestamp = timestamp > 0d ? timestamp : Time.time;
            lastSeenGpsLat = lat;
            lastSeenGpsLng = lng;
            unchangedGpsSinceTime = Time.time;
            return;
        }

        if (unchangedGpsSinceTime < 0f)
        {
            unchangedGpsSinceTime = Time.time;
        }
    }

    static bool CoordinatesNearlyEqual(double lat1, double lng1, double lat2, double lng2)
    {
        return Math.Abs(lat1 - lat2) < 0.0000005 && Math.Abs(lng1 - lng2) < 0.0000005;
    }

    void TryRestartStaleForegroundLocation()
    {
        if (isRestartingLocationService || unchangedGpsSinceTime < 0f)
        {
            return;
        }

        if (Time.time - unchangedGpsSinceTime < foregroundLocationStaleSeconds)
        {
            return;
        }

        if (Time.time < nextLocationServiceRestartTime)
        {
            return;
        }

        RestartForegroundLocationService();
    }

    void RestartForegroundLocationService()
    {
        if (!Input.location.isEnabledByUser)
        {
            return;
        }

        nextLocationServiceRestartTime = Time.time + foregroundLocationRestartCooldownSeconds;
        unchangedGpsSinceTime = Time.time;
        foregroundMovementConfirmCount = 0;

        if (Input.location.status == LocationServiceStatus.Running ||
            Input.location.status == LocationServiceStatus.Initializing)
        {
            Input.location.Stop();
        }

        Input.location.Start(5f, 0f);
        lastGpsStatusMessage = "Live · refreshing GPS";
    }

    void SyncTotalDistanceFromStorage(bool forceFromPrefs)
    {
        float storedDistance = PrefGetFloat("TotalDistance");
        if (!HomeDailyDistanceSchedule.IsGpsDistanceAccumulationAllowed)
        {
            // Memory may still be 0 if Home configured the schedule before Start() loaded prefs.
            if (forceFromPrefs || totalDistance < 0.01f)
            {
                totalDistance = storedDistance;
                return;
            }

            // Reject native/background GPS increases during the play window.
            if (storedDistance > totalDistance + 0.01f)
            {
                PrefSetFloat("TotalDistance", (float)totalDistance);
                PlayerPrefs.Save();
            }
            else if (storedDistance < totalDistance - 0.01f)
            {
                totalDistance = storedDistance;
            }

            return;
        }

        if (forceFromPrefs || !isTrackingForeground)
        {
            totalDistance = storedDistance;
            return;
        }

        if (HomeDailyDistanceSchedule.IsEnabled && storedDistance + 0.01f < totalDistance)
        {
            // Prefs was lowered by daily rollover; do not keep pre-rollover GPS in memory.
            totalDistance = storedDistance;
            return;
        }

        totalDistance = Math.Max(totalDistance, storedDistance);
    }

    void ClampTotalDistanceToStorageWhenPlayLocked()
    {
        if (HomeDailyDistanceSchedule.IsGpsDistanceAccumulationAllowed)
        {
            return;
        }

        SyncTotalDistanceFromStorage(forceFromPrefs: true);
    }

    void RefreshIosAuthorizationLabel()
    {
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            string label = _GetIOSLocationAuthorizationLabel();
            lastIosAuthLabel = string.IsNullOrEmpty(label) ? "Unknown" : label;
        }
        catch (Exception e)
        {
            lastIosAuthLabel = $"Error ({e.Message})";
        }
#else
        lastIosAuthLabel = Input.location.isEnabledByUser ? "Enabled (Editor)" : "Disabled (Editor)";
#endif
    }

    bool IsBackgroundMovementSignificant(double deltaMeters, float accuracy, float estimatedSpeedMps = -1f, bool applyStationaryGate = true)
    {
        float requiredDelta = minMovementMeters;
        if (applyStationaryGate && estimatedSpeedMps >= 0f && estimatedSpeedMps < minSpeedForWalkingMps)
        {
            requiredDelta = Mathf.Max(requiredDelta, stationaryMinMovementMeters);
        }

        if (accuracy <= idealHorizontalAccuracyMeters)
        {
            requiredDelta = Mathf.Max(requiredDelta, accuracy * accuracyMovementMultiplier);
        }

        return deltaMeters > requiredDelta;
    }

    void ApplyForegroundMovement(float lat, float lng, float accuracy)
    {
        if (!HomeDailyDistanceSchedule.IsGpsDistanceAccumulationAllowed)
        {
            return;
        }

        double rawDelta = CalculateDistance(
            lastForegroundPosition.x,
            lastForegroundPosition.y,
            lat,
            lng);

        double credited = ComputeForegroundCreditedMeters(rawDelta, accuracy);
        if (credited <= 0d)
        {
            return;
        }

        CreditWalkMeters(credited);
        if (HomeDailyDistanceSchedule.IsEnabled)
        {
            totalDistance = GetTotalDistanceMeters();
        }

        lastForegroundPosition = new Vector2(lat, lng);

        double gpsTimestamp = -1d;
        if (Input.location.status == LocationServiceStatus.Running)
        {
            gpsTimestamp = Input.location.lastData.timestamp;
        }

        MarkSpeedReference(lat, lng, gpsTimestamp);

        if (Time.time - lastForegroundPrefsSaveTime >= foregroundPrefsSaveIntervalSeconds)
        {
            PersistWalkTrackingPrefs();
            SaveLastKnownPosition(lat, lng);
            lastForegroundPrefsSaveTime = Time.time;
        }

        AppendLog("Foreground-Walk", lat, lng);
    }

    void SaveLastKnownPosition(double lat, double lng)
    {
        PrefSetFloat("LastSavedLat", (float)lat);
        PrefSetFloat("LastSavedLng", (float)lng);
        PlayerPrefs.Save();
    }

    void UpdateUI(string statusMessage = "")
    {
        if (debugText == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            lastGpsStatusMessage = statusMessage;
        }

        SyncTotalDistanceFromStorage(forceFromPrefs: !isTrackingForeground);
        appLaunchCount = PrefGetInt("AppLaunchCount");
        appKilledCount = PrefGetInt("AppKilledCount");
        backgroundEnterCount = PrefGetInt("BackgroundEnterCount");
        backgroundReturnCount = PrefGetInt("BackgroundReturnCount");
        mmFileActivatedCount = PrefGetInt("MM_ActivatedCount");
        mmLogFileUpdateCount = CountMmUpdatesInLogFile();
        deferredLogFileUpdateCount = GetCachedLogCount("iOS_Background_Deferred", ref cachedDeferredLogFileUpdateCount);

        string accuracyText = lastReportedAccuracyMeters > 0f
            ? $"{lastReportedAccuracyMeters:F0} m"
            : "N/A";
        string trackingText = isTrackingForeground ? "LIVE" : "BACKGROUND";
        string networkText = GetNetworkReachabilityLabel();
        string positionText = hasSmoothDisplayPosition
            ? $"{smoothDisplayPosition.x:F6}, {smoothDisplayPosition.y:F6}"
            : "N/A";

        float playDistanceMeters = GetTotalDistanceMeters();
        float backgroundGpsMeters = GetBackgroundAccumulatedMeters();
        bool scheduleOn = HomeDailyDistanceSchedule.IsEnabled;
        string distanceBlock = scheduleOn
            ? $"<b><size=34><color=#FFD700>Play Distance (gauge) : {playDistanceMeters:F2} m</color></size></b>\n" +
              $"<size=30><color=#7FDFFF>Background GPS today : {backgroundGpsMeters:F2} m</color></size>\n" +
              $"<size=26><color=#AAAAAA>Walking credits Background GPS while daily schedule is on; Play Distance changes at rollover / first-play gift.</color></size>\n"
            : $"<b><size=34><color=#FFD700>Total Distance : {playDistanceMeters:F2} m</color></size></b>\n";

        debugText.text =
            $"<b><size=36>=== GPS TRACKER DEBUG ===</size></b>\n\n" +
            $"<size=28>Daily schedule : <b>{(scheduleOn ? "ON" : "OFF")}</b></size>\n" +
            distanceBlock +
            $"<size=28>Mode : {trackingText}  |  Network : {networkText}</size>\n" +
            $"<size=28>iOS Auth : <b>{lastIosAuthLabel}</b>  (need <b>Always</b> for SLC/background)</size>\n" +
            $"<size=28>GPS : {lastGpsStatusMessage}  |  Accuracy : {accuracyText}</size>\n" +
            $"<size=28>Position : {positionText}</size>\n" +
            $"<size=28>--------------------------------------------------------------------------------</size>\n" +
            $"<size=28><b>[APP STATUS]</b>                         |  <b>[BACKGROUND]</b>\n" +
            $"App Launched : {appLaunchCount,3} times            |  Enter Background   : {backgroundEnterCount,3} times\n" +
            $"App Killed   : {appKilledCount,3} times            |  Back to Foreground : {backgroundReturnCount,3} times\n" +
            $"                                         |  <b>MM (SLC/killed) : {mmFileActivatedCount,3}</b> (Prefs)\n" +
            $"                                         |  MM Log Lines      : {mmLogFileUpdateCount,3}\n" +
            $"                                         |  Deferred Log      : {deferredLogFileUpdateCount,3}\n" +
            $"<size=28>--------------------------------------------------------------------------------</size>";

        debugText.ForceMeshUpdate();
    }

    void AppendLog(string tag, double lat, double lng)
    {
        try
        {
            string logLine = string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyy-MM-dd HH:mm:ss},{1},{2:F8},{3:F8}\n",
                DateTime.Now,
                tag,
                lat,
                lng);
            File.AppendAllText(logFilePath, logLine);
            InvalidateLogCountCache();
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
    {
        double R = 6371e3;
        double phi1 = lat1 * Math.PI / 180;
        double phi2 = lat2 * Math.PI / 180;
        double deltaPhi = (lat2 - lat1) * Math.PI / 180;
        double deltaLambda = (lon2 - lon1) * Math.PI / 180;

        double a = Math.Sin(deltaPhi / 2) * Math.Sin(deltaPhi / 2) +
                   Math.Cos(phi1) * Math.Cos(phi2) *
                   Math.Sin(deltaLambda / 2) * Math.Sin(deltaLambda / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }
}
