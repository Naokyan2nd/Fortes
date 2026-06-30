using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ノートストック UI のアイコン 1 件（プール用）。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class NoteStockIconView : MonoBehaviour
{
    [SerializeField]
    private RectTransform _rectTransform;

    [SerializeField]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    private Image _iconImage;

    private Tween _moveTween;

    /// <summary>判定位置への移動などに使う RectTransform。</summary>
    public RectTransform IconRect
    {
        get
        {
            if (_rectTransform == null)
            {
                _rectTransform = transform as RectTransform;
            }

            return _rectTransform;
        }
    }

    private void Awake()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
        }

        if (_iconImage == null)
        {
            _iconImage = GetComponent<Image>();
        }
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    /// <summary>スプライトを差し替える。</summary>
    public void ApplySprite(Sprite sprite)
    {
        if (_iconImage == null)
        {
            _iconImage = GetComponent<Image>();
        }

        if (_iconImage != null && sprite != null)
        {
            _iconImage.sprite = sprite;
            _iconImage.enabled = true;
        }
    }

    /// <summary>プール返却前のリセット。</summary>
    public void ResetForPool()
    {
        KillTweens();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }

        RectTransform rt = IconRect;
        if (rt != null)
        {
            rt.localScale = Vector3.one;
        }

        gameObject.SetActive(false);
    }

    /// <summary>ワールド座標へ移動する Tween を開始する。</summary>
    public Tween PlayMoveToWorld(Vector3 worldTarget, float duration, Ease ease)
    {
        KillTweens();
        gameObject.SetActive(true);

        _moveTween = IconRect
            .DOMove(worldTarget, duration)
            .SetEase(ease)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(true);

        _moveTween.OnKill(() => _moveTween = null);
        return _moveTween;
    }

    /// <summary>進行中の Tween を停止する。</summary>
    public void KillTweens()
    {
        if (_moveTween != null && _moveTween.IsActive())
        {
            _moveTween.Kill(false);
        }

        _moveTween = null;

        RectTransform rt = IconRect;
        if (rt != null)
        {
            rt.DOKill(false);
        }
    }
}
