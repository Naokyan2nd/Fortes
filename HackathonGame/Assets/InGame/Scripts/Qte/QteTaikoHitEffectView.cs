using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 太鼓 QTE タップ時のヒットエフェクト 1 件（DOTween・プール用）。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class QteTaikoHitEffectView : MonoBehaviour
{
    [SerializeField]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    private RectTransform _rectTransform;

    [SerializeField]
    private Image _effectImage;

    [Header("DOTween")]
    [SerializeField]
    private float _duration = 0.2f;

    [SerializeField]
    private float _targetScaleMultiplier = 1.5f;

    [SerializeField]
    private Ease _scaleEase = Ease.OutQuad;

    [SerializeField]
    private Ease _fadeEase = Ease.Linear;

    private Sequence _activeSequence;

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

        if (_effectImage == null)
        {
            _effectImage = GetComponent<Image>();
        }
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    /// <summary>プール返却前のリセット。</summary>
    public void ResetForPool()
    {
        KillTweens();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }

        Transform scaleTarget = _rectTransform != null ? _rectTransform : transform;
        scaleTarget.localScale = Vector3.one;
        gameObject.SetActive(false);
    }

    /// <summary>ヒットエフェクトスプライトを差し替える。</summary>
    public void ApplySprite(Sprite sprite)
    {
        if (_effectImage == null)
        {
            _effectImage = GetComponent<Image>();
        }

        if (_effectImage != null && sprite != null)
        {
            _effectImage.sprite = sprite;
        }
    }

    /// <summary>
    /// スケールアップとフェードアウトを同時再生する。完了後に非表示にし onComplete を呼ぶ。
    /// </summary>
    public void PlayHitEffect(Action onComplete)
    {
        KillTweens();

        if (_canvasGroup == null)
        {
            onComplete?.Invoke();
            return;
        }

        Transform scaleTarget = _rectTransform != null ? _rectTransform : transform;
        Vector3 startScale = scaleTarget.localScale;
        Vector3 endScale = startScale * _targetScaleMultiplier;

        _canvasGroup.alpha = 1f;
        gameObject.SetActive(true);

        Tweener fadeTween = _canvasGroup.DOFade(0f, _duration).SetEase(_fadeEase);
        Tween scaleTween = scaleTarget.DOScale(endScale, _duration).SetEase(_scaleEase);

        _activeSequence = DOTween.Sequence();
        _activeSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _activeSequence.SetUpdate(true);
        // 空 Sequence への Join だけだと尺がずれて早く完了し、フェード途中で非表示になることがある。
        _activeSequence.Append(fadeTween);
        _activeSequence.Join(scaleTween);
        _activeSequence.OnComplete(() =>
        {
            _activeSequence = null;
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
            onComplete?.Invoke();
        });
        _activeSequence.OnKill(() => _activeSequence = null);
    }

    private void KillTweens()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill(false);
        }

        _activeSequence = null;

        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill(false);
        }

        if (_rectTransform != null)
        {
            _rectTransform.DOKill(false);
        }
        else
        {
            transform.DOKill(false);
        }
    }
}
