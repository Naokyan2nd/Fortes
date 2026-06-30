using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-50)]
public class ResultSceneManager : MonoBehaviour
{
    [SerializeField] private Transform characterRoot;
    [SerializeField] private Transform albumRoot;

    [Header("Character Fade-In (match HomeScene)")]
    [Tooltip("Active outfit variant Image. Auto-resolved from Character children when unset.")]
    [SerializeField] private Image resultCharacterImage;
    [SerializeField] private Image characterShadowImage;
    [SerializeField] private bool animateCharacterFadeIn = true;
    [SerializeField] private float characterFadeInDuration = 1.2f;
    [SerializeField] private float characterFadeInStartDelay = 0.2f;
    [SerializeField] private Button toStageButton;
    [SerializeField] private Button toHomeButton;
    [SerializeField] private Button testButton;

    [Header("Entry Slide-In (match ScanScene)")]
    [SerializeField] private RectTransform resultPanel;
    [SerializeField] private UIRectSlideInEntryAnimator resultPanelSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator toStageButtonSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator toHomeButtonSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator testButtonSlideInAnimator;

    [Header("Banner Fly-In")]
    [SerializeField] private RectTransform bannerRect;
    [SerializeField] private UIStageReadyBannerFlyIn bannerFlyIn;
    [SerializeField] private Vector2 bannerFlyInFromPosition = new(1050f, 160f);
    [SerializeField] private float bannerFlyInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] private float bannerFlyInEaseOutPower = 3f;
    [SerializeField] private bool bannerFadeInDuringFly;

    [Header("Album Fly-In")]
    [SerializeField] private RectTransform albumRect;
    [SerializeField] private UIStageReadyBannerFlyIn albumFlyIn;
    [SerializeField] private Vector2 albumFlyInFromPosition = new(-1710f, -710f);
    [SerializeField] private float albumFlyInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] private float albumFlyInEaseOutPower = 3f;
    [SerializeField] private bool albumFadeInDuringFly;

    [Header("Result BGM")]
    [SerializeField] private AudioClip victoryBgmClip;
    [SerializeField] private AudioClip defeatBgmClip;
    [SerializeField] [Range(0f, 1f)] private float resultBgmVolume = 1f;
    [SerializeField] private bool loopResultBgm = true;

    [Header("Level Bar (Result > LevelBar)")]
    [SerializeField] private PlayerLevelConfig playerLevelConfig;
    [SerializeField] private GameObject standardLevelBarRoot;
    [SerializeField] private TMP_Text levelDisplayText;
    [SerializeField] private TMP_Text currentExperienceText;
    [SerializeField] private TMP_Text maxExperienceText;
    [SerializeField] private Image experienceBarFillImage;
    [SerializeField] private Image experienceBarBackgroundImage;
    [SerializeField] private Color experienceBarFillColor = new(0.22f, 0.55f, 1f, 1f);
    [SerializeField] private Color experienceBarBackgroundColor = Color.white;

    [Header("Level Bar (Result > LevelBarInGameTutorial)")]
    [SerializeField] private GameObject tutorialLevelBarRoot;
    [SerializeField] private TMP_Text tutorialLevelDisplayText;
    [SerializeField] private TMP_Text tutorialCurrentExperienceText;
    [SerializeField] private TMP_Text tutorialMaxExperienceText;
    [SerializeField] private Image tutorialExperienceBarFillImage;

    private bool _experienceBarImageConfigured;
    private bool _useTutorialLevelBar;
    private AudioSource _resultBgmSource;

    private bool _navInProgress;
    private Coroutine _buttonEntryCoroutine;
    static readonly string[] ResultOutcomePanelNames =
    {
        "WinNormal",
        "WinRare",
        "WinSupreRare",
        StageBattleStageIds.TutorialVictoryPanelName,
        StageBattleStageIds.DefeatPanelName,
    };

    static readonly string[] BannerOutcomeChildNames = { "Win", "Lose" };
    private Color _resultCharacterRestColor;
    private bool _resultCharacterColorCached;
    private Color _characterShadowRestColor;
    private bool _characterShadowColorCached;

    bool _resultEntryDesignRestCaptured;
    Vector2 _bannerDesignRestAnchoredPosition;
    Vector2 _albumDesignRestAnchoredPosition;
    Vector2 _resultPanelDesignRestAnchoredPosition;
    Vector2 _toStageButtonDesignRestAnchoredPosition;
    Vector2 _toHomeButtonDesignRestAnchoredPosition;
    Vector2 _testButtonDesignRestAnchoredPosition;

    void Awake()
    {
        ApplyPlayerLevelConfigIfAvailable();
        EnsureCharacterRoot();
        EnsureAlbumRoot();
        EnsureButtonReferences();
        EnsureButtonPressFeedback();
        EnsureResultPanelReference();
        EnsureBannerReference();
        EnsureAlbumRectReference();
        CaptureResultEntryDesignRestPositions();
        EnsureResultPanelSlideInAnimator();
        EnsureButtonSlideInAnimators();
        EnsureBannerFlyIn();
        EnsureAlbumFlyIn();
        PrepareAllEntryElementsOffScreen();
        EnsureLevelBarRootReferences();
        ApplyLevelBarVisibility(false);
        EnsureLevelBarReferences();
        HideAllResultOutcomePanels();
        HideAllBannerOutcomeChildren();
    }

    void Start()
    {
        BindButtons();
        ApplyOutfitVisuals();
        ApplyResultOutcomePanelsFromSession();
        RestartButtonEntrySequence();
    }

    void OnEnable()
    {
        _navInProgress = false;

        if (OutfitLoadoutManager.Instance != null)
        {
            OutfitLoadoutManager.Instance.OnLoadoutChanged += OnOutfitLoadoutChanged;
        }

        if (PlayerLevelManager.Instance != null)
        {
            ApplyPlayerLevelConfigIfAvailable();

            PlayerLevelManager.Instance.OnProgressChanged -= OnPlayerLevelProgressChanged;
            PlayerLevelManager.Instance.OnProgressChanged += OnPlayerLevelProgressChanged;
        }

        ApplyOutfitVisuals();
        ApplyLevelProgressDisplay();
    }

    void OnDisable()
    {
        StopResultBgm();
        StopButtonEntrySequence();
        SnapAllEntryElementsToRest();

        if (OutfitLoadoutManager.Instance != null)
        {
            OutfitLoadoutManager.Instance.OnLoadoutChanged -= OnOutfitLoadoutChanged;
        }

        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnProgressChanged -= OnPlayerLevelProgressChanged;
        }
    }

    void OnPlayerLevelProgressChanged()
    {
        ApplyLevelProgressDisplay();
    }

    void OnOutfitLoadoutChanged(ItemType type)
    {
        if (type != ItemType.Top && type != ItemType.Bottom && type != ItemType.CD)
        {
            return;
        }

        ApplyOutfitVisuals();
    }

    void ApplyOutfitVisuals()
    {
        EnsureCharacterRoot();
        OutfitItemVisualHelper.ApplyHomeCharacterVariant(characterRoot);
        InvalidateResultCharacterImageCache();

        EnsureAlbumRoot();
        OutfitItemVisualHelper.ApplyEquipmentSlotVariant(albumRoot, ItemType.CD);
    }

    void EnsureAlbumRoot()
    {
        if (albumRoot != null)
        {
            return;
        }

        GameObject albumObject = GameObject.Find("Album");
        if (albumObject != null)
        {
            albumRoot = albumObject.transform;
        }
    }

    void EnsureAlbumRectReference()
    {
        if (albumRect != null)
        {
            return;
        }

        EnsureAlbumRoot();
        if (albumRoot != null)
        {
            albumRect = albumRoot as RectTransform;
            return;
        }

        GameObject albumObject = GameObject.Find("Album");
        if (albumObject != null)
        {
            albumRect = albumObject.transform as RectTransform;
        }
    }

    void EnsureAlbumFlyIn()
    {
        EnsureAlbumRectReference();

        if (albumFlyIn == null)
        {
            UIStageReadyBannerFlyIn[] flyIns = GetComponents<UIStageReadyBannerFlyIn>();
            for (int i = 0; i < flyIns.Length; i++)
            {
                if (flyIns[i] != null && flyIns[i] != bannerFlyIn)
                {
                    albumFlyIn = flyIns[i];
                    break;
                }
            }

            if (albumFlyIn == null)
            {
                albumFlyIn = gameObject.AddComponent<UIStageReadyBannerFlyIn>();
            }
        }

        albumFlyIn.Configure(
            albumRect,
            albumFlyInFromPosition,
            GetAlbumDesignRestPosition(),
            albumFlyInDuration,
            albumFlyInEaseOutPower,
            albumFadeInDuringFly,
            "Album",
            bringToFront: false);

        albumFlyIn.EnsureBannerReference();
    }

    void PrepareAlbumHiddenForFlyIn()
    {
        EnsureAlbumFlyIn();
        if (albumFlyIn == null)
        {
            return;
        }

        albumFlyIn.PrepareHidden();
    }

    void EnsureLevelBarRootReferences()
    {
        EnsureResultPanelReference();
        if (resultPanel == null)
        {
            return;
        }

        if (standardLevelBarRoot == null)
        {
            Transform levelBar = resultPanel.Find("LevelBar");
            if (levelBar != null)
            {
                standardLevelBarRoot = levelBar.gameObject;
            }
        }

        if (tutorialLevelBarRoot == null)
        {
            Transform tutorialLevelBar = resultPanel.Find("LevelBarInGameTutorial");
            if (tutorialLevelBar != null)
            {
                tutorialLevelBarRoot = tutorialLevelBar.gameObject;
            }
        }
    }

    void ApplyLevelBarVisibility(bool useTutorialLevelBar)
    {
        _useTutorialLevelBar = useTutorialLevelBar;

        if (standardLevelBarRoot != null)
        {
            standardLevelBarRoot.SetActive(!useTutorialLevelBar);
        }

        if (tutorialLevelBarRoot != null)
        {
            tutorialLevelBarRoot.SetActive(useTutorialLevelBar);
        }
    }

    void EnsureLevelBarReferences()
    {
        if (_useTutorialLevelBar)
        {
            EnsureTutorialLevelBarReferences();
            return;
        }

        if (levelDisplayText == null)
        {
            levelDisplayText = FindLevelBarTextUnder(standardLevelBarRoot != null ? standardLevelBarRoot.transform : null, "Level");
        }

        if (currentExperienceText == null)
        {
            currentExperienceText = FindLevelBarTextUnder(
                standardLevelBarRoot != null ? standardLevelBarRoot.transform : null,
                "CurrentExperience");
        }

        if (maxExperienceText == null)
        {
            maxExperienceText = FindLevelBarTextUnder(
                standardLevelBarRoot != null ? standardLevelBarRoot.transform : null,
                "MaxExperience");
        }

        if (experienceBarFillImage == null)
        {
            experienceBarFillImage = FindExperienceBarFillImageUnder(
                standardLevelBarRoot != null ? standardLevelBarRoot.transform : null);
        }

        EnsureExperienceBarBackground();
        ConfigureExperienceBarImages();
    }

    void EnsureTutorialLevelBarReferences()
    {
        Transform root = tutorialLevelBarRoot != null ? tutorialLevelBarRoot.transform : null;

        if (tutorialLevelDisplayText == null)
        {
            tutorialLevelDisplayText = FindLevelBarTextUnder(root, "Level");
        }

        if (tutorialCurrentExperienceText == null)
        {
            tutorialCurrentExperienceText = FindLevelBarTextUnder(root, "CurrentExperience");
        }

        if (tutorialMaxExperienceText == null)
        {
            tutorialMaxExperienceText = FindLevelBarTextUnder(root, "MaxExperience");
        }

        if (tutorialExperienceBarFillImage == null)
        {
            tutorialExperienceBarFillImage = FindExperienceBarFillImageUnder(root);
        }
    }

    void EnsureExperienceBarBackground()
    {
        if (experienceBarFillImage == null)
        {
            return;
        }

        if (experienceBarBackgroundImage == null)
        {
            Transform fillTransform = experienceBarFillImage.transform;
            Transform parent = fillTransform.parent;
            if (parent != null)
            {
                Transform existingBackground = parent.Find("ExperienceBarBackground");
                if (existingBackground != null)
                {
                    experienceBarBackgroundImage = existingBackground.GetComponent<Image>();
                }
            }
        }

        if (experienceBarBackgroundImage != null)
        {
            return;
        }

        Transform fill = experienceBarFillImage.transform;
        Transform barParent = fill.parent;
        if (barParent == null)
        {
            return;
        }

        var backgroundObject = new GameObject(
            "ExperienceBarBackground",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        var backgroundRect = backgroundObject.GetComponent<RectTransform>();
        var fillRect = (RectTransform)fill;
        backgroundRect.SetParent(barParent, false);
        backgroundRect.SetSiblingIndex(fill.GetSiblingIndex());
        CopyRectTransform(fillRect, backgroundRect);

        experienceBarBackgroundImage = backgroundObject.GetComponent<Image>();
        Image fillImage = experienceBarFillImage;
        experienceBarBackgroundImage.sprite = fillImage.sprite;
        experienceBarBackgroundImage.material = fillImage.material;
        experienceBarBackgroundImage.type = Image.Type.Simple;
        experienceBarBackgroundImage.raycastTarget = false;
    }

    void ConfigureExperienceBarImages()
    {
        if (experienceBarBackgroundImage != null)
        {
            experienceBarBackgroundImage.type = Image.Type.Simple;
            experienceBarBackgroundImage.fillAmount = 1f;
            experienceBarBackgroundImage.color = experienceBarBackgroundColor;
        }

        if (experienceBarFillImage == null || _experienceBarImageConfigured)
        {
            return;
        }

        experienceBarFillImage.type = Image.Type.Filled;
        experienceBarFillImage.fillMethod = Image.FillMethod.Horizontal;
        experienceBarFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        experienceBarFillImage.color = experienceBarFillColor;
        _experienceBarImageConfigured = true;
    }

    static void CopyRectTransform(RectTransform source, RectTransform destination)
    {
        destination.anchorMin = source.anchorMin;
        destination.anchorMax = source.anchorMax;
        destination.anchoredPosition = source.anchoredPosition;
        destination.sizeDelta = source.sizeDelta;
        destination.pivot = source.pivot;
        destination.localRotation = source.localRotation;
        destination.localScale = source.localScale;
    }

    static TMP_Text FindLevelBarTextUnder(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        Transform child = root.Find(objectName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    static Image FindExperienceBarFillImageUnder(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform barTransform = root.Find("ExperienceBar");
        return barTransform != null ? barTransform.GetComponent<Image>() : null;
    }

    void ApplyLevelProgressDisplay()
    {
        EnsureLevelBarReferences();
        ResolveLevelProgress(out int level, out int currentExp, out int maxExp);

        if (_useTutorialLevelBar)
        {
            if (tutorialLevelDisplayText != null)
            {
                tutorialLevelDisplayText.text = level.ToString(CultureInfo.InvariantCulture);
            }

            if (tutorialMaxExperienceText != null)
            {
                tutorialMaxExperienceText.text = $"/ {FormatExperienceValue(maxExp)}";
            }

            return;
        }

        if (levelDisplayText != null)
        {
            levelDisplayText.text = level.ToString(CultureInfo.InvariantCulture);
        }

        if (currentExperienceText != null)
        {
            currentExperienceText.text = FormatExperienceValue(currentExp);
        }

        if (maxExperienceText != null)
        {
            maxExperienceText.text = $"/ {FormatExperienceValue(maxExp)}";
        }

        ApplyExperienceBarFill(experienceBarFillImage, currentExp, maxExp);
    }

    void ApplyExperienceBarFill(Image fillImage, int currentExp, int maxExp)
    {
        if (fillImage == null)
        {
            return;
        }

        ConfigureExperienceBarImages();

        if (experienceBarBackgroundImage != null)
        {
            experienceBarBackgroundImage.color = experienceBarBackgroundColor;
        }

        fillImage.color = experienceBarFillColor;
        fillImage.fillAmount = GetExperienceFillRatio(currentExp, maxExp);
    }

    static float GetExperienceFillRatio(int currentExp, int maxExp)
    {
        if (maxExp > 0)
        {
            return Mathf.Clamp01((float)currentExp / maxExp);
        }

        return currentExp > 0 ? 1f : 0f;
    }

    void ResolveLevelProgress(out int level, out int currentExp, out int maxExp)
    {
        if (PlayerLevelManager.Instance != null)
        {
            level = PlayerLevelManager.Instance.CurrentLevel;
            currentExp = PlayerLevelManager.Instance.CurrentLevelExp;
            maxExp = PlayerLevelManager.Instance.GetExpRequiredForCurrentLevel();
            return;
        }

        level = 1;
        currentExp = 0;
        maxExp = GetMaxExpForLevel(level);
    }

    int GetMaxExpForLevel(int level)
    {
        level = Mathf.Clamp(level, 1, PlayerLevelManager.MaxLevel);
        if (playerLevelConfig != null)
        {
            return playerLevelConfig.GetExpRequiredForLevel(level);
        }

        return PlayerLevelManager.GetDefaultExpRequiredForLevel(level);
    }

    static string FormatExperienceValue(int value)
    {
        return Mathf.Max(0, value).ToString(CultureInfo.InvariantCulture);
    }

    void BindButtons()
    {
        if (toStageButton != null)
        {
            toStageButton.onClick.RemoveListener(OnToStageClicked);
            toStageButton.onClick.AddListener(OnToStageClicked);
        }

        if (toHomeButton != null)
        {
            toHomeButton.onClick.RemoveListener(OnToHomeClicked);
            toHomeButton.onClick.AddListener(OnToHomeClicked);
        }

        if (testButton != null)
        {
            testButton.onClick.RemoveListener(OnTestButtonClicked);
            testButton.onClick.AddListener(OnTestButtonClicked);
        }
    }

    void RestartButtonEntrySequence()
    {
        StopButtonEntrySequence();
        ApplyResultEntryDesignRestPositions();
        EnsureResultPanelSlideInAnimator();
        EnsureButtonSlideInAnimators();
        EnsureBannerFlyIn();
        EnsureAlbumFlyIn();
        PrepareAllEntryElementsOffScreen();
        _buttonEntryCoroutine = StartCoroutine(PlayButtonEntrySequence());
    }

    void StopButtonEntrySequence()
    {
        if (_buttonEntryCoroutine != null)
        {
            StopCoroutine(_buttonEntryCoroutine);
            _buttonEntryCoroutine = null;
        }
    }

    IEnumerator PlayButtonEntrySequence()
    {
        int runningSlideCount = 0;

        Action onSlideStarted = () => runningSlideCount++;
        Action onSlideComplete = () => runningSlideCount--;

        ResolveResultCharacterImage();
        ResolveCharacterShadowImage();
        CacheResultCharacterRestColors();
        PrepareResultCharacterForFadeIn();
        ApplyResultCharacterDrawOrder();

        TryStartCharacterFadeIn(onSlideStarted, onSlideComplete);
        TryStartBannerFlyIn(onSlideStarted, onSlideComplete);
        TryStartAlbumFlyIn(onSlideStarted, onSlideComplete);
        TryStartRectSlideIn(resultPanelSlideInAnimator, onSlideStarted, onSlideComplete);
        TryStartSlideIn(toStageButtonSlideInAnimator, toStageButton, onSlideStarted, onSlideComplete);
        TryStartSlideIn(toHomeButtonSlideInAnimator, toHomeButton, onSlideStarted, onSlideComplete);
        TryStartSlideIn(testButtonSlideInAnimator, testButton, onSlideStarted, onSlideComplete);

        while (runningSlideCount > 0)
        {
            yield return null;
        }

        ApplyResultCharacterDrawOrder();
        _buttonEntryCoroutine = null;
    }

    void TryStartSlideIn(
        UIButtonSlideInEntryAnimator animator,
        Button button,
        Action onSlideStarted,
        Action onSlideComplete)
    {
        if (animator != null)
        {
            onSlideStarted?.Invoke();
            StartCoroutine(CompleteSlideIn(animator, onSlideComplete));
            return;
        }

        SnapButtonToRest(button);
    }

    void TryStartBannerFlyIn(Action onFlyInStarted, Action onFlyInComplete)
    {
        if (bannerFlyIn != null)
        {
            onFlyInStarted?.Invoke();
            StartCoroutine(CompleteBannerFlyIn(bannerFlyIn, onFlyInComplete));
            return;
        }

        SnapBannerToRest();
    }

    void TryStartAlbumFlyIn(Action onFlyInStarted, Action onFlyInComplete)
    {
        if (albumFlyIn != null)
        {
            onFlyInStarted?.Invoke();
            StartCoroutine(CompleteBannerFlyIn(albumFlyIn, onFlyInComplete));
            return;
        }

        SnapAlbumToRest();
    }

    void TryStartRectSlideIn(
        UIRectSlideInEntryAnimator animator,
        Action onSlideStarted,
        Action onSlideComplete)
    {
        if (animator != null)
        {
            onSlideStarted?.Invoke();
            StartCoroutine(CompleteRectSlideIn(animator, onSlideComplete));
            return;
        }

        SnapResultPanelToRest();
    }

    static IEnumerator CompleteBannerFlyIn(UIStageReadyBannerFlyIn flyIn, Action onFlyInComplete)
    {
        yield return flyIn.PlayFlyIn();
        onFlyInComplete?.Invoke();
    }

    static IEnumerator CompleteSlideIn(UIButtonSlideInEntryAnimator animator, Action onSlideComplete)
    {
        yield return animator.PlaySlideIn();
        onSlideComplete?.Invoke();
    }

    static IEnumerator CompleteRectSlideIn(UIRectSlideInEntryAnimator animator, Action onSlideComplete)
    {
        yield return animator.PlaySlideIn();
        onSlideComplete?.Invoke();
    }

    void TryStartCharacterFadeIn(Action onFadeStarted, Action onFadeComplete)
    {
        if (!ShouldAnimateCharacterFadeIn())
        {
            SnapResultCharacterToRestColor();
            return;
        }

        onFadeStarted?.Invoke();
        StartCoroutine(RunCharacterFadeIn(onFadeComplete));
    }

    IEnumerator RunCharacterFadeIn(Action onFadeComplete)
    {
        yield return AnimateResultCharacterFadeIn();
        onFadeComplete?.Invoke();
    }

    void SnapAllEntryElementsToRest()
    {
        ApplyResultEntryDesignRestPositions();
        SnapResultCharacterToRestColor();
        SnapBannerToRest();
        SnapAlbumToRest();
        ApplyResultCharacterDrawOrder();
        SnapResultPanelToRest();
        SnapButtonToRest(toStageButton);
        SnapButtonToRest(toHomeButton);
        SnapButtonToRest(testButton);
    }

    /// <summary>
    /// Banner fly-in uses SetAsLastSibling; restore Character / shadow in front of Banner.
    /// </summary>
    void ApplyResultCharacterDrawOrder()
    {
        EnsureBannerReference();
        EnsureCharacterRoot();
        ResolveCharacterShadowImage();

        if (bannerRect == null || characterRoot == null)
        {
            return;
        }

        RectTransform characterRect = characterRoot as RectTransform;
        if (characterRect == null)
        {
            return;
        }

        int bannerIndex = bannerRect.GetSiblingIndex();
        RectTransform shadowRect = characterShadowImage != null
            ? characterShadowImage.rectTransform
            : null;

        if (shadowRect != null)
        {
            shadowRect.SetSiblingIndex(bannerIndex + 1);
            characterRect.SetSiblingIndex(bannerIndex + 2);
            return;
        }

        characterRect.SetSiblingIndex(bannerIndex + 1);
    }

    void SnapBannerToRest()
    {
        if (bannerFlyIn != null)
        {
            bannerFlyIn.ShowAtRest();
        }
    }

    void SnapAlbumToRest()
    {
        if (albumFlyIn != null)
        {
            albumFlyIn.ShowAtRest();
        }
    }

    void SnapResultPanelToRest()
    {
        if (resultPanelSlideInAnimator != null)
        {
            resultPanelSlideInAnimator.SnapToRest();
        }
    }

    static void SnapButtonToRest(Button button)
    {
        if (button == null)
        {
            return;
        }

        UIButtonSlideInEntryAnimator slideIn = button.GetComponent<UIButtonSlideInEntryAnimator>();
        if (slideIn != null)
        {
            slideIn.SnapToRest();
            return;
        }

        button.interactable = true;
    }

    void EnsureButtonPressFeedback()
    {
        EnsurePressFeedbackOnButton(toStageButton);
        EnsurePressFeedbackOnButton(toHomeButton);
        EnsurePressFeedbackOnButton(testButton);
    }

    static void EnsurePressFeedbackOnButton(Button button)
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

    void EnsureResultPanelReference()
    {
        if (resultPanel != null)
        {
            return;
        }

        GameObject resultObject = GameObject.Find("Result");
        if (resultObject != null)
        {
            resultPanel = resultObject.transform as RectTransform;
        }
    }

    void HideAllResultOutcomePanels()
    {
        EnsureResultPanelReference();
        if (resultPanel == null)
        {
            return;
        }

        for (int i = 0; i < ResultOutcomePanelNames.Length; i++)
        {
            Transform child = resultPanel.Find(ResultOutcomePanelNames[i]);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    void HideAllBannerOutcomeChildren()
    {
        EnsureBannerReference();
        if (bannerRect == null)
        {
            return;
        }

        for (int i = 0; i < BannerOutcomeChildNames.Length; i++)
        {
            Transform child = bannerRect.Find(BannerOutcomeChildNames[i]);
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }
    }

    void ApplyBannerOutcome(bool isVictory)
    {
        EnsureBannerReference();
        if (bannerRect == null)
        {
            return;
        }

        string activeChildName = isVictory ? "Win" : "Lose";
        for (int i = 0; i < BannerOutcomeChildNames.Length; i++)
        {
            string childName = BannerOutcomeChildNames[i];
            Transform child = bannerRect.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(childName == activeChildName);
            }
        }
    }

    void ApplyPlayerLevelConfigIfAvailable()
    {
        if (playerLevelConfig != null && PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.ApplyConfig(playerLevelConfig);
        }
    }

    void ApplyResultOutcomePanelsFromSession()
    {
        HideAllResultOutcomePanels();
        HideAllBannerOutcomeChildren();

        if (!BattleResultSession.TryConsume(out bool isVictory, out string stageId))
        {
            ApplyLevelBarVisibility(false);
            ApplyLevelProgressDisplay();
            return;
        }

        bool useTutorialLevelBar = isVictory && TutorialStageIds.IsTutorialStageId(stageId);
        ApplyLevelBarVisibility(useTutorialLevelBar);

        ApplyTutorialNavigationRestriction(stageId, isVictory);
        ApplyBannerOutcome(isVictory);
        PlayResultBgm(isVictory);

        string panelName = isVictory
            ? StageBattleStageIds.GetVictoryPanelNameForStageId(stageId)
            : StageBattleStageIds.DefeatPanelName;

        EnsureResultPanelReference();
        if (resultPanel == null)
        {
            ApplyLevelProgressDisplay();
            return;
        }

        Transform panel = resultPanel.Find(panelName);
        if (panel != null)
        {
            panel.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                $"[ResultSceneManager] Result 配下に '{panelName}' が見つかりません（stageId={stageId}, victory={isVictory}）。",
                this);
        }

        ApplyLevelProgressDisplay();
    }

    void ApplyTutorialNavigationRestriction(string stageId, bool isVictory)
    {
        bool hideStageButton = isVictory && TutorialStageIds.IsTutorialStageId(stageId);
        if (toStageButton != null)
        {
            toStageButton.gameObject.SetActive(!hideStageButton);
        }
    }

    void PlayResultBgm(bool isVictory)
    {
        AudioClip clip = isVictory ? victoryBgmClip : defeatBgmClip;
        if (clip == null)
        {
            StopResultBgm();
            return;
        }

        EnsureResultBgmSource();
        _resultBgmSource.Stop();
        _resultBgmSource.clip = clip;
        _resultBgmSource.loop = loopResultBgm;
        _resultBgmSource.volume = Mathf.Clamp01(resultBgmVolume);
        _resultBgmSource.pitch = 1f;
        _resultBgmSource.Play();
    }

    void StopResultBgm()
    {
        if (_resultBgmSource != null && _resultBgmSource.isPlaying)
        {
            _resultBgmSource.Stop();
        }
    }

    void EnsureResultBgmSource()
    {
        if (_resultBgmSource != null)
        {
            return;
        }

        _resultBgmSource = gameObject.AddComponent<AudioSource>();
        _resultBgmSource.playOnAwake = false;
        _resultBgmSource.loop = loopResultBgm;
        _resultBgmSource.spatialBlend = 0f;
    }

    void EnsureResultPanelSlideInAnimator()
    {
        EnsureResultPanelReference();
        if (resultPanel == null)
        {
            return;
        }

        if (resultPanelSlideInAnimator == null)
        {
            resultPanelSlideInAnimator = resultPanel.GetComponent<UIRectSlideInEntryAnimator>();
        }

        if (resultPanelSlideInAnimator == null)
        {
            resultPanelSlideInAnimator = resultPanel.gameObject.AddComponent<UIRectSlideInEntryAnimator>();
        }

        resultPanelSlideInAnimator.SetTarget(resultPanel, "Result");
    }

    void EnsureButtonSlideInAnimators()
    {
        toStageButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(toStageButtonSlideInAnimator, toStageButton, "ToStageButton");
        toHomeButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(toHomeButtonSlideInAnimator, toHomeButton, "ToHomeButton");
        testButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(testButtonSlideInAnimator, testButton, "TestButton");
    }

    void EnsureBannerFlyIn()
    {
        EnsureBannerReference();

        if (bannerFlyIn == null)
        {
            UIStageReadyBannerFlyIn[] flyIns = GetComponents<UIStageReadyBannerFlyIn>();
            for (int i = 0; i < flyIns.Length; i++)
            {
                if (flyIns[i] != null && flyIns[i] != albumFlyIn)
                {
                    bannerFlyIn = flyIns[i];
                    break;
                }
            }

            if (bannerFlyIn == null)
            {
                bannerFlyIn = gameObject.AddComponent<UIStageReadyBannerFlyIn>();
            }
        }

        bannerFlyIn.Configure(
            bannerRect,
            bannerFlyInFromPosition,
            GetBannerDesignRestPosition(),
            bannerFlyInDuration,
            bannerFlyInEaseOutPower,
            bannerFadeInDuringFly,
            "Banner",
            bringToFront: false);

        bannerFlyIn.EnsureBannerReference();
    }

    void CaptureResultEntryDesignRestPositions()
    {
        if (_resultEntryDesignRestCaptured)
        {
            return;
        }

        EnsureBannerReference();
        if (bannerRect != null)
        {
            _bannerDesignRestAnchoredPosition = bannerRect.anchoredPosition;
        }

        EnsureAlbumRectReference();
        if (albumRect != null)
        {
            _albumDesignRestAnchoredPosition = albumRect.anchoredPosition;
        }

        EnsureResultPanelReference();
        if (resultPanel != null)
        {
            _resultPanelDesignRestAnchoredPosition = resultPanel.anchoredPosition;
        }

        CacheButtonDesignRest(toStageButton, ref _toStageButtonDesignRestAnchoredPosition);
        CacheButtonDesignRest(toHomeButton, ref _toHomeButtonDesignRestAnchoredPosition);
        CacheButtonDesignRest(testButton, ref _testButtonDesignRestAnchoredPosition);
        _resultEntryDesignRestCaptured = true;
    }

    void ApplyResultEntryDesignRestPositions()
    {
        if (!_resultEntryDesignRestCaptured)
        {
            CaptureResultEntryDesignRestPositions();
        }

        if (!_resultEntryDesignRestCaptured)
        {
            return;
        }

        if (bannerRect != null)
        {
            bannerRect.anchoredPosition = _bannerDesignRestAnchoredPosition;
        }

        if (albumRect != null)
        {
            albumRect.anchoredPosition = _albumDesignRestAnchoredPosition;
        }

        if (resultPanel != null)
        {
            resultPanel.anchoredPosition = _resultPanelDesignRestAnchoredPosition;
        }

        ApplyButtonDesignRest(toStageButton, _toStageButtonDesignRestAnchoredPosition);
        ApplyButtonDesignRest(toHomeButton, _toHomeButtonDesignRestAnchoredPosition);
        ApplyButtonDesignRest(testButton, _testButtonDesignRestAnchoredPosition);
    }

    Vector2 GetBannerDesignRestPosition()
    {
        if (!_resultEntryDesignRestCaptured)
        {
            CaptureResultEntryDesignRestPositions();
        }

        return _resultEntryDesignRestCaptured
            ? _bannerDesignRestAnchoredPosition
            : bannerRect != null ? bannerRect.anchoredPosition : Vector2.zero;
    }

    Vector2 GetAlbumDesignRestPosition()
    {
        if (!_resultEntryDesignRestCaptured)
        {
            CaptureResultEntryDesignRestPositions();
        }

        return _resultEntryDesignRestCaptured
            ? _albumDesignRestAnchoredPosition
            : albumRect != null ? albumRect.anchoredPosition : Vector2.zero;
    }

    static void CacheButtonDesignRest(Button button, ref Vector2 restPosition)
    {
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        if (rect != null)
        {
            restPosition = rect.anchoredPosition;
        }
    }

    static void ApplyButtonDesignRest(Button button, Vector2 restPosition)
    {
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        if (rect != null)
        {
            rect.anchoredPosition = restPosition;
        }
    }

    void EnsureBannerReference()
    {
        if (bannerRect != null)
        {
            return;
        }

        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
        {
            return;
        }

        Transform[] transforms = canvasObject.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == "Banner")
            {
                bannerRect = candidate as RectTransform;
                return;
            }
        }
    }

    void PrepareAllEntryElementsOffScreen()
    {
        PrepareBannerHiddenForFlyIn();
        PrepareAlbumHiddenForFlyIn();
        PrepareEntryRectOffScreen(resultPanelSlideInAnimator);
        PrepareEntryButtonOffScreen(toStageButtonSlideInAnimator);
        PrepareEntryButtonOffScreen(toHomeButtonSlideInAnimator);
        PrepareEntryButtonOffScreen(testButtonSlideInAnimator);
    }

    void PrepareBannerHiddenForFlyIn()
    {
        EnsureBannerFlyIn();
        if (bannerFlyIn == null)
        {
            return;
        }

        bannerFlyIn.PrepareHidden();
    }

    static void PrepareEntryRectOffScreen(UIRectSlideInEntryAnimator animator)
    {
        if (animator == null)
        {
            return;
        }

        animator.PrepareOffScreenRight();
    }

    static void PrepareEntryButtonOffScreen(UIButtonSlideInEntryAnimator animator)
    {
        if (animator == null)
        {
            return;
        }

        animator.PrepareOffScreenRight();
    }

    static UIButtonSlideInEntryAnimator EnsureSlideInAnimatorOnButton(
        UIButtonSlideInEntryAnimator animator,
        Button button,
        string fallbackName)
    {
        if (button == null)
        {
            return null;
        }

        if (animator == null)
        {
            animator = button.GetComponent<UIButtonSlideInEntryAnimator>();
        }

        if (animator == null)
        {
            animator = button.gameObject.AddComponent<UIButtonSlideInEntryAnimator>();
        }

        animator.SetTarget(button, fallbackName);
        return animator;
    }

    void OnTestButtonClicked()
    {
        StartCoroutine(HandleButtonClick(testButton, ReloadResultScene));
    }

    void ReloadResultScene()
    {
        if (SceneTransferManager.Instance == null)
        {
            Debug.LogError("[ResultSceneManager] SceneTransferManager が見つかりません。");
            _navInProgress = false;
            return;
        }

        SnapAllEntryElementsToRest();
        SceneTransferManager.Instance.LoadNewScene(SceneNames.Result, saveToHistory: false);
    }

    void OnToStageClicked()
    {
        StartCoroutine(HandleButtonClick(toStageButton, () => Navigate(SceneNames.Stage)));
    }

    void OnToHomeClicked()
    {
        StartCoroutine(HandleButtonClick(toHomeButton, () => Navigate(SceneNames.Home)));
    }

    IEnumerator HandleButtonClick(Button button, Action navigate)
    {
        if (_navInProgress)
        {
            yield break;
        }

        _navInProgress = true;

        if (button != null)
        {
            button.interactable = false;
            UIButtonPressFeedback pressFeedback = button.GetComponent<UIButtonPressFeedback>();
            if (pressFeedback != null)
            {
                yield return pressFeedback.PlayClickConfirm();
            }
        }

        navigate?.Invoke();
    }

    void Navigate(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            _navInProgress = false;
            return;
        }

        if (SceneTransferManager.Instance == null)
        {
            Debug.LogError("[ResultSceneManager] SceneTransferManager が見つかりません。");
            _navInProgress = false;
            return;
        }

        SceneTransferManager.Instance.ClearHistory();
        SceneTransferManager.Instance.LoadNewScene(sceneName, saveToHistory: false);
    }

    void EnsureCharacterRoot()
    {
        if (characterRoot != null)
        {
            return;
        }

        GameObject characterObject = GameObject.Find("Character");
        if (characterObject != null)
        {
            characterRoot = characterObject.transform;
        }
    }

    bool ShouldAnimateCharacterFadeIn()
    {
        return animateCharacterFadeIn
            && (_resultCharacterColorCached || _characterShadowColorCached);
    }

    void InvalidateResultCharacterImageCache()
    {
        resultCharacterImage = null;
        _resultCharacterColorCached = false;
        _characterShadowColorCached = false;
    }

    void ResolveResultCharacterImage()
    {
        EnsureCharacterRoot();
        if (characterRoot == null)
        {
            resultCharacterImage = null;
            return;
        }

        Image activeImage = null;
        for (int i = 0; i < characterRoot.childCount; i++)
        {
            Transform child = characterRoot.GetChild(i);
            if (!child.gameObject.activeInHierarchy)
            {
                continue;
            }

            Image image = child.GetComponent<Image>();
            if (image != null)
            {
                activeImage = image;
                break;
            }
        }

        resultCharacterImage = activeImage;
    }

    void ResolveCharacterShadowImage()
    {
        if (characterShadowImage != null)
        {
            return;
        }

        GameObject shadowObject = GameObject.Find("CharacterShadow");
        if (shadowObject != null)
        {
            characterShadowImage = shadowObject.GetComponent<Image>();
        }
    }

    void CacheResultCharacterRestColors()
    {
        _resultCharacterColorCached = false;
        _characterShadowColorCached = false;

        if (resultCharacterImage != null)
        {
            _resultCharacterRestColor = resultCharacterImage.color;
            if (_resultCharacterRestColor.a <= 0f)
            {
                _resultCharacterRestColor.a = 1f;
            }

            _resultCharacterColorCached = true;
        }

        if (characterShadowImage != null)
        {
            _characterShadowRestColor = characterShadowImage.color;
            if (_characterShadowRestColor.a <= 0f)
            {
                _characterShadowRestColor.a = 1f;
            }

            _characterShadowColorCached = true;
        }
    }

    void SetResultCharacterAlpha(float alpha)
    {
        if (resultCharacterImage != null && _resultCharacterColorCached)
        {
            Color color = _resultCharacterRestColor;
            color.a = Mathf.Clamp01(alpha) * _resultCharacterRestColor.a;
            resultCharacterImage.color = color;
        }

        if (characterShadowImage != null && _characterShadowColorCached)
        {
            Color color = _characterShadowRestColor;
            color.a = Mathf.Clamp01(alpha) * _characterShadowRestColor.a;
            characterShadowImage.color = color;
        }
    }

    void PrepareResultCharacterForFadeIn()
    {
        if (!ShouldAnimateCharacterFadeIn())
        {
            return;
        }

        SetResultCharacterAlpha(0f);
    }

    void SnapResultCharacterToRestColor()
    {
        if (_resultCharacterColorCached && resultCharacterImage != null)
        {
            resultCharacterImage.color = _resultCharacterRestColor;
        }

        if (_characterShadowColorCached && characterShadowImage != null)
        {
            characterShadowImage.color = _characterShadowRestColor;
        }
    }

    IEnumerator AnimateResultCharacterFadeIn()
    {
        if (!ShouldAnimateCharacterFadeIn())
        {
            yield break;
        }

        float delay = Mathf.Max(0f, characterFadeInStartDelay);
        float duration = Mathf.Max(0f, characterFadeInDuration);
        float targetAlpha = 1f;

        PrepareResultCharacterForFadeIn();
        ApplyResultCharacterDrawOrder();

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (duration <= 0f)
        {
            SnapResultCharacterToRestColor();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetResultCharacterAlpha(Mathf.Lerp(0f, targetAlpha, t));
            yield return null;
        }

        SnapResultCharacterToRestColor();
    }

    void EnsureButtonReferences()
    {
        if (toStageButton == null)
        {
            toStageButton = FindButton("ToStageButton");
        }

        if (toHomeButton == null)
        {
            toHomeButton = FindButton("ToHomeButton");
        }

        if (testButton == null)
        {
            testButton = FindButton("TestButton");
        }
    }

    static Button FindButton(string objectName)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }
}
