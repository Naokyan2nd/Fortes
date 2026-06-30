using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CraftSceneManager : MonoBehaviour
{
    public enum CraftCategory
    {
        None,
        Top,
        Bottom,
        CD,
    }

    [SerializeField] private Button backToHomeButton;

    [Header("Back To Home Entry Slide-In")]
    [SerializeField] private UIButtonSlideInEntryAnimator backToHomeButtonSlideInAnimator;
    [SerializeField] private float backToHomeSlideInOffsetX = 1400f;
    [SerializeField] private float backToHomeSlideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] private float backToHomeSlideInEaseOutPower = 3f;

    [Header("Right Side Entry Slide-In")]
    [SerializeField] private UIButtonSlideInEntryAnimator toCraftButtonSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator topButtonSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator bottomButtonSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator cdButtonSlideInAnimator;
    [SerializeField] private float rightSideButtonSlideInOffsetX = 1400f;
    [SerializeField] private float rightSideButtonSlideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] private float rightSideButtonSlideInEaseOutPower = 3f;

    [Header("Battle Reward Entry Slide-In")]
    [SerializeField] private RectTransform superRareRewardRect;
    [SerializeField] private RectTransform rareRewardRect;
    [SerializeField] private RectTransform normalRewardRect;
    [SerializeField] private UIRectSlideInEntryAnimator superRareRewardSlideInAnimator;
    [SerializeField] private UIRectSlideInEntryAnimator rareRewardSlideInAnimator;
    [SerializeField] private UIRectSlideInEntryAnimator normalRewardSlideInAnimator;
    [SerializeField] private float battleRewardSlideInOffsetX = 1400f;
    [SerializeField] private float battleRewardSlideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] private float battleRewardSlideInEaseOutPower = 3f;

    [Header("Category Tabs")]
    [SerializeField] private Button topButton;
    [SerializeField] private Button bottomButton;
    [SerializeField] private Button cdButton;
    [SerializeField] private GameObject topPanel;
    [SerializeField] private GameObject bottomPanel;
    [SerializeField] private GameObject cdPanel;
    [SerializeField] private GameObject bottomPanelAfterCrafting;
    [SerializeField] private GameObject cdPanelAfterCrafting;
    [SerializeField] private CraftCategory initialCategory = CraftCategory.Top;

    [Header("Craft Confirm")]
    [SerializeField] private Button toCraftButton;
    [SerializeField] [Range(0.05f, 1f)] private float toCraftButtonDisabledAlpha = 0.18f;
    [SerializeField] private GameObject craftConfirmPanel;
    [SerializeField] private Button craftRefuseButton;
    [SerializeField] private Button craftConfirmButton;

    [Header("After Crafting")]
    [SerializeField] private GameObject afterCraftingPanel;
    [SerializeField] private Button afterCraftEquipButton;
    [SerializeField] private Button afterCraftContinueButton;

    [Header("Panel Scale-In")]
    [SerializeField] private UIRectScaleInAnimator craftConfirmPanelScaleInAnimator;
    [SerializeField] private UIRectScaleInAnimator afterCraftingPanelScaleInAnimator;
    [SerializeField] private float craftPanelScaleInDuration = 0.35f;
    [SerializeField] [Range(1f, 6f)] private float craftPanelScaleInEaseOutPower = 3f;

    [Header("Panel Item Scroll (like Stage Noises, horizontal)")]
    [SerializeField] private float craftSiblingSpreadOffset = 30f;
    [SerializeField] private float craftSlotAdsorptionThreshold = 120f;
    [SerializeField] private float craftFocusAnimDuration = 0.2f;
    [SerializeField] [Range(1f, 6f)] private float craftFocusAnimEaseOutPower = 3f;
    [SerializeField] [Range(0.1f, 1f)] private float craftUnfocusedBrightness = 0.45f;
    [SerializeField] private float craftSnapDuration = 0.28f;
    [SerializeField] [Range(1f, 6f)] private float craftSnapEaseOutPower = 3f;
    [SerializeField] [Range(0.05f, 0.5f)] private float craftScrollEdgeRubberBandStrength = 0.22f;
    [SerializeField] private float craftScrollEdgeMaxOvershoot = 48f;
    [SerializeField] private float craftScrollBoundSpringDuration = 0.2f;
    [SerializeField] [Range(1f, 6f)] private float craftScrollBoundSpringEaseOutPower = 3f;

    GameObject _topSelected;
    GameObject _bottomSelected;
    GameObject _cdSelected;
    CraftCategory _activeCategory = CraftCategory.None;
    CraftPanelHorizontalScroll _horizontalScroll;
    CraftPanelSlotFocus _slotFocus;
    BattleRewardInventoryAmountDisplay _rewardAmountDisplay;
    bool _craftConfirmPanelVisible;
    ItemData _lastCraftedItem;
    Coroutine _craftConfirmPanelScaleCoroutine;
    Coroutine _afterCraftingPanelScaleCoroutine;

    void Awake()
    {
        EnsureReferences();
        CacheSelectedIndicators();
        EnsureCraftPanelInteraction();
        EnsureBattleRewardAmountDisplay();
        HideAllPanels();
        HideAllSelectedIndicators();
        HideCraftConfirmPanel();
        HideAfterCraftingPanel();
        EnsureBackToHomeSlideInAnimator();
        EnsureRightSideButtonSlideInAnimators();
        EnsureBattleRewardSlideInAnimators();
        EnsureCraftPanelScaleInAnimators();
    }

    void OnEnable()
    {
        SubscribeBattleRewardsForConfirmPanel();
        SubscribeInventoryForCraftPanels();
    }

    void OnDisable()
    {
        UnsubscribeBattleRewardsForConfirmPanel();
        UnsubscribeInventoryForCraftPanels();
    }

    void OnDestroy()
    {
        UnbindCraftConfirmUi();
        UnbindCraftSlotFocusEvents();
        UnsubscribeBattleRewardsForConfirmPanel();
        UnsubscribeInventoryForCraftPanels();
    }

    void Start()
    {
        if (backToHomeButton != null)
        {
            backToHomeButton.onClick.AddListener(() =>
            {
                SceneTransferManager.Instance.GoBack();
            });
        }

        BindCategoryButtons();
        BindCraftConfirmUi();
        SubscribeBattleRewardsForConfirmPanel();
        SetActiveCategory(initialCategory);

        PlayBackToHomeSlideIn();
        PlayRightSideButtonsSlideIn();
        PlayBattleRewardSlideIn();
    }

    void EnsureReferences()
    {
        if (backToHomeButton == null)
        {
            backToHomeButton = FindSceneButton("BackToHome");
        }

        if (topButton == null)
        {
            topButton = FindSceneButton("TopButton");
        }

        if (bottomButton == null)
        {
            bottomButton = FindSceneButton("BottomButton");
        }

        if (cdButton == null)
        {
            cdButton = FindSceneButton("CDButton");
        }

        if (topPanel == null)
        {
            topPanel = FindSceneObject("TopPanel");
        }

        if (bottomPanel == null)
        {
            bottomPanel = FindSceneObject("BottomPanel");
        }

        if (cdPanel == null)
        {
            cdPanel = FindSceneObject("CDPanel");
        }

        if (bottomPanelAfterCrafting == null)
        {
            bottomPanelAfterCrafting = FindSceneObject("BottomPanelAfterCrafting");
        }

        if (cdPanelAfterCrafting == null)
        {
            cdPanelAfterCrafting = FindSceneObject("CDPanelAfterCrafting");
        }

        if (toCraftButton == null)
        {
            toCraftButton = FindSceneButton("ToCraftButton");
        }

        if (craftConfirmPanel == null)
        {
            craftConfirmPanel = FindSceneObject("CraftConfirmPanel");
        }

        if (craftRefuseButton == null)
        {
            craftRefuseButton = FindSceneButton("RefuseButton");
        }

        if (craftConfirmButton == null)
        {
            craftConfirmButton = FindSceneButton("ConfirmButton");
        }

        if (afterCraftingPanel == null)
        {
            afterCraftingPanel = FindSceneObject("AfterCraftingPanel");
        }

        if (afterCraftEquipButton == null)
        {
            afterCraftEquipButton = FindSceneButton("EquipButton");
        }

        if (afterCraftContinueButton == null)
        {
            afterCraftContinueButton = FindSceneButton("ContinueButton");
        }
    }

    void CacheSelectedIndicators()
    {
        _topSelected = FindSelectedChild(topButton);
        _bottomSelected = FindSelectedChild(bottomButton);
        _cdSelected = FindSelectedChild(cdButton);
    }

    void EnsureCraftPanelInteraction()
    {
        if (_horizontalScroll == null)
        {
            _horizontalScroll = GetComponent<CraftPanelHorizontalScroll>();
            if (_horizontalScroll == null)
            {
                _horizontalScroll = gameObject.AddComponent<CraftPanelHorizontalScroll>();
            }
        }

        if (_slotFocus == null)
        {
            _slotFocus = GetComponent<CraftPanelSlotFocus>();
            if (_slotFocus == null)
            {
                _slotFocus = gameObject.AddComponent<CraftPanelSlotFocus>();
            }
        }
    }

    void EnsureBattleRewardAmountDisplay()
    {
        if (_rewardAmountDisplay == null)
        {
            _rewardAmountDisplay = GetComponent<BattleRewardInventoryAmountDisplay>();
            if (_rewardAmountDisplay == null)
            {
                _rewardAmountDisplay = gameObject.AddComponent<BattleRewardInventoryAmountDisplay>();
            }
        }

        _rewardAmountDisplay.EnsureReferences();
        EnsureBattleRewardRects();
    }

    void EnsureBattleRewardRects()
    {
        if (superRareRewardRect == null)
        {
            superRareRewardRect = FindCraftHudRewardRect("SuperRareReward");
        }

        if (rareRewardRect == null)
        {
            rareRewardRect = FindCraftHudRewardRect("RareReward");
        }

        if (normalRewardRect == null)
        {
            normalRewardRect = FindCraftHudRewardRect("NormalReward");
        }
    }

    static RectTransform FindCraftHudRewardRect(string rewardObjectName)
    {
        GameObject canvasObject = GameObject.Find("Canvas");
        if (canvasObject == null)
        {
            return null;
        }

        Transform rewardTransform = canvasObject.transform.Find(rewardObjectName);
        return rewardTransform as RectTransform;
    }

    void EnsureBackToHomeSlideInAnimator()
    {
        if (backToHomeButton == null)
        {
            backToHomeButton = FindSceneButton("BackToHome");
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

        backToHomeButtonSlideInAnimator.SetTarget(backToHomeButton, "BackToHome");
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

    void EnsureRightSideButtonSlideInAnimators()
    {
        toCraftButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            toCraftButtonSlideInAnimator,
            toCraftButton,
            "ToCraftButton",
            fromLeft: false);

        topButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            topButtonSlideInAnimator,
            topButton,
            "TopButton",
            fromLeft: false);

        bottomButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            bottomButtonSlideInAnimator,
            bottomButton,
            "BottomButton",
            fromLeft: false);

        cdButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            cdButtonSlideInAnimator,
            cdButton,
            "CDButton",
            fromLeft: false);

        toCraftButtonSlideInAnimator?.PrepareOffScreenRight();
        topButtonSlideInAnimator?.PrepareOffScreenRight();
        bottomButtonSlideInAnimator?.PrepareOffScreenRight();
        cdButtonSlideInAnimator?.PrepareOffScreenRight();
    }

    UIButtonSlideInEntryAnimator EnsureSlideInAnimatorOnButton(
        UIButtonSlideInEntryAnimator animator,
        Button button,
        string fallbackName,
        bool fromLeft)
    {
        if (button == null)
        {
            return animator;
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
        animator.ConfigureSlideFromLeft(
            rightSideButtonSlideInOffsetX,
            rightSideButtonSlideInDuration,
            rightSideButtonSlideInEaseOutPower,
            fromLeft);
        return animator;
    }

    void PlayRightSideButtonsSlideIn()
    {
        PlayButtonSlideIn(topButtonSlideInAnimator);
        PlayButtonSlideIn(bottomButtonSlideInAnimator);
        PlayButtonSlideIn(cdButtonSlideInAnimator);
        PlayToCraftButtonSlideIn();
    }

    void PlayButtonSlideIn(UIButtonSlideInEntryAnimator animator)
    {
        if (animator == null)
        {
            return;
        }

        StartCoroutine(animator.PlaySlideIn());
    }

    void PlayToCraftButtonSlideIn()
    {
        if (toCraftButtonSlideInAnimator == null)
        {
            return;
        }

        StartCoroutine(PlayToCraftButtonSlideInRoutine());
    }

    IEnumerator PlayToCraftButtonSlideInRoutine()
    {
        yield return toCraftButtonSlideInAnimator.PlaySlideIn();
        RefreshToCraftButtonState();
    }

    void EnsureBattleRewardSlideInAnimators()
    {
        EnsureBattleRewardRects();

        superRareRewardSlideInAnimator = EnsureRectSlideInAnimatorFromLeft(
            superRareRewardSlideInAnimator,
            superRareRewardRect,
            "SuperRareReward");

        rareRewardSlideInAnimator = EnsureRectSlideInAnimatorFromLeft(
            rareRewardSlideInAnimator,
            rareRewardRect,
            "RareReward");

        normalRewardSlideInAnimator = EnsureRectSlideInAnimatorFromLeft(
            normalRewardSlideInAnimator,
            normalRewardRect,
            "NormalReward");

        superRareRewardSlideInAnimator?.PrepareOffScreenLeft();
        rareRewardSlideInAnimator?.PrepareOffScreenLeft();
        normalRewardSlideInAnimator?.PrepareOffScreenLeft();
    }

    UIRectSlideInEntryAnimator EnsureRectSlideInAnimatorFromLeft(
        UIRectSlideInEntryAnimator animator,
        RectTransform rect,
        string fallbackName)
    {
        if (rect == null)
        {
            return animator;
        }

        if (animator == null)
        {
            animator = rect.GetComponent<UIRectSlideInEntryAnimator>();
        }

        if (animator == null)
        {
            animator = rect.gameObject.AddComponent<UIRectSlideInEntryAnimator>();
        }

        animator.SetTarget(rect, fallbackName);
        animator.ConfigureFromLeft(
            battleRewardSlideInOffsetX,
            battleRewardSlideInDuration,
            battleRewardSlideInEaseOutPower);
        return animator;
    }

    void PlayBattleRewardSlideIn()
    {
        PlayRectSlideIn(superRareRewardSlideInAnimator);
        PlayRectSlideIn(rareRewardSlideInAnimator);
        PlayRectSlideIn(normalRewardSlideInAnimator);
    }

    void PlayRectSlideIn(UIRectSlideInEntryAnimator animator)
    {
        if (animator == null)
        {
            return;
        }

        StartCoroutine(animator.PlaySlideIn());
    }

    void BindCategoryButtons()
    {
        if (topButton != null)
        {
            topButton.onClick.RemoveListener(OnTopCategoryClicked);
            topButton.onClick.AddListener(OnTopCategoryClicked);
        }

        if (bottomButton != null)
        {
            bottomButton.onClick.RemoveListener(OnBottomCategoryClicked);
            bottomButton.onClick.AddListener(OnBottomCategoryClicked);
        }

        if (cdButton != null)
        {
            cdButton.onClick.RemoveListener(OnCdCategoryClicked);
            cdButton.onClick.AddListener(OnCdCategoryClicked);
        }
    }

    void OnTopCategoryClicked()
    {
        SetActiveCategory(CraftCategory.Top);
    }

    void OnBottomCategoryClicked()
    {
        SetActiveCategory(CraftCategory.Bottom);
    }

    void OnCdCategoryClicked()
    {
        SetActiveCategory(CraftCategory.CD);
    }

    void SetActiveCategory(CraftCategory category)
    {
        if (category == CraftCategory.None || category == _activeCategory)
        {
            return;
        }

        _activeCategory = category;
        HideCraftConfirmPanel();
        HideAfterCraftingPanel();

        bool showTop = category == CraftCategory.Top;
        bool showBottom = category == CraftCategory.Bottom;
        bool showCd = category == CraftCategory.CD;

        SetSelectedActive(_topSelected, showTop);
        SetSelectedActive(_bottomSelected, showBottom);
        SetSelectedActive(_cdSelected, showCd);

        RefreshActiveCategoryCraftPanel();
    }

    void RefreshActiveCategoryCraftPanel()
    {
        if (_activeCategory == CraftCategory.None)
        {
            return;
        }

        ShowOnlyPanel(_activeCategory);
        ConfigureCraftPanelInteraction(GetActivePanelForCategory(_activeCategory));
    }

    void ConfigureCraftPanelInteraction(GameObject panel)
    {
        if (panel == null)
        {
            return;
        }

        EnsureCraftPanelInteraction();

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        if (panelRect == null)
        {
            return;
        }

        _horizontalScroll.Configure(panelRect);
        _slotFocus.Configure(
            panelRect,
            _horizontalScroll,
            referenceChildName: null,
            spreadOffset: craftSiblingSpreadOffset,
            adsorptionThreshold: craftSlotAdsorptionThreshold,
            enlargeAnimDuration: craftFocusAnimDuration,
            enlargeAnimEaseOutPower: craftFocusAnimEaseOutPower,
            scrollSnapDuration: craftSnapDuration,
            scrollSnapEaseOutPower: craftSnapEaseOutPower,
            edgeRubberBandStrength: craftScrollEdgeRubberBandStrength,
            edgeMaxOvershoot: craftScrollEdgeMaxOvershoot,
            boundSpringDuration: craftScrollBoundSpringDuration,
            boundSpringEaseOutPower: craftScrollBoundSpringEaseOutPower,
            dimmedSiblingBrightness: craftUnfocusedBrightness);

        _horizontalScroll.SetScrollBounds(_slotFocus);
        _slotFocus.RefreshRestLayoutFromScene();

        _slotFocus.SnapToNearestChildAtCurrentScroll();
        BindCraftSlotFocusEvents();
        RefreshToCraftButtonState();
    }

    void BindCraftConfirmUi()
    {
        if (toCraftButton != null)
        {
            toCraftButton.onClick.RemoveListener(OnToCraftButtonClicked);
            toCraftButton.onClick.AddListener(OnToCraftButtonClicked);
        }

        if (craftRefuseButton != null)
        {
            craftRefuseButton.onClick.RemoveListener(OnCraftRefuseButtonClicked);
            craftRefuseButton.onClick.AddListener(OnCraftRefuseButtonClicked);
        }

        if (craftConfirmButton != null)
        {
            craftConfirmButton.onClick.RemoveListener(OnCraftConfirmButtonClicked);
            craftConfirmButton.onClick.AddListener(OnCraftConfirmButtonClicked);
        }

        if (afterCraftEquipButton != null)
        {
            afterCraftEquipButton.onClick.RemoveListener(OnAfterCraftEquipButtonClicked);
            afterCraftEquipButton.onClick.AddListener(OnAfterCraftEquipButtonClicked);
        }

        if (afterCraftContinueButton != null)
        {
            afterCraftContinueButton.onClick.RemoveListener(OnAfterCraftContinueButtonClicked);
            afterCraftContinueButton.onClick.AddListener(OnAfterCraftContinueButtonClicked);
        }
    }

    void UnbindCraftConfirmUi()
    {
        if (toCraftButton != null)
        {
            toCraftButton.onClick.RemoveListener(OnToCraftButtonClicked);
        }

        if (craftRefuseButton != null)
        {
            craftRefuseButton.onClick.RemoveListener(OnCraftRefuseButtonClicked);
        }

        if (craftConfirmButton != null)
        {
            craftConfirmButton.onClick.RemoveListener(OnCraftConfirmButtonClicked);
        }

        if (afterCraftEquipButton != null)
        {
            afterCraftEquipButton.onClick.RemoveListener(OnAfterCraftEquipButtonClicked);
        }

        if (afterCraftContinueButton != null)
        {
            afterCraftContinueButton.onClick.RemoveListener(OnAfterCraftContinueButtonClicked);
        }
    }

    void BindCraftSlotFocusEvents()
    {
        if (_slotFocus == null)
        {
            return;
        }

        _slotFocus.FocusedChildChanged -= OnCraftFocusedSlotChanged;
        _slotFocus.FocusedChildChanged += OnCraftFocusedSlotChanged;
    }

    void UnbindCraftSlotFocusEvents()
    {
        if (_slotFocus == null)
        {
            return;
        }

        _slotFocus.FocusedChildChanged -= OnCraftFocusedSlotChanged;
    }

    void OnCraftFocusedSlotChanged(RectTransform focusedChild)
    {
        RefreshToCraftButtonState();

        if (_craftConfirmPanelVisible)
        {
            ApplyCraftConfirmFrameForFocusedSlot(focusedChild != null ? focusedChild.name : null);
            RefreshConfirmPanelRewardTexts();
            RefreshCraftConfirmButtonState();
        }
    }

    void SubscribeBattleRewardsForConfirmPanel()
    {
        if (PlayerBattleRewardManager.Instance != null)
        {
            PlayerBattleRewardManager.Instance.OnRewardsChanged -= OnBattleRewardsChangedForConfirmPanel;
            PlayerBattleRewardManager.Instance.OnRewardsChanged += OnBattleRewardsChangedForConfirmPanel;
        }
    }

    void UnsubscribeBattleRewardsForConfirmPanel()
    {
        if (PlayerBattleRewardManager.Instance != null)
        {
            PlayerBattleRewardManager.Instance.OnRewardsChanged -= OnBattleRewardsChangedForConfirmPanel;
        }
    }

    void OnBattleRewardsChangedForConfirmPanel()
    {
        RefreshToCraftButtonState();

        if (_craftConfirmPanelVisible)
        {
            RefreshConfirmPanelRewardTexts();
            RefreshCraftConfirmButtonState();
        }
    }

    void OnToCraftButtonClicked()
    {
        if (!CanOpenCraftConfirmForFocusedSlot() || !CanPerformCraftForFocusedSlot())
        {
            return;
        }

        ShowCraftConfirmPanel();
    }

    void OnCraftRefuseButtonClicked()
    {
        HideCraftConfirmPanel();
    }

    void OnCraftConfirmButtonClicked()
    {
        if (!CanPerformCraftForFocusedSlot())
        {
            return;
        }

        PerformCraftForFocusedSlot();
    }

    void OnAfterCraftEquipButtonClicked()
    {
        HideAfterCraftingPanel();
        OutfitSceneReturnContext.Clear();
        SceneTransferManager.Instance.LoadNewScene(SceneNames.Outfit);
    }

    void OnAfterCraftContinueButtonClicked()
    {
        HideAfterCraftingPanel();
    }

    void RefreshToCraftButtonState()
    {
        if (toCraftButton == null)
        {
            return;
        }

        bool canCraft = CanPerformCraftForFocusedSlot();
        toCraftButton.interactable = canCraft;
        ApplyToCraftButtonVisual(canCraft);
    }

    void ApplyToCraftButtonVisual(bool canCraft)
    {
        if (toCraftButton == null)
        {
            return;
        }

        toCraftButton.transition = Selectable.Transition.None;

        if (toCraftButton.targetGraphic is Image image)
        {
            if (canCraft)
            {
                Color normal = toCraftButton.colors.normalColor;
                image.color = new Color(normal.r, normal.g, normal.b, 1f);
            }
            else
            {
                float alpha = Mathf.Clamp(toCraftButtonDisabledAlpha, 0.05f, 1f);
                image.color = new Color(1f, 1f, 1f, alpha);
            }
        }

        UIButtonPressFeedback pressFeedback = toCraftButton.GetComponent<UIButtonPressFeedback>();
        if (pressFeedback != null)
        {
            pressFeedback.RecacheRestVisualFromCurrent(preferButtonNormalColor: canCraft);
        }
    }

    bool CanOpenCraftConfirmForFocusedSlot()
    {
        return HasCraftConfirmFrameForSlot(GetFocusedCraftSlotName());
    }

    void EnsureCraftPanelScaleInAnimators()
    {
        craftConfirmPanelScaleInAnimator = EnsurePanelScaleInAnimator(
            craftConfirmPanelScaleInAnimator,
            craftConfirmPanel,
            "CraftConfirmPanel");

        afterCraftingPanelScaleInAnimator = EnsurePanelScaleInAnimator(
            afterCraftingPanelScaleInAnimator,
            afterCraftingPanel,
            "AfterCraftingPanel");
    }

    UIRectScaleInAnimator EnsurePanelScaleInAnimator(
        UIRectScaleInAnimator animator,
        GameObject panel,
        string fallbackName)
    {
        if (panel == null)
        {
            return animator;
        }

        RectTransform rect = panel.transform as RectTransform;
        if (rect == null)
        {
            return animator;
        }

        if (animator == null)
        {
            animator = rect.GetComponent<UIRectScaleInAnimator>();
        }

        if (animator == null)
        {
            animator = rect.gameObject.AddComponent<UIRectScaleInAnimator>();
        }

        animator.SetTarget(rect, fallbackName);
        animator.Configure(craftPanelScaleInDuration, craftPanelScaleInEaseOutPower);
        return animator;
    }

    void PlayCraftConfirmPanelScaleIn()
    {
        if (craftConfirmPanelScaleInAnimator == null)
        {
            return;
        }

        StopPanelScaleIn(ref _craftConfirmPanelScaleCoroutine);
        _craftConfirmPanelScaleCoroutine = StartCoroutine(CraftConfirmPanelScaleInRoutine());
    }

    void PlayAfterCraftingPanelScaleIn()
    {
        if (afterCraftingPanelScaleInAnimator == null)
        {
            return;
        }

        StopPanelScaleIn(ref _afterCraftingPanelScaleCoroutine);
        _afterCraftingPanelScaleCoroutine = StartCoroutine(AfterCraftingPanelScaleInRoutine());
    }

    IEnumerator CraftConfirmPanelScaleInRoutine()
    {
        yield return craftConfirmPanelScaleInAnimator.PlayScaleIn();
        _craftConfirmPanelScaleCoroutine = null;
    }

    IEnumerator AfterCraftingPanelScaleInRoutine()
    {
        yield return afterCraftingPanelScaleInAnimator.PlayScaleIn();
        _afterCraftingPanelScaleCoroutine = null;
    }

    void StopPanelScaleIn(ref Coroutine routine)
    {
        if (routine == null)
        {
            return;
        }

        StopCoroutine(routine);
        routine = null;
    }

    void ShowCraftConfirmPanel()
    {
        if (craftConfirmPanel == null || !CanOpenCraftConfirmForFocusedSlot())
        {
            return;
        }

        craftConfirmPanel.SetActive(true);
        _craftConfirmPanelVisible = true;
        ApplyCraftConfirmFrameForFocusedSlot(GetFocusedCraftSlotName());
        RefreshCraftConfirmButtonState();
        PlayCraftConfirmPanelScaleIn();
    }

    void HideCraftConfirmPanel()
    {
        StopPanelScaleIn(ref _craftConfirmPanelScaleCoroutine);
        craftConfirmPanelScaleInAnimator?.SnapToRest();

        if (craftConfirmPanel != null)
        {
            craftConfirmPanel.SetActive(false);
        }

        _craftConfirmPanelVisible = false;
    }

    string GetFocusedCraftSlotName()
    {
        return _slotFocus != null && _slotFocus.FocusedChild != null
            ? _slotFocus.FocusedChild.name
            : null;
    }

    void ApplyCraftConfirmFrameForFocusedSlot(string focusedSlotName)
    {
        if (craftConfirmPanel == null)
        {
            return;
        }

        Transform panelRoot = craftConfirmPanel.transform;

        for (int i = 0; i < panelRoot.childCount; i++)
        {
            Transform child = panelRoot.GetChild(i);
            if (!IsCraftConfirmFrameRoot(child.name))
            {
                continue;
            }

            bool show = CraftConfirmFrameMatchesSlot(child.name, focusedSlotName);
            child.gameObject.SetActive(show);
        }

        RefreshConfirmPanelRewardTexts();
        RefreshCraftConfirmButtonState();
    }

    void RefreshCraftConfirmButtonState()
    {
        if (craftConfirmButton == null)
        {
            return;
        }

        craftConfirmButton.interactable = _craftConfirmPanelVisible && CanPerformCraftForFocusedSlot();
    }

    bool CanPerformCraftForFocusedSlot()
    {
        Transform craftSlotRoot = _slotFocus != null ? _slotFocus.FocusedChild : null;
        if (craftSlotRoot == null)
        {
            return false;
        }

        ItemData resultItem = GetCraftResultItemForSlotName(craftSlotRoot.name);
        if (resultItem == null)
        {
            return false;
        }

        if (InventoryManager.Instance != null && InventoryManager.Instance.Owns(resultItem))
        {
            return false;
        }

        return CraftConfirmPanelRewardDisplay.CanAffordCraft(craftSlotRoot);
    }

    void PerformCraftForFocusedSlot()
    {
        Transform craftSlotRoot = _slotFocus != null ? _slotFocus.FocusedChild : null;
        if (craftSlotRoot == null || PlayerBattleRewardManager.Instance == null)
        {
            return;
        }

        ItemData resultItem = GetCraftResultItemForSlotName(craftSlotRoot.name);
        if (resultItem == null)
        {
            return;
        }

        BattleRewardCounts cost = CraftConfirmPanelRewardDisplay.GetRequiredCosts(craftSlotRoot);
        if (!PlayerBattleRewardManager.Instance.TrySpendRewards(cost))
        {
            RefreshCraftConfirmButtonState();
            return;
        }

        if (InventoryManager.Instance != null && !InventoryManager.Instance.Owns(resultItem))
        {
            InventoryManager.Instance.AddItem(resultItem);
        }

        _lastCraftedItem = resultItem;
        string slotName = craftSlotRoot.name;

        HideCraftConfirmPanel();
        ShowAfterCraftingPanel(slotName);
        RefreshCraftPanelAfterCraftingForSlot(slotName);
    }

    void RefreshCraftPanelAfterCraftingForSlot(string craftedSlotName)
    {
        if (!TryGetCategoryForCraftSlot(craftedSlotName, out CraftCategory category))
        {
            return;
        }

        if (_activeCategory == category)
        {
            RefreshActiveCategoryCraftPanel();
        }
        else
        {
            SetCategoryPanelPairActive(category, isCategoryActive: false);
        }
    }

    void ShowAfterCraftingPanel(string focusedSlotName)
    {
        if (afterCraftingPanel == null || string.IsNullOrEmpty(focusedSlotName))
        {
            return;
        }

        Transform panelRoot = afterCraftingPanel.transform;
        for (int i = 0; i < panelRoot.childCount; i++)
        {
            Transform child = panelRoot.GetChild(i);
            if (!IsAfterCraftingContentRoot(child.name))
            {
                continue;
            }

            bool show = AfterCraftingContentMatchesSlot(child.name, focusedSlotName);
            child.gameObject.SetActive(show);
        }

        afterCraftingPanel.SetActive(true);
        PlayAfterCraftingPanelScaleIn();
    }

    void HideAfterCraftingPanel()
    {
        StopPanelScaleIn(ref _afterCraftingPanelScaleCoroutine);
        afterCraftingPanelScaleInAnimator?.SnapToRest();

        if (afterCraftingPanel != null)
        {
            afterCraftingPanel.SetActive(false);
        }

        _lastCraftedItem = null;
    }

    static ItemData GetCraftResultItemForSlotName(string slotName)
    {
        if (string.IsNullOrEmpty(slotName))
        {
            return null;
        }

        StarterInventoryConfig config = Resources.Load<StarterInventoryConfig>("StarterInventoryConfig");
        if (config == null)
        {
            return null;
        }

        return slotName switch
        {
            "BetterBottom" => config.betterBottom,
            "BetterCD" => config.betterCD,
            "BetterTop" => config.betterTop,
            _ => null,
        };
    }

    static bool IsAfterCraftingContentRoot(string childName)
    {
        return childName is not "Background" and not "EquipButton" and not "ContinueButton";
    }

    static bool AfterCraftingContentMatchesSlot(string contentRootName, string focusedSlotName)
    {
        if (string.IsNullOrEmpty(focusedSlotName))
        {
            return false;
        }

        if (string.Equals(contentRootName, focusedSlotName, System.StringComparison.Ordinal))
        {
            return true;
        }

        const string confirmSuffix = "Confirm";
        if (contentRootName.EndsWith(confirmSuffix, System.StringComparison.Ordinal)
            && contentRootName.Length > confirmSuffix.Length)
        {
            string baseName = contentRootName.Substring(0, contentRootName.Length - confirmSuffix.Length);
            return string.Equals(baseName, focusedSlotName, System.StringComparison.Ordinal);
        }

        return false;
    }

    void RefreshConfirmPanelRewardTexts()
    {
        if (!_craftConfirmPanelVisible || craftConfirmPanel == null)
        {
            return;
        }

        Transform frameRoot = GetActiveConfirmFrameRoot();
        Transform craftSlotRoot = _slotFocus != null ? _slotFocus.FocusedChild : null;
        if (frameRoot == null || craftSlotRoot == null)
        {
            return;
        }

        CraftConfirmPanelRewardDisplay.Refresh(frameRoot, craftSlotRoot);
    }

    Transform GetActiveConfirmFrameRoot()
    {
        if (craftConfirmPanel == null)
        {
            return null;
        }

        Transform panelRoot = craftConfirmPanel.transform;
        for (int i = 0; i < panelRoot.childCount; i++)
        {
            Transform child = panelRoot.GetChild(i);
            if (!IsCraftConfirmFrameRoot(child.name))
            {
                continue;
            }

            if (child.gameObject.activeSelf)
            {
                return child;
            }
        }

        return null;
    }

    bool HasCraftConfirmFrameForSlot(string focusedSlotName)
    {
        if (craftConfirmPanel == null || string.IsNullOrEmpty(focusedSlotName))
        {
            return false;
        }

        Transform panelRoot = craftConfirmPanel.transform;
        for (int i = 0; i < panelRoot.childCount; i++)
        {
            Transform child = panelRoot.GetChild(i);
            if (!IsCraftConfirmFrameRoot(child.name))
            {
                continue;
            }

            if (CraftConfirmFrameMatchesSlot(child.name, focusedSlotName))
            {
                return true;
            }
        }

        return false;
    }

    static bool IsCraftConfirmFrameRoot(string childName)
    {
        return childName is not "RefuseButton" and not "ConfirmButton";
    }

    static bool CraftConfirmFrameMatchesSlot(string frameRootName, string focusedSlotName)
    {
        if (string.IsNullOrEmpty(focusedSlotName))
        {
            return false;
        }

        if (string.Equals(frameRootName, focusedSlotName, System.StringComparison.Ordinal))
        {
            return true;
        }

        const string frameSuffix = "Frame";
        if (frameRootName.EndsWith(frameSuffix, System.StringComparison.Ordinal)
            && frameRootName.Length > frameSuffix.Length)
        {
            string frameBaseName = frameRootName.Substring(0, frameRootName.Length - frameSuffix.Length);
            return string.Equals(frameBaseName, focusedSlotName, System.StringComparison.Ordinal);
        }

        return false;
    }

    void SubscribeInventoryForCraftPanels()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= OnInventoryChangedForCraftPanels;
            InventoryManager.Instance.OnInventoryChanged += OnInventoryChangedForCraftPanels;
        }
    }

    void UnsubscribeInventoryForCraftPanels()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= OnInventoryChangedForCraftPanels;
        }
    }

    void OnInventoryChangedForCraftPanels()
    {
        RefreshToCraftButtonState();

        if (_activeCategory == CraftCategory.None)
        {
            return;
        }

        RefreshActiveCategoryCraftPanel();
    }

    static bool TryGetCategoryForCraftSlot(string slotName, out CraftCategory category)
    {
        category = slotName switch
        {
            "BetterBottom" => CraftCategory.Bottom,
            "BetterCD" => CraftCategory.CD,
            "BetterTop" => CraftCategory.Top,
            _ => CraftCategory.None,
        };

        return category != CraftCategory.None;
    }

    static string GetCraftSlotNameForCategory(CraftCategory category)
    {
        return category switch
        {
            CraftCategory.Bottom => "BetterBottom",
            CraftCategory.CD => "BetterCD",
            CraftCategory.Top => "BetterTop",
            _ => null,
        };
    }

    GameObject GetNormalPanelForCategory(CraftCategory category)
    {
        return category switch
        {
            CraftCategory.Top => topPanel,
            CraftCategory.Bottom => bottomPanel,
            CraftCategory.CD => cdPanel,
            _ => null,
        };
    }

    GameObject GetAfterCraftPanelForCategory(CraftCategory category)
    {
        return category switch
        {
            CraftCategory.Bottom => bottomPanelAfterCrafting,
            CraftCategory.CD => cdPanelAfterCrafting,
            _ => null,
        };
    }

    bool ShouldUseAfterCraftPanel(CraftCategory category)
    {
        string slotName = GetCraftSlotNameForCategory(category);
        if (string.IsNullOrEmpty(slotName))
        {
            return false;
        }

        GameObject afterPanel = GetAfterCraftPanelForCategory(category);
        if (afterPanel == null)
        {
            return false;
        }

        ItemData resultItem = GetCraftResultItemForSlotName(slotName);
        if (resultItem == null || InventoryManager.Instance == null)
        {
            return false;
        }

        return InventoryManager.Instance.Owns(resultItem);
    }

    GameObject GetActivePanelForCategory(CraftCategory category)
    {
        if (ShouldUseAfterCraftPanel(category))
        {
            GameObject afterPanel = GetAfterCraftPanelForCategory(category);
            if (afterPanel != null)
            {
                return afterPanel;
            }
        }

        return GetNormalPanelForCategory(category);
    }

    void ShowOnlyPanel(CraftCategory category)
    {
        SetCategoryPanelPairActive(CraftCategory.Top, category == CraftCategory.Top);
        SetCategoryPanelPairActive(CraftCategory.Bottom, category == CraftCategory.Bottom);
        SetCategoryPanelPairActive(CraftCategory.CD, category == CraftCategory.CD);
    }

    void SetCategoryPanelPairActive(CraftCategory category, bool isCategoryActive)
    {
        bool useAfterCraftPanel = isCategoryActive && ShouldUseAfterCraftPanel(category);
        GameObject normalPanel = GetNormalPanelForCategory(category);
        GameObject afterPanel = GetAfterCraftPanelForCategory(category);

        if (normalPanel != null)
        {
            normalPanel.SetActive(isCategoryActive && !useAfterCraftPanel);
        }

        if (afterPanel != null)
        {
            afterPanel.SetActive(isCategoryActive && useAfterCraftPanel);
        }
    }

    void HideAllPanels()
    {
        SetCategoryPanelPairActive(CraftCategory.Top, isCategoryActive: false);
        SetCategoryPanelPairActive(CraftCategory.Bottom, isCategoryActive: false);
        SetCategoryPanelPairActive(CraftCategory.CD, isCategoryActive: false);
    }

    void HideAllSelectedIndicators()
    {
        SetSelectedActive(_topSelected, false);
        SetSelectedActive(_bottomSelected, false);
        SetSelectedActive(_cdSelected, false);
    }

    static void SetSelectedActive(GameObject selectedRoot, bool active)
    {
        if (selectedRoot != null)
        {
            selectedRoot.SetActive(active);
        }
    }

    static GameObject FindSelectedChild(Button categoryButton)
    {
        if (categoryButton == null)
        {
            return null;
        }

        Transform selectedTransform = categoryButton.transform.Find("Selected");
        return selectedTransform != null ? selectedTransform.gameObject : null;
    }

    static Button FindSceneButton(string objectName)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    static GameObject FindSceneObject(string objectName)
    {
        return GameObject.Find(objectName);
    }
}
