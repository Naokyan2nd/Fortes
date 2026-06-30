using System;

using System.Collections.Generic;

using System.Threading;

using Cysharp.Threading.Tasks;

using DG.Tweening;

using UnityEngine;



/// <summary>

/// QTE 終了後、確定倍率をプレイヤーへ吸収するチャージ演出。

/// </summary>

public sealed partial class QteLiveMultiplierView

{

    [Header("プレイヤーへチャージ吸収")]

    [SerializeField]

    [InspectorName("チャージ用オーバーレイ親")]

    private RectTransform _chargeOverlayParent;



    [SerializeField]

    [InspectorName("太鼓 UI 復帰先（未設定時は初期親）")]

    private RectTransform _taikoMultiplierHomeParent;



    [SerializeField]

    [InspectorName("チャージ対象 Rect（未設定時はメイン倍率ラベル）")]

    private RectTransform _chargeFlyRect;



    [SerializeField]

    [InspectorName("オーバーレイ吸収時間（秒）")]

    [Range(0.05f, 1.5f)]

    private float _chargeAbsorbDuration = 0.3f;



    [SerializeField]

    [InspectorName("オーバーレイ吸収スケールイージング")]

    private Ease _chargeAbsorbScaleEase = Ease.InQuad;



    [SerializeField]

    [InspectorName("プレイヤー VFX 後パンチまでの遅延（秒）")]

    [Range(0f, 0.2f)]

    private float _chargeVfxToPunchDelaySeconds = 0.05f;



    [SerializeField]

    [Tooltip("チャージ VFX と同時にプレイヤー上へ表示する戦闘 FloatingText。")]

    private CombatFloatingTextPresenter _combatFloatingText;



    private Vector2 _chargeFlyRectRestAnchoredPosition;

    private bool _hasChargeFlyRectRestPosition;



    private void Awake()

    {

        CacheTaikoMultiplierHomeParent();

        CacheChargeFlyRectRestPosition();

        EnsureChargeOverlayOnQteCanvas();

    }



    /// <summary>五線譜退場後、倍率 UI を常駐 Canvas へ退避する。</summary>

    public void DetachToChargeOverlay()

    {

        HideApBadge();



        if (_chargeOverlayParent == null)

        {

            Debug.LogWarning("[QteLiveMultiplierView] チャージ用オーバーレイ親が未設定です。", this);

            return;

        }



        if (transform is not RectTransform root)

        {

            return;

        }



        CacheTaikoMultiplierHomeParent();



        if (!IsUnderChargeOverlay() && root.parent is RectTransform currentParent)

        {

            _taikoMultiplierHomeParent = currentParent;

        }



        root.SetParent(_chargeOverlayParent, worldPositionStays: true);

        root.SetAsLastSibling();

        EnsureAbsorbIconRoot();

        EnsureParentCanvasScale();

    }



    /// <summary>チャージ用に退避した親へ UI ルートを戻す（次 QTE 前）。</summary>

    public void RestoreFromChargeOverlay()

    {

        if (!this)

        {

            return;

        }



        if (transform is not RectTransform root)

        {

            return;

        }



        CacheTaikoMultiplierHomeParent();



        RectTransform home = ResolveTaikoMultiplierHomeParent();

        bool underOverlay = IsUnderChargeOverlay();



        if (home == null)

        {

            Debug.LogWarning(

                "[QteLiveMultiplierView] 太鼓 UI 復帰先が未設定のため QteChargeOverlay から復帰できません。",

                this);

        }

        else if (underOverlay || root.parent != home)

        {

            root.SetParent(home, false);

            ApplyTaikoHomeLayout(root);

        }

        else if (root.parent == home)

        {

            ApplyTaikoHomeLayout(root);

        }



        ResetChargeFlyVisualState();

        EnsureParentCanvasScale();

        RebuildMultiplierLayout();



#if UNITY_EDITOR || DEVELOPMENT_BUILD

        LogMultiplierHierarchyState("RestoreFromChargeOverlay", underOverlay);

#endif

    }



    /// <summary>確定倍率をプレイヤーへ吸収し、チャージリアクション完了まで待機する。</summary>

    public async UniTask PlayChargeAbsorbToPlayerAsync(

        PlayerView playerView,

        IReadOnlyList<QteJudgment> judgments,

        CancellationToken token)

    {

        if (playerView == null)

        {

            return;

        }



        HideApBadge();

        EnsureChargeLabelVisible();

        EnsureChargeOverlayOnQteCanvas();



        await PlayOverlayChargeShrinkFadeAsync(token);



        QteJudgment sparkleTier = ResolveChargeSparkleTier(judgments);

        SpawnMergeRadialSparkles(sparkleTier);



        float product = _sessionSettings != null

            ? QteOutcomeCalculator.ComputeProductMultiplier(judgments, _sessionSettings)

            : 0f;

        if (_combatFloatingText != null)

        {

            _combatFloatingText.ShowQteMultiplier(

                product,

                playerView.GetFloatingTextWorldPosition());

        }



        playerView.SpawnMultiplierChargeVfx();



        if (_chargeVfxToPunchDelaySeconds > 0f)

        {

            await UniTask.Delay(

                TimeSpan.FromSeconds(_chargeVfxToPunchDelaySeconds),

                cancellationToken: token);

        }



        await playerView.PlayMultiplierChargeReactionAsync(token);

    }



