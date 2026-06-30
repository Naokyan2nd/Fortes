using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スクロール中の音符 1 個（プール用）。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public sealed class QteTaikoNoteView : MonoBehaviour
{
    [SerializeField]
    private RectTransform _rectTransform;

    [SerializeField]
    private CanvasGroup _canvasGroup;

    [SerializeField]
    private Image _noteImage;

    private Tweener _fadeTween;

    private void Awake()
    {
        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_noteImage == null)
        {
            _noteImage = GetComponent<Image>();
        }
    }

    private void OnDestroy()
    {
        KillFade();
    }

    /// <summary>プールに戻す（画面外へ退避してから非表示）。</summary>
    public void ResetForPool(float offScreenX, float laneY)
    {
        KillFade();
        ResetVisualAlpha();

        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
        }

        _rectTransform.anchoredPosition = new Vector2(offScreenX, laneY);
        gameObject.SetActive(false);
    }

    /// <summary>音符の幅（ローカル）。</summary>
    public float NoteWidth
    {
        get
        {
            RectTransform rt = _rectTransform != null ? _rectTransform : transform as RectTransform;
            return rt != null ? rt.rect.width : 0f;
        }
    }

    /// <summary>画面外に配置してから表示する。</summary>
    public void ActivateAt(float anchoredX, float anchoredY)
    {
        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
        }

        KillFade();
        ResetVisualAlpha();
        gameObject.SetActive(false);
        _rectTransform.anchoredPosition = new Vector2(anchoredX, anchoredY);
        gameObject.SetActive(true);
    }

    /// <summary>音符スプライトを差し替える。</summary>
    public void ApplySprite(Sprite sprite)
    {
        if (_noteImage == null)
        {
            _noteImage = GetComponent<Image>();
        }

        if (_noteImage != null && sprite != null)
        {
            _noteImage.sprite = sprite;
        }
    }

    /// <summary>Good 帯通過 Miss 時のフェードアウト。</summary>
    public void PlayPassMissFadeOut(float duration, Action onComplete)
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        KillFade();
        _canvasGroup.alpha = 1f;

        if (duration <= 0f)
        {
            _canvasGroup.alpha = 0f;
            onComplete?.Invoke();
            return;
        }

        _fadeTween = _canvasGroup
            .DOFade(0f, duration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                _fadeTween = null;
                onComplete?.Invoke();
            })
            .OnKill(() => _fadeTween = null);
    }

    /// <summary>進行中のフェードを停止する。</summary>
    public void KillFade()
    {
        if (_fadeTween != null && _fadeTween.IsActive())
        {
            _fadeTween.Kill(false);
        }

        _fadeTween = null;
    }

    /// <summary>位置のみ更新。</summary>
    public void SetAnchoredPosition(float anchoredX, float anchoredY)
    {
        _rectTransform.anchoredPosition = new Vector2(anchoredX, anchoredY);
    }

    /// <summary>判定に使う RectTransform。</summary>
    public RectTransform RectTransform
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

    /// <summary>ノート中心の anchoredPosition（親ローカル）。</summary>
    public Vector2 GetCenterAnchoredPosition()
    {
        RectTransform rt = RectTransform;
        return rt.anchoredPosition + new Vector2(GetCenterAnchoredOffsetX(), 0f);
    }

    /// <summary>rect.center と anchoredPosition（pivot）の X 差（親ローカル）。</summary>
    public float GetCenterAnchoredOffsetX()
    {
        RectTransform rt = RectTransform;
        RectTransform parent = rt.parent as RectTransform;
        if (parent == null)
        {
            return 0f;
        }

        Camera cam = null;
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            cam = canvas.worldCamera;
        }

        Vector3 centerWorld = rt.TransformPoint(rt.rect.center);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                RectTransformUtility.WorldToScreenPoint(cam, centerWorld),
                cam,
                out Vector2 centerLocal))
        {
            return 0f;
        }

        return centerLocal.x - rt.anchoredPosition.x;
    }

    private void ResetVisualAlpha()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
    }
}
