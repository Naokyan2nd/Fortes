using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// QTE 判定結果（Perfect / Miss）を画面上部スロットへストックする。
/// Good は格納しない。スロット数は QTE 開始時に可変で構築する。
/// </summary>
public sealed class NoteStockUIManager : MonoBehaviour
{
    private sealed class FlyingIcon
    {
        public NoteStockIconView Icon;
        public NoteStockSlotView TargetSlot;
        public bool IsMiss;
    }

    [SerializeField]
    private QteTaikoSettingsSO _settings;

    [SerializeField]
    private RectTransform _slotContainer;

    [SerializeField]
    private RectTransform _iconFlyRoot;

    [SerializeField]
    private NoteStockSlotView _slotPrefab;

    [SerializeField]
    private NoteStockIconView _perfectIconPrefab;

    [SerializeField]
    private NoteStockIconView _missIconPrefab;

    [Header("Fly")]
    [SerializeField]
    private bool _useFlyAnimation = true;

    [SerializeField]
    private float _flyDuration = 0.35f;

    [SerializeField]
    private Ease _flyEase = Ease.OutCubic;

    [SerializeField]
    private int _poolPrewarmCount = 8;

    private readonly List<NoteStockSlotView> _slots = new List<NoteStockSlotView>();
    private readonly Stack<NoteStockIconView> _perfectPool = new Stack<NoteStockIconView>();
    private readonly Stack<NoteStockIconView> _missPool = new Stack<NoteStockIconView>();
    private readonly List<FlyingIcon> _flying = new List<FlyingIcon>();
    private int _filledCount;
    private Transform _slotContainerHomeParent;
    private int _slotContainerHomeSiblingIndex;
    private HorizontalLayoutGroup _slotLayoutGroup;
    private ContentSizeFitter _slotContentSizeFitter;
    private bool _slotLayoutGroupWasEnabled;
    private bool _slotContentSizeFitterWasEnabled;
    private bool _gatherLayoutSuspended;

    private void Awake()
    {
        CaptureSlotContainerHome();
        PrewarmPool(_perfectIconPrefab, _perfectPool);
        PrewarmPool(_missIconPrefab, _missPool);
    }

    private void OnDestroy()
    {
        EndSession();
    }

    /// <summary>QTE 開始時。スキルのノート数に合わせて枠を構築する。</summary>
    public void BeginSession(int slotCount)
    {
        RestoreAfterSummary();
        EndSession();

        if (_slotContainer == null || _slotPrefab == null || slotCount <= 0)
        {
            return;
        }

        for (int i = 0; i < slotCount; i++)
        {
            NoteStockSlotView slot = Instantiate(_slotPrefab, _slotContainer);
            slot.name = $"NoteStockSlot_{i}";
            slot.ClearIcon();
            _slots.Add(slot);
        }

        _filledCount = 0;
    }

