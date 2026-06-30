using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// QTE 吸収トレイル用キラキラ（Sparkle）のプール要素。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class QteAbsorbSparkleView : MonoBehaviour
{
    [SerializeField]
    private RectTransform _rectTransform;

    [SerializeField]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    private Image _sparkleImage;

    private Sequence _playSequence;

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

        if (_sparkleImage == null)
        {
            _sparkleImage = GetComponent<Image>();
        }
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    /// <summary>指定位置で縮小フェードを再生する。</summary>
    public void PlayAt(
        RectTransform parent,
        Vector2 anchoredPosition,
        float fadeDuration,
        float startScale,
        float endScale,
        Ease scaleEase,
        Color tint)
    {
        KillTweens();

        RectTransform rt = IconRect;
        if (parent != null && rt.parent != parent)
        {
            rt.SetParent(parent, false);
        }

        gameObject.SetActive(true);
        rt.SetAsLastSibling();
        rt.anchoredPosition = anchoredPosition;
        rt.localRotation = Quaternion.identity;

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }

        ApplyTint(tint);

        float clampedDuration = Mathf.Max(fadeDuration, 0.01f);
        Vector3 endScaleVec = Vector3.one * endScale;
        rt.localScale = Vector3.one * startScale;

        _playSequence = DOTween.Sequence();
        _playSequence.SetUpdate(true);
        _playSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _playSequence.Append(rt.DOScale(endScaleVec, clampedDuration).SetEase(scaleEase));

        if (_canvasGroup != null)
        {
            _playSequence.Join(_canvasGroup.DOFade(0f, clampedDuration).SetEase(Ease.Linear));
        }

        _playSequence.OnComplete(ResetForPool);
        _playSequence.OnKill(() => _playSequence = null);
    }

    /// <summary>指定位置から外向きへ移動しながら縮小フェードを再生する。</summary>
    public void PlayBurstAt(
        RectTransform parent,
        Vector2 anchoredStart,
        Vector2 anchoredEnd,
        float duration,
        float startScale,
        float endScale,
        Ease moveEase,
        Ease scaleEase,
        Color tint)
    {
        KillTweens();

        RectTransform rt = IconRect;
        if (parent != null && rt.parent != parent)
        {
            rt.SetParent(parent, false);
        }

        gameObject.SetActive(true);
        rt.SetAsLastSibling();
        rt.anchoredPosition = anchoredStart;
        rt.localRotation = Quaternion.identity;

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }

        ApplyTint(tint);

        float clampedDuration = Mathf.Max(duration, 0.01f);
        Vector3 endScaleVec = Vector3.one * endScale;
        rt.localScale = Vector3.one * startScale;

        _playSequence = DOTween.Sequence();
        _playSequence.SetUpdate(true);
        _playSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _playSequence.Append(rt.DOAnchorPos(anchoredEnd, clampedDuration).SetEase(moveEase));
        _playSequence.Join(rt.DOScale(endScaleVec, clampedDuration).SetEase(scaleEase));

        if (_canvasGroup != null)
        {
            _playSequence.Join(_canvasGroup.DOFade(0f, clampedDuration).SetEase(Ease.Linear));
        }

        _playSequence.OnComplete(ResetForPool);
        _playSequence.OnKill(() => _playSequence = null);
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
            rt.localRotation = Quaternion.identity;
        }

        ApplyTint(Color.white);

        gameObject.SetActive(false);
    }

    private void ApplyTint(Color tint)
    {
        if (_sparkleImage == null)
        {
            _sparkleImage = GetComponent<Image>();
        }

        if (_sparkleImage != null)
        {
            _sparkleImage.color = tint;
        }
    }

    /// <summary>進行中の Tween を停止する。</summary>
    public void KillTweens()
    {
        if (_playSequence != null && _playSequence.IsActive())
        {
            _playSequence.Kill(complete: false);
        }

        _playSequence = null;

        RectTransform rt = IconRect;
        if (rt != null)
        {
            rt.DOKill(false);
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill(false);
        }
    }
}
