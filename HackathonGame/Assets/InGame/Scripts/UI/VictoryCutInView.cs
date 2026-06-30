using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// バトル勝利・敗北のカットイン演出（DOTween）。
/// </summary>
public sealed class VictoryCutInView : MonoBehaviour
{
    public enum OutroMode
    {
        /// <summary>退場演出なし。</summary>
        None = 0,

        /// <summary>退場用 CanvasGroup で全体フェードアウト。</summary>
        CanvasFade = 1,

        /// <summary>全体暗転パネルを 0→1 にしてから遷移。</summary>
        BlackPanelFade = 2,
    }

    [Header("参照")]
    [Tooltip("ResultView 直下の全体暗転パネル（Image または CanvasGroup）。")]
    [SerializeField]
    private CanvasGroup _screenDimCanvasGroup;

    [SerializeField]
    private Image _screenDimImage;

    [Tooltip("背面リングの親。子の Mini 円を動かします。")]
    [SerializeField]
    private Transform _backPopCircleRoot;

    [SerializeField]
    private RectTransform _miniLeftPopCircle;

    [SerializeField]
    private RectTransform _miniRightPopCircle;

    [SerializeField]
    private RectTransform _bannerRect;

    [Tooltip("バナー内の暗転オーバーレイ。")]
    [SerializeField]
    private CanvasGroup _bannerDimCanvasGroup;

    [SerializeField]
    private Image _bannerDimImage;

    [SerializeField]
    private RectTransform _victoryTextRect;

    [SerializeField]
    private Image _victoryTextImage;

    [SerializeField]
    private RectTransform _popCircleRect;

    [SerializeField]
    private Image _popCircleImage;

    [Tooltip("退場フェード用。未設定時は ResultCanvas の CanvasGroup を探します。")]
    [SerializeField]
    private CanvasGroup _outroCanvasGroup;

    [Header("敗北カットイン参照")]
    [SerializeField]
    private GameObject _defeatRoot;

    [SerializeField]
    private Transform _defeatBackPopCircleRoot;

    [SerializeField]
    private RectTransform _defeatMiniLeftPopCircle;

    [SerializeField]
    private RectTransform _defeatMiniRightPopCircle;

    [SerializeField]
    private RectTransform _defeatBannerRect;

    [SerializeField]
    private Image _defeatBannerDimImage;

    [SerializeField]
    private RectTransform _defeatOutcomeTextRect;

    [SerializeField]
    private Image _defeatOutcomeTextImage;

    [Header("全体（暗転）")]
    [SerializeField]
    [Range(0f, 1f)]
    private float _screenDimTargetAlpha = 0.55f;

    [SerializeField]
    private float _screenDimFadeInDuration = 0.12f;

    [SerializeField]
    private Ease _screenDimFadeInEase = Ease.Linear;

    [Header("背面リング（Mini 左右）")]
    [Tooltip("フェーズ1開始時のスケール倍率（Rest=1 に対する）。")]
    [SerializeField]
    private float _backRingScaleFrom = 0.55f;

    [Tooltip("フェーズ1終了時のスケール倍率（ここまで一気に拡大）。")]
    [SerializeField]
    private float _backRingScaleHold = 1f;

    [Tooltip("フェーズ2終了時のスケール倍率。Hold より大きい値でさらに拡大（小さい場合は Hold に揃えます）。")]
    [SerializeField]
    private float _backRingScaleFadeEnd = 1.5f;

    [SerializeField]
    private float _backRingPopInDuration = 0.35f;

    [SerializeField]
    private float _backRingFadeOutDuration = 0.85f;

    [Tooltip("フェーズ1終了時の透明度。")]
    [SerializeField]
    [Range(0f, 1f)]
    private float _backRingPeakAlpha = 1f;

    [Tooltip("フェーズ2終了時の透明度（フェードアウト先）。")]
    [SerializeField]
    [Range(0f, 1f)]
    private float _backRingFadeOutAlpha = 0f;

    [SerializeField]
    private Ease _backRingPopInScaleEase = Ease.OutQuad;

    [SerializeField]
    private Ease _backRingPopInAlphaEase = Ease.OutQuad;

    [SerializeField]
    private Ease _backRingFadeOutScaleEase = Ease.Linear;

    [SerializeField]
    private Ease _backRingFadeOutAlphaEase = Ease.Linear;

    [SerializeField]
    private float _miniEnterOffsetX = 120f;

    [SerializeField]
    private float _miniEnterDuration = 0.4f;

    [SerializeField]
    private float _miniEnterStaggerSeconds = 0.05f;

    [SerializeField]
    private Ease _miniEnterEase = Ease.OutCubic;

    [Header("バナー")]
    [SerializeField]
    private float _bannerEnterOffsetX = -1600f;

    [SerializeField]
    private float _bannerEnterOffsetY = -80f;

    [SerializeField]
    private float _bannerEnterDelaySeconds = 0.08f;

    [SerializeField]
    private float _bannerEnterDuration = 0.38f;

    [SerializeField]
    private Ease _bannerEnterEase = Ease.OutCubic;

    [Tooltip("バナー内暗転の開始 alpha（1=真っ黒）。")]
    [SerializeField]
    [Range(0f, 1f)]
    private float _bannerDimStartAlpha = 0.6f;

    [SerializeField]
    private float _bannerDimFadeDuration = 0.25f;

    [SerializeField]
    private Ease _bannerDimFadeEase = Ease.OutQuad;

    [Header("結果テキスト（バナー上・勝利/敗北共通）")]
    [SerializeField]
    private float _victoryTextDelayAfterBannerSettle = 0.12f;

    [SerializeField]
    private float _victoryTextScaleFrom = 1.28f;

    [SerializeField]
    private float _victoryTextPopDuration = 0.22f;

    [SerializeField]
    private Ease _victoryTextPopEase = Ease.OutBack;

    [SerializeField]
    private float _victoryTextFadeInDuration = 0.1f;

    [SerializeField]
    private float _victoryTextPunchStrength = 0.08f;

    [Header("手前リング（PopCircle）")]
    [Tooltip("フェーズ1開始時のスケール倍率（Rest=1 に対する）。")]
    [SerializeField]
    private float _popCircleScaleFrom = 0.55f;

