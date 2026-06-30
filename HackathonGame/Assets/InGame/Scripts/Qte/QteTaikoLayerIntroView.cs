using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// QTE 五線譜レイヤーの入退場（入場: VisualRoot Y スケール、退場: 下方向スライド + ブラックパネルフェード）。
/// </summary>
public sealed class QteTaikoLayerIntroView : MonoBehaviour
{
    [SerializeField]
    private RectTransform _visualRoot;

    [SerializeField]
    private CanvasGroup _blackPanel;

    [SerializeField]
    private QteTaikoView _qteTaikoView;

    [Header("Intro")]
    [SerializeField]
    private float _staffScaleFromY = 0f;

    [SerializeField]
    private float _staffScaleToY = 1f;

    [SerializeField]
    private float _staffScaleDuration = 0.22f;

    [SerializeField]
    private Ease _staffScaleEase = Ease.OutQuad;

    [SerializeField]
    private float _blackFadeInDuration = 0.15f;

    [SerializeField]
    private Ease _blackFadeInEase = Ease.Linear;

    [Header("Exit")]
    [SerializeField]
    private float _staffExitDuration = 0.28f;

    [SerializeField]
    private float _staffExitOffsetY = -1200f;

    [SerializeField]
    private Ease _staffExitEase = Ease.InQuad;

    [SerializeField]
    [FormerlySerializedAs("_outroDuration")]
    private float _blackFadeOutDuration = 0.2f;

    [SerializeField]
    [FormerlySerializedAs("_outroEase")]
    private Ease _blackFadeOutEase = Ease.InQuad;

    private Vector2 _visualRootRestAnchoredPosition;
    private bool _hasVisualRootRestPosition;

    private float _initialBlackAlpha = 1f;
    private bool _capturedInitialBlackAlpha;
    private Sequence _activeSequence;
    private bool _introComplete;

    /// <summary>入場アニメ完了後 true。</summary>
    public bool IsIntroComplete => _introComplete;

    /// <summary>ノーツ判定前に五線譜 Y スケールが 0 のまま残っていれば復帰する。</summary>
    public void EnsureStaffScaleForPlay()
    {
        if (_visualRoot == null)
        {
            return;
        }

        Vector3 scale = _visualRoot.localScale;
        if (scale.y < 0.01f)
        {
            scale.y = _staffScaleToY;
            _visualRoot.localScale = scale;
        }
    }

    /// <summary>タップ用ブラックパネル（サマリースキップの Raycast 先）。</summary>
    public GameObject BlackPanelObject => _blackPanel != null ? _blackPanel.gameObject : null;

    private void Awake()
    {
        CaptureInitialBlackAlpha();
        CaptureVisualRootRestPosition();
    }

    private void OnDisable()
    {
        KillAndReset();
    }

    /// <summary>表示直前のリセット（非表示状態）。</summary>
    public void PrepareForShow()
    {
        KillTweens();
        CaptureInitialBlackAlpha();
        EnsureVisualRootRestPosition();
        _introComplete = false;
        RestoreVisualRootPosition();

        if (_visualRoot != null)
        {
            Vector3 scale = _visualRoot.localScale;
            scale.y = _staffScaleFromY;
            _visualRoot.localScale = scale;
        }

        SetBlackAlpha(0f);
        SetBlackRaycast(false);
        _qteTaikoView?.SetInputEnabled(false);
    }

    /// <summary>サマリー五線譜退場後に、次の入場／退場用へレイアウトを戻す。</summary>
    public void RestoreVisualRootAfterSummary()
    {
        KillTweens();
        EnsureVisualRootRestPosition();
        RestoreVisualRootPosition();

        if (_visualRoot != null)
        {
            Vector3 scale = _visualRoot.localScale;
            scale.y = _staffScaleToY;
            _visualRoot.localScale = scale;
        }

        SetBlackRaycast(false);
        _qteTaikoView?.SetInputEnabled(false);
    }

    /// <summary>入場: Y スケール → ブラックフェードイン。</summary>
    public async UniTask PlayIntroAsync(CancellationToken token)
    {
        if (_visualRoot == null)
        {
            Debug.LogError("[QteTaikoLayerIntroView] _visualRoot が未設定です。", this);
            CompleteIntroWithoutAnimation();
            return;
        }

        KillTweens();
        _introComplete = false;
        _qteTaikoView?.SetInputEnabled(false);

        bool completed = false;
        _activeSequence = DOTween.Sequence();
        _activeSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _activeSequence.SetUpdate(true);

        _activeSequence.Append(
            _visualRoot
                .DOScaleY(_staffScaleToY, _staffScaleDuration)
                .SetEase(_staffScaleEase));

        if (_blackPanel != null && _blackFadeInDuration > 0f)
        {
            _activeSequence.Append(
                _blackPanel
                    .DOFade(_initialBlackAlpha, _blackFadeInDuration)
                    .SetEase(_blackFadeInEase));
        }
        else if (_blackPanel != null)
        {
            SetBlackAlpha(_initialBlackAlpha);
        }

        _activeSequence.OnComplete(() => completed = true);
        _activeSequence.OnKill(() => completed = true);

        await UniTask.WaitUntil(() => completed, cancellationToken: token);

        if (token.IsCancellationRequested)
        {
            return;
        }

        CompleteIntro();
    }

    /// <summary>サマリー用: 五線譜のみ画面下へ退場（ブラックパネルはそのまま）。</summary>
    public async UniTask PlayStaffExitAsync(CancellationToken token)
    {
        await PlaySlideExitAsync(fadeBlackPanel: false, token);
    }