    /// <summary>

    /// チャージ完了後。太鼓レイヤーへ戻さずオーバーレイ上で非表示化する（次 QTE 開始時に復帰）。

    /// </summary>

    public void PrepareForNextQteAfterCharge()

    {

        if (!this)

        {

            return;

        }



        ResetChargeFlyVisualState();

        ResetMainLabel();

        EnsureParentCanvasScale();

        RebuildMultiplierLayout();



#if UNITY_EDITOR || DEVELOPMENT_BUILD

        LogMultiplierHierarchyState("PrepareForNextQteAfterCharge", IsUnderChargeOverlay());

#endif

    }



    private async UniTask PlayOverlayChargeShrinkFadeAsync(CancellationToken token)

    {

        RectTransform labelRect = ResolveChargeFlyRect();

        if (labelRect == null)

        {

            return;

        }



        QtePresenter.EnsureQteCanvasScale(transform);

        Canvas.ForceUpdateCanvases();



        float duration = Mathf.Max(0.05f, _chargeAbsorbDuration);



        labelRect.DOKill(false);

        labelRect.localScale = Vector3.one;



        if (_mainMultiplierGroup != null)

        {

            _mainMultiplierGroup.DOKill(false);

            _mainMultiplierGroup.alpha = 1f;

        }



        Sequence sequence = DOTween.Sequence();

        sequence.SetUpdate(true);

        sequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        sequence.Append(labelRect.DOScale(Vector3.zero, duration).SetEase(_chargeAbsorbScaleEase));



        if (_mainMultiplierGroup != null)

        {

            sequence.Join(_mainMultiplierGroup.DOFade(0f, duration));

        }



        await sequence.ToUniTask(cancellationToken: token);

        RestoreChargeFlyRectLayout();

    }



    private RectTransform ResolveTaikoMultiplierHomeParent()

    {

        if (_taikoMultiplierHomeParent != null)

        {

            return _taikoMultiplierHomeParent;

        }



        CacheTaikoMultiplierHomeParent();

        return _taikoMultiplierHomeParent;

    }



    private void CacheTaikoMultiplierHomeParent()

    {

        if (_taikoMultiplierHomeParent != null)

        {

            return;

        }



        if (transform.parent is RectTransform parent && !IsChargeOverlayParent(parent))

        {

            _taikoMultiplierHomeParent = parent;

        }

    }



    private bool IsUnderChargeOverlay()

    {

        return transform.parent == _chargeOverlayParent;

    }



    private bool IsChargeOverlayParent(RectTransform parent)

    {

        return _chargeOverlayParent != null && parent == _chargeOverlayParent;

    }



    private static void ApplyTaikoHomeLayout(RectTransform root)

    {

        root.anchorMin = Vector2.zero;

        root.anchorMax = Vector2.one;

        root.offsetMin = Vector2.zero;

        root.offsetMax = Vector2.zero;

        root.localScale = Vector3.one;

        root.localRotation = Quaternion.identity;

    }



    private void ResetChargeFlyVisualState()

    {

        _hasChargeFlyRectRestPosition = false;

        if (transform is RectTransform root)

        {

            root.DOKill(false);

            root.localScale = Vector3.one;

        }



        RestoreChargeFlyRectLayout();



        if (_mainMultiplierGroup != null)

        {

            _mainMultiplierGroup.DOKill(false);

            _mainMultiplierGroup.alpha = 1f;

        }

    }



    private void CacheChargeFlyRectRestPosition()

    {

        if (_hasChargeFlyRectRestPosition)

        {

            return;

        }



        RectTransform flyRect = ResolveChargeFlyRect();

        if (flyRect == null)

        {

            return;

        }



        _chargeFlyRectRestAnchoredPosition = flyRect.anchoredPosition;

        _hasChargeFlyRectRestPosition = true;

    }



    private void RestoreChargeFlyRectLayout()

    {

        CacheChargeFlyRectRestPosition();



        RectTransform flyRect = ResolveChargeFlyRect();

        if (flyRect == null)

        {

            return;

        }



        flyRect.DOKill(false);

        flyRect.localScale = Vector3.one;

        flyRect.localRotation = Quaternion.identity;

        if (_hasChargeFlyRectRestPosition)

        {

            flyRect.anchoredPosition = _chargeFlyRectRestAnchoredPosition;

        }

    }



    private void EnsureParentCanvasScale()

    {

        QtePresenter.EnsureQteCanvasScale(transform);

    }



    /// <summary>QteChargeOverlay を QTECanvas 配下へ移し、太鼓 UI と同じ描画順に揃える。</summary>

