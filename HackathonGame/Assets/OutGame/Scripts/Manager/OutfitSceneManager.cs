using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class OutfitSceneManager : MonoBehaviour
{
    enum OutfitViewMode
    {
        Outfit,
        Clothe,
    }

    enum ClotheCategoryType
    {
        None,
        Top,
        Bottom,
        CD,
    }

    [SerializeField] private Button backButton;
    [SerializeField] private Button toClotheButton;
    [SerializeField] private Button toCdButton;

    [Header("Entry Slide-In")]
    [SerializeField] private UIButtonSlideInEntryAnimator backToHomeButtonSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator toClotheButtonSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator toCdButtonSlideInAnimator;
    [SerializeField] private float backToHomeSlideInOffsetX = 1400f;
    [SerializeField] private float navigationButtonSlideInOffsetX = 1400f;
    [SerializeField] private float slideInDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] private float slideInEaseOutPower = 3f;

    [Header("Status & Current Equipment")]
    [SerializeField] private StageReadyStatusStatsDisplay statusStatsDisplay;
    [SerializeField] private Transform currentEquipmentRoot;

    [Header("UI Entry Slide-In (from right)")]
    [SerializeField] private RectTransform statusRect;
    [SerializeField] private RectTransform currentEquipmentRect;
    [SerializeField] private UIRectSlideInEntryAnimator statusSlideInAnimator;
    [SerializeField] private UIRectSlideInEntryAnimator currentEquipmentSlideInAnimator;
    [SerializeField] private float uiPanelSlideInOffsetX = 1400f;

    [Header("Album (equipped CD visuals)")]
    [SerializeField] private Transform albumRoot;
    [SerializeField] private RectTransform albumRect;
    [SerializeField] private RectTransform albumCdRect;
    [SerializeField] private UIStageReadyBannerFlyIn albumFlyIn;
    [SerializeField] private UIStageReadyBannerFlyIn albumCdFlyIn;
    [SerializeField] private Vector2 albumFlyInFromPosition = new(-1545f, -590f);
    [SerializeField] private float albumFlyInDuration = 0.28f;

    Vector2 _albumRestAnchoredPosition;
    Vector2 _albumCdRestAnchoredPosition;
    bool _albumRestPositionCached;
    bool _albumCdRestPositionCached;

    [Header("Character (outfit visuals + fade-in)")]
    [SerializeField] private Transform characterRoot;
    [Tooltip("Active outfit variant Image. Auto-resolved from Character children when unset.")]
    [SerializeField] private Image characterImage;
    [SerializeField] private Image characterShadowImage;
    [SerializeField] private bool animateCharacterFadeIn = true;
    [SerializeField] private float characterFadeInDuration = 1.2f;
    [SerializeField] private float characterFadeInStartDelay = 0.2f;

    [Header("Outfit / Clothe view")]
    [SerializeField] private Vector2 outfitCharacterVariantAnchoredPosition = new(-380f, -35f);
    [SerializeField] private Vector2 clotheCharacterVariantAnchoredPosition = new(0f, -35f);
    [SerializeField] private Vector2 outfitCharacterShadowAnchoredPosition = new(-490f, -495f);
    [SerializeField] private Vector2 clotheCharacterShadowAnchoredPosition = new(0f, -495f);
    [SerializeField] private float viewModeTransitionDuration = 0.45f;
    [SerializeField] [Range(1f, 6f)] private float viewModeTransitionEaseOutPower = 3f;
    [Tooltip("Shown only in Outfit view (e.g. Status, Album). Auto-filled by name when empty.")]
    [SerializeField] private GameObject[] outfitModeUiRoots;
    [Tooltip("Shown only in Clothe view. Leave empty if none.")]
    [SerializeField] private GameObject[] clotheModeUiRoots;

    [Header("Clothe view UI entry")]
    [SerializeField] private UIRectSlideInEntryAnimator clotheStatusSlideInAnimator;
    [SerializeField] private UIRectSlideInEntryAnimator clothePanelSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator clotheTopButtonSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator clotheBottomButtonSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator clotheCdButtonSlideInAnimator;
    [SerializeField] private UIButtonSlideInEntryAnimator toChangeButtonSlideInAnimator;
    [SerializeField] private float clotheUiSlideInOffsetX = 1400f;

    OutfitViewMode _viewMode = OutfitViewMode.Outfit;
    RectTransform _characterVariantRect;
    RectTransform _characterShadowRect;

    Color _characterRestColor;
    bool _characterColorCached;
    Color _characterShadowRestColor;
    bool _characterShadowColorCached;

    [Header("アイテムポップアップ（シーン上の Panel を指定）")]
    [SerializeField] private GameObject itemTopPanel;
    [SerializeField] private GameObject itemBottomPanel;
    [SerializeField] private GameObject itemCDPanel;
    [SerializeField] private Button openTopButton;
    [SerializeField] private Button openBottomButton;
    [SerializeField] private Button openCDButton;

    bool _navInProgress;
    Coroutine _entryCoroutine;

    Button _clotheTopCategoryButton;
    Button _clotheBottomCategoryButton;
    Button _clotheCdCategoryButton;
    Button _toChangeButton;
    readonly Dictionary<ItemType, ItemData> _clothePendingSelection = new Dictionary<ItemType, ItemData>();
    GameObject _clotheTopSelectRoot;
    GameObject _clotheBottomSelectRoot;
    GameObject _clotheCdSelectRoot;
    ClotheCategoryType _activeClotheCategory = ClotheCategoryType.None;
    ClotheCategoryType _clotheEntryInitialCategory = ClotheCategoryType.Top;

    struct ClotheItemButtonBinding
    {
        public Button Button;
        public ItemType ItemType;
        public ItemData Item;
        public Outline SelectionOutline;
    }

    static readonly Vector2 ClotheItemSelectionOutlineDistance = new Vector2(6f, -6f);

    readonly List<ClotheItemButtonBinding> _clotheItemButtonBindings = new List<ClotheItemButtonBinding>();
    bool _clotheItemSelectionBound;
    Transform _clotheStatusRoot;

    bool _clotheSlideDesignRestCaptured;
    Vector2 _clotheStatusDesignRestAnchoredPosition;
    Vector2 _clothePanelDesignRestAnchoredPosition;
    Vector2 _clotheTopButtonDesignRestAnchoredPosition;
    Vector2 _clotheBottomButtonDesignRestAnchoredPosition;
    Vector2 _clotheCdButtonDesignRestAnchoredPosition;
    Vector2 _clotheToChangeButtonDesignRestAnchoredPosition;

    void Awake()
    {
        EnsureNavigationButtonReferences();
        EnsureNavigationButtonSlideInAnimators();
        EnsureNavigationButtonPressFeedback();
        PrepareNavigationButtonsOffScreen();
        EnsureUiPanelSlideInAnimators();
        PrepareUiPanelsOffScreen();
        EnsureAlbumFlyIn();
        PrepareAlbumHiddenForEntry();
        EnsureAlbumCdFlyIn();
        PrepareAlbumCdHiddenForEntry();
        EnsureStatusAndEquipmentDisplays();
        RefreshStatusAndEquipmentDisplays();
        RefreshCharacterAppearance();
        PrepareCharacterForFadeIn();
        EnsureViewModeUiReferences();
        ApplyOutfitAlbumVisual();
        CaptureClotheSlideDesignRestPositions();
        EnsureItemPanelReferences();
        HideClotheItemPanels();
        ApplyViewModeUiVisibility();
    }

    void OnEnable()
    {
        if (OutfitLoadoutManager.Instance != null)
        {
            OutfitLoadoutManager.Instance.OnLoadoutChanged += OnOutfitLoadoutChanged;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += OnInventoryChanged;
        }

        EnsureStatusAndEquipmentDisplays();
        RefreshStatusAndEquipmentDisplays();
        ApplyOutfitAlbumVisual();
        RefreshCharacterAppearance();
        PrepareCharacterForFadeIn();
        RefreshClotheItemSelectionVisuals();
    }

    void OnDisable()
    {
        if (OutfitLoadoutManager.Instance != null)
        {
            OutfitLoadoutManager.Instance.OnLoadoutChanged -= OnOutfitLoadoutChanged;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= OnInventoryChanged;
        }

        SnapCharacterToOutfitViewPositions();
        _viewMode = OutfitViewMode.Outfit;
    }

    void OnInventoryChanged()
    {
        RefreshClotheItemSelectionVisuals();
    }

    void Start()
    {
        BindNavigationButtons();
        BindItemPanelButtons();
        BindClotheCategoryButtons();
        RefreshStatusAndEquipmentDisplays();
        _entryCoroutine = StartCoroutine(PlaySceneEntrySequence());
    }

    void OnOutfitLoadoutChanged(ItemType changedType)
    {
        if (changedType == ItemType.CD)
        {
            ApplyOutfitAlbumVisual();
        }

        RefreshStatusAndEquipmentDisplays();
        if (_viewMode == OutfitViewMode.Clothe)
        {
            SyncClothePendingFromEquipped(changedType);
        }

        RefreshClotheItemSelectionVisuals();
        RefreshCharacterAppearance();
        SnapCharacterToRestColor();
        SnapCharacterToCurrentViewModePositions();
    }

    void EnsureStatusAndEquipmentDisplays()
    {
        statusRect = EnsureRectTransform(statusRect, "Status");
        currentEquipmentRect = EnsureRectTransform(currentEquipmentRect, "CurrentEquipment");

        if (statusStatsDisplay == null)
        {
            statusStatsDisplay = GetComponent<StageReadyStatusStatsDisplay>();
            if (statusStatsDisplay == null)
            {
                statusStatsDisplay = gameObject.AddComponent<StageReadyStatusStatsDisplay>();
            }
        }

        if (statusRect != null)
        {
            statusStatsDisplay.Configure(statusRect, "AttackPoint", "HealthPoint");
        }

        EnsureCurrentEquipmentRoot();
    }

    void RefreshStatusAndEquipmentDisplays()
    {
        statusStatsDisplay?.Refresh();
        ApplyCurrentEquipmentVisual();
    }

    void EnsureCurrentEquipmentRoot()
    {
        if (currentEquipmentRoot != null)
        {
            return;
        }

        if (currentEquipmentRect != null)
        {
            currentEquipmentRoot = currentEquipmentRect;
            return;
        }

        GameObject equipmentObject = GameObject.Find("CurrentEquipment");
        if (equipmentObject != null)
        {
            currentEquipmentRoot = equipmentObject.transform;
        }
    }

    void ApplyCurrentEquipmentVisual()
    {
        EnsureCurrentEquipmentRoot();
        if (currentEquipmentRoot == null)
        {
            return;
        }

        OutfitItemVisualHelper.ApplyOutfitCurrentEquipmentVariant(currentEquipmentRoot);
    }

    void RefreshCharacterAppearance()
    {
        ApplyCharacterOutfitVariant();
        ResolveCharacterImage();
        ResolveCharacterShadowImage();
        CacheCharacterRestColors();
    }

    void ApplyCharacterOutfitVariant()
    {
        EnsureCharacterReferences();
        if (characterRoot == null)
        {
            return;
        }

        OutfitItemVisualHelper.ApplyHomeCharacterVariant(characterRoot);
        characterImage = null;
    }

    void ApplyOutfitAlbumVisual(ItemData cdItemOverride = null)
    {
        EnsureAlbumSceneReferences();
        ItemData cdItem = cdItemOverride ?? ResolveDisplayedCdItem();
        Transform root = ResolveAlbumRoot();
        if (root != null)
        {
            OutfitItemVisualHelper.ApplyOutfitAlbumVariant(root, cdItem);
        }
    }

    ItemData ResolveDisplayedCdItem()
    {
        if (_viewMode == OutfitViewMode.Clothe && _activeClotheCategory == ClotheCategoryType.CD)
        {
            ItemData pendingCd = GetPendingClotheItem(ItemType.CD);
            if (pendingCd != null)
            {
                return pendingCd;
            }
        }

        return OutfitLoadoutManager.Instance != null
            ? OutfitLoadoutManager.Instance.GetSelected(ItemType.CD)
            : null;
    }

    void BindNavigationButtons()
    {
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackToHomeClicked);
            backButton.onClick.AddListener(OnBackToHomeClicked);
        }

        if (toClotheButton != null)
        {
            toClotheButton.onClick.RemoveListener(OnToClotheClicked);
            toClotheButton.onClick.AddListener(OnToClotheClicked);
        }

        if (toCdButton != null)
        {
            toCdButton.onClick.RemoveListener(OnToCdClicked);
            toCdButton.onClick.AddListener(OnToCdClicked);
        }
    }

    void BindItemPanelButtons()
    {
        if (openTopButton != null && itemTopPanel != null)
        {
            openTopButton.onClick.AddListener(() => itemTopPanel.SetActive(true));
        }

        if (openBottomButton != null && itemBottomPanel != null)
        {
            openBottomButton.onClick.AddListener(() => itemBottomPanel.SetActive(true));
        }

        if (openCDButton != null && itemCDPanel != null)
        {
            openCDButton.onClick.AddListener(() => itemCDPanel.SetActive(true));
        }
    }

    void BindClotheCategoryButtons()
    {
        EnsureClotheCategoryReferences();
        BindClotheCategoryButtonListeners();
        ClearClotheCategorySelection();
    }

    void BindClotheCategoryButtonListeners()
    {
        if (_clotheTopCategoryButton != null)
        {
            _clotheTopCategoryButton.onClick.RemoveListener(OnClotheTopCategoryClicked);
            _clotheTopCategoryButton.onClick.AddListener(OnClotheTopCategoryClicked);
        }

        if (_clotheBottomCategoryButton != null)
        {
            _clotheBottomCategoryButton.onClick.RemoveListener(OnClotheBottomCategoryClicked);
            _clotheBottomCategoryButton.onClick.AddListener(OnClotheBottomCategoryClicked);
        }

        if (_clotheCdCategoryButton != null)
        {
            _clotheCdCategoryButton.onClick.RemoveListener(OnClotheCdCategoryClicked);
            _clotheCdCategoryButton.onClick.AddListener(OnClotheCdCategoryClicked);
        }

        BindToChangeButtonListener();
    }

    void EnsureToChangeButtonReference()
    {
        if (_toChangeButton != null)
        {
            return;
        }

        _toChangeButton = FindClotheSceneButton(GetClotheHierarchyRoot(), "ToChangeButton");
    }

    void BindToChangeButtonListener()
    {
        EnsureToChangeButtonReference();
        if (_toChangeButton == null)
        {
            return;
        }

        _toChangeButton.onClick.RemoveListener(OnToChangeButtonClicked);
        _toChangeButton.onClick.AddListener(OnToChangeButtonClicked);
    }

    void OnToChangeButtonClicked()
    {
        if (_viewMode != OutfitViewMode.Clothe || _activeClotheCategory == ClotheCategoryType.None)
        {
            return;
        }

        if (OutfitLoadoutManager.Instance == null)
        {
            return;
        }

        ItemType itemType = ClotheCategoryToItemType(_activeClotheCategory);
        ItemData pending = GetPendingClotheItem(itemType);
        if (pending == null)
        {
            return;
        }

        OutfitLoadoutManager.Instance.SetSelected(itemType, pending);
        SyncClothePendingFromEquipped(itemType);
        if (itemType == ItemType.CD)
        {
            ApplyOutfitAlbumVisual(pending);
        }

        RefreshClotheItemSelectionVisuals();
    }

    void EnsureClotheCategoryReferences()
    {
        Transform clotheRoot = GetClotheHierarchyRoot();
        if (clotheRoot == null)
        {
            return;
        }

        _clotheTopCategoryButton = FindNamedButtonUnder(clotheRoot, "TopButton");
        _clotheBottomCategoryButton = FindNamedButtonUnder(clotheRoot, "BottomButton");
        _clotheCdCategoryButton = FindNamedButtonUnder(clotheRoot, "CDButton");

        Transform clothePanel = FindNamedTransformUnder(clotheRoot, "ClothePanel");
        if (clothePanel == null)
        {
            return;
        }

        _clotheTopSelectRoot = FindNamedGameObjectUnder(clothePanel, "TopSelect");
        _clotheBottomSelectRoot = FindNamedGameObjectUnder(clothePanel, "BottomSelect");
        _clotheCdSelectRoot = FindNamedGameObjectUnder(clothePanel, "CDSelect");
    }

    Transform GetClotheHierarchyRoot()
    {
        if (clotheModeUiRoots != null)
        {
            for (int i = 0; i < clotheModeUiRoots.Length; i++)
            {
                if (clotheModeUiRoots[i] != null)
                {
                    return clotheModeUiRoots[i].transform;
                }
            }
        }

        GameObject clotheRootObject = FindSceneObject("Clothe");
        return clotheRootObject != null ? clotheRootObject.transform : null;
    }

    static Button FindNamedButtonUnder(Transform root, string objectName)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == objectName)
            {
                return buttons[i];
            }
        }

        return null;
    }

    static Transform FindNamedTransformUnder(Transform root, string objectName)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == objectName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    static GameObject FindNamedGameObjectUnder(Transform root, string objectName)
    {
        Transform found = FindNamedTransformUnder(root, objectName);
        return found != null ? found.gameObject : null;
    }

    void OnClotheTopCategoryClicked()
    {
        SetActiveClotheCategory(ClotheCategoryType.Top);
    }

    void OnClotheBottomCategoryClicked()
    {
        SetActiveClotheCategory(ClotheCategoryType.Bottom);
    }

    void OnClotheCdCategoryClicked()
    {
        SetActiveClotheCategory(ClotheCategoryType.CD);
    }

    void SetActiveClotheCategory(ClotheCategoryType category)
    {
        if (_viewMode != OutfitViewMode.Clothe)
        {
            return;
        }

        EnsureClotheCategoryReferences();

        if (_activeClotheCategory == category)
        {
            ClearClotheCategorySelection();
            return;
        }

        ClearClotheCategorySelection();
        ActivateClotheCategoryUi(category);
    }

    void ActivateClotheCategoryUi(ClotheCategoryType category)
    {
        EnsureClotheCategoryReferences();
        _activeClotheCategory = category;

        bool showTop = category == ClotheCategoryType.Top;
        bool showBottom = category == ClotheCategoryType.Bottom;
        bool showCd = category == ClotheCategoryType.CD;

        SetCategoryButtonChildrenActive(_clotheTopCategoryButton, showTop);
        SetSelectRootActive(_clotheTopSelectRoot, showTop);
        SetCategoryButtonChildrenActive(_clotheBottomCategoryButton, showBottom);
        SetSelectRootActive(_clotheBottomSelectRoot, showBottom);
        SetCategoryButtonChildrenActive(_clotheCdCategoryButton, showCd);
        SetSelectRootActive(_clotheCdSelectRoot, showCd);

        RefreshClotheItemSelectionVisuals();
    }

    void ClearClotheCategorySelection()
    {
        EnsureClotheCategoryReferences();
        _activeClotheCategory = ClotheCategoryType.None;
        SetCategoryButtonChildrenActive(_clotheTopCategoryButton, false);
        SetCategoryButtonChildrenActive(_clotheBottomCategoryButton, false);
        SetCategoryButtonChildrenActive(_clotheCdCategoryButton, false);
        SetSelectRootActive(_clotheTopSelectRoot, false);
        SetSelectRootActive(_clotheBottomSelectRoot, false);
        SetSelectRootActive(_clotheCdSelectRoot, false);
        RefreshClotheStatusForActiveCategory();
    }

    static void SetCategoryButtonChildrenActive(Button categoryButton, bool active)
    {
        if (categoryButton == null)
        {
            return;
        }

        Transform buttonTransform = categoryButton.transform;
        for (int i = 0; i < buttonTransform.childCount; i++)
        {
            buttonTransform.GetChild(i).gameObject.SetActive(active);
        }
    }

    static void SetSelectRootActive(GameObject selectRoot, bool active)
    {
        if (selectRoot == null)
        {
            return;
        }

        selectRoot.SetActive(active);
        if (!active)
        {
            return;
        }

        Transform selectTransform = selectRoot.transform;
        for (int i = 0; i < selectTransform.childCount; i++)
        {
            selectTransform.GetChild(i).gameObject.SetActive(true);
        }
    }

    void EnsureClotheStatusRoot()
    {
        if (_clotheStatusRoot != null)
        {
            return;
        }

        Transform clotheRoot = GetClotheHierarchyRoot();
        if (clotheRoot == null)
        {
            return;
        }

        Transform statusTransform = FindNamedTransformUnder(clotheRoot, "ClotheStatus");
        if (statusTransform != null)
        {
            _clotheStatusRoot = statusTransform;
        }
    }

    void EnsureClotheItemSelectionBindings()
    {
        if (_clotheItemSelectionBound)
        {
            return;
        }

        EnsureClotheCategoryReferences();
        if (_clotheTopSelectRoot == null
            && _clotheBottomSelectRoot == null
            && _clotheCdSelectRoot == null)
        {
            return;
        }

        _clotheItemButtonBindings.Clear();

        RegisterClotheItemButtonsUnderSelect(_clotheTopSelectRoot);
        RegisterClotheItemButtonsUnderSelect(_clotheBottomSelectRoot);
        RegisterClotheItemButtonsUnderSelect(_clotheCdSelectRoot);

        _clotheItemSelectionBound = true;
    }

    void RegisterClotheItemButtonsUnderSelect(GameObject selectRoot)
    {
        if (selectRoot == null)
        {
            return;
        }

        Button[] buttons = selectRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null
                || !TryParseClotheItemButtonName(button.name, out ItemType itemType, out bool isBetter))
            {
                continue;
            }

            ItemData item = ResolveClotheItemData(itemType, isBetter);
            if (item == null)
            {
                continue;
            }

            Outline selectionOutline = EnsureClotheItemSelectionOutline(button);

            _clotheItemButtonBindings.Add(new ClotheItemButtonBinding
            {
                Button = button,
                ItemType = itemType,
                Item = item,
                SelectionOutline = selectionOutline,
            });

            button.onClick.RemoveAllListeners();
            ItemData capturedItem = item;
            button.onClick.AddListener(() => OnClotheItemButtonClicked(capturedItem));
        }
    }

    void OnClotheItemButtonClicked(ItemData item)
    {
        if (_viewMode != OutfitViewMode.Clothe || item == null)
        {
            return;
        }

        if (OutfitLoadoutManager.Instance == null
            || InventoryManager.Instance == null
            || !InventoryManager.Instance.Owns(item))
        {
            return;
        }

        SetPendingClotheItem(item.itemType, item);
        if (item.itemType == ItemType.CD)
        {
            ApplyOutfitAlbumVisual(item);
        }

        RefreshClotheItemSelectionVisuals();
    }

    void ResetClothePendingFromEquipped()
    {
        _clothePendingSelection.Clear();
        if (OutfitLoadoutManager.Instance == null)
        {
            return;
        }

        SyncClothePendingFromEquipped(ItemType.Top);
        SyncClothePendingFromEquipped(ItemType.Bottom);
        SyncClothePendingFromEquipped(ItemType.CD);
    }

    void SyncClothePendingFromEquipped(ItemType itemType)
    {
        if (OutfitLoadoutManager.Instance == null)
        {
            return;
        }

        ItemData equipped = OutfitLoadoutManager.Instance.GetSelected(itemType);
        if (equipped != null)
        {
            _clothePendingSelection[itemType] = equipped;
        }
    }

    ItemData GetPendingClotheItem(ItemType itemType)
    {
        if (_clothePendingSelection.TryGetValue(itemType, out ItemData pending) && pending != null)
        {
            return pending;
        }

        return OutfitLoadoutManager.Instance != null
            ? OutfitLoadoutManager.Instance.GetSelected(itemType)
            : null;
    }

    void SetPendingClotheItem(ItemType itemType, ItemData item)
    {
        if (item == null || item.itemType != itemType)
        {
            return;
        }

        _clothePendingSelection[itemType] = item;
    }

    static Outline EnsureClotheItemSelectionOutline(Button button)
    {
        if (button == null)
        {
            return null;
        }

        Transform selectedFrameTransform = button.transform.Find("SelectedFrame");
        if (selectedFrameTransform != null)
        {
            selectedFrameTransform.gameObject.SetActive(false);
        }

        Graphic graphic = button.targetGraphic;
        if (graphic == null)
        {
            graphic = button.GetComponent<Graphic>();
        }

        if (graphic == null)
        {
            return null;
        }

        Outline outline = graphic.GetComponent<Outline>();
        if (outline == null)
        {
            outline = graphic.gameObject.AddComponent<Outline>();
        }

        outline.effectColor = Color.white;
        outline.effectDistance = ClotheItemSelectionOutlineDistance;
        outline.useGraphicAlpha = true;
        outline.enabled = false;
        return outline;
    }

    void RefreshClotheItemSelectionVisuals()
    {
        EnsureClotheItemSelectionBindings();
        EnsureClotheStatusRoot();

        for (int i = 0; i < _clotheItemButtonBindings.Count; i++)
        {
            ClotheItemButtonBinding binding = _clotheItemButtonBindings[i];
            bool ownsItem = binding.Item != null
                && InventoryManager.Instance != null
                && InventoryManager.Instance.Owns(binding.Item);

            if (binding.Button != null)
            {
                binding.Button.gameObject.SetActive(ownsItem);
            }

            if (binding.SelectionOutline == null)
            {
                continue;
            }

            bool isSelected = ownsItem && IsClotheItemPendingSelected(binding.ItemType, binding.Item);
            binding.SelectionOutline.enabled = isSelected;
        }

        RefreshClotheStatusForActiveCategory();
    }

    void RefreshClotheStatusForActiveCategory()
    {
        EnsureClotheStatusRoot();
        if (_clotheStatusRoot == null)
        {
            return;
        }

        if (_activeClotheCategory == ClotheCategoryType.None)
        {
            OutfitItemVisualHelper.HideAllClotheStatusVariants(_clotheStatusRoot);
            return;
        }

        ItemType itemType = ClotheCategoryToItemType(_activeClotheCategory);
        OutfitItemVisualHelper.ApplyClotheStatusForItemType(
            _clotheStatusRoot,
            itemType,
            GetPendingClotheItem(itemType));
    }

    static ItemType ClotheCategoryToItemType(ClotheCategoryType category)
    {
        return category switch
        {
            ClotheCategoryType.Top => ItemType.Top,
            ClotheCategoryType.Bottom => ItemType.Bottom,
            ClotheCategoryType.CD => ItemType.CD,
            _ => ItemType.Top,
        };
    }

    bool IsClotheItemPendingSelected(ItemType itemType, ItemData item)
    {
        if (item == null)
        {
            return false;
        }

        ItemData pending = GetPendingClotheItem(itemType);
        if (pending == null)
        {
            return false;
        }

        if (ReferenceEquals(pending, item))
        {
            return true;
        }

        string pendingId = OutfitLoadoutManager.GetStableItemId(pending);
        string itemId = OutfitLoadoutManager.GetStableItemId(item);
        return !string.IsNullOrEmpty(pendingId)
            && string.Equals(pendingId, itemId, StringComparison.Ordinal);
    }

    static bool TryParseClotheItemButtonName(string buttonName, out ItemType itemType, out bool isBetter)
    {
        itemType = ItemType.Top;
        isBetter = false;
        if (string.IsNullOrEmpty(buttonName))
        {
            return false;
        }

        if (buttonName.StartsWith("Starter", StringComparison.Ordinal))
        {
            isBetter = false;
            return TryMapClotheItemSuffix(buttonName.Substring("Starter".Length), out itemType);
        }

        if (buttonName.StartsWith("Better", StringComparison.Ordinal))
        {
            isBetter = true;
            return TryMapClotheItemSuffix(buttonName.Substring("Better".Length), out itemType);
        }

        return false;
    }

    static bool TryMapClotheItemSuffix(string suffix, out ItemType itemType)
    {
        switch (suffix)
        {
            case "Top":
                itemType = ItemType.Top;
                return true;
            case "Bottom":
                itemType = ItemType.Bottom;
                return true;
            case "CD":
                itemType = ItemType.CD;
                return true;
            default:
                itemType = ItemType.Top;
                return false;
        }
    }

    static ItemData ResolveClotheItemData(ItemType itemType, bool isBetter)
    {
        StarterInventoryConfig config = Resources.Load<StarterInventoryConfig>("StarterInventoryConfig");
        if (config == null)
        {
            return null;
        }

        return itemType switch
        {
            ItemType.Top => isBetter ? config.betterTop : config.starterTop,
            ItemType.Bottom => isBetter ? config.betterBottom : config.starterBottom,
            ItemType.CD => isBetter ? config.betterCD : config.starterCD,
            _ => null,
        };
    }

    IEnumerator PlaySceneEntrySequence()
    {
        int runningSlideCount = 0;
        Action onSlideStarted = () => runningSlideCount++;
        Action onSlideComplete = () => runningSlideCount--;

        Coroutine albumEntryCoroutine = StartCoroutine(PlayAlbumEntrySequence());

        StartCharacterFadeIn(onSlideStarted, onSlideComplete);
        StartButtonSlideIn(backToHomeButtonSlideInAnimator, onSlideStarted, onSlideComplete);
        StartButtonSlideIn(toClotheButtonSlideInAnimator, onSlideStarted, onSlideComplete);
        StartButtonSlideIn(toCdButtonSlideInAnimator, onSlideStarted, onSlideComplete);
        StartRectSlideIn(statusSlideInAnimator, onSlideStarted, onSlideComplete);
        StartRectSlideIn(currentEquipmentSlideInAnimator, onSlideStarted, onSlideComplete);

        while (runningSlideCount > 0)
        {
            yield return null;
        }

        if (albumEntryCoroutine != null)
        {
            yield return albumEntryCoroutine;
        }

        _entryCoroutine = null;
    }

    IEnumerator PlayAlbumEntrySequence()
    {
        ApplyOutfitAlbumVisual();
        ConfigureAlbumFlyInForEntry();

        if (albumFlyIn != null)
        {
            yield return albumFlyIn.PlayFlyIn();
        }
        else if (albumRect != null)
        {
            albumRect.gameObject.SetActive(true);
            albumRect.anchoredPosition = _albumRestAnchoredPosition;
        }

        if (albumCdFlyIn != null)
        {
            ConfigureAlbumCdFlyInFromActiveAlbum();
            yield return albumCdFlyIn.PlayFlyIn();
        }
        else if (albumCdRect != null)
        {
            albumCdRect.gameObject.SetActive(true);
        }

        ApplyOutfitAlbumVisual();
        SnapAlbumVisualsToRestAfterEntry();
    }

    void SnapAlbumVisualsToRestAfterEntry()
    {
        if (albumFlyIn != null)
        {
            albumFlyIn.ShowAtRest();
            return;
        }

        EnsureAlbumRectReference();
        if (albumRect != null)
        {
            albumRect.gameObject.SetActive(true);
            albumRect.anchoredPosition = _albumRestAnchoredPosition;
        }
    }

    void StartButtonSlideIn(
        UIButtonSlideInEntryAnimator animator,
        Action onSlideStarted,
        Action onSlideComplete)
    {
        if (animator == null)
        {
            return;
        }

        onSlideStarted?.Invoke();
        StartCoroutine(CompleteButtonSlideIn(animator, onSlideComplete));
    }

    void StartRectSlideIn(
        UIRectSlideInEntryAnimator animator,
        Action onSlideStarted,
        Action onSlideComplete)
    {
        if (animator == null)
        {
            return;
        }

        onSlideStarted?.Invoke();
        StartCoroutine(CompleteRectSlideIn(animator, onSlideComplete));
    }

    void StartCharacterFadeIn(Action onFadeStarted, Action onFadeComplete)
    {
        RefreshCharacterAppearance();

        if (!ShouldAnimateCharacterFadeIn())
        {
            SnapCharacterToRestColor();
            return;
        }

        onFadeStarted?.Invoke();
        StartCoroutine(RunCharacterFadeIn(onFadeComplete));
    }

    IEnumerator RunCharacterFadeIn(Action onFadeComplete)
    {
        yield return AnimateCharacterFadeIn();
        onFadeComplete?.Invoke();
    }

    static IEnumerator CompleteButtonSlideIn(UIButtonSlideInEntryAnimator animator, Action onSlideComplete)
    {
        yield return animator.PlaySlideIn();
        onSlideComplete?.Invoke();
    }

    static IEnumerator CompleteRectSlideIn(UIRectSlideInEntryAnimator animator, Action onSlideComplete)
    {
        yield return animator.PlaySlideIn();
        onSlideComplete?.Invoke();
    }

    void OnBackToHomeClicked()
    {
        if (_viewMode == OutfitViewMode.Clothe)
        {
            StartCoroutine(HandleExitClotheViewMode(backButton));
            return;
        }

        StartCoroutine(HandleNavigationButtonClick(backButton, () => OutfitSceneReturnContext.HandleBackToHome()));
    }

    void OnToClotheClicked()
    {
        if (_viewMode == OutfitViewMode.Clothe)
        {
            return;
        }

        _clotheEntryInitialCategory = ClotheCategoryType.Top;
        StartCoroutine(HandleEnterClotheViewMode(toClotheButton));
    }

    void OnToCdClicked()
    {
        if (_viewMode == OutfitViewMode.Clothe)
        {
            return;
        }

        _clotheEntryInitialCategory = ClotheCategoryType.CD;
        StartCoroutine(HandleEnterClotheViewMode(toCdButton));
    }

    IEnumerator HandleEnterClotheViewMode(Button button)
    {
        if (_navInProgress || _viewMode == OutfitViewMode.Clothe)
        {
            yield break;
        }

        _navInProgress = true;
        yield return PlayButtonPressFeedback(button);
        yield return TransitionToClotheViewMode();
        _navInProgress = false;
    }

    IEnumerator HandleExitClotheViewMode(Button button)
    {
        if (_navInProgress || _viewMode != OutfitViewMode.Clothe)
        {
            yield break;
        }

        _navInProgress = true;
        yield return PlayButtonPressFeedback(button);
        yield return TransitionToOutfitViewMode();
        _navInProgress = false;
    }

    static IEnumerator PlayButtonPressFeedback(Button button)
    {
        if (button == null)
        {
            yield break;
        }

        button.interactable = false;
        UIButtonPressFeedback pressFeedback = button.GetComponent<UIButtonPressFeedback>();
        if (pressFeedback != null)
        {
            yield return pressFeedback.PlayClickConfirm();
        }

        button.interactable = true;
    }

    IEnumerator TransitionToClotheViewMode()
    {
        SetClotheModeUiActive(false);

        yield return RunParallelRoutines(
            AnimateCharacterViewMode(
                outfitCharacterVariantAnchoredPosition,
                clotheCharacterVariantAnchoredPosition,
                outfitCharacterShadowAnchoredPosition,
                clotheCharacterShadowAnchoredPosition),
            PlayOutfitUiExitSequence());

        _viewMode = OutfitViewMode.Clothe;
        yield return PlayClotheModeUiEntrySequence();
    }

    IEnumerator TransitionToOutfitViewMode()
    {
        PrepareOutfitUiForEntry();

        yield return RunParallelRoutines(
            AnimateCharacterViewMode(
                clotheCharacterVariantAnchoredPosition,
                outfitCharacterVariantAnchoredPosition,
                clotheCharacterShadowAnchoredPosition,
                outfitCharacterShadowAnchoredPosition),
            PlayClotheModeUiExitSequence());

        _viewMode = OutfitViewMode.Outfit;
        SetClotheModeUiActive(false);
        ClearClotheCategorySelection();
        _clothePendingSelection.Clear();
        _clotheEntryInitialCategory = ClotheCategoryType.Top;
        ApplyViewModeUiVisibility();
        ApplyOutfitAlbumVisual();

        yield return PlayOutfitUiEntrySequence();
    }

    IEnumerator PlayOutfitUiExitSequence()
    {
        int runningCount = 0;
        Action onStarted = () => runningCount++;
        Action onComplete = () => runningCount--;

        StartOptionalButtonSlideOut(toClotheButtonSlideInAnimator, onStarted, onComplete);
        StartOptionalButtonSlideOut(toCdButtonSlideInAnimator, onStarted, onComplete);
        StartOptionalRectSlideOut(statusSlideInAnimator, onStarted, onComplete);
        StartOptionalRectSlideOut(currentEquipmentSlideInAnimator, onStarted, onComplete);

        onStarted();
        StartCoroutine(RunRoutineWithCompletion(PlayAlbumExitSequence(), onComplete));

        while (runningCount > 0)
        {
            yield return null;
        }
    }

    IEnumerator PlayAlbumExitSequence()
    {
        if (albumCdFlyIn != null && IsAlbumCdVisibleForFlyOut())
        {
            yield return albumCdFlyIn.PlayFlyOut();
        }
        else
        {
            HideAlbumCd();
        }

        if (albumFlyIn != null && IsAlbumVisibleForFlyOut())
        {
            yield return albumFlyIn.PlayFlyOut();
        }
        else
        {
            HideAlbum();
        }
    }

    bool IsAlbumCdVisibleForFlyOut()
    {
        EnsureAlbumCdRectReference();
        return albumCdRect != null && albumCdRect.gameObject.activeInHierarchy;
    }

    bool IsAlbumVisibleForFlyOut()
    {
        EnsureAlbumRectReference();
        return albumRect != null && albumRect.gameObject.activeInHierarchy;
    }

    void HideAlbumCd()
    {
        if (albumCdFlyIn != null)
        {
            albumCdFlyIn.PrepareHidden();
            return;
        }

        if (albumCdRect != null)
        {
            albumCdRect.gameObject.SetActive(false);
        }
    }

    void HideAlbum()
    {
        if (albumFlyIn != null)
        {
            albumFlyIn.PrepareHidden();
            return;
        }

        if (albumRect != null)
        {
            albumRect.gameObject.SetActive(false);
        }
    }

    IEnumerator PlayOutfitUiEntrySequence()
    {
        int runningCount = 0;
        Action onStarted = () => runningCount++;
        Action onComplete = () => runningCount--;

        onStarted();
        StartCoroutine(RunRoutineWithCompletion(PlayAlbumEntrySequence(), onComplete));

        StartButtonSlideIn(toClotheButtonSlideInAnimator, onStarted, onComplete);
        StartButtonSlideIn(toCdButtonSlideInAnimator, onStarted, onComplete);
        StartRectSlideIn(statusSlideInAnimator, onStarted, onComplete);
        StartRectSlideIn(currentEquipmentSlideInAnimator, onStarted, onComplete);

        while (runningCount > 0)
        {
            yield return null;
        }
    }

    void PrepareOutfitUiForEntry()
    {
        ActivateOutfitExclusiveUiRoots();
        PrepareOutfitModePanelsOffScreen();
        PrepareAlbumHiddenForEntry();
        PrepareAlbumCdHiddenForEntry();
    }

    void ActivateOutfitExclusiveUiRoots()
    {
        if (toClotheButton != null)
        {
            toClotheButton.gameObject.SetActive(true);
        }

        if (toCdButton != null)
        {
            toCdButton.gameObject.SetActive(true);
        }

        if (statusRect != null)
        {
            statusRect.gameObject.SetActive(true);
        }

        if (currentEquipmentRect != null)
        {
            currentEquipmentRect.gameObject.SetActive(true);
        }

        if (albumRect != null)
        {
            albumRect.gameObject.SetActive(true);
        }
    }

    void PrepareOutfitModePanelsOffScreen()
    {
        toClotheButtonSlideInAnimator?.PrepareOffScreenRight();
        toCdButtonSlideInAnimator?.PrepareOffScreenRight();
        statusSlideInAnimator?.PrepareOffScreenRight();
        currentEquipmentSlideInAnimator?.PrepareOffScreenRight();
    }

    void StartOptionalButtonSlideOut(
        UIButtonSlideInEntryAnimator animator,
        Action onSlideStarted,
        Action onSlideComplete,
        bool deactivateOnComplete = true)
    {
        if (animator == null)
        {
            return;
        }

        onSlideStarted?.Invoke();
        StartCoroutine(CompleteButtonSlideOut(animator, onSlideComplete, deactivateOnComplete));
    }

    void StartOptionalRectSlideOut(
        UIRectSlideInEntryAnimator animator,
        Action onSlideStarted,
        Action onSlideComplete,
        bool deactivateOnComplete = true)
    {
        if (animator == null)
        {
            return;
        }

        onSlideStarted?.Invoke();
        StartCoroutine(CompleteRectSlideOut(animator, onSlideComplete, deactivateOnComplete));
    }

    void StartOptionalBannerFlyOut(
        UIStageReadyBannerFlyIn flyIn,
        Action onFlyOutStarted,
        Action onFlyOutComplete)
    {
        if (flyIn == null)
        {
            return;
        }

        onFlyOutStarted?.Invoke();
        StartCoroutine(CompleteBannerFlyOut(flyIn, onFlyOutComplete));
    }

    static IEnumerator CompleteButtonSlideOut(
        UIButtonSlideInEntryAnimator animator,
        Action onSlideComplete,
        bool deactivateOnComplete = true)
    {
        yield return animator.PlaySlideOut(deactivateOnComplete);
        onSlideComplete?.Invoke();
    }

    static IEnumerator CompleteRectSlideOut(
        UIRectSlideInEntryAnimator animator,
        Action onSlideComplete,
        bool deactivateOnComplete = true)
    {
        yield return animator.PlaySlideOut(deactivateOnComplete);
        onSlideComplete?.Invoke();
    }

    static IEnumerator CompleteBannerFlyOut(UIStageReadyBannerFlyIn flyIn, Action onFlyOutComplete)
    {
        yield return flyIn.PlayFlyOut();
        onFlyOutComplete?.Invoke();
    }

    IEnumerator RunParallelRoutines(params IEnumerator[] routines)
    {
        if (routines == null || routines.Length == 0)
        {
            yield break;
        }

        int remaining = 0;
        for (int i = 0; i < routines.Length; i++)
        {
            if (routines[i] == null)
            {
                continue;
            }

            remaining++;
            StartCoroutine(RunRoutineWithCompletion(routines[i], () => remaining--));
        }

        while (remaining > 0)
        {
            yield return null;
        }
    }

    static IEnumerator RunRoutineWithCompletion(IEnumerator routine, Action onComplete)
    {
        yield return routine;
        onComplete?.Invoke();
    }

    IEnumerator HandleNavigationButtonClick(Button button, Action navigate)
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

    void EnsureNavigationButtonReferences()
    {
        if (backButton == null)
        {
            backButton = FindSceneButton("BackToHomeButton");
        }

        if (toClotheButton == null)
        {
            toClotheButton = FindSceneButton("ToClotheButton");
        }

        if (toCdButton == null)
        {
            toCdButton = FindSceneButton("ToCDButton");
        }
    }

    static Button FindSceneButton(string objectName)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    void EnsureNavigationButtonSlideInAnimators()
    {
        backToHomeButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            backToHomeButtonSlideInAnimator,
            backButton,
            "BackToHomeButton",
            fromLeft: true,
            backToHomeSlideInOffsetX);

        toClotheButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            toClotheButtonSlideInAnimator,
            toClotheButton,
            "ToClotheButton",
            fromLeft: false,
            navigationButtonSlideInOffsetX);

        toCdButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            toCdButtonSlideInAnimator,
            toCdButton,
            "ToCDButton",
            fromLeft: false,
            navigationButtonSlideInOffsetX);
    }

    UIButtonSlideInEntryAnimator EnsureSlideInAnimatorOnButton(
        UIButtonSlideInEntryAnimator animator,
        Button button,
        string fallbackName,
        bool fromLeft,
        float offsetX)
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
        animator.ConfigureSlideFromLeft(offsetX, slideInDuration, slideInEaseOutPower, fromLeft);
        return animator;
    }

    void PrepareNavigationButtonsOffScreen()
    {
        backToHomeButtonSlideInAnimator?.PrepareOffScreenLeft();
        toClotheButtonSlideInAnimator?.PrepareOffScreenRight();
        toCdButtonSlideInAnimator?.PrepareOffScreenRight();
    }

    void EnsureUiPanelSlideInAnimators()
    {
        statusRect = EnsureRectTransform(statusRect, "Status");
        currentEquipmentRect = EnsureRectTransform(currentEquipmentRect, "CurrentEquipment");

        statusSlideInAnimator = EnsureRectSlideInAnimator(
            statusSlideInAnimator,
            statusRect,
            "Status");

        currentEquipmentSlideInAnimator = EnsureRectSlideInAnimator(
            currentEquipmentSlideInAnimator,
            currentEquipmentRect,
            "CurrentEquipment");
    }

    RectTransform EnsureRectTransform(RectTransform rect, string objectName)
    {
        if (rect != null)
        {
            return rect;
        }

        GameObject targetObject = GameObject.Find(objectName);
        if (targetObject != null)
        {
            return targetObject.transform as RectTransform;
        }

        Transform clotheRoot = GetClotheHierarchyRoot();
        if (clotheRoot == null)
        {
            return null;
        }

        return FindNamedTransformUnder(clotheRoot, objectName) as RectTransform;
    }

    UIRectSlideInEntryAnimator EnsureRectSlideInAnimator(
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
        animator.Configure(uiPanelSlideInOffsetX, slideInDuration, slideInEaseOutPower);
        return animator;
    }

    void PrepareUiPanelsOffScreen()
    {
        statusSlideInAnimator?.PrepareOffScreenRight();
        currentEquipmentSlideInAnimator?.PrepareOffScreenRight();
    }

    void EnsureClotheModeSlideInAnimators()
    {
        RectTransform clotheStatusRect = EnsureRectTransform(null, "ClotheStatus");
        RectTransform clothePanelRect = EnsureRectTransform(null, "ClothePanel");

        clotheStatusSlideInAnimator = EnsureRectSlideInAnimatorFromLeft(
            clotheStatusSlideInAnimator,
            clotheStatusRect,
            "ClotheStatus");

        clothePanelSlideInAnimator = EnsureRectSlideInAnimator(
            clothePanelSlideInAnimator,
            clothePanelRect,
            "ClothePanel");

        Transform clotheRoot = GetClotheHierarchyRoot();
        Button topButton = FindClotheSceneButton(clotheRoot, "TopButton");
        Button bottomButton = FindClotheSceneButton(clotheRoot, "BottomButton");
        Button cdButton = FindClotheSceneButton(clotheRoot, "CDButton");
        Button toChangeButton = FindClotheSceneButton(clotheRoot, "ToChangeButton");

        clotheTopButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            clotheTopButtonSlideInAnimator,
            topButton,
            "TopButton",
            fromLeft: false,
            clotheUiSlideInOffsetX);

        clotheBottomButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            clotheBottomButtonSlideInAnimator,
            bottomButton,
            "BottomButton",
            fromLeft: false,
            clotheUiSlideInOffsetX);

        clotheCdButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            clotheCdButtonSlideInAnimator,
            cdButton,
            "CDButton",
            fromLeft: false,
            clotheUiSlideInOffsetX);

        toChangeButtonSlideInAnimator = EnsureSlideInAnimatorOnButton(
            toChangeButtonSlideInAnimator,
            toChangeButton,
            "ToChangeButton",
            fromLeft: false,
            clotheUiSlideInOffsetX);
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
        animator.ConfigureFromLeft(clotheUiSlideInOffsetX, slideInDuration, slideInEaseOutPower);
        return animator;
    }

    void PrepareClotheUiOffScreen()
    {
        clotheStatusSlideInAnimator?.PrepareOffScreenLeft();
        clothePanelSlideInAnimator?.PrepareOffScreenRight();
        clotheTopButtonSlideInAnimator?.PrepareOffScreenRight();
        clotheBottomButtonSlideInAnimator?.PrepareOffScreenRight();
        clotheCdButtonSlideInAnimator?.PrepareOffScreenRight();
        toChangeButtonSlideInAnimator?.PrepareOffScreenRight();
    }

    void EnsureClotheSlideTargetsActiveForEntry()
    {
        EnsureClotheModeSlideInAnimators();
        SetSlideTargetActive(clotheStatusSlideInAnimator?.TargetRect);
        SetSlideTargetActive(clothePanelSlideInAnimator?.TargetRect);
        SetSlideTargetActive(clotheTopButtonSlideInAnimator?.TargetButton);
        SetSlideTargetActive(clotheBottomButtonSlideInAnimator?.TargetButton);
        SetSlideTargetActive(clotheCdButtonSlideInAnimator?.TargetButton);
        SetSlideTargetActive(toChangeButtonSlideInAnimator?.TargetButton);
    }

    static void SetSlideTargetActive(RectTransform rect)
    {
        if (rect != null)
        {
            rect.gameObject.SetActive(true);
        }
    }

    static void SetSlideTargetActive(Button button)
    {
        if (button != null)
        {
            button.gameObject.SetActive(true);
        }
    }

    IEnumerator PlayClotheModeUiEntrySequence()
    {
        HideClotheItemPanels();
        SetUiRootsActive(clotheModeUiRoots, true);
        ApplyClotheSlideDesignRestPositions();
        EnsureClotheSlideTargetsActiveForEntry();
        EnsureClotheCategoryReferences();
        BindClotheCategoryButtonListeners();
        EnsureClotheItemSelectionBindings();
        PrepareDefaultClotheSelectionForEntry();
        PrepareClotheUiOffScreen();

        int runningCount = 0;
        Action onStarted = () => runningCount++;
        Action onComplete = () => runningCount--;

        StartRectSlideIn(clotheStatusSlideInAnimator, onStarted, onComplete);
        StartRectSlideIn(clothePanelSlideInAnimator, onStarted, onComplete);
        StartButtonSlideIn(clotheTopButtonSlideInAnimator, onStarted, onComplete);
        StartButtonSlideIn(clotheBottomButtonSlideInAnimator, onStarted, onComplete);
        StartButtonSlideIn(clotheCdButtonSlideInAnimator, onStarted, onComplete);
        StartButtonSlideIn(toChangeButtonSlideInAnimator, onStarted, onComplete);

        while (runningCount > 0)
        {
            yield return null;
        }
    }

    IEnumerator PlayClotheModeUiExitSequence()
    {
        ApplyClotheSlideDesignRestPositions();
        EnsureClotheModeSlideInAnimators();
        SnapClotheUiToRest();

        int runningCount = 0;
        Action onStarted = () => runningCount++;
        Action onComplete = () => runningCount--;

        StartOptionalRectSlideOut(clotheStatusSlideInAnimator, onStarted, onComplete, deactivateOnComplete: false);
        StartOptionalRectSlideOut(clothePanelSlideInAnimator, onStarted, onComplete, deactivateOnComplete: false);
        StartOptionalButtonSlideOut(clotheTopButtonSlideInAnimator, onStarted, onComplete, deactivateOnComplete: false);
        StartOptionalButtonSlideOut(clotheBottomButtonSlideInAnimator, onStarted, onComplete, deactivateOnComplete: false);
        StartOptionalButtonSlideOut(clotheCdButtonSlideInAnimator, onStarted, onComplete, deactivateOnComplete: false);
        StartOptionalButtonSlideOut(toChangeButtonSlideInAnimator, onStarted, onComplete, deactivateOnComplete: false);

        while (runningCount > 0)
        {
            yield return null;
        }
    }

    void PrepareDefaultClotheSelectionForEntry()
    {
        ResetClothePendingFromEquipped();
        ActivateClotheCategoryUi(_clotheEntryInitialCategory);
    }

    void ApplyDefaultClotheSelectionOnEnter()
    {
        if (_viewMode != OutfitViewMode.Clothe)
        {
            return;
        }

        EnsureClotheItemSelectionBindings();
        PrepareDefaultClotheSelectionForEntry();
    }

    void EnsureNavigationButtonPressFeedback()
    {
        EnsurePressFeedbackOnButton(backButton);
        EnsurePressFeedbackOnButton(toClotheButton);
        EnsurePressFeedbackOnButton(toCdButton);
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

    void EnsureCDPanelReferences()
    {
        if (itemCDPanel == null)
        {
            GameObject panelObject = GameObject.Find("CDPanel");
            if (panelObject != null)
            {
                itemCDPanel = panelObject;
            }
        }

        if (openCDButton == null)
        {
            openCDButton = FindSceneButton("SelectCD");
        }
    }

    void EnsureItemPanelReferences()
    {
        if (itemTopPanel == null)
        {
            GameObject panelObject = GameObject.Find("ItemTopPanel");
            if (panelObject != null)
            {
                itemTopPanel = panelObject;
            }
        }

        if (itemBottomPanel == null)
        {
            GameObject panelObject = GameObject.Find("ItemBottomPanel");
            if (panelObject != null)
            {
                itemBottomPanel = panelObject;
            }
        }

        if (openTopButton == null)
        {
            openTopButton = FindSceneButton("SelectTops");
        }

        if (openBottomButton == null)
        {
            openBottomButton = FindSceneButton("SelectBottoms");
        }

        EnsureCDPanelReferences();
    }

    void EnsureAlbumSceneReferences()
    {
        EnsureAlbumRectReference();
        if (albumRoot == null && albumRect != null)
        {
            albumRoot = albumRect;
        }

        EnsureAlbumCdRectReference();
    }

    Transform ResolveAlbumRoot()
    {
        if (albumRoot != null)
        {
            return albumRoot;
        }

        EnsureAlbumRectReference();
        if (albumRect != null)
        {
            albumRoot = albumRect;
            return albumRect;
        }

        Transform found = FindSceneTransformIncludingInactive("Album");
        if (found != null)
        {
            albumRoot = found;
            albumRect = found as RectTransform;
        }

        return albumRoot;
    }

    void EnsureAlbumRectReference()
    {
        if (albumRect != null)
        {
            return;
        }

        if (TryGetOutfitUiRootTransform("Album", out Transform albumTransform))
        {
            albumRoot = albumTransform;
            albumRect = albumTransform as RectTransform;
            return;
        }

        Transform found = FindSceneTransformIncludingInactive("Album");
        if (found != null)
        {
            albumRoot = found;
            albumRect = found as RectTransform;
        }
    }

    void EnsureAlbumCdRectReference()
    {
        if (albumCdRect != null)
        {
            return;
        }

        Transform found = FindSceneTransformIncludingInactive("AlbumCD");
        if (found != null)
        {
            albumCdRect = found as RectTransform;
        }
    }

    bool TryGetOutfitUiRootTransform(string objectName, out Transform found)
    {
        if (outfitModeUiRoots != null)
        {
            for (int i = 0; i < outfitModeUiRoots.Length; i++)
            {
                GameObject rootObject = outfitModeUiRoots[i];
                if (rootObject != null && rootObject.name == objectName)
                {
                    found = rootObject.transform;
                    return true;
                }
            }
        }

        found = null;
        return false;
    }

    static Transform FindSceneTransformIncludingInactive(string objectName)
    {
        GameObject[] sceneObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            GameObject candidate = sceneObjects[i];
            if (candidate == null
                || candidate.hideFlags != HideFlags.None
                || !candidate.scene.IsValid()
                || !string.Equals(candidate.name, objectName, StringComparison.Ordinal))
            {
                continue;
            }

            return candidate.transform;
        }

        GameObject activeObject = GameObject.Find(objectName);
        return activeObject != null ? activeObject.transform : null;
    }

    void CacheAlbumRestAnchoredPosition()
    {
        EnsureAlbumRectReference();
        if (_albumRestPositionCached || albumRect == null)
        {
            return;
        }

        _albumRestAnchoredPosition = albumRect.anchoredPosition;
        _albumRestPositionCached = true;
    }

    void EnsureAlbumFlyIn()
    {
        EnsureAlbumRectReference();
        if (albumRect == null)
        {
            return;
        }

        CacheAlbumRestAnchoredPosition();

        if (albumFlyIn == null)
        {
            UIStageReadyBannerFlyIn[] flyIns = GetComponents<UIStageReadyBannerFlyIn>();
            for (int i = 0; i < flyIns.Length; i++)
            {
                if (flyIns[i] != null && flyIns[i] != albumCdFlyIn)
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
            _albumRestAnchoredPosition,
            albumFlyInDuration,
            slideInEaseOutPower,
            useFadeIn: false,
            "Album",
            bringToFront: false);

        albumFlyIn.EnsureBannerReference();
    }

    void EnsureAlbumCdFlyIn()
    {
        EnsureAlbumCdRectReference();
        if (albumCdRect == null)
        {
            return;
        }

        CacheAlbumCdRestAnchoredPosition();

        if (albumCdFlyIn == null)
        {
            UIStageReadyBannerFlyIn[] flyIns = GetComponents<UIStageReadyBannerFlyIn>();
            for (int i = 0; i < flyIns.Length; i++)
            {
                if (flyIns[i] != null && flyIns[i] != albumFlyIn)
                {
                    albumCdFlyIn = flyIns[i];
                    break;
                }
            }

            if (albumCdFlyIn == null)
            {
                albumCdFlyIn = gameObject.AddComponent<UIStageReadyBannerFlyIn>();
            }
        }

        ConfigureAlbumCdFlyInFromActiveAlbum();
        albumCdFlyIn.EnsureBannerReference();
    }

    void CacheAlbumCdRestAnchoredPosition()
    {
        EnsureAlbumCdRectReference();
        if (_albumCdRestPositionCached || albumCdRect == null)
        {
            return;
        }

        _albumCdRestAnchoredPosition = albumCdRect.anchoredPosition;
        _albumCdRestPositionCached = true;
    }

    void ConfigureAlbumFlyInForEntry()
    {
        CacheAlbumRestAnchoredPosition();

        if (albumFlyIn == null)
        {
            EnsureAlbumFlyIn();
            return;
        }

        EnsureAlbumRectReference();
        if (albumRect == null)
        {
            return;
        }

        albumFlyIn.Configure(
            albumRect,
            albumFlyInFromPosition,
            _albumRestAnchoredPosition,
            albumFlyInDuration,
            slideInEaseOutPower,
            useFadeIn: false,
            "Album",
            bringToFront: false);
    }

    void ConfigureAlbumCdFlyInFromActiveAlbum()
    {
        EnsureAlbumCdRectReference();
        if (albumCdFlyIn == null || albumCdRect == null)
        {
            return;
        }

        CacheAlbumCdRestAnchoredPosition();

        albumCdFlyIn.Configure(
            albumCdRect,
            ResolveAlbumCdFlyInFromPosition(),
            _albumCdRestAnchoredPosition,
            albumFlyInDuration,
            slideInEaseOutPower,
            useFadeIn: false,
            "AlbumCD",
            bringToFront: false);
    }

    RectTransform ResolveActiveAlbumVariantRect()
    {
        Transform root = ResolveAlbumRoot();
        if (root == null)
        {
            return albumRect;
        }

        string activeChildName = OutfitItemVisualHelper.GetOutfitAlbumChildName(ResolveDisplayedCdItem());
        RectTransform variant = root.Find(activeChildName) as RectTransform;
        if (variant != null)
        {
            return variant;
        }

        RectTransform starterAlbum = root.Find("StarterAlbum") as RectTransform;
        return starterAlbum != null ? starterAlbum : albumRect;
    }

    Vector2 ResolveAlbumCdFlyInFromPosition()
    {
        EnsureAlbumCdRectReference();
        RectTransform fromRect = ResolveActiveAlbumVariantRect();
        if (fromRect == null || albumCdRect == null)
        {
            return _albumRestAnchoredPosition;
        }

        RectTransform positionRoot = albumCdRect.parent as RectTransform;
        if (positionRoot == null)
        {
            return fromRect.anchoredPosition;
        }

        return GetAnchoredPositionInParent(fromRect, positionRoot);
    }

    static Vector2 GetAnchoredPositionInParent(RectTransform source, RectTransform parent)
    {
        Vector3 worldCenter = source.TransformPoint(source.rect.center);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            RectTransformUtility.WorldToScreenPoint(null, worldCenter),
            null,
            out Vector2 localPoint);
        return localPoint;
    }

    void PrepareAlbumHiddenForEntry()
    {
        if (albumFlyIn != null)
        {
            albumFlyIn.PrepareHidden();
            return;
        }

        if (albumRect != null)
        {
            albumRect.gameObject.SetActive(false);
        }
    }

    void PrepareAlbumCdHiddenForEntry()
    {
        if (albumCdFlyIn != null)
        {
            albumCdFlyIn.PrepareHidden();
            return;
        }

        if (albumCdRect != null)
        {
            albumCdRect.gameObject.SetActive(false);
        }
    }

    void EnsureCharacterReferences()
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

    void ResolveCharacterImage()
    {
        EnsureCharacterReferences();
        if (characterRoot == null)
        {
            characterImage = null;
            return;
        }

        if (characterImage != null && characterImage.gameObject.activeInHierarchy)
        {
            return;
        }

        characterImage = null;
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
                characterImage = image;
                return;
            }
        }
    }

    void ResolveCharacterShadowImage()
    {
        if (characterShadowImage != null)
        {
            return;
        }

        GameObject shadowObject = GameObject.Find("CharacterShadow");
        if (shadowObject == null)
        {
            return;
        }

        characterShadowImage = shadowObject.GetComponent<Image>();
        if (characterShadowImage == null)
        {
            characterShadowImage = shadowObject.GetComponentInChildren<Image>(true);
        }
    }

    void EnsureViewModeUiReferences()
    {
        if (outfitModeUiRoots == null || outfitModeUiRoots.Length == 0)
        {
            outfitModeUiRoots = CollectNonNullSceneRoots(
                FindSceneObject("Status"),
                FindSceneObject("CurrentEquipment"),
                FindSceneObject("Album"),
                FindSceneObject("ToClotheButton"),
                FindSceneObject("ToCDButton"));
        }

        if (clotheModeUiRoots == null || clotheModeUiRoots.Length == 0)
        {
            clotheModeUiRoots = BuildDefaultClotheModeUiRoots();
        }

        EnsureAlbumSceneReferences();
    }

    static GameObject[] BuildDefaultClotheModeUiRoots()
    {
        GameObject clotheRoot = FindSceneObject("Clothe");
        if (clotheRoot != null)
        {
            return new[] { clotheRoot };
        }

        return CollectNonNullSceneRoots(
            FindSceneObject("ClotheStatus"),
            FindSceneObject("ClothePanel"),
            FindSceneObject("ToChangeButton"),
            FindSceneObject("TopSelect"),
            FindSceneObject("BottomSelect"),
            FindSceneObject("CDSelect"),
            FindSceneObject("TopButton"),
            FindSceneObject("BottomButton"),
            FindSceneObject("CDButton"));
    }

    static GameObject[] CollectNonNullSceneRoots(params GameObject[] candidates)
    {
        if (candidates == null || candidates.Length == 0)
        {
            return Array.Empty<GameObject>();
        }

        var roots = new List<GameObject>(candidates.Length);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null)
            {
                roots.Add(candidates[i]);
            }
        }

        return roots.ToArray();
    }

    static GameObject FindSceneObject(string objectName)
    {
        GameObject sceneObject = GameObject.Find(objectName);
        return sceneObject;
    }

    void ApplyViewModeUiVisibility()
    {
        bool showOutfitUi = _viewMode == OutfitViewMode.Outfit;
        if (showOutfitUi)
        {
            SetClotheModeUiActive(false);

            if (toClotheButton != null && !toClotheButton.gameObject.activeInHierarchy)
            {
                toClotheButton.gameObject.SetActive(true);
            }

            if (toCdButton != null && !toCdButton.gameObject.activeInHierarchy)
            {
                toCdButton.gameObject.SetActive(true);
            }

            return;
        }

        SetUiRootsActive(outfitModeUiRoots, false);
        ShowClotheModeUiAtRest();
    }

    void SetClotheModeUiActive(bool active)
    {
        SetUiRootsActive(clotheModeUiRoots, active);
        if (!active)
        {
            ClearClotheCategorySelection();
            HideClotheItemPanels();
        }
    }

    void ShowClotheModeUiAtRest()
    {
        HideClotheItemPanels();
        SetUiRootsActive(clotheModeUiRoots, true);
        ApplyClotheSlideDesignRestPositions();
        EnsureClotheSlideTargetsActiveForEntry();
        SnapClotheUiToRest();
        EnsureClotheCategoryReferences();
        BindClotheCategoryButtonListeners();
        EnsureClotheItemSelectionBindings();
        ApplyDefaultClotheSelectionOnEnter();
    }

    void SnapClotheUiToRest()
    {
        clotheStatusSlideInAnimator?.SnapToRest();
        clothePanelSlideInAnimator?.SnapToRest();
        clotheTopButtonSlideInAnimator?.SnapToRest();
        clotheBottomButtonSlideInAnimator?.SnapToRest();
        clotheCdButtonSlideInAnimator?.SnapToRest();
        toChangeButtonSlideInAnimator?.SnapToRest();
    }

    void CaptureClotheSlideDesignRestPositions()
    {
        if (_clotheSlideDesignRestCaptured)
        {
            return;
        }

        Transform clotheRoot = GetClotheHierarchyRoot();
        if (clotheRoot == null)
        {
            return;
        }

        RectTransform statusRect = FindNamedTransformUnder(clotheRoot, "ClotheStatus") as RectTransform;
        RectTransform panelRect = FindNamedTransformUnder(clotheRoot, "ClothePanel") as RectTransform;
        if (statusRect != null)
        {
            _clotheStatusDesignRestAnchoredPosition = statusRect.anchoredPosition;
        }

        if (panelRect != null)
        {
            _clothePanelDesignRestAnchoredPosition = panelRect.anchoredPosition;
        }

        CacheButtonDesignRest(clotheRoot, "TopButton", ref _clotheTopButtonDesignRestAnchoredPosition);
        CacheButtonDesignRest(clotheRoot, "BottomButton", ref _clotheBottomButtonDesignRestAnchoredPosition);
        CacheButtonDesignRest(clotheRoot, "CDButton", ref _clotheCdButtonDesignRestAnchoredPosition);
        CacheButtonDesignRest(clotheRoot, "ToChangeButton", ref _clotheToChangeButtonDesignRestAnchoredPosition);
        _clotheSlideDesignRestCaptured = true;
    }

    static void CacheButtonDesignRest(Transform clotheRoot, string buttonName, ref Vector2 restPosition)
    {
        Button button = FindNamedButtonUnder(clotheRoot, buttonName);
        RectTransform buttonRect = button != null ? button.transform as RectTransform : null;
        if (buttonRect != null)
        {
            restPosition = buttonRect.anchoredPosition;
        }
    }

    void ApplyClotheSlideDesignRestPositions()
    {
        if (!_clotheSlideDesignRestCaptured)
        {
            CaptureClotheSlideDesignRestPositions();
        }

        if (!_clotheSlideDesignRestCaptured)
        {
            return;
        }

        Transform clotheRoot = GetClotheHierarchyRoot();
        if (clotheRoot == null)
        {
            return;
        }

        RectTransform statusRect = FindNamedTransformUnder(clotheRoot, "ClotheStatus") as RectTransform;
        RectTransform panelRect = FindNamedTransformUnder(clotheRoot, "ClothePanel") as RectTransform;
        if (statusRect != null)
        {
            statusRect.anchoredPosition = _clotheStatusDesignRestAnchoredPosition;
        }

        if (panelRect != null)
        {
            panelRect.anchoredPosition = _clothePanelDesignRestAnchoredPosition;
        }

        ApplyButtonDesignRest(clotheRoot, "TopButton", _clotheTopButtonDesignRestAnchoredPosition);
        ApplyButtonDesignRest(clotheRoot, "BottomButton", _clotheBottomButtonDesignRestAnchoredPosition);
        ApplyButtonDesignRest(clotheRoot, "CDButton", _clotheCdButtonDesignRestAnchoredPosition);
        ApplyButtonDesignRest(clotheRoot, "ToChangeButton", _clotheToChangeButtonDesignRestAnchoredPosition);
    }

    static void ApplyButtonDesignRest(Transform clotheRoot, string buttonName, Vector2 restPosition)
    {
        Button button = FindNamedButtonUnder(clotheRoot, buttonName);
        RectTransform buttonRect = button != null ? button.transform as RectTransform : null;
        if (buttonRect != null)
        {
            buttonRect.anchoredPosition = restPosition;
        }
    }

    static Button FindClotheSceneButton(Transform clotheRoot, string objectName)
    {
        if (clotheRoot != null)
        {
            Button buttonUnderClothe = FindNamedButtonUnder(clotheRoot, objectName);
            if (buttonUnderClothe != null)
            {
                return buttonUnderClothe;
            }
        }

        return FindSceneButton(objectName);
    }

    void HideClotheItemPanels()
    {
        EnsureItemPanelReferences();

        if (itemTopPanel != null)
        {
            itemTopPanel.SetActive(false);
        }

        if (itemBottomPanel != null)
        {
            itemBottomPanel.SetActive(false);
        }

        if (itemCDPanel != null)
        {
            itemCDPanel.SetActive(false);
        }
    }

    static void SetUiRootsActive(GameObject[] roots, bool active)
    {
        if (roots == null)
        {
            return;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null)
            {
                roots[i].SetActive(active);
            }
        }
    }

    void ResolveCharacterVariantRect()
    {
        ResolveCharacterImage();
        _characterVariantRect = characterImage != null ? characterImage.rectTransform : null;
    }

    void ResolveCharacterShadowRect()
    {
        if (_characterShadowRect != null)
        {
            return;
        }

        if (characterShadowImage != null)
        {
            _characterShadowRect = characterShadowImage.rectTransform;
            return;
        }

        GameObject shadowObject = GameObject.Find("CharacterShadow");
        if (shadowObject != null)
        {
            _characterShadowRect = shadowObject.GetComponent<RectTransform>();
        }
    }

    void SnapCharacterToOutfitViewPositions()
    {
        SnapCharacterViewModePositions(
            outfitCharacterVariantAnchoredPosition,
            outfitCharacterShadowAnchoredPosition);
    }

    void SnapCharacterToClotheViewPositions()
    {
        SnapCharacterViewModePositions(
            clotheCharacterVariantAnchoredPosition,
            clotheCharacterShadowAnchoredPosition);
    }

    void SnapCharacterToCurrentViewModePositions()
    {
        if (_viewMode == OutfitViewMode.Clothe)
        {
            SnapCharacterToClotheViewPositions();
            return;
        }

        SnapCharacterToOutfitViewPositions();
    }

    void SnapCharacterViewModePositions(Vector2 variantPosition, Vector2 shadowPosition)
    {
        ResolveCharacterVariantRect();
        ResolveCharacterShadowRect();

        if (_characterVariantRect != null)
        {
            _characterVariantRect.anchoredPosition = variantPosition;
        }

        if (_characterShadowRect != null)
        {
            _characterShadowRect.anchoredPosition = shadowPosition;
        }
    }

    IEnumerator AnimateCharacterViewMode(
        Vector2 variantStart,
        Vector2 variantEnd,
        Vector2 shadowStart,
        Vector2 shadowEnd)
    {
        ResolveCharacterVariantRect();
        ResolveCharacterShadowRect();

        bool animateVariant = _characterVariantRect != null;
        bool animateShadow = _characterShadowRect != null;
        if (!animateVariant && !animateShadow)
        {
            yield break;
        }

        float duration = Mathf.Max(0f, viewModeTransitionDuration);
        if (duration <= 0f)
        {
            if (animateVariant)
            {
                _characterVariantRect.anchoredPosition = variantEnd;
            }

            if (animateShadow)
            {
                _characterShadowRect.anchoredPosition = shadowEnd;
            }

            yield break;
        }

        if (animateVariant)
        {
            _characterVariantRect.anchoredPosition = variantStart;
        }

        if (animateShadow)
        {
            _characterShadowRect.anchoredPosition = shadowStart;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float eased = EvaluateViewModeEaseOut(Mathf.Clamp01(elapsed / duration));

            if (animateVariant)
            {
                _characterVariantRect.anchoredPosition = Vector2.LerpUnclamped(variantStart, variantEnd, eased);
            }

            if (animateShadow)
            {
                _characterShadowRect.anchoredPosition = Vector2.LerpUnclamped(shadowStart, shadowEnd, eased);
            }

            yield return null;
        }

        if (animateVariant)
        {
            _characterVariantRect.anchoredPosition = variantEnd;
        }

        if (animateShadow)
        {
            _characterShadowRect.anchoredPosition = shadowEnd;
        }
    }

    static float EvaluateViewModeEaseOut(float normalizedTime, float easeOutPower)
    {
        normalizedTime = Mathf.Clamp01(normalizedTime);
        float power = Mathf.Max(1f, easeOutPower);
        return 1f - Mathf.Pow(1f - normalizedTime, power);
    }

    float EvaluateViewModeEaseOut(float normalizedTime)
    {
        return EvaluateViewModeEaseOut(normalizedTime, viewModeTransitionEaseOutPower);
    }

    void CacheCharacterRestColors()
    {
        _characterColorCached = false;
        _characterShadowColorCached = false;

        if (characterImage != null)
        {
            _characterRestColor = characterImage.color;
            if (_characterRestColor.a <= 0f)
            {
                _characterRestColor.a = 1f;
            }

            _characterColorCached = true;
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

    bool ShouldAnimateCharacterFadeIn()
    {
        return animateCharacterFadeIn
            && (_characterColorCached || _characterShadowColorCached);
    }

    void SetCharacterAlpha(float alpha)
    {
        if (characterImage != null && _characterColorCached)
        {
            Color color = _characterRestColor;
            color.a = Mathf.Clamp01(alpha) * _characterRestColor.a;
            characterImage.color = color;
        }

        if (characterShadowImage != null && _characterShadowColorCached)
        {
            Color color = _characterShadowRestColor;
            color.a = Mathf.Clamp01(alpha) * _characterShadowRestColor.a;
            characterShadowImage.color = color;
        }
    }

    void PrepareCharacterForFadeIn()
    {
        if (!ShouldAnimateCharacterFadeIn())
        {
            return;
        }

        SetCharacterAlpha(0f);
    }

    void SnapCharacterToRestColor()
    {
        if (_characterColorCached && characterImage != null)
        {
            characterImage.color = _characterRestColor;
        }

        if (_characterShadowColorCached && characterShadowImage != null)
        {
            characterShadowImage.color = _characterShadowRestColor;
        }
    }

    IEnumerator AnimateCharacterFadeIn()
    {
        if (!ShouldAnimateCharacterFadeIn())
        {
            yield break;
        }

        float delay = Mathf.Max(0f, characterFadeInStartDelay);
        float duration = Mathf.Max(0f, characterFadeInDuration);

        PrepareCharacterForFadeIn();

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        if (duration <= 0f)
        {
            SnapCharacterToRestColor();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetCharacterAlpha(t);
            yield return null;
        }

        SnapCharacterToRestColor();
    }
}
