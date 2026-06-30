using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ノートストック UI の空枠 1 つ（アイコン格納先）。
/// </summary>
public sealed class NoteStockSlotView : MonoBehaviour
{
    [SerializeField]
    private RectTransform _iconAnchor;

    [SerializeField]
    private CanvasGroup _slotCanvasGroup;

    [SerializeField]
    private Image _frameImage;

    private const float DefaultGatherWidth = 72f;

    private QteJudgment _stockedJudgment = QteJudgment.Good;
    private bool _isCanceled;

    /// <summary>ストックされた判定（Good はストックされない）。</summary>
    public QteJudgment StockedJudgment => _stockedJudgment;

    public bool IsCanceled => _isCanceled;

    public bool IsMiss => _stockedJudgment == QteJudgment.Miss;

    public bool IsPerfect => _stockedJudgment == QteJudgment.Perfect;

    /// <summary>アイコンを配置するアンカー。</summary>
    public RectTransform IconAnchor
    {
        get
        {
            if (_iconAnchor == null)
            {
                _iconAnchor = transform as RectTransform;
            }

            return _iconAnchor;
        }
    }

    public RectTransform RectTransform => transform as RectTransform;

    /// <summary>スロットの CanvasGroup（未設定時は null）。</summary>
    public CanvasGroup SlotCanvasGroup => _slotCanvasGroup;

    private void Awake()
    {
        if (_slotCanvasGroup == null)
        {
            _slotCanvasGroup = GetComponent<CanvasGroup>();
        }

        if (_frameImage == null)
        {
            _frameImage = GetComponent<Image>();
        }
    }

    /// <summary>集約レイアウト用の幅（スケール適用後の見かけ幅）。</summary>
    public float GetGatherLayoutWidth(float targetScale)
    {
        RectTransform rt = RectTransform;
        if (rt == null)
        {
            return DefaultGatherWidth * targetScale;
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        float width = LayoutUtility.GetPreferredWidth(rt);
        if (width <= 1f)
        {
            width = rt.rect.width;
        }

        if (width <= 1f)
        {
            width = DefaultGatherWidth;
        }

        return width * targetScale;
    }

    /// <summary>サマリー中は半透明枠を隠し、アイコンのみ見せる。</summary>
    public void SetSummaryFrameVisible(bool visible)
    {
        if (_frameImage == null)
        {
            _frameImage = GetComponent<Image>();
        }

        if (_frameImage != null)
        {
            _frameImage.enabled = visible;
        }
    }

    /// <summary>判定を記録する（アイコン格納前）。</summary>
    public void SetStocked(QteJudgment judgment)
    {
        _stockedJudgment = judgment;
        _isCanceled = false;
        if (_slotCanvasGroup != null)
        {
            _slotCanvasGroup.alpha = 1f;
        }
    }

    /// <summary>Miss による打ち消し演出。</summary>
    public void PlayCanceledByMiss(float duration)
    {
        _isCanceled = true;
        if (_slotCanvasGroup == null)
        {
            return;
        }

        _slotCanvasGroup.DOKill(false);
        _slotCanvasGroup.DOFade(0.25f, duration).SetUpdate(true);
        RectTransform rt = RectTransform;
        if (rt != null)
        {
            rt.DOKill(false);
            rt.DOScale(0.6f, duration).SetEase(Ease.InQuad).SetUpdate(true);
        }
    }

    /// <summary>ストックアイコンを枠内に固定する。</summary>
    public void SetIcon(RectTransform icon)
    {
        if (icon == null)
        {
            return;
        }

        RectTransform anchor = IconAnchor;
        icon.SetParent(anchor, false);
        icon.localPosition = Vector3.zero;
        icon.localRotation = Quaternion.identity;
        icon.localScale = Vector3.one;
        icon.gameObject.SetActive(true);
    }

    /// <summary>IconAnchor 配下のアイコンを中央に再スナップする。</summary>
    public void SnapIconToAnchor()
    {
        RectTransform anchor = IconAnchor;
        for (int i = 0; i < anchor.childCount; i++)
        {
            Transform child = anchor.GetChild(i);
            if (child == null)
            {
                continue;
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
        }
    }

    /// <summary>枠内のアイコンをすべて破棄する。</summary>
    public void ClearIcon()
    {
        RectTransform anchor = IconAnchor;
        for (int i = anchor.childCount - 1; i >= 0; i--)
        {
            Destroy(anchor.GetChild(i).gameObject);
        }
    }

    /// <summary>サマリー演出後に見た目・親を初期状態へ戻す。</summary>
    public void ResetAfterSummary()
    {
        if (_slotCanvasGroup != null)
        {
            _slotCanvasGroup.DOKill(false);
            _slotCanvasGroup.alpha = 1f;
        }

        RectTransform rt = RectTransform;
        if (rt != null)
        {
            rt.DOKill(false);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        _isCanceled = false;
        SetSummaryFrameVisible(true);
    }
}