    /// <summary>QTE 終了時。枠とプールをクリアする。</summary>
    public void EndSession()
    {
        RestoreAfterSummary();

        for (int i = _flying.Count - 1; i >= 0; i--)
        {
            FlyingIcon entry = _flying[i];
            if (entry.Icon != null)
            {
                entry.Icon.KillTweens();
                ReturnIcon(entry.Icon, entry.IsMiss);
            }
        }

        _flying.Clear();

        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i] != null)
            {
                Destroy(_slots[i].gameObject);
            }
        }

        _slots.Clear();
        _filledCount = 0;
        ReturnAllIconsToPool(_perfectPool);
        ReturnAllIconsToPool(_missPool);
    }

    /// <summary>スロット列の RectTransform（サマリー集約でコンテナごと動かす）。</summary>
    public RectTransform SlotContainer => _slotContainer;

    /// <summary>ストック済みスロット（左から順、_filledCount 個）。</summary>
    public IReadOnlyList<NoteStockSlotView> GetFilledSlots()
    {
        if (_filledCount <= 0)
        {
            return System.Array.Empty<NoteStockSlotView>();
        }

        return _slots.GetRange(0, _filledCount);
    }

    /// <summary>サマリー用にスロット列をオーバーレイへ移し、飛行中アイコンを停止する。</summary>
    public void PrepareForSummary(RectTransform summaryOverlay)
    {
        CancelAllFlying();

        if (summaryOverlay == null || _slotContainer == null)
        {
            return;
        }

        CaptureSlotContainerHome();
        _slotContainer.SetParent(summaryOverlay, true);
    }

    /// <summary>サマリー集約前：レイアウト停止・未使用枠非表示・アイコン再スナップ。</summary>
    public void PrepareGatherPresentation(IReadOnlyList<NoteStockSlotView> filledSlots)
    {
        SuspendSlotContainerLayout();

        var filledSet = new HashSet<NoteStockSlotView>();
        if (filledSlots != null)
        {
            for (int i = 0; i < filledSlots.Count; i++)
            {
                NoteStockSlotView slot = filledSlots[i];
                if (slot != null)
                {
                    filledSet.Add(slot);
                }
            }
        }

        for (int i = 0; i < _slots.Count; i++)
        {
            NoteStockSlotView slot = _slots[i];
            if (slot == null)
            {
                continue;
            }

            bool isFilled = filledSet.Contains(slot);
            slot.gameObject.SetActive(isFilled);
            if (!isFilled)
            {
                continue;
            }

            slot.SetSummaryFrameVisible(false);
            slot.SnapIconToAnchor();
        }
    }

    /// <summary>サマリー演出後にスロット列・各枠を NoteStockBar 上へ戻す。</summary>
    public void RestoreAfterSummary()
    {
        CancelAllFlying();
        ResumeSlotContainerLayout();

        for (int i = 0; i < _slots.Count; i++)
        {
            NoteStockSlotView slot = _slots[i];
            if (slot == null)
            {
                continue;
            }

            slot.gameObject.SetActive(true);
            slot.ResetAfterSummary();
            RectTransform slotRt = slot.RectTransform;
            if (slotRt != null && _slotContainer != null && slotRt.parent != _slotContainer)
            {
                slotRt.SetParent(_slotContainer, false);
            }
        }

        RestoreSlotContainerHome();
    }

    /// <summary>進行中の飛行アイコンをスロットへ固定する。</summary>
    public void CancelAllFlying()
    {
        for (int i = _flying.Count - 1; i >= 0; i--)
        {
            FlyingIcon entry = _flying[i];
            if (entry.Icon != null && entry.TargetSlot != null)
            {
                entry.Icon.KillTweens();
                entry.TargetSlot.SetIcon(entry.Icon.IconRect);
                ReturnIcon(entry.Icon, entry.IsMiss);
            }
        }

        _flying.Clear();
    }

    /// <summary>判定確定時。Perfect / Miss のみ左から空き枠へ格納する。</summary>
    public void OnJudgmentResolved(QteJudgment judgment, Vector3 worldStart, SkillCategory category)
    {
        if (judgment == QteJudgment.Good)
        {
            return;
        }

        if (_filledCount >= _slots.Count)
        {
            Debug.LogWarning("[NoteStockUIManager] スロットが満杯です。", this);
            return;
        }

        bool isMiss = judgment == QteJudgment.Miss;
        NoteStockIconView icon = RentIcon(isMiss);
        if (icon == null)
        {
            return;
        }

        Sprite sprite = isMiss ? GetMissSprite() : _settings?.GetNoteSprite(category);
        icon.ApplySprite(sprite);

        NoteStockSlotView slot = _slots[_filledCount];
        slot.SetStocked(judgment);
        _filledCount++;

        if (_useFlyAnimation && _iconFlyRoot != null)
        {
            PlaceIconForFly(icon, worldStart);
            FlyingIcon entry = new FlyingIcon
            {
                Icon = icon,
                TargetSlot = slot,
                IsMiss = isMiss,
            };
            _flying.Add(entry);

            Tween tween = icon.PlayMoveToWorld(slot.IconAnchor.position, _flyDuration, _flyEase);
            tween.OnComplete(() => CompleteFly(entry));
            tween.OnKill(() => RemoveFlying(entry));
        }
        else
        {
            slot.SetIcon(icon.IconRect);
        }
    }

    private void CompleteFly(FlyingIcon entry)
    {
        if (!_flying.Remove(entry) || entry.Icon == null || entry.TargetSlot == null)
        {
            return;
        }

        entry.TargetSlot.SetIcon(entry.Icon.IconRect);
    }

    private void RemoveFlying(FlyingIcon entry)
    {
        _flying.Remove(entry);
    }

    private void CaptureSlotContainerHome()
    {
        if (_slotContainer == null)
        {
            return;
        }

        if (_slotContainerHomeParent == null)
        {
            _slotContainerHomeParent = _slotContainer.parent;
            _slotContainerHomeSiblingIndex = _slotContainer.GetSiblingIndex();
        }
    }

    private void RestoreSlotContainerHome()
    {
        if (_slotContainer == null)
        {
            return;
        }

        CaptureSlotContainerHome();
        if (_slotContainerHomeParent == null)
        {
            return;
        }

        _slotContainer.DOKill(false);

        if (_slotContainer.parent != _slotContainerHomeParent)
        {
            _slotContainer.SetParent(_slotContainerHomeParent, false);
        }

        int maxIndex = _slotContainerHomeParent.childCount - 1;
        int siblingIndex = Mathf.Clamp(_slotContainerHomeSiblingIndex, 0, Mathf.Max(0, maxIndex));
        _slotContainer.SetSiblingIndex(siblingIndex);
        _slotContainer.localScale = Vector3.one;
        _slotContainer.localRotation = Quaternion.identity;

        if (_slotContainer.GetComponent<LayoutGroup>() != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_slotContainer);
        }
    }

    private void SuspendSlotContainerLayout()
    {
        if (_slotContainer == null || _gatherLayoutSuspended)
        {
            return;
        }

        if (_slotLayoutGroup == null)
        {
            _slotLayoutGroup = _slotContainer.GetComponent<HorizontalLayoutGroup>();
        }

        if (_slotContentSizeFitter == null)
        {
            _slotContentSizeFitter = _slotContainer.GetComponent<ContentSizeFitter>();
        }

        if (_slotLayoutGroup != null)
        {
            _slotLayoutGroupWasEnabled = _slotLayoutGroup.enabled;
            _slotLayoutGroup.enabled = false;
        }

        if (_slotContentSizeFitter != null)
        {
            _slotContentSizeFitterWasEnabled = _slotContentSizeFitter.enabled;
            _slotContentSizeFitter.enabled = false;
        }

        _gatherLayoutSuspended = true;
    }

    private void ResumeSlotContainerLayout()
    {
        if (!_gatherLayoutSuspended)
        {
            return;
        }

        if (_slotLayoutGroup != null)
        {
            _slotLayoutGroup.enabled = _slotLayoutGroupWasEnabled;
        }

        if (_slotContentSizeFitter != null)
        {
            _slotContentSizeFitter.enabled = _slotContentSizeFitterWasEnabled;
        }

        _gatherLayoutSuspended = false;

        if (_slotContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_slotContainer);
        }
    }

    private void PlaceIconForFly(NoteStockIconView icon, Vector3 worldStart)
    {
        RectTransform rt = icon.IconRect;
        rt.SetParent(_iconFlyRoot, true);
        rt.position = worldStart;
        rt.localScale = Vector3.one;
        icon.gameObject.SetActive(true);
    }

    private Sprite GetMissSprite()
    {
        if (_settings != null)
        {
            Sprite sprite = _settings.GetMissStockSprite();
            if (sprite != null)
            {
                return sprite;
            }
        }

        if (_missIconPrefab != null)
        {
            Image image = _missIconPrefab.GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                return image.sprite;
            }
        }

        return null;
    }

    private void PrewarmPool(NoteStockIconView prefab, Stack<NoteStockIconView> pool)
    {
        if (prefab == null || _iconFlyRoot == null || _poolPrewarmCount <= 0)
        {
            return;
        }

        for (int i = pool.Count; i < _poolPrewarmCount; i++)
        {
            NoteStockIconView instance = Instantiate(prefab, _iconFlyRoot);
            instance.ResetForPool();
            pool.Push(instance);
        }
    }

    private NoteStockIconView RentIcon(bool isMiss)
    {
        NoteStockIconView prefab = isMiss ? _missIconPrefab : _perfectIconPrefab;
        Stack<NoteStockIconView> pool = isMiss ? _missPool : _perfectPool;
        if (prefab == null || _iconFlyRoot == null)
        {
            return null;
        }

        if (pool.Count > 0)
        {
            NoteStockIconView rented = pool.Pop();
            rented.gameObject.SetActive(true);
            return rented;
        }

        NoteStockIconView created = Instantiate(prefab, _iconFlyRoot);
        created.ResetForPool();
        created.gameObject.SetActive(true);
        return created;
    }

    private void ReturnIcon(NoteStockIconView icon, bool isMiss)
    {
        if (icon == null)
        {
            return;
        }

        icon.ResetForPool();
        Stack<NoteStockIconView> pool = isMiss ? _missPool : _perfectPool;
        pool.Push(icon);
    }

    private static void ReturnAllIconsToPool(Stack<NoteStockIconView> pool)
    {
        while (pool.Count > 0)
        {
            NoteStockIconView icon = pool.Pop();
            if (icon != null)
            {
                icon.KillTweens();
                if (icon.gameObject != null)
                {
                    Destroy(icon.gameObject);
                }
            }
        }
    }
}
