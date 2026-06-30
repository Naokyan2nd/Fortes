using System;
using System.Collections.Generic;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スキルボタン2枠（攻撃・回復）の入力通知と本番用スプライト表示。
/// いずれか選択中は非選択側を戻るボタン表示に切り替える。
/// スケールは HUD スライド対象のルートとは別の子 <see cref="SelectionScaleRootName"/> に適用する。
/// </summary>
public sealed class CommandPanelView : MonoBehaviour
{
    public const int AttackSlotIndex = 0;
    private const int HealSlotIndex = 1;
    private const string ScaleTweenId = "CommandPanelScale";
    private const string SelectionScaleRootName = "SelectionScaleRoot";

    [Serializable]
    private sealed class CommandSlotVisual
    {
        public Image FrameImage;
    }

    [SerializeField]
    private Button[] _skillButtons;

    [SerializeField]
    private CommandSlotVisual[] _slotVisuals;

    [Header("Production Button Sprites")]
    [SerializeField]
    private Sprite _attackNormalSprite;

    [SerializeField]
    private Sprite _attackSelectedSprite;

    [SerializeField]
    private Sprite _healNormalSprite;

    [SerializeField]
    private Sprite _healSelectedSprite;

    [SerializeField]
    private Sprite _backSprite;

    [Header("Selection Scale")]
    [SerializeField]
    private float _selectedScale = 1.08f;

    [SerializeField]
    private float _normalScale = 1f;

    [SerializeField]
    private float _scaleTweenDuration = 0.12f;

    [Header("Tutorial Guide")]
    [SerializeField]
    private GameObject _attackGuideArrow;

    private const int AttackGuideArrowSortingOrder = 451;

    private Canvas _attackGuideArrowCanvas;

    private readonly Subject<int> _skillClicked = new Subject<int>();
    private int _selectedSlot = -1;
    private Transform[] _scaleTargets;
    private Vector3[] _buttonRootRestScales;

    private void Awake()
    {
        if (_skillButtons == null || _skillButtons.Length == 0)
        {
            Debug.LogError("[CommandPanelView] スキルボタン配列が未設定です。", this);
            return;
        }

        EnsureScaleTargets();
        RebuildSlotVisualRefs();

        for (int i = 0; i < _skillButtons.Length; i++)
        {
            ConfigureButton(i);
        }

        for (int i = 0; i < _skillButtons.Length; i++)
        {
            if (_skillButtons[i] == null)
            {
                Debug.LogError("[CommandPanelView] スキルボタンが null です。", this);
                continue;
            }

            int captured = i;
            _skillButtons[i].onClick.RemoveAllListeners();
            _skillButtons[i].onClick.AddListener(() => _skillClicked.OnNext(captured));
        }

        RefreshAllSlotVisuals();
        ApplyAllSlotScalesImmediate();
        ResolveAttackGuideArrowReference();
        SetAttackArrowVisible(false);
    }

    /// <summary>チュートリアル攻撃誘導用矢印の表示切替（GameObject.SetActive）。</summary>
    public void SetAttackArrowVisible(bool visible)
    {
        if (_attackGuideArrow == null)
        {
            return;
        }

        if (visible)
        {
            EnsureAttackGuideArrowCanvas();
        }

        _attackGuideArrow.SetActive(visible);
    }

    private void ResolveAttackGuideArrowReference()
    {
        if (_attackGuideArrow != null)
        {
            return;
        }

        Transform arrow = transform.Find("Arrow");
        if (arrow != null)
        {
            _attackGuideArrow = arrow.gameObject;
        }
    }