    private void EnsureChargeOverlayOnQteCanvas()

    {

        if (_chargeOverlayParent == null)

        {

            return;

        }



        RectTransform qteCanvas = ResolveQteCanvasRoot();

        if (qteCanvas == null || _chargeOverlayParent.parent == qteCanvas)

        {

            return;

        }



        _chargeOverlayParent.SetParent(qteCanvas, false);

        _chargeOverlayParent.anchorMin = Vector2.zero;

        _chargeOverlayParent.anchorMax = Vector2.one;

        _chargeOverlayParent.offsetMin = Vector2.zero;

        _chargeOverlayParent.offsetMax = Vector2.zero;

        _chargeOverlayParent.localScale = Vector3.one;

        _chargeOverlayParent.SetAsLastSibling();

        QtePresenter.EnsureQteCanvasScale(_chargeOverlayParent);

    }



    private RectTransform ResolveQteCanvasRoot()

    {

        RectTransform taikoHome = ResolveTaikoMultiplierHomeParent();

        if (taikoHome != null && taikoHome.parent is RectTransform qteCanvas)

        {

            return qteCanvas;

        }



        Canvas canvas = GetComponentInParent<Canvas>();

        return canvas != null ? canvas.rootCanvas.transform as RectTransform : null;

    }



    private void RebuildMultiplierLayout()

    {

        if (transform is not RectTransform root)

        {

            return;

        }



        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(root);

        if (_multiplierAnchor is RectTransform anchor)

        {

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(anchor);

        }



        if (_mainMultiplierLabel != null)

        {

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_mainMultiplierLabel.rectTransform);

        }

    }



    /// <summary>QTE 中のノーツ吸収直前。チャージ用レイアウト復帰は行わない。</summary>

    private void EnsureReadyForInQteAbsorb()

    {

        if (!gameObject.activeSelf)

        {

            gameObject.SetActive(true);

        }



        QtePresenter.EnsureQteCanvasScale(transform);

        EnsureAbsorbIconRoot();

        Canvas.ForceUpdateCanvases();

    }



    private void EnsureMultiplierVisibleForSession()

    {

        if (!gameObject.activeSelf)

        {

            gameObject.SetActive(true);

        }



        QtePresenter.EnsureQteCanvasScale(transform);

        Canvas.ForceUpdateCanvases();

        if (IsUnderChargeOverlay())

        {

            RestoreChargeFlyRectLayout();

        }



        RebuildMultiplierLayout();



        if (_mainMultiplierGroup != null)

        {

            _mainMultiplierGroup.DOKill(false);

            _mainMultiplierGroup.alpha = 1f;

        }



#if UNITY_EDITOR || DEVELOPMENT_BUILD

        if (!gameObject.activeInHierarchy)

        {

            string parentName = transform.parent != null ? transform.parent.name : "(null)";

            Debug.LogWarning(

                $"[QteLiveMultiplierView] 倍率 UI の親が非アクティブです: parent={parentName}",

                this);

        }

#endif

    }



    private void EnsureChargeLabelVisible()

    {

        if (_mainMultiplierLabel != null)

        {

            _mainMultiplierLabel.gameObject.SetActive(true);

        }



        if (_mainMultiplierGroup != null)

        {

            _mainMultiplierGroup.alpha = 1f;

        }

    }



    private RectTransform ResolveChargeFlyRect()

    {

        if (_chargeFlyRect != null)

        {

            return _chargeFlyRect;

        }



        if (_mainMultiplierLabel != null)

        {

            return _mainMultiplierLabel.rectTransform;

        }



        return _multiplierAnchor;

    }



    private static QteJudgment ResolveChargeSparkleTier(IReadOnlyList<QteJudgment> judgments)

    {

        if (judgments == null || judgments.Count == 0)

        {

            return QteJudgment.Good;

        }



        if (QteOutcomeCalculator.IsAllPerfect(judgments))

        {

            return QteJudgment.Perfect;

        }



        for (int i = 0; i < judgments.Count; i++)

        {

            if (judgments[i] == QteJudgment.Good)

            {

                return QteJudgment.Good;

            }

        }



        return QteJudgment.Good;

    }



#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void LogMultiplierHierarchyState(string context, bool underOverlay)

    {

        string parentName = transform.parent != null ? transform.parent.name : "(null)";

        RectTransform flyRect = ResolveChargeFlyRect();

        string labelState = "n/a";

        if (_mainMultiplierLabel != null)

        {

            RectTransform labelRt = _mainMultiplierLabel.rectTransform;

            float groupAlpha = _mainMultiplierGroup != null ? _mainMultiplierGroup.alpha : -1f;

            labelState =

                $"active={_mainMultiplierLabel.gameObject.activeSelf} " +

                $"scale={labelRt.localScale} anchored={labelRt.anchoredPosition} groupAlpha={groupAlpha}";

        }



        Debug.Log(

            $"[QteLiveMultiplierView] {context}: parent={parentName} underOverlay={underOverlay} {labelState}",

            this);

    }

#endif

}