    [Tooltip("フェーズ1終了時のスケール倍率。")]
    [SerializeField]
    private float _popCircleScaleHold = 1f;

    [Tooltip("フェーズ2終了時のスケール倍率。Hold より大きい値でさらに拡大（小さい場合は Hold に揃えます）。")]
    [SerializeField]
    private float _popCircleScaleFadeEnd = 1.5f;

    [SerializeField]
    private float _popCirclePopInDuration = 0.45f;

    [SerializeField]
    private float _popCircleFadeOutDuration = 0.85f;

    [Tooltip("フェーズ1終了時の透明度。")]
    [SerializeField]
    [Range(0f, 1f)]
    private float _popCirclePeakAlpha = 1f;

    [Tooltip("フェーズ2終了時の透明度（フェードアウト先）。")]
    [SerializeField]
    [Range(0f, 1f)]
    private float _popCircleFadeOutAlpha = 0f;

    [SerializeField]
    private Ease _popCirclePopInScaleEase = Ease.OutQuad;

    [SerializeField]
    private Ease _popCirclePopInAlphaEase = Ease.OutQuad;

    [SerializeField]
    private Ease _popCircleFadeOutScaleEase = Ease.Linear;

    [SerializeField]
    private Ease _popCircleFadeOutAlphaEase = Ease.Linear;

    [Header("敗北カットイン（タイミング）")]
    [Tooltip("敗北時、入場演出を開始するまでの待ち時間（秒）。")]
    [SerializeField]
    private float _defeatIntroStartDelaySeconds = 0.2f;

    [Tooltip("敗北時、全体暗転が終わってから背面リングを始めるまでの追加待ち（秒）。")]
    [SerializeField]
    private float _defeatBackRingDelayAfterScreenDimSeconds = 0.06f;

    [Tooltip("敗北時、結果テキスト表示をさらに遅らせる秒数。")]
    [SerializeField]
    private float _defeatOutcomeTextExtraDelaySeconds = 0.12f;

    [Header("入場完了タイミング")]
    [Tooltip("リングのフェーズ2開始からこの秒数経過で入場演出を完了します（フェーズ2のTweenが終わっていなくても次へ進みます）。0以下で全Tweenの完了まで待ちます。")]
    [SerializeField]
    private float _ringPhase2ProceedAfterSeconds = 0.4f;

    [Header("退場（拡張用）")]
    [SerializeField]
    private OutroMode _outroMode = OutroMode.CanvasFade;

    [SerializeField]
    private float _outroDuration = 0.3f;

    [SerializeField]
    private Ease _outroEase = Ease.InQuad;

    private Vector2 _bannerRestAnchoredPosition;
    private Vector2 _defeatBannerRestAnchoredPosition;
    private Vector3 _bannerRestLocalPosition;
    private Vector3 _defeatBannerRestLocalPosition;
    private Vector2 _miniLeftRestAnchoredPosition;
    private Vector2 _defeatMiniLeftRestAnchoredPosition;
    private Vector2 _miniRightRestAnchoredPosition;
    private Vector2 _defeatMiniRightRestAnchoredPosition;
    private Vector3 _miniLeftRestScale;
    private Vector3 _defeatMiniLeftRestScale;
    private Vector3 _miniRightRestScale;
    private Vector3 _defeatMiniRightRestScale;
    private Vector3 _popCircleRestScale;
    private Color _miniLeftBaseColor;
    private Color _defeatMiniLeftBaseColor;
    private Color _miniRightBaseColor;
    private Color _defeatMiniRightBaseColor;
    private Color _popCircleBaseColor;
    private Color _victoryTextBaseColor;
    private Color _defeatOutcomeTextBaseColor;
    private Color _bannerDimBaseColor;
    private Color _defeatBannerDimBaseColor;
    private Color _screenDimBaseColor;
    private bool _hasCachedVictoryRestState;
    private bool _hasCachedDefeatRestState;
    private bool _hasCachedScreenDimColor;
    private bool _useDefeatLayout;
    private Sequence _activeSequence;
    private Tween _bannerSlideTween;
    private BannerRectSnapshot? _bannerSlideRectSnapshot;

    private struct BannerRectSnapshot
    {
        public Vector2 AnchorMin;
        public Vector2 AnchorMax;
        public Vector2 Pivot;
        public Vector2 AnchoredPosition;
        public Vector2 SizeDelta;
    }

    private Transform ActiveBackPopCircleRoot =>
        _useDefeatLayout ? _defeatBackPopCircleRoot : _backPopCircleRoot;

    private RectTransform ActiveMiniLeftPopCircle =>
        _useDefeatLayout ? _defeatMiniLeftPopCircle : _miniLeftPopCircle;

    private RectTransform ActiveMiniRightPopCircle =>
        _useDefeatLayout ? _defeatMiniRightPopCircle : _miniRightPopCircle;

    private RectTransform ActiveBannerRect =>
        _useDefeatLayout ? _defeatBannerRect : _bannerRect;

    private Image ActiveBannerDimImage =>
        _useDefeatLayout ? _defeatBannerDimImage : _bannerDimImage;

    private RectTransform ActiveOutcomeTextRect =>
        _useDefeatLayout ? _defeatOutcomeTextRect : _victoryTextRect;

    private Image ActiveOutcomeTextImage =>
        _useDefeatLayout ? _defeatOutcomeTextImage : _victoryTextImage;

    private RectTransform ActivePopCircleRect => _useDefeatLayout ? null : _popCircleRect;

    private Image ActivePopCircleImage => _useDefeatLayout ? null : _popCircleImage;

    private Vector2 ActiveBannerRestAnchoredPosition =>
        _useDefeatLayout ? _defeatBannerRestAnchoredPosition : _bannerRestAnchoredPosition;

    private Vector3 ActiveBannerRestLocalPosition =>
        _useDefeatLayout ? _defeatBannerRestLocalPosition : _bannerRestLocalPosition;

    private Vector2 ActiveMiniLeftRestAnchoredPosition =>
        _useDefeatLayout ? _defeatMiniLeftRestAnchoredPosition : _miniLeftRestAnchoredPosition;

