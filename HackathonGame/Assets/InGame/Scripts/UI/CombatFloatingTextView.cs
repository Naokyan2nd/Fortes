using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 戦闘 FloatingText 1 件（プール用・DOTween 演出）。
/// </summary>
public sealed class CombatFloatingTextView : MonoBehaviour
{
    [SerializeField]
    private RectTransform _rectTransform;

    [SerializeField]
    private TMP_Text _faceLabel;

    [SerializeField]
    private TMP_Text _outlineLabel;

    [Header("DOTween motion (screen / canvas local units)")]
    [SerializeField]
    private float _jumpHeightMin = 60f;

    [SerializeField]
    private float _jumpHeightMax = 110f;

    [SerializeField]
    private float _driftXRange = 28f;

    [SerializeField]
    private float _fallDistance = 50f;

    [Header("DOTween motion (world space units)")]
    [SerializeField]
    private float _worldJumpHeightMin = 0.6f;

    [SerializeField]
    private float _worldJumpHeightMax = 1.1f;

    [SerializeField]
    private float _worldDriftXRange = 0.28f;

    [SerializeField]
    private float _worldFallDistance = 0.5f;

    [SerializeField]
    private float _riseDuration = 0.22f;

    [SerializeField]
    private float _fallDuration = 0.48f;

    [SerializeField]
    private Ease _riseEase = Ease.OutQuad;

    [SerializeField]
    private Ease _fallEase = Ease.InQuad;

    [Header("Optional scale pop")]
    [SerializeField]
    private bool _useScalePop = true;

    [SerializeField]
    private float _scalePopFrom = 0.75f;

    [SerializeField]
    private float _scalePopDuration = 0.12f;

    private Sequence _activeSequence;
    private Action<CombatFloatingTextView> _onRequestReturn;

    /// <summary>RectTransform（プール配置用）。</summary>
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

    private void Awake()
    {
        if (_rectTransform == null)
        {
            _rectTransform = transform as RectTransform;
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
        _onRequestReturn = null;

        if (_rectTransform != null)
        {
            _rectTransform.localScale = Vector3.one;
        }

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Screen Space 用。Face / Outline に同じテキストを設定し anchoredPosition で演出する。
    /// </summary>
    public void PlayShow(
        string text,
        Vector2 startAnchoredPosition,
        Action<CombatFloatingTextView> onRequestReturn)
    {
        KillTweens();
        _onRequestReturn = onRequestReturn;

        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = startAnchoredPosition;
            _rectTransform.localScale = Vector3.one;
        }

        SetLabelText(text);
        gameObject.SetActive(true);

        if (_rectTransform == null)
        {
            CompleteAndNotify();
            return;
        }

        float jumpMin = Mathf.Min(_jumpHeightMin, _jumpHeightMax);
        float jumpMax = Mathf.Max(_jumpHeightMin, _jumpHeightMax);
        float peakY = startAnchoredPosition.y + UnityEngine.Random.Range(jumpMin, jumpMax);
        float endX = _driftXRange > 0f
            ? startAnchoredPosition.x + UnityEngine.Random.Range(-_driftXRange, _driftXRange)
            : startAnchoredPosition.x;
        Vector2 peakPos = new Vector2(
            Mathf.Lerp(startAnchoredPosition.x, endX, 0.35f),
            peakY);
        Vector2 endPos = new Vector2(endX, startAnchoredPosition.y - _fallDistance);

        _activeSequence = DOTween.Sequence();
        _activeSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        AppendScalePop(_activeSequence);
        _activeSequence.Append(
            _rectTransform.DOAnchorPos(peakPos, _riseDuration)
                .SetEase(_riseEase)
                .SetUpdate(true));
        _activeSequence.Append(
            _rectTransform.DOAnchorPos(endPos, _fallDuration)
                .SetEase(_fallEase)
                .SetUpdate(true));
        _activeSequence.OnComplete(CompleteAndNotify);
        _activeSequence.OnKill(() => _activeSequence = null);
    }

    /// <summary>
    /// World Space 用。ワールド座標に配置し、カメラ移動後もワールド上に残る。
    /// </summary>
    public void PlayShowWorld(
        string text,
        Vector3 startWorldPosition,
        Action<CombatFloatingTextView> onRequestReturn)
    {
        KillTweens();
        _onRequestReturn = onRequestReturn;

        if (_rectTransform != null)
        {
            _rectTransform.position = startWorldPosition;
            _rectTransform.localScale = Vector3.one;
        }

        SetLabelText(text);
        gameObject.SetActive(true);

        if (_rectTransform == null)
        {
            CompleteAndNotify();
            return;
        }

        float jumpMin = Mathf.Min(_worldJumpHeightMin, _worldJumpHeightMax);
        float jumpMax = Mathf.Max(_worldJumpHeightMin, _worldJumpHeightMax);
        float peakY = startWorldPosition.y + UnityEngine.Random.Range(jumpMin, jumpMax);
        float endX = _worldDriftXRange > 0f
            ? startWorldPosition.x + UnityEngine.Random.Range(-_worldDriftXRange, _worldDriftXRange)
            : startWorldPosition.x;
        Vector3 peakPos = new Vector3(
            Mathf.Lerp(startWorldPosition.x, endX, 0.35f),
            peakY,
            startWorldPosition.z);
        Vector3 endPos = new Vector3(endX, startWorldPosition.y - _worldFallDistance, startWorldPosition.z);

        _activeSequence = DOTween.Sequence();
        _activeSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        AppendScalePop(_activeSequence);
        _activeSequence.Append(
            _rectTransform.DOMove(peakPos, _riseDuration)
                .SetEase(_riseEase)
                .SetUpdate(true));
        _activeSequence.Append(
            _rectTransform.DOMove(endPos, _fallDuration)
                .SetEase(_fallEase)
                .SetUpdate(true));
        _activeSequence.OnComplete(CompleteAndNotify);
        _activeSequence.OnKill(() => _activeSequence = null);
    }

    /// <summary>演出を打ち切ってプール返却を依頼する（任意）。</summary>
    public void NotifyShowComplete()
    {
        CompleteAndNotify();
    }

    private void AppendScalePop(Sequence sequence)
    {
        if (!_useScalePop || _rectTransform == null)
        {
            return;
        }

        _rectTransform.localScale = Vector3.one * _scalePopFrom;
        sequence.Append(
            _rectTransform.DOScale(1f, _scalePopDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true));
    }

    private void SetLabelText(string text)
    {
        if (_faceLabel != null)
        {
            _faceLabel.text = text;
        }

        if (_outlineLabel != null)
        {
            _outlineLabel.text = text;
        }
    }

    private void CompleteAndNotify()
    {
        KillTweens();
        Action<CombatFloatingTextView> callback = _onRequestReturn;
        _onRequestReturn = null;
        callback?.Invoke(this);
    }

    private void KillTweens()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill(false);
        }

        _activeSequence = null;

        if (_rectTransform != null)
        {
            _rectTransform.DOKill(false);
        }
    }
}