    private void EnsureAttackGuideArrowCanvas()
    {
        if (_attackGuideArrow == null)
        {
            return;
        }

        if (_attackGuideArrowCanvas == null)
        {
            _attackGuideArrowCanvas = _attackGuideArrow.GetComponent<Canvas>();
            if (_attackGuideArrowCanvas == null)
            {
                _attackGuideArrowCanvas = _attackGuideArrow.AddComponent<Canvas>();
            }

            if (_attackGuideArrow.GetComponent<GraphicRaycaster>() == null)
            {
                _attackGuideArrow.AddComponent<GraphicRaycaster>();
            }
        }

        _attackGuideArrowCanvas.overrideSorting = true;
        _attackGuideArrowCanvas.sortingOrder = AttackGuideArrowSortingOrder;
    }

    private void EnsureScaleTargets()
    {
        _scaleTargets = new Transform[_skillButtons.Length];
        _buttonRootRestScales = new Vector3[_skillButtons.Length];

        for (int i = 0; i < _skillButtons.Length; i++)
        {
            Button button = _skillButtons[i];
            if (button == null)
            {
                continue;
            }

            _buttonRootRestScales[i] = button.transform.localScale;
            _scaleTargets[i] = EnsureSelectionScaleRoot(button);
        }
    }

    private static Transform EnsureSelectionScaleRoot(Button button)
    {
        Transform buttonTransform = button.transform;
        Transform existing = buttonTransform.Find(SelectionScaleRootName);
        if (existing != null)
        {
            return existing;
        }

        var scaleRootGo = new GameObject(SelectionScaleRootName, typeof(RectTransform));
        Transform scaleRoot = scaleRootGo.transform;
        scaleRoot.SetParent(buttonTransform, false);
        StretchRectToParent((RectTransform)scaleRoot);

        Image image = button.targetGraphic as Image;
        if (image != null && image.transform == buttonTransform)
        {
            var graphicGo = new GameObject("Graphic", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            graphicGo.transform.SetParent(scaleRoot, false);
            StretchRectToParent(graphicGo.GetComponent<RectTransform>());

            Image newImage = graphicGo.GetComponent<Image>();
            CopyImageSettings(image, newImage);
            button.targetGraphic = newImage;
            Destroy(image);
        }

        var childrenToMove = new List<Transform>();
        for (int i = 0; i < buttonTransform.childCount; i++)
        {
            Transform child = buttonTransform.GetChild(i);
            if (child != scaleRoot)
            {
                childrenToMove.Add(child);
            }
        }

        for (int i = 0; i < childrenToMove.Count; i++)
        {
            childrenToMove[i].SetParent(scaleRoot, true);
        }

        scaleRoot.localScale = Vector3.one;
        return scaleRoot;
    }

    private static void StretchRectToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void CopyImageSettings(Image source, Image destination)
    {
        destination.sprite = source.sprite;
        destination.color = source.color;
        destination.material = source.material;
        destination.raycastTarget = source.raycastTarget;
        destination.maskable = source.maskable;
        destination.type = source.type;
        destination.preserveAspect = source.preserveAspect;
        destination.fillCenter = source.fillCenter;
        destination.fillMethod = source.fillMethod;
        destination.fillAmount = source.fillAmount;
        destination.fillClockwise = source.fillClockwise;
        destination.fillOrigin = source.fillOrigin;
        destination.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
    }

    private void RebuildSlotVisualRefs()
    {
        _slotVisuals = new CommandSlotVisual[_skillButtons.Length];
        for (int i = 0; i < _skillButtons.Length; i++)
        {
            _slotVisuals[i] = BuildSlotVisual(i);
        }
    }

    private CommandSlotVisual BuildSlotVisual(int slotIndex)
    {
        Button button = _skillButtons[slotIndex];
        CommandSlotVisual visual = new CommandSlotVisual();
        if (button == null)
        {
            return visual;
        }

        visual.FrameImage = button.targetGraphic as Image;
        return visual;
    }

    private void ConfigureButton(int slotIndex)
    {
        Button button = _skillButtons[slotIndex];
        if (button == null)
        {
            return;
        }

        button.transition = Selectable.Transition.None;
    }

    private void OnDestroy()
    {
        KillScaleTweens();
        _skillClicked.Dispose();
    }

    /// <summary>スキルスロットが押されたときのストリーム（0始まり）。</summary>
    public Observable<int> OnSkillSlotClicked => _skillClicked;

    /// <summary>スロットのボタン <see cref="RectTransform"/>（スポットライト穴の追従用）。</summary>
    public RectTransform GetSlotButtonRectTransform(int slotIndex)
    {
        if (_skillButtons == null || slotIndex < 0 || slotIndex >= _skillButtons.Length)
        {
            return null;
        }

        Button button = _skillButtons[slotIndex];
        return button != null ? button.transform as RectTransform : null;
    }

    /// <summary>指定スロットが戻るボタン表示か。</summary>
    public bool IsSlotBackButton(int slotIndex)
    {
        if (_selectedSlot < 0 || slotIndex == _selectedSlot)
        {
            return false;
        }

        if (_skillButtons == null || slotIndex < 0 || slotIndex >= _skillButtons.Length)
        {
            return false;
        }

        Button button = _skillButtons[slotIndex];
        return button != null && button.interactable;
    }

    /// <summary>
    /// 全スキルボタンの操作可否を一括設定する。
    /// </summary>
    public void SetAllInteractable(bool interactable)
    {
        if (_skillButtons == null)
        {
            return;
        }

        for (int i = 0; i < _skillButtons.Length; i++)
        {
            SetSlotInteractableWithoutRefresh(i, interactable);
        }

        if (!interactable)
        {
            ClearSelection();
            return;
        }

        RefreshAfterInteractableBatch();
    }

    /// <summary>
    /// スロットの操作可否。
    /// </summary>
    public void SetSlotInteractable(int slotIndex, bool interactable)
    {
        SetSlotInteractableWithoutRefresh(slotIndex, interactable);
        RefreshSlotVisual(slotIndex);
        ApplyAllSlotScalesImmediate();
    }

    /// <summary>
    /// 操作可否のみ更新（見た目の更新は <see cref="RefreshAfterInteractableBatch"/> でまとめて行う）。
    /// </summary>
    public void SetSlotInteractableWithoutRefresh(int slotIndex, bool interactable)
    {
        if (_skillButtons == null || slotIndex < 0 || slotIndex >= _skillButtons.Length)
        {
            return;
        }

        Button button = _skillButtons[slotIndex];
        if (button != null)
        {
            button.interactable = interactable;
        }

        if (!interactable && _selectedSlot == slotIndex)
        {
            _selectedSlot = -1;
        }
    }

    /// <summary>
    /// <see cref="SetSlotInteractableWithoutRefresh"/> 適用後にスプライト・スケールを一括反映する。
    /// </summary>
    public void RefreshAfterInteractableBatch()
    {
        RefreshAllSlotVisuals();
        ApplyAllSlotScalesImmediate();
    }

    /// <summary>
    /// 指定スロットを選択中表示にする。非選択の使用可能枠は戻る表示。
    /// </summary>
    public void SetSelectedSlot(int slotIndex)
    {
        if (_skillButtons == null || slotIndex < 0 || slotIndex >= _skillButtons.Length)
        {
            return;
        }

        if (_skillButtons[slotIndex] != null && !_skillButtons[slotIndex].interactable)
        {
            return;
        }

        int previousSlot = _selectedSlot;
        _selectedSlot = slotIndex;
        RefreshAllSlotVisuals();
        RefreshSlotScaleIfChanged(previousSlot, slotIndex);
    }

    /// <summary>
    /// 選択表示を解除し、全枠を未選択スプライトに戻す。
    /// </summary>
    public void ClearSelection()
    {
        if (_selectedSlot < 0)
        {
            return;
        }

        _selectedSlot = -1;
        RefreshAllSlotVisuals();
        ApplyAllSlotScalesImmediate();
    }

    private void RefreshAllSlotVisuals()
    {
        if (_skillButtons == null)
        {
            return;
        }

        for (int i = 0; i < _skillButtons.Length; i++)
        {
            RefreshSlotVisual(i);
        }
    }

    private void ApplyAllSlotScalesImmediate()
    {
        if (_skillButtons == null)
        {
            return;
        }

        for (int i = 0; i < _skillButtons.Length; i++)
        {
            ApplySlotScale(i, immediate: true);
        }
    }

    private void RefreshSlotScaleIfChanged(int previousSlot, int currentSlot)
    {
        if (previousSlot == currentSlot)
        {
            return;
        }

        if (previousSlot >= 0)
        {
            ApplySlotScale(previousSlot, immediate: true);
        }
        else
        {
            for (int i = 0; i < _skillButtons.Length; i++)
            {
                if (i != currentSlot)
                {
                    ApplySlotScale(i, immediate: true);
                }
            }
        }

        ApplySlotScale(currentSlot, immediate: false);
    }

    private void RefreshSlotVisual(int slotIndex)
    {
        Image frame = GetFrameImage(slotIndex);
        if (frame == null)
        {
            return;
        }

        frame.color = Color.white;
        Sprite sprite = ResolveSprite(slotIndex);
        if (sprite != null)
        {
            frame.sprite = sprite;
        }
    }

    private Sprite ResolveSprite(int slotIndex)
    {
        if (IsSlotBackButton(slotIndex))
        {
            return _backSprite;
        }

        bool isSelected = _selectedSlot == slotIndex;
        return slotIndex switch
        {
            AttackSlotIndex => isSelected ? _attackSelectedSprite : _attackNormalSprite,
            HealSlotIndex => isSelected ? _healSelectedSprite : _healNormalSprite,
            _ => null
        };
    }

    private Image GetFrameImage(int slotIndex)
    {
        if (_slotVisuals != null && slotIndex >= 0 && slotIndex < _slotVisuals.Length && _slotVisuals[slotIndex] != null)
        {
            return _slotVisuals[slotIndex].FrameImage;
        }

        Button button = _skillButtons != null && slotIndex >= 0 && slotIndex < _skillButtons.Length
            ? _skillButtons[slotIndex]
            : null;
        return button != null ? button.targetGraphic as Image : null;
    }

    private void ApplySlotScale(int slotIndex, bool immediate)
    {
        if (_skillButtons == null || slotIndex < 0 || slotIndex >= _skillButtons.Length)
        {
            return;
        }

        Button button = _skillButtons[slotIndex];
        Transform scaleTarget = _scaleTargets != null && slotIndex < _scaleTargets.Length
            ? _scaleTargets[slotIndex]
            : null;
        if (button == null || scaleTarget == null)
        {
            return;
        }

        if (_buttonRootRestScales != null && slotIndex < _buttonRootRestScales.Length)
        {
            button.transform.localScale = _buttonRootRestScales[slotIndex];
        }

        float scaleMultiplier = _selectedSlot == slotIndex ? _selectedScale : _normalScale;
        Vector3 scaleVector = Vector3.one * scaleMultiplier;
        DOTween.Kill(scaleTarget, ScaleTweenId);

        if (immediate || _scaleTweenDuration <= 0f)
        {
            scaleTarget.localScale = scaleVector;
            return;
        }

        if ((scaleTarget.localScale - scaleVector).sqrMagnitude < 0.0001f)
        {
            scaleTarget.localScale = scaleVector;
            return;
        }

        scaleTarget
            .DOScale(scaleVector, _scaleTweenDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .SetId(ScaleTweenId);
    }

    private void KillScaleTweens()
    {
        if (_scaleTargets == null)
        {
            return;
        }

        for (int i = 0; i < _scaleTargets.Length; i++)
        {
            if (_scaleTargets[i] != null)
            {
                DOTween.Kill(_scaleTargets[i], ScaleTweenId);
            }
        }
    }
}