    /// <summary>退場: 五線譜を下へスライド + ブラックフェードを同時再生。</summary>
    public async UniTask PlayOutroAsync(CancellationToken token)
    {
        await PlaySlideExitAsync(fadeBlackPanel: true, token);
    }

    private async UniTask PlaySlideExitAsync(bool fadeBlackPanel, CancellationToken token)
    {
        KillTweens();
        _introComplete = false;
        _qteTaikoView?.SetInputEnabled(false);
        SetBlackRaycast(false);

        if (_visualRoot == null)
        {
            if (fadeBlackPanel)
            {
                SetBlackAlpha(0f);
            }

            return;
        }

        EnsureVisualRootRestPosition();

        bool completed = false;
        _activeSequence = DOTween.Sequence();
        _activeSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _activeSequence.SetUpdate(true);

        Vector2 target = _visualRootRestAnchoredPosition + new Vector2(0f, _staffExitOffsetY);
        if (_staffExitDuration > 0f)
        {
            _activeSequence.Append(
                _visualRoot
                    .DOAnchorPos(target, _staffExitDuration)
                    .SetEase(_staffExitEase));

            if (fadeBlackPanel && _blackPanel != null && _blackFadeOutDuration > 0f)
            {
                _activeSequence.Join(
                    _blackPanel
                        .DOFade(0f, _blackFadeOutDuration)
                        .SetEase(_blackFadeOutEase));
            }
        }
        else
        {
            _visualRoot.anchoredPosition = target;
            if (fadeBlackPanel)
            {
                SetBlackAlpha(0f);
            }

            completed = true;
        }

        if (fadeBlackPanel && _blackPanel != null && _staffExitDuration > 0f && _blackFadeOutDuration <= 0f)
        {
            SetBlackAlpha(0f);
        }

        _activeSequence.OnComplete(() => completed = true);
        _activeSequence.OnKill(() => completed = true);

        await UniTask.WaitUntil(() => completed, cancellationToken: token);
    }

    /// <summary>入場完了まで待機。</summary>
    public async UniTask WaitUntilIntroCompleteAsync(CancellationToken token)
    {
        if (_introComplete)
        {
            return;
        }

        await UniTask.WaitUntil(() => _introComplete, cancellationToken: token);
    }

    /// <summary>Tween 停止と非表示用リセット。</summary>
    public void KillAndReset()
    {
        KillTweens();
        ResetToHiddenState();
    }

    /// <summary>非表示用の見た目リセット。</summary>
    public void ResetToHiddenState()
    {
        _introComplete = false;
        RestoreVisualRootPosition();

        if (_visualRoot != null)
        {
            Vector3 scale = _visualRoot.localScale;
            scale.y = _staffScaleFromY;
            _visualRoot.localScale = scale;
        }

        SetBlackAlpha(0f);
        SetBlackRaycast(false);
        _qteTaikoView?.SetInputEnabled(false);
    }

    private void RestoreVisualRootPosition()
    {
        if (_visualRoot == null)
        {
            return;
        }

        _visualRoot.anchoredPosition = _visualRootRestAnchoredPosition;
    }

    private void CaptureVisualRootRestPosition()
    {
        if (_visualRoot == null || _hasVisualRootRestPosition)
        {
            return;
        }

        _visualRootRestAnchoredPosition = _visualRoot.anchoredPosition;
        _hasVisualRootRestPosition = true;
    }

    private void EnsureVisualRootRestPosition()
    {
        if (!_hasVisualRootRestPosition)
        {
            CaptureVisualRootRestPosition();
        }
    }

    /// <summary>サマリー中のスキップ用タップ受付を有効化する。</summary>
    public void SetSummarySkipRaycast(bool enabled)
    {
        SetBlackRaycast(enabled);
    }

    private void CompleteIntro()
    {
        SetBlackAlpha(_initialBlackAlpha);
        SetBlackRaycast(true);
        _qteTaikoView?.SetInputEnabled(true);
        _introComplete = true;
    }

    private void CompleteIntroWithoutAnimation()
    {
        if (_visualRoot != null)
        {
            Vector3 scale = _visualRoot.localScale;
            scale.y = _staffScaleToY;
            _visualRoot.localScale = scale;
        }

        CompleteIntro();
    }

    private void CaptureInitialBlackAlpha()
    {
        if (_capturedInitialBlackAlpha || _blackPanel == null)
        {
            return;
        }

        if (_blackPanel.alpha > 0.001f)
        {
            _initialBlackAlpha = _blackPanel.alpha;
        }
        else
        {
            Image image = _blackPanel.GetComponent<Image>();
            _initialBlackAlpha = image != null ? image.color.a : 1f;
        }

        _capturedInitialBlackAlpha = true;
    }

    private void SetBlackAlpha(float alpha)
    {
        if (_blackPanel != null)
        {
            _blackPanel.alpha = alpha;
        }
    }

    private void SetBlackRaycast(bool blocks)
    {
        if (_blackPanel == null)
        {
            return;
        }

        _blackPanel.blocksRaycasts = blocks;
        _blackPanel.interactable = blocks;

        if (_blackPanel.TryGetComponent(out Image image))
        {
            image.raycastTarget = blocks;
        }
    }

    private void KillTweens()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill(false);
        }

        _activeSequence = null;

        if (_visualRoot != null)
        {
            _visualRoot.DOKill(false);
        }

        if (_blackPanel != null)
        {
            _blackPanel.DOKill(false);
        }
    }
}