    private Vector2 ActiveMiniRightRestAnchoredPosition =>
        _useDefeatLayout ? _defeatMiniRightRestAnchoredPosition : _miniRightRestAnchoredPosition;

    private Vector3 ActiveMiniLeftRestScale =>
        _useDefeatLayout ? _defeatMiniLeftRestScale : _miniLeftRestScale;

    private Vector3 ActiveMiniRightRestScale =>
        _useDefeatLayout ? _defeatMiniRightRestScale : _miniRightRestScale;

    private Vector3 ActivePopCircleRestScale => _popCircleRestScale;

    private Color ActiveMiniLeftBaseColor =>
        _useDefeatLayout ? _defeatMiniLeftBaseColor : _miniLeftBaseColor;

    private Color ActiveMiniRightBaseColor =>
        _useDefeatLayout ? _defeatMiniRightBaseColor : _miniRightBaseColor;

    private Color ActiveOutcomeTextBaseColor =>
        _useDefeatLayout ? _defeatOutcomeTextBaseColor : _victoryTextBaseColor;

    private Color ActiveBannerDimBaseColor =>
        _useDefeatLayout ? _defeatBannerDimBaseColor : _bannerDimBaseColor;

    private float IntroTimelineOffset =>
        _useDefeatLayout ? Mathf.Max(_defeatIntroStartDelaySeconds, 0f) : 0f;

    private float BackRingTimelineStart
    {
        get
        {
            if (!_useDefeatLayout)
            {
                return IntroTimelineOffset;
            }

            float afterDim = _screenDimFadeInDuration > 0f
                ? _screenDimFadeInDuration
                : 0f;
            return IntroTimelineOffset + afterDim + Mathf.Max(_defeatBackRingDelayAfterScreenDimSeconds, 0f);
        }
    }

    private float BannerEnterTimelineStart
    {
        get
        {
            if (!_useDefeatLayout)
            {
                return IntroTimelineOffset + _bannerEnterDelaySeconds;
            }

            float afterDim = IntroTimelineOffset;
            if (_screenDimFadeInDuration > 0f)
            {
                afterDim += _screenDimFadeInDuration;
            }

            return afterDim + _bannerEnterDelaySeconds;
        }
    }

    private float BannerSettleTimeline =>
        BannerEnterTimelineStart + Mathf.Max(_bannerEnterDuration, 0f);

    private float OutcomeTextTimelineStart =>
        BannerSettleTimeline
        + _victoryTextDelayAfterBannerSettle
        + (_useDefeatLayout ? Mathf.Max(_defeatOutcomeTextExtraDelaySeconds, 0f) : 0f);

    private void Awake()
    {
        TryResolveScreenDimReference();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void TryResolveScreenDimReference()
    {
        if (_screenDimCanvasGroup != null || _screenDimImage != null)
        {
            return;
        }

        Transform dim = transform.Find("DimOverlay");
        if (dim != null)
        {
            _screenDimImage = dim.GetComponent<Image>();
        }
    }

    private void EnsureScreenDimOverlay()
    {
        if (_screenDimCanvasGroup != null || _screenDimImage != null)
        {
            return;
        }

        var overlayGo = new GameObject(
            "DimOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        var rect = overlayGo.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = overlayGo.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        _screenDimImage = image;
    }

    /// <summary>表示直前のリセットと初期姿勢。</summary>
    public void PrepareForShow(bool isVictory)
    {
        KillTweens();
        _useDefeatLayout = !isVictory;
        TryResolveScreenDimReference();
        EnsureScreenDimOverlay();
        CacheRestStateIfNeeded();
        EnsureOutroCanvasGroup();
        SetAllCutInRootsInactive();

        ApplyLayoutVisibility(isVictory);
        SetCutInRootsActive(true);
        SetScreenDimActive(true);

        Canvas.ForceUpdateCanvases();
        RefreshActiveBannerRestTransform();
        PrepareBannerEnterOffScreen();
        SetBannerEnterHidden(true);

        ApplyScreenDimAlpha(0f);
        ApplyBannerDimVisible();

        Transform backRoot = ActiveBackPopCircleRoot;
        if (backRoot != null)
        {
            backRoot.localScale = Vector3.one;
        }

        ApplyMiniCircleEnterStart();
        ApplyBackRingHidden();
        ApplyOutcomeTextHidden();
        ApplyPopCircleHidden();
    }

    /// <summary>カットイン入場演出を再生する（勝利）。</summary>
    public UniTask PlayIntroAsync(CancellationToken token)
    {
        return PlayIntroAsync(token, isVictory: true);
    }

    /// <summary>カットイン入場演出を再生する。</summary>
    public async UniTask PlayIntroAsync(CancellationToken token, bool isVictory)
    {
        PrepareForShow(isVictory);

        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
        Canvas.ForceUpdateCanvases();
        if (_useDefeatLayout)
        {
            RefreshActiveBannerRestTransform();
        }

        PrepareBannerEnterOffScreen();
        SetBannerEnterHidden(true);

        _activeSequence = DOTween.Sequence();
        _activeSequence.SetUpdate(true);
        _activeSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        AppendScreenDimFadeIn(_activeSequence);
        AppendBackRingIntro(_activeSequence);
        AppendOutcomeTextIntro(_activeSequence);
        AppendPopCircleIntro(_activeSequence);
        ExtendSequenceToIntroEnd(_activeSequence);
        StartBannerIntroTweens();

        bool completed = false;
        _activeSequence.OnComplete(() => completed = true);
        _activeSequence.OnKill(() => completed = true);

        if (_ringPhase2ProceedAfterSeconds > 0f)
        {
            float proceedAt = GetIntroProceedTime();
            await UniTask.Delay(
                TimeSpan.FromSeconds(proceedAt),
                DelayType.UnscaledDeltaTime,
                cancellationToken: token);
        }
        else
        {
            await UniTask.WaitUntil(() => completed, cancellationToken: token);
        }

        await WaitForBannerIntroTweensAsync(token);
    }

    private async UniTask WaitForBannerIntroTweensAsync(CancellationToken token)
    {
        await WaitForTweenAsync(_bannerSlideTween, token);
    }

    private static async UniTask WaitForTweenAsync(Tween tween, CancellationToken token)
    {
        if (tween == null || !tween.IsActive() || tween.IsComplete())
        {
            return;
        }

        await UniTask.WaitUntil(() => !tween.IsActive() || tween.IsComplete(), cancellationToken: token);
    }

    /// <summary>
    /// Insert だけの Tween は Sequence の Append 長に含まれず、暗転終了時に Sequence が完了して
    /// バナー等が途中で止まるのを防ぐ。
    /// </summary>
    private void ExtendSequenceToIntroEnd(Sequence sequence)
    {
        float appendEnd = sequence.Duration();
        float introEnd = GetIntroTimelineEnd();
        float gap = introEnd - appendEnd;
        if (gap > 0.001f)
        {
            sequence.AppendInterval(gap);
        }
    }

    /// <summary>入場を打ち切って次へ進める最早時刻（バナー入場完了まで待つ）。</summary>
    private float GetIntroProceedTime()
    {
        float ringProceed = GetLatestRingPhase2StartTime() + Mathf.Max(_ringPhase2ProceedAfterSeconds, 0f);
        float bannerDone = BannerEnterTimelineStart + Mathf.Max(_bannerEnterDuration, 0f);
        return Mathf.Max(ringProceed, bannerDone);
    }

    /// <summary>入場演出タイムライン上の終了時刻（秒）。</summary>
    private float GetIntroTimelineEnd()
    {
        float end = BannerSettleTimeline;
        end = Mathf.Max(end, OutcomeTextTimelineStart + Mathf.Max(_victoryTextPopDuration, 0f));

        if (ActivePopCircleRect != null)
        {
            end = Mathf.Max(
                end,
                OutcomeTextTimelineStart
                    + Mathf.Max(_popCirclePopInDuration, 0f)
                    + Mathf.Max(_popCircleFadeOutDuration, 0f));
        }

        if (ActiveMiniLeftPopCircle != null || ActiveMiniRightPopCircle != null)
        {
            end = Mathf.Max(
                end,
                BackRingTimelineStart
                    + _miniEnterStaggerSeconds
                    + Mathf.Max(_miniEnterDuration, 0f));
            end = Mathf.Max(
                end,
                BackRingTimelineStart
                    + Mathf.Max(_backRingPopInDuration, 0f)
                    + Mathf.Max(_backRingFadeOutDuration, 0f));
        }

        return end;
    }

    /// <summary>背面・手前リングのうち、最も遅くフェーズ2に入る時刻（入場開始からの秒）。</summary>
    private float GetLatestRingPhase2StartTime()
    {
        float latest = 0f;

        if (ActiveMiniLeftPopCircle != null || ActiveMiniRightPopCircle != null)
        {
            latest = Mathf.Max(
                latest,
                BackRingTimelineStart + Mathf.Max(_backRingPopInDuration, 0f));
        }

        RectTransform popCircle = ActivePopCircleRect;
        if (popCircle != null)
        {
            float popStart = OutcomeTextTimelineStart;
            latest = Mathf.Max(latest, popStart + Mathf.Max(_popCirclePopInDuration, 0f));
        }

        return latest;
    }

    /// <summary>退場演出を再生する。</summary>
    public async UniTask PlayOutroAsync(CancellationToken token)
    {
        KillTweens();

        if (_outroMode == OutroMode.None || _outroDuration <= 0f)
        {
            return;
        }

        Sequence outro = DOTween.Sequence();
        outro.SetUpdate(true);
        outro.SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        switch (_outroMode)
        {
            case OutroMode.CanvasFade:
                if (_outroCanvasGroup != null)
                {
                    outro.Append(_outroCanvasGroup.DOFade(0f, _outroDuration).SetEase(_outroEase));
                }

                break;

            case OutroMode.BlackPanelFade:
                if (_screenDimCanvasGroup != null)
                {
                    outro.Append(
                        _screenDimCanvasGroup
                            .DOFade(1f, _outroDuration)
                            .SetEase(_outroEase));
                }
                else if (_screenDimImage != null)
                {
                    outro.Append(
                        _screenDimImage
                            .DOFade(1f, _outroDuration)
                            .SetEase(_outroEase));
                }

                break;
        }

        bool completed = false;
        outro.OnComplete(() => completed = true);
        outro.OnKill(() => completed = true);

        await UniTask.WaitUntil(() => completed, cancellationToken: token);
    }

    /// <summary>演出を打ち切る。</summary>
    public void KillTweens()
    {
        if (_activeSequence != null && _activeSequence.IsActive())
        {
            _activeSequence.Kill();
        }

        _activeSequence = null;

        KillBannerIntroTweens();
        KillCutInTargetTweens(_miniLeftPopCircle, _miniRightPopCircle, _victoryTextRect, _popCircleRect, _popCircleImage, _backPopCircleRoot);
        KillCutInTargetTweens(_defeatMiniLeftPopCircle, _defeatMiniRightPopCircle, _defeatOutcomeTextRect, null, null, _defeatBackPopCircleRoot);

        if (_outroCanvasGroup != null)
        {
            _outroCanvasGroup.DOKill(false);
        }
    }

    private void CacheRestStateIfNeeded()
    {
        if (!_hasCachedVictoryRestState)
        {
            CacheLayoutRestState(
                _bannerRect,
                _miniLeftPopCircle,
                _miniRightPopCircle,
                _victoryTextImage,
                _bannerDimImage,
                ref _bannerRestAnchoredPosition,
                ref _miniLeftRestAnchoredPosition,
                ref _miniRightRestAnchoredPosition,
                ref _miniLeftRestScale,
                ref _miniRightRestScale,
                ref _miniLeftBaseColor,
                ref _miniRightBaseColor,
                ref _victoryTextBaseColor,
                ref _bannerDimBaseColor);
            if (_popCircleRect != null)
            {
                _popCircleRestScale = _popCircleRect.localScale;
            }

            if (_popCircleImage != null)
            {
                _popCircleBaseColor = _popCircleImage.color;
            }

            _hasCachedVictoryRestState = true;
        }

        if (!_hasCachedDefeatRestState)
        {
            CacheLayoutRestState(
                _defeatBannerRect,
                _defeatMiniLeftPopCircle,
                _defeatMiniRightPopCircle,
                _defeatOutcomeTextImage,
                _defeatBannerDimImage,
                ref _defeatBannerRestAnchoredPosition,
                ref _defeatMiniLeftRestAnchoredPosition,
                ref _defeatMiniRightRestAnchoredPosition,
                ref _defeatMiniLeftRestScale,
                ref _defeatMiniRightRestScale,
                ref _defeatMiniLeftBaseColor,
                ref _defeatMiniRightBaseColor,
                ref _defeatOutcomeTextBaseColor,
                ref _defeatBannerDimBaseColor);
            _hasCachedDefeatRestState = true;
        }

        if (!_hasCachedScreenDimColor && _screenDimImage != null)
        {
            _screenDimBaseColor = _screenDimImage.color;
            _hasCachedScreenDimColor = true;
        }
    }

    private static void CacheLayoutRestState(
        RectTransform bannerRect,
        RectTransform miniLeft,
        RectTransform miniRight,
        Image outcomeTextImage,
        Image bannerDimImage,
        ref Vector2 bannerRestPos,
        ref Vector2 miniLeftRestPos,
        ref Vector2 miniRightRestPos,
        ref Vector3 miniLeftRestScale,
        ref Vector3 miniRightRestScale,
        ref Color miniLeftColor,
        ref Color miniRightColor,
        ref Color outcomeTextColor,
        ref Color bannerDimColor)
    {
        if (bannerRect != null)
        {
            bannerRestPos = bannerRect.anchoredPosition;
        }

        // localPosition は RefreshActiveBannerRestTransform で毎回更新する。

        if (miniLeft != null)
        {
            miniLeftRestPos = miniLeft.anchoredPosition;
            miniLeftRestScale = miniLeft.localScale;
            Image leftImage = miniLeft.GetComponent<Image>();
            if (leftImage != null)
            {
                miniLeftColor = leftImage.color;
            }
        }

        if (miniRight != null)
        {
            miniRightRestPos = miniRight.anchoredPosition;
            miniRightRestScale = miniRight.localScale;
            Image rightImage = miniRight.GetComponent<Image>();
            if (rightImage != null)
            {
                miniRightColor = rightImage.color;
            }
        }

        if (outcomeTextImage != null)
        {
            outcomeTextColor = outcomeTextImage.color;
        }

        if (bannerDimImage != null)
        {
            bannerDimColor = bannerDimImage.color;
        }
    }

    private void ApplyLayoutVisibility(bool isVictory)
    {
        if (_defeatRoot != null)
        {
            _defeatRoot.SetActive(!isVictory);
        }

        SetVictoryHierarchyRootActive(isVictory);

        if (_backPopCircleRoot != null)
        {
            _backPopCircleRoot.gameObject.SetActive(isVictory);
        }

        if (_bannerRect != null)
        {
            _bannerRect.gameObject.SetActive(isVictory);
        }
    }

    private void SetAllCutInRootsInactive()
    {
        if (_defeatRoot != null)
        {
            _defeatRoot.SetActive(false);
        }

        SetVictoryHierarchyRootActive(false);

        if (_backPopCircleRoot != null)
        {
            _backPopCircleRoot.gameObject.SetActive(false);
        }

        if (_bannerRect != null)
        {
            _bannerRect.gameObject.SetActive(false);
        }
    }

    private void SetVictoryHierarchyRootActive(bool active)
    {
        Transform node = _backPopCircleRoot != null ? _backPopCircleRoot : _bannerRect;
        if (node == null)
        {
            return;
        }

        Transform parent = node.parent;
        if (parent != null && parent != transform)
        {
            parent.gameObject.SetActive(active);
        }
    }

    private static void KillCutInTargetTweens(
        RectTransform miniLeft,
        RectTransform miniRight,
        RectTransform outcomeTextRect,
        RectTransform popCircleRect,
        Image popCircleImage,
        Transform backPopRoot)
    {
        if (miniLeft != null)
        {
            miniLeft.DOKill(false);
        }

        if (miniRight != null)
        {
            miniRight.DOKill(false);
        }

        KillImageTween(miniLeft);
        KillImageTween(miniRight);

        if (outcomeTextRect != null)
        {
            outcomeTextRect.DOKill(false);
        }

        if (popCircleRect != null)
        {
            popCircleRect.DOKill(false);
        }

        if (popCircleImage != null)
        {
            popCircleImage.DOKill(false);
        }

        if (backPopRoot != null)
        {
            backPopRoot.DOKill(false);
        }
    }

    private void EnsureOutroCanvasGroup()
    {
        if (_outroCanvasGroup != null)
        {
            _outroCanvasGroup.alpha = 1f;
            return;
        }

        Transform searchRoot = FindResultCanvasTransform();
        if (searchRoot == null)
        {
            return;
        }

        _outroCanvasGroup = searchRoot.GetComponent<CanvasGroup>();
        if (_outroCanvasGroup == null)
        {
            _outroCanvasGroup = searchRoot.gameObject.AddComponent<CanvasGroup>();
        }

        _outroCanvasGroup.alpha = 1f;
    }

    private Transform FindResultCanvasTransform()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == "ResultCanvas")
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private void SetCutInRootsActive(bool active)
    {
        Transform backRoot = ActiveBackPopCircleRoot;
        if (backRoot != null)
        {
            backRoot.gameObject.SetActive(active);
        }

        RectTransform banner = ActiveBannerRect;
        if (banner != null)
        {
            banner.gameObject.SetActive(active);
        }
    }

    private void SetScreenDimActive(bool active)
    {
        if (_screenDimCanvasGroup != null)
        {
            _screenDimCanvasGroup.gameObject.SetActive(active);
        }
        else if (_screenDimImage != null)
        {
            _screenDimImage.gameObject.SetActive(active);
        }
    }

    private void AppendScreenDimFadeIn(Sequence sequence)
    {
        if (IntroTimelineOffset > 0f)
        {
            sequence.AppendInterval(IntroTimelineOffset);
        }

        if (_screenDimFadeInDuration <= 0f || _screenDimTargetAlpha <= 0f)
        {
            ApplyScreenDimAlpha(_screenDimTargetAlpha);
            return;
        }

        if (_screenDimCanvasGroup != null)
        {
            sequence.Append(
                _screenDimCanvasGroup
                    .DOFade(_screenDimTargetAlpha, _screenDimFadeInDuration)
                    .SetEase(_screenDimFadeInEase));
            return;
        }

        if (_screenDimImage != null)
        {
            sequence.Append(
                _screenDimImage
                    .DOFade(_screenDimTargetAlpha, _screenDimFadeInDuration)
                    .SetEase(_screenDimFadeInEase));
        }
    }

    private void AppendBackRingIntro(Sequence sequence)
    {
        float ringStartTime = BackRingTimelineStart;

        RectTransform miniLeft = ActiveMiniLeftPopCircle;
        if (miniLeft != null)
        {
            float slideStart = ringStartTime + _miniEnterStaggerSeconds;
            sequence.Insert(
                slideStart,
                miniLeft
                    .DOAnchorPos(ActiveMiniLeftRestAnchoredPosition, _miniEnterDuration)
                    .SetEase(_miniEnterEase));
            AppendRingTwoPhase(
                sequence,
                miniLeft,
                miniLeft.GetComponent<Image>(),
                ActiveMiniLeftRestScale,
                ActiveMiniLeftBaseColor,
                _backRingScaleFrom,
                _backRingScaleHold,
                _backRingScaleFadeEnd,
                _backRingPopInDuration,
                _backRingFadeOutDuration,
                _backRingPeakAlpha,
                _backRingFadeOutAlpha,
                _backRingPopInScaleEase,
                _backRingPopInAlphaEase,
                _backRingFadeOutScaleEase,
                _backRingFadeOutAlphaEase,
                ringStartTime);
        }

        RectTransform miniRight = ActiveMiniRightPopCircle;
        if (miniRight != null)
        {
            sequence.Insert(
                ringStartTime,
                miniRight
                    .DOAnchorPos(ActiveMiniRightRestAnchoredPosition, _miniEnterDuration)
                    .SetEase(_miniEnterEase));
            AppendRingTwoPhase(
                sequence,
                miniRight,
                miniRight.GetComponent<Image>(),
                ActiveMiniRightRestScale,
                ActiveMiniRightBaseColor,
                _backRingScaleFrom,
                _backRingScaleHold,
                _backRingScaleFadeEnd,
                _backRingPopInDuration,
                _backRingFadeOutDuration,
                _backRingPeakAlpha,
                _backRingFadeOutAlpha,
                _backRingPopInScaleEase,
                _backRingPopInAlphaEase,
                _backRingFadeOutScaleEase,
                _backRingFadeOutAlphaEase,
                ringStartTime);
        }
    }

    /// <summary>
    /// バナーは Sequence に入れない（Sequence 完了時に Insert が打ち切られるため独立 Tween で再生）。
    /// </summary>
    private void StartBannerIntroTweens()
    {
        KillBannerIntroTweens();

        RectTransform banner = ActiveBannerRect;
        if (banner != null && _bannerEnterDuration > 0f)
        {
            float slideDelay = BannerEnterTimelineStart;
            Sequence slideSequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(banner.gameObject, LinkBehaviour.KillOnDestroy);

            if (slideDelay > 0f)
            {
                slideSequence.AppendInterval(slideDelay);
            }

            if (_useDefeatLayout)
            {
                slideSequence.AppendCallback(() =>
                {
                    PrepareDefeatBannerSlideStart(banner);
                    SetBannerEnterHidden(false);
                });
                slideSequence.Append(
                    banner
                        .DOAnchorPos(Vector2.zero, _bannerEnterDuration)
                        .SetEase(_bannerEnterEase)
                        .OnComplete(() => RestoreBannerRectAfterSlide(banner)));
            }
            else
            {
                slideSequence.AppendCallback(() =>
                {
                    PrepareBannerEnterOffScreen();
                    SetBannerEnterHidden(false);
                });
                slideSequence.Append(
                    banner
                        .DOAnchorPos(ActiveBannerRestAnchoredPosition, _bannerEnterDuration)
                        .SetEase(_bannerEnterEase)
                        .OnComplete(SnapActiveBannerToRest));
            }

            _bannerSlideTween = slideSequence;
        }

    }

    private void KillBannerIntroTweens()
    {
        bool slideWasRunning = _bannerSlideTween != null && _bannerSlideTween.IsActive() && !_bannerSlideTween.IsComplete();
        if (_bannerSlideTween != null && _bannerSlideTween.IsActive())
        {
            _bannerSlideTween.Kill();
        }

        _bannerSlideTween = null;

        if (slideWasRunning)
        {
            if (_useDefeatLayout)
            {
                RestoreBannerRectAfterSlide(ActiveBannerRect);
            }
            else
            {
                SnapActiveBannerToRest();
            }
        }

        KillActiveBannerDimTweens();
    }

    private void KillActiveBannerDimTweens()
    {
        if (_bannerDimCanvasGroup != null)
        {
            _bannerDimCanvasGroup.DOKill(false);
        }

        Image bannerDim = ActiveBannerDimImage;
        if (bannerDim != null)
        {
            bannerDim.DOKill(false);
        }
    }

    private void AppendOutcomeTextIntro(Sequence sequence)
    {
        RectTransform outcomeText = ActiveOutcomeTextRect;
        if (outcomeText == null)
        {
            return;
        }

        float startTime = OutcomeTextTimelineStart;
        Image outcomeImage = ActiveOutcomeTextImage;
        Color outcomeBaseColor = ActiveOutcomeTextBaseColor;

        sequence.InsertCallback(startTime, () =>
        {
            if (outcomeText == null)
            {
                return;
            }

            outcomeText.localScale = Vector3.one * _victoryTextScaleFrom;
            if (outcomeImage != null)
            {
                Color c = outcomeBaseColor;
                c.a = 0f;
                outcomeImage.color = c;
            }
        });

        if (_victoryTextPopDuration > 0f)
        {
            sequence.Insert(
                startTime,
                outcomeText
                    .DOScale(Vector3.one, _victoryTextPopDuration)
                    .SetEase(_victoryTextPopEase));
        }

        if (outcomeImage != null && _victoryTextFadeInDuration > 0f)
        {
            sequence.Insert(
                startTime,
                outcomeImage
                    .DOFade(outcomeBaseColor.a, _victoryTextFadeInDuration)
                    .SetEase(Ease.Linear));
        }

        if (_victoryTextPunchStrength > 0f)
        {
            float punchTime = startTime + _victoryTextPopDuration;
            sequence.Insert(
                punchTime,
                outcomeText.DOPunchScale(
                    Vector3.one * _victoryTextPunchStrength,
                    0.2f,
                    1,
                    0.5f));
        }
    }

    private void AppendPopCircleIntro(Sequence sequence)
    {
        RectTransform popCircle = ActivePopCircleRect;
        if (popCircle == null)
        {
            return;
        }

        float startTime = OutcomeTextTimelineStart;

        AppendRingTwoPhase(
            sequence,
            popCircle,
            ActivePopCircleImage,
            ActivePopCircleRestScale,
            _popCircleBaseColor,
            _popCircleScaleFrom,
            _popCircleScaleHold,
            _popCircleScaleFadeEnd,
            _popCirclePopInDuration,
            _popCircleFadeOutDuration,
            _popCirclePeakAlpha,
            _popCircleFadeOutAlpha,
            _popCirclePopInScaleEase,
            _popCirclePopInAlphaEase,
            _popCircleFadeOutScaleEase,
            _popCircleFadeOutAlphaEase,
            startTime);
    }

    private static void AppendRingTwoPhase(
        Sequence sequence,
        RectTransform rect,
        Image image,
        Vector3 restScale,
        Color baseColor,
        float scaleFrom,
        float scaleHold,
        float scaleFadeEnd,
        float popInDuration,
        float fadeOutDuration,
        float peakAlpha,
        float fadeOutAlpha,
        Ease popInScaleEase,
        Ease popInAlphaEase,
        Ease fadeOutScaleEase,
        Ease fadeOutAlphaEase,
        float startTime)
    {
        if (rect == null)
        {
            return;
        }

        float clampedHold = Mathf.Max(scaleHold, 0.001f);
        float clampedFadeEnd = Mathf.Max(scaleFadeEnd, clampedHold);

        Vector3 scaleStart = Vector3.Scale(restScale, Vector3.one * scaleFrom);
        Vector3 scaleMid = Vector3.Scale(restScale, Vector3.one * clampedHold);
        Vector3 scaleEnd = Vector3.Scale(restScale, Vector3.one * clampedFadeEnd);
        float clampedPeakAlpha = Mathf.Clamp01(peakAlpha);
        float clampedFadeOutAlpha = Mathf.Clamp01(fadeOutAlpha);

        sequence.InsertCallback(
            startTime,
            () =>
            {
                rect.localScale = scaleStart;
                if (image != null)
                {
                    Color c = baseColor;
                    c.a = 0f;
                    image.color = c;
                }
            });

        if (popInDuration > 0f)
        {
            sequence.Insert(
                startTime,
                rect.DOScale(scaleMid, popInDuration).SetEase(popInScaleEase));
            if (image != null)
            {
                sequence.Insert(
                    startTime,
                    image.DOFade(clampedPeakAlpha, popInDuration).SetEase(popInAlphaEase));
            }
        }
        else
        {
            sequence.InsertCallback(
                startTime,
                () =>
                {
                    rect.localScale = scaleMid;
                    if (image != null)
                    {
                        Color c = baseColor;
                        c.a = clampedPeakAlpha;
                        image.color = c;
                    }
                });
        }

        float fadeStart = startTime + Mathf.Max(popInDuration, 0f);
        if (fadeOutDuration > 0f)
        {
            sequence.Insert(
                fadeStart,
                rect.DOScale(scaleEnd, fadeOutDuration).SetEase(fadeOutScaleEase));
            if (image != null)
            {
                sequence.Insert(
                    fadeStart,
                    image.DOFade(clampedFadeOutAlpha, fadeOutDuration).SetEase(fadeOutAlphaEase));
            }
        }
    }

    private void ApplyBackRingHidden()
    {
        ApplyRingHidden(ActiveMiniLeftPopCircle, ActiveMiniLeftRestScale, ActiveMiniLeftBaseColor, _backRingScaleFrom);
        ApplyRingHidden(ActiveMiniRightPopCircle, ActiveMiniRightRestScale, ActiveMiniRightBaseColor, _backRingScaleFrom);
    }

    private void ApplyPopCircleHidden()
    {
        RectTransform popCircle = ActivePopCircleRect;
        if (popCircle == null)
        {
            return;
        }

        ApplyRingHidden(popCircle, ActivePopCircleRestScale, _popCircleBaseColor, _popCircleScaleFrom);
    }

    private static void ApplyRingHidden(
        RectTransform rect,
        Vector3 restScale,
        Color baseColor,
        float scaleFrom)
    {
        if (rect != null)
        {
            rect.localScale = Vector3.Scale(restScale, Vector3.one * scaleFrom);
        }

        Image image = rect != null ? rect.GetComponent<Image>() : null;
        if (image != null)
        {
            Color c = baseColor;
            c.a = 0f;
            image.color = c;
        }
    }

    private static void KillImageTween(RectTransform target)
    {
        if (target == null)
        {
            return;
        }

        Image image = target.GetComponent<Image>();
        if (image != null)
        {
            image.DOKill(false);
        }
    }

    private void ApplyScreenDimAlpha(float alpha)
    {
        if (_screenDimCanvasGroup != null)
        {
            _screenDimCanvasGroup.alpha = alpha;
            return;
        }

        if (_screenDimImage != null)
        {
            Color c = _screenDimBaseColor;
            c.a = alpha;
            _screenDimImage.color = c;
        }
    }

    private void RefreshActiveBannerDimBaseColor()
    {
        Image bannerDim = ActiveBannerDimImage;
        if (bannerDim == null)
        {
            return;
        }

        Color baseColor = bannerDim.color;
        baseColor.a = 1f;

        if (_useDefeatLayout)
        {
            _defeatBannerDimBaseColor = baseColor;
        }
        else
        {
            _bannerDimBaseColor = baseColor;
        }
    }

    /// <summary>Editor 設定の見た目を保ちつつ、バナー内暗転の表示 alpha だけを適用する。</summary>
    private void ApplyBannerDimVisible()
    {
        if (_bannerDimStartAlpha <= 0f)
        {
            return;
        }

        Image bannerDim = ActiveBannerDimImage;
        if (bannerDim == null)
        {
            return;
        }

        bannerDim.gameObject.SetActive(true);
        bannerDim.enabled = true;
        RefreshActiveBannerDimBaseColor();
        SetBannerDimAlpha(_bannerDimStartAlpha);
    }

    private void SetBannerDimAlpha(float alpha)
    {
        if (_bannerDimCanvasGroup != null && !_useDefeatLayout)
        {
            _bannerDimCanvasGroup.alpha = alpha;
            return;
        }

        Image bannerDim = ActiveBannerDimImage;
        if (bannerDim != null)
        {
            Color c = ActiveBannerDimBaseColor;
            c.a = alpha;
            bannerDim.color = c;
        }
    }

    private void ApplyMiniCircleEnterStart()
    {
        RectTransform miniLeft = ActiveMiniLeftPopCircle;
        if (miniLeft != null)
        {
            miniLeft.anchoredPosition = ActiveMiniLeftRestAnchoredPosition + new Vector2(-_miniEnterOffsetX, 0f);
            SetImageAlpha(miniLeft, 0f);
        }

        RectTransform miniRight = ActiveMiniRightPopCircle;
        if (miniRight != null)
        {
            miniRight.anchoredPosition = ActiveMiniRightRestAnchoredPosition + new Vector2(_miniEnterOffsetX, 0f);
            SetImageAlpha(miniRight, 0f);
        }
    }

    private void RefreshActiveBannerRestTransform()
    {
        RectTransform banner = ActiveBannerRect;
        if (banner == null)
        {
            return;
        }

        if (_useDefeatLayout)
        {
            _defeatBannerRestAnchoredPosition = banner.anchoredPosition;
            _defeatBannerRestLocalPosition = banner.localPosition;
            _bannerSlideRectSnapshot = CaptureBannerRect(banner);
        }
        else
        {
            _bannerRestAnchoredPosition = banner.anchoredPosition;
            _bannerRestLocalPosition = banner.localPosition;
            _bannerSlideRectSnapshot = null;
        }
    }

    private static BannerRectSnapshot CaptureBannerRect(RectTransform banner)
    {
        return new BannerRectSnapshot
        {
            AnchorMin = banner.anchorMin,
            AnchorMax = banner.anchorMax,
            Pivot = banner.pivot,
            AnchoredPosition = banner.anchoredPosition,
            SizeDelta = banner.sizeDelta,
        };
    }

    private void SnapActiveBannerToRest()
    {
        RectTransform banner = ActiveBannerRect;
        if (banner == null)
        {
            return;
        }

        banner.anchoredPosition = ActiveBannerRestAnchoredPosition;
        banner.localPosition = ActiveBannerRestLocalPosition;
        ApplyBannerDimVisible();
    }

    private Vector2 ActiveBannerEnterAnchoredPosition =>
        ActiveBannerRestAnchoredPosition + new Vector2(_bannerEnterOffsetX, _bannerEnterOffsetY);

    /// <summary>休止位置をキャッシュ済みの前提で、画面外の入場開始位置へ置く（表示は別途 CanvasGroup）。</summary>
    private void PrepareBannerEnterOffScreen()
    {
        RectTransform banner = ActiveBannerRect;
        if (banner == null)
        {
            return;
        }

        if (_useDefeatLayout)
        {
            return;
        }

        banner.anchoredPosition = ActiveBannerEnterAnchoredPosition;
    }

    private void PrepareDefeatBannerSlideStart(RectTransform banner)
    {
        if (!_bannerSlideRectSnapshot.HasValue)
        {
            _bannerSlideRectSnapshot = CaptureBannerRect(banner);
        }

        BannerRectSnapshot snapshot = _bannerSlideRectSnapshot.Value;
        Vector2 size = banner.rect.size;
        if (size.sqrMagnitude < 1f)
        {
            size = snapshot.SizeDelta;
        }

        banner.anchorMin = new Vector2(0.5f, 0.5f);
        banner.anchorMax = new Vector2(0.5f, 0.5f);
        banner.pivot = new Vector2(0.5f, 0.5f);
        banner.sizeDelta = size;
        banner.anchoredPosition = new Vector2(_bannerEnterOffsetX, _bannerEnterOffsetY);
    }

    private void RestoreBannerRectAfterSlide(RectTransform banner)
    {
        SetBannerEnterHidden(false);

        if (banner == null || !_bannerSlideRectSnapshot.HasValue)
        {
            return;
        }

        BannerRectSnapshot snapshot = _bannerSlideRectSnapshot.Value;
        banner.anchorMin = snapshot.AnchorMin;
        banner.anchorMax = snapshot.AnchorMax;
        banner.pivot = snapshot.Pivot;
        banner.sizeDelta = snapshot.SizeDelta;
        banner.anchoredPosition = snapshot.AnchoredPosition;
        _bannerSlideRectSnapshot = null;

        if (_useDefeatLayout)
        {
            _defeatBannerRestAnchoredPosition = snapshot.AnchoredPosition;
            _defeatBannerRestLocalPosition = banner.localPosition;
        }
    }

    private void SetBannerEnterHidden(bool hidden)
    {
        RectTransform banner = ActiveBannerRect;
        if (banner == null)
        {
            return;
        }

        CanvasGroup canvasGroup = banner.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = banner.gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = hidden ? 0f : 1f;
        canvasGroup.blocksRaycasts = !hidden;

        if (!hidden)
        {
            ApplyBannerDimVisible();
        }
    }

    private void ApplyOutcomeTextHidden()
    {
        RectTransform outcomeText = ActiveOutcomeTextRect;
        if (outcomeText != null)
        {
            outcomeText.localScale = Vector3.one * _victoryTextScaleFrom;
        }

        Image outcomeImage = ActiveOutcomeTextImage;
        if (outcomeImage != null)
        {
            Color c = ActiveOutcomeTextBaseColor;
            c.a = 0f;
            outcomeImage.color = c;
        }
    }

    private static void SetImageAlpha(RectTransform target, float alpha)
    {
        if (target == null)
        {
            return;
        }

        Image image = target.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
