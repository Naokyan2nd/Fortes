using System;

using System.Collections.Generic;

using System.Threading;

using Cysharp.Threading.Tasks;

using DG.Tweening;

using TMPro;

using UnityEngine;
using UnityEngine.UI;



/// <summary>

/// QTE 中のライブ倍率（数値表示・ノーツ吸収・フィナーレ）。

/// 倍率計算は QteOutcomeCalculator に委譲。ECG 参照は将来復活用に SerializeField のみ保持。

/// </summary>

public sealed partial class QteLiveMultiplierView : MonoBehaviour

{

    private enum MultiplierCountMode

    {

        Both,

        UpOnly,

        InstantOnDecrease,

    }



    [Header("参照")]

    [SerializeField]

    [InspectorName("倍率表示アンカー")]

    private RectTransform _multiplierAnchor;



    [SerializeField]

    [InspectorName("ミスシェイク用（吸収終点と分離）")]

    private RectTransform _missShakeTarget;



    [SerializeField]

    [InspectorName("メイン倍率ラベル")]

    private TMP_Text _mainMultiplierLabel;



    [SerializeField]

    [InspectorName("メイン倍率用 Canvas Group")]

    private CanvasGroup _mainMultiplierGroup;



    [SerializeField]

    [InspectorName("吸収アイコン前面レイヤー")]

    private RectTransform _absorbIconRoot;



    [SerializeField]

    [InspectorName("吸収ノーツアイコンプレハブ")]

    private QteAbsorbNoteIconView _absorbNoteIconPrefab;



    [SerializeField]

    [InspectorName("全 Perfect バッジラベル")]

    private TMP_Text _apBadgeLabel;



    [SerializeField]

    [InspectorName("APバッジ（スタンプ）")]

    private RectTransform _apBadgeRect;



    [SerializeField]

    [InspectorName("APバッジ Canvas Group")]

    private CanvasGroup _apBadgeGroup;



    [Header("心拍ゲージ（将来復活用・現行 QTE では未使用）")]

    [SerializeField]

    [InspectorName("ECG FX ルート")]

    private GameObject _ecgFxRoot;



    [SerializeField]

    [InspectorName("ECG 波形")]

    private EcgWaveformRenderer _ecgRenderer;



    [SerializeField]

    [InspectorName("Perfect パルス振幅")]

    [Range(0f, 2f)]

    private float _perfectPulseScale = 1f;



    [SerializeField]

    [InspectorName("Good パルス振幅")]

    [Range(0f, 2f)]

    private float _goodPulseScale = 0.35f;



    [SerializeField]

    [InspectorName("フィナーレ パルス振幅")]

    [Range(0f, 3f)]

    private float _finalePulseScale = 1.4f;



    [Header("吸収")]

    [SerializeField]

    [InspectorName("飛行時間（秒）")]

    private float _chipFlyDuration = 0.2f;

    /// <summary>ノーツ吸収 Tween の秒数（タイムアウト計算用）。</summary>
    public float AbsorbFlyDurationSeconds => _chipFlyDuration;



    [SerializeField]

    [InspectorName("ノーツ開始スケール")]

    [Range(0.1f, 3f)]

    private float _noteFlyStartScale = 1f;



    [SerializeField]

    [InspectorName("ノーツ終了スケール")]

    [Range(0.05f, 1.5f)]

    private float _noteFlyEndScale = 0.4f;



    [SerializeField]

    [InspectorName("スケール縮小イージング")]

    private Ease _noteFlyScaleEase = Ease.OutQuad;



    [SerializeField]

    [InspectorName("スケール縮小開始遅延（飛行時間に対する割合）")]

    [Range(0f, 0.9f)]

    private float _noteFlyScaleDelayRatio;



    [SerializeField]

    [InspectorName("スケール縮小に使う飛行時間の割合")]

    [Range(0.1f, 1f)]

    private float _noteFlyScaleDurationRatio = 0.65f;



    [SerializeField]

    [InspectorName("飛行中のZ回転数")]

    [Range(0f, 8f)]

    private float _noteFlySpinRotations = 2f;



    [SerializeField]

    [InspectorName("吸収Sparkleプレハブ")]

    private QteAbsorbSparkleView _absorbSparklePrefab;



    [SerializeField]

    [InspectorName("トレイル生成間隔（秒・0で均等分配）")]

    [Range(0f, 0.2f)]

    private float _trailSpawnInterval;



    [SerializeField]

    [InspectorName("トレイル散らばり半径")]

    [Range(0f, 120f)]

    private float _trailSparkleSpreadRadius = 28f;



    [SerializeField]

    [InspectorName("Sparkleフェード時間（秒）")]

    [Range(0.05f, 1f)]

    private float _sparkleFadeDuration = 0.22f;



    [SerializeField]

    [InspectorName("Sparkle開始スケール")]

    [Range(0.1f, 3f)]

    private float _sparkleStartScale = 1f;



    [SerializeField]

    [InspectorName("Sparkle終了スケール")]

    [Range(0.01f, 1.5f)]

    private float _sparkleEndScale = 0.25f;



    [SerializeField]

    [InspectorName("Sparkleスケールイージング")]

    private Ease _sparkleScaleEase = Ease.OutQuad;



    [Header("Good 吸収 Sparkle")]

    [SerializeField]

    private QteAbsorbSparkleJudgmentSettings _goodAbsorbSparkle = new QteAbsorbSparkleJudgmentSettings

    {

        TrailSparkleCount = 6,

        MergeRadialSparkleCount = 8,

        TrailStartScaleMultiplier = 2f,

        MergeRadialStartScaleMultiplier = 2.5f,

        MergeRadialBurstDistanceMin = 32f,

        MergeRadialBurstDistanceMax = 96f,

    };



    [Header("Perfect 吸収 Sparkle")]

    [SerializeField]

    private QteAbsorbSparkleJudgmentSettings _perfectAbsorbSparkle = new QteAbsorbSparkleJudgmentSettings

    {

        TrailSparkleCount = 14,

        MergeRadialSparkleCount = 16,

        TrailStartScaleMultiplier = 3f,

        MergeRadialStartScaleMultiplier = 4f,

        MergeRadialBurstDistanceMin = 48f,

        MergeRadialBurstDistanceMax = 120f,

    };



    [Header("Perfect パステル色")]

    [SerializeField]

    [InspectorName("パステル色 1")]

    private Color _perfectPastelColor1 = new Color(1f, 0.84f, 0.91f, 1f);



    [SerializeField]

    [InspectorName("パステル色 2")]

    private Color _perfectPastelColor2 = new Color(0.78f, 0.96f, 0.91f, 1f);



    [SerializeField]

    [InspectorName("パステル色 3")]

    private Color _perfectPastelColor3 = new Color(0.88f, 0.83f, 1f, 1f);



    [SerializeField]

    [InspectorName("合流放射時間（秒）")]

    [Range(0.05f, 1f)]

    private float _mergeRadialBurstDuration = 0.28f;



    [SerializeField]

    [InspectorName("合流放射移動イージング")]

    private Ease _mergeRadialMoveEase = Ease.OutQuad;



    [Header("合流ジャスト")]

    [SerializeField]

    [InspectorName("合流ジャスト時間（秒）")]

    private float _mergeJuiceDuration = 0.1f;



    [SerializeField]

    [InspectorName("合流ジャスト横スケール")]

    private float _mergeJuiceSquashX = 1.14f;



    [SerializeField]

    [InspectorName("合流ジャスト縦スケール")]

    private float _mergeJuiceSquashY = 0.86f;



    [SerializeField]

    [InspectorName("合流ジャストバウンス Y")]

    private float _mergeJuiceBounceY = 8f;



    [Header("ミス時")]

    [SerializeField]

    [InspectorName("シェイク時間（秒）")]

    private float _missShakeDuration = 0.12f;



    [SerializeField]

    [InspectorName("シェイク強度")]

    private float _missShakeStrength = 12f;



    [Header("倍率カウント")]

    [SerializeField]

    [InspectorName("カウントアップ有効")]

    private bool _enableMultiplierCount = true;



    [SerializeField]

    [InspectorName("カウント時間（秒）")]

    private float _multiplierCountDuration = 0.22f;



    [SerializeField]

    [InspectorName("カウントイージング")]

    private Ease _multiplierCountEase = Ease.OutCubic;



    [SerializeField]

    [InspectorName("カウント最小差分")]

    private float _multiplierCountMinDelta = 0.001f;



    [SerializeField]

    [InspectorName("カウントモード")]

    private MultiplierCountMode _multiplierCountMode = MultiplierCountMode.Both;



    [Header("フィナーレ")]

    [SerializeField]

    [InspectorName("フィナーレ拡大ピーク")]

    private float _finaleScalePeak = 1.15f;



    [SerializeField]

    [InspectorName("フィナーレ拡大時間（秒）")]

    private float _finaleDuration = 0.28f;



    [SerializeField]

    [InspectorName("表示ホールド時間（秒）")]

    private float _finaleHoldSeconds = 0.2f;



    [Header("APバッジスタンプ")]

    [SerializeField]

    [InspectorName("スタンプ開始スケール")]

    private float _apStampStartScale = 3f;



    [SerializeField]

    [InspectorName("スタンプ縮小時間（秒）")]

    private float _apStampDuration = 0.3f;



    [SerializeField]

    [InspectorName("スタンプフェードイン時間（秒）")]

    private float _apStampFadeDuration = 0.25f;



    [SerializeField]

    [InspectorName("スタンプ着地パンチ時間（秒）")]

    private float _apStampPunchDuration = 0.15f;



    [SerializeField]

    [InspectorName("スタンプ着地シェイク強度")]

    private float _apStampShakeStrength = 8f;



    [SerializeField]

    [InspectorName("全 Perfect バッジ表示時間（秒）")]

    private float _apBadgeHoldSeconds = 0.35f;



    private readonly List<QteAbsorbNoteIconView> _absorbIconPool = new List<QteAbsorbNoteIconView>();

    private readonly List<QteAbsorbSparkleView> _sparklePool = new List<QteAbsorbSparkleView>();

    private int _pendingAbsorptionCount;

    private RectTransform _runtimeMissShakeTarget;

    private BattleSettingsSO _sessionSettings;

    private float _displayedMultiplier;



    /// <summary>次の QTE 開始前にリセット。</summary>

    public void ResetForNextQte()

    {

        if (!this)

        {

            return;

        }

        RestoreFromChargeOverlay();

        ResetChargeFlyVisualState();

        ResetMissShakeTargetVisual();

        QtePresenter.EnsureQteCanvasScale(transform);

        RebuildMultiplierLayout();

        _pendingAbsorptionCount = 0;

        ResetMultiplierRevealQueue();

        _sessionSettings = null;

        DestroyAbsorbIcons();

        ResetMainLabel();

        if (!IsEcgSceneTestMode())

        {

            DisableEcgSession();

            _ecgRenderer?.ResetBaseline();

        }

    }



    /// <summary>QTE 入場完了時。×1 を表示。</summary>

    public void BeginSession(BattleSettingsSO settings)

    {

        ResetForNextQte();

        _sessionSettings = settings;

        ResetMultiplierRevealQueue();

        if (IsEcgSceneTestMode())

        {

            return;

        }



        ShowMainMultiplier(1f, playJuice: false, animateCount: false);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogMultiplierHierarchyState("BeginSession", IsUnderChargeOverlay());
#endif

    }



    /// <summary>判定確定時。積算表示更新と Perfect/Good ノーツ吸収。</summary>

    /// <summary>ノーツ返却前に吸収アンカーを計算する（座標変換のタイミングずれ対策）。</summary>
    public bool TryComputeAbsorbAnchors(
        RectTransform noteRect,
        Vector3 spawnWorld,
        out Vector2 anchoredStart,
        out Vector2 anchoredEnd)
    {
        anchoredStart = Vector2.zero;
        anchoredEnd = Vector2.zero;

        QtePresenter.EnsureQteCanvasScale(transform);
        if (noteRect != null)
        {
            QtePresenter.EnsureQteCanvasScale(noteRect);
        }

        Canvas.ForceUpdateCanvases();
        EnsureAbsorbIconRoot();

        RectTransform flyParent = GetAbsorbFlyParent();
        RectTransform targetRect = ResolveAbsorbTargetRect();
        if (flyParent == null || targetRect == null)
        {
            return false;
        }

        Vector3 targetWorld = targetRect.TransformPoint(targetRect.rect.center);
        bool startOk = TryWorldToAnchoredInRect(flyParent, spawnWorld, out anchoredStart);
        bool endOk = TryWorldToAnchoredInRect(flyParent, targetWorld, out anchoredEnd);

        if (!startOk && noteRect != null)
        {
            startOk = TryWorldToAnchoredInRect(
                flyParent,
                noteRect.TransformPoint(noteRect.rect.center),
                out anchoredStart);
        }

        return startOk && endOk;
    }

    private RectTransform ResolveAbsorbTargetRect()
    {
        if (_multiplierAnchor != null)
        {
            return _multiplierAnchor;
        }

        return _mainMultiplierLabel != null ? _mainMultiplierLabel.rectTransform : null;
    }

    /// <summary>進行中の吸収演出の完了を待つ。</summary>

    public async UniTask WaitPendingAbsorptionsAsync(CancellationToken token)

    {

        float timeoutSeconds = Mathf.Max(_chipFlyDuration * 8f, 8f);

        try

        {

            await UniTask

                .WaitUntil(() => _pendingAbsorptionCount <= 0, cancellationToken: token)

                .Timeout(TimeSpan.FromSeconds(timeoutSeconds));

        }

        catch (OperationCanceledException)

        {

            if (token.IsCancellationRequested)

            {

                throw;

            }



#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(

                $"[QteLiveMultiplierView] 吸収待機がタイムアウトしました (pending={_pendingAbsorptionCount})",

                this);

#endif
            _pendingAbsorptionCount = 0;

        }

    }



    /// <summary>全ノート完了後のフィナーレ拡大。AP スタンプは攻撃・回復とも、×2 ラベルは回復 AP のみ。</summary>

    public async UniTask PlayFinaleAsync(

        IReadOnlyList<QteJudgment> judgments,

        BattleSettingsSO settings,

        SkillCategory skillCategory,

        CancellationToken token)

    {

        if (settings == null || judgments == null)

        {

            return;

        }



        float product = QteOutcomeCalculator.ComputeProductMultiplier(judgments, settings);

        ShowMainMultiplier(product, playJuice: false, animateCount: false);



        bool isAp = QteOutcomeCalculator.IsAllPerfect(judgments);

        bool showStamp = isAp;

        bool showHealBonus = isAp && skillCategory == SkillCategory.Heal;



        UniTask mainFinaleTask = PlayMainMultiplierFinaleAsync(token);

        if (showStamp || showHealBonus)

        {

            await UniTask.WhenAll(

                mainFinaleTask,

                PlayApFinalePresentationAsync(settings, showStamp, showHealBonus, token));

        }

        else

        {

            await mainFinaleTask;

        }



        if (_finaleHoldSeconds > 0f)

        {

            await UniTask.Delay(TimeSpan.FromSeconds(_finaleHoldSeconds), cancellationToken: token);

        }

    }



    private async UniTask PlayMainMultiplierFinaleAsync(CancellationToken token)

    {

        if (_mainMultiplierLabel == null)

        {

            return;

        }



        RectTransform labelRt = _mainMultiplierLabel.rectTransform;

        labelRt.DOKill(false);

        Vector3 restScale = Vector3.one;

        labelRt.localScale = restScale;

        await labelRt

            .DOScale(restScale * _finaleScalePeak, _finaleDuration * 0.45f)

            .SetEase(Ease.OutQuad)

            .SetUpdate(true)

            .ToUniTask(cancellationToken: token);

        await labelRt

            .DOScale(restScale, _finaleDuration * 0.55f)

            .SetEase(Ease.OutBack)

            .SetUpdate(true)

            .ToUniTask(cancellationToken: token);

    }



    private async UniTask PlayApFinalePresentationAsync(

        BattleSettingsSO settings,

        bool showStamp,

        bool showHealBonus,

        CancellationToken token)

    {

        List<UniTask> tasks = new List<UniTask>(2);

        if (showStamp)

        {

            tasks.Add(ShowApStampAsync(token));

        }



        if (showHealBonus)

        {

            tasks.Add(ShowHealBonusLabelAsync(settings, token));

        }



        if (tasks.Count == 0)

        {

            return;

        }



        await UniTask.WhenAll(tasks);



        if (_apBadgeHoldSeconds > 0f)

        {

            await UniTask.Delay(TimeSpan.FromSeconds(_apBadgeHoldSeconds), cancellationToken: token);

        }

    }



    private void EnableEcgSession()

    {

        if (IsEcgSceneTestMode())

        {

            return;

        }



        if (_ecgFxRoot != null)

        {

            _ecgFxRoot.SetActive(true);

        }

        else if (_ecgRenderer != null)

        {

            _ecgRenderer.gameObject.SetActive(true);

        }

    }



    private void DisableEcgSession()

    {

        if (IsEcgSceneTestMode())

        {

            return;

        }



        if (_ecgFxRoot != null)

        {

            _ecgFxRoot.SetActive(false);

        }

        else if (_ecgRenderer != null)

        {

            _ecgRenderer.gameObject.SetActive(false);

        }

    }



    private bool IsEcgSceneTestMode()

    {

        return _ecgRenderer != null && _ecgRenderer.SceneTestMode;

    }



    /// <summary>
    /// ノーツ吸収演出の有無。倍率への寄与（Perfect 打ち消し）は <see cref="QteOutcomeCalculator"/> 側で別管理。
    /// </summary>
    private static bool TryShouldAbsorbNote(QteJudgment judgment)
    {
        return judgment == QteJudgment.Perfect || judgment == QteJudgment.Good;
    }



    private void TriggerPulse(QteJudgment judgment)

    {

        if (_ecgRenderer == null)

        {

            return;

        }



        float scale = judgment switch

        {

            QteJudgment.Perfect => _perfectPulseScale,

            QteJudgment.Good => _goodPulseScale,

            _ => 0f

        };



        _ecgRenderer.AddPulse(scale);

    }



    private void ShowMainMultiplier(float product, bool playJuice, bool animateCount = true)

    {

        if (_mainMultiplierLabel == null)

        {

            return;

        }



        EnsureMultiplierVisibleForSession();

        _mainMultiplierLabel.gameObject.SetActive(true);



        if (_mainMultiplierGroup != null)

        {

            _mainMultiplierGroup.DOKill(false);

            _mainMultiplierGroup.alpha = 1f;

        }



        _mainMultiplierLabel.ForceMeshUpdate();

        AnimateMultiplierText(product, animateCount);



        if (!playJuice)

        {

            return;

        }



        RectTransform labelRt = _mainMultiplierLabel.rectTransform;

        if (labelRt != null)

        {

            PlayImpactJuice(

                labelRt,

                Vector3.one,

                _mergeJuiceDuration,

                _mergeJuiceSquashX,

                _mergeJuiceSquashY,

                _mergeJuiceBounceY);

        }

    }



    private bool ShouldAnimateMultiplierCount(float from, float to, bool animateRequested)

    {

        if (!animateRequested || !_enableMultiplierCount)

        {

            return false;

        }



        if (Mathf.Abs(to - from) < _multiplierCountMinDelta)

        {

            return false;

        }



        if (to < from)

        {

            return _multiplierCountMode == MultiplierCountMode.Both;

        }



        return to > from;

    }



    private void AnimateMultiplierText(float target, bool animate)

    {

        if (_mainMultiplierLabel == null)

        {

            return;

        }



        float from = _displayedMultiplier;

        if (!ShouldAnimateMultiplierCount(from, target, animate))

        {

            _mainMultiplierLabel.DOKill(false);

            _displayedMultiplier = target;

            _mainMultiplierLabel.text = FormatMultiplierText(target);

            return;

        }



        _mainMultiplierLabel.DOKill(false);



        float duration = Mathf.Max(_multiplierCountDuration, 0.01f);

        Ease ease = target >= from

            ? _multiplierCountEase

            : (_multiplierCountMode == MultiplierCountMode.Both ? Ease.InCubic : _multiplierCountEase);



        float v = from;

        DOTween.To(() => v, x =>

            {

                v = x;

                _displayedMultiplier = x;

                _mainMultiplierLabel.text = FormatMultiplierText(x);

            }, target, duration)

            .SetEase(ease)

            .SetUpdate(true)

            .SetTarget(_mainMultiplierLabel)

            .OnComplete(() =>

            {

                _displayedMultiplier = target;

                _mainMultiplierLabel.text = FormatMultiplierText(target);

            });

    }



    private void ResetMainLabel()

    {

        _displayedMultiplier = 0f;



        if (_mainMultiplierLabel != null)

        {

            _mainMultiplierLabel.DOKill(false);

            RectTransform labelRt = _mainMultiplierLabel.rectTransform;

            if (labelRt != null)

            {

                labelRt.DOKill(false);

                labelRt.localScale = Vector3.one;

                labelRt.localRotation = Quaternion.identity;

                RestoreChargeFlyRectLayout();

            }



            _mainMultiplierLabel.gameObject.SetActive(false);

        }



        if (_mainMultiplierGroup != null)

        {

            _mainMultiplierGroup.DOKill(false);

            _mainMultiplierGroup.alpha = 0f;

        }



        HideApBadge();

    }



    private RectTransform ResolveMissShakeTarget()

    {

        if (_missShakeTarget != null)

        {

            return _missShakeTarget;

        }



        if (_runtimeMissShakeTarget != null)

        {

            return _runtimeMissShakeTarget;

        }



        RectTransform parent = _multiplierAnchor != null

            ? _multiplierAnchor

            : _mainMultiplierLabel?.rectTransform;

        if (parent == null)

        {

            return null;

        }



        Transform existing = parent.Find("MissShakeTarget");

        if (existing is RectTransform existingRect)

        {

            _runtimeMissShakeTarget = existingRect;

            return existingRect;

        }



        var go = new GameObject("MissShakeTarget", typeof(RectTransform));

        var rt = go.GetComponent<RectTransform>();

        rt.SetParent(parent, false);

        rt.anchorMin = new Vector2(0.5f, 0.5f);

        rt.anchorMax = new Vector2(0.5f, 0.5f);

        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.anchoredPosition = Vector2.zero;

        rt.sizeDelta = Vector2.zero;

        rt.localScale = Vector3.one;

        _runtimeMissShakeTarget = rt;

        return rt;

    }



    private void ResetMissShakeTargetVisual()

    {

        RectTransform target = _missShakeTarget != null ? _missShakeTarget : _runtimeMissShakeTarget;

        if (target == null)

        {

            return;

        }



        target.DOKill(false);

        target.anchoredPosition = Vector2.zero;

    }



    private void PlayMissShake()

    {

        RectTransform target = ResolveMissShakeTarget();

        if (target == null || _missShakeDuration <= 0f)

        {

            return;

        }



        target.DOKill(false);

        target.DOPunchAnchorPos(new Vector2(_missShakeStrength, 0f), _missShakeDuration, 4, 0.5f)

            .SetUpdate(true);

    }



#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void LogAbsorbSkip(string reason)
    {
        Debug.LogWarning($"[QteLiveMultiplierView] 吸収演出スキップ: {reason}", this);
    }
#else
    private void LogAbsorbSkip(string reason) { }
#endif



    private Vector3 GetAbsorbTargetWorldPosition()

    {

        if (_multiplierAnchor != null)

        {

            return _multiplierAnchor.position;

        }



        if (_mainMultiplierLabel != null)

        {

            return _mainMultiplierLabel.rectTransform.position;

        }



        return transform.position;

    }



    private bool TryResolveAbsorbAnchors(

        RectTransform flyParent,

        Vector3 spawnPosition,

        bool usePrecomputedStart,

        Vector2 precomputedAnchoredStart,

        bool usePrecomputedEnd,

        Vector2 precomputedAnchoredEnd,

        out Vector2 anchoredStart,

        out Vector2 anchoredEnd)

    {

        anchoredStart = Vector2.zero;

        anchoredEnd = Vector2.zero;



        RectTransform targetRect = ResolveAbsorbTargetRect();

        if (targetRect != null)

        {

            QtePresenter.EnsureQteCanvasScale(targetRect);

        }



        QtePresenter.EnsureQteCanvasScale(flyParent);

        Canvas.ForceUpdateCanvases();



        // 飛行直前は倍率ラベルのジャスト等でレイアウトが変わるため、世界座標→anchored を優先し、
        // 判定確定時に取った precomputed はフォールバックのみ使う（2 ノート目以降の座標陳腐化対策）。
        bool startOk = TryWorldToAnchoredInRect(flyParent, spawnPosition, out anchoredStart);

        if (!startOk && usePrecomputedStart)

        {

            anchoredStart = precomputedAnchoredStart;

            startOk = true;

        }



        bool endOk = TryWorldToAnchoredInRect(

            flyParent,

            GetAbsorbTargetWorldPosition(),

            out anchoredEnd);

        if (!endOk && usePrecomputedEnd)

        {

            anchoredEnd = precomputedAnchoredEnd;

            endOk = true;

        }



        return startOk && endOk;

    }



    private async UniTask<(bool ok, Vector2 anchoredStart, Vector2 anchoredEnd)> TryResolveAbsorbAnchorsWithRetryAsync(

        RectTransform flyParent,

        Vector3 spawnPosition,

        bool usePrecomputedStart,

        Vector2 precomputedAnchoredStart,

        bool usePrecomputedEnd,

        Vector2 precomputedAnchoredEnd,

        CancellationToken token)

    {

        EnsureReadyForInQteAbsorb();



        if (TryResolveAbsorbAnchors(

                flyParent,

                spawnPosition,

                usePrecomputedStart,

                precomputedAnchoredStart,

                usePrecomputedEnd,

                precomputedAnchoredEnd,

                out Vector2 anchoredStart,

                out Vector2 anchoredEnd))

        {

            return (true, anchoredStart, anchoredEnd);

        }



        await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);



        EnsureReadyForInQteAbsorb();



        bool ok = TryResolveAbsorbAnchors(

            flyParent,

            spawnPosition,

            usePrecomputedStart,

            precomputedAnchoredStart,

            usePrecomputedEnd,

            precomputedAnchoredEnd,

            out anchoredStart,

            out anchoredEnd);

        return (ok, anchoredStart, anchoredEnd);

    }



    private RectTransform GetAbsorbFlyParent()

    {

        if (_absorbIconRoot != null)

        {

            return _absorbIconRoot;

        }



        return _multiplierAnchor != null ? _multiplierAnchor : transform as RectTransform;

    }



    private static bool TryWorldToAnchoredInRect(
        RectTransform rect,
        Vector3 world,
        out Vector2 anchored)
    {
        anchored = Vector2.zero;

        if (rect == null)
        {
            return false;
        }

        QtePresenter.EnsureQteCanvasScale(rect);
        Canvas.ForceUpdateCanvases();

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        RectTransform rootCanvasRect = canvas != null && canvas.rootCanvas != null
            ? canvas.rootCanvas.transform as RectTransform
            : null;
        Camera eventCam = ResolveCanvasEventCamera(rect);
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(eventCam, world);

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, eventCam, out anchored))
        {
            return true;
        }

        if (eventCam != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out anchored))
        {
            return true;
        }

        if (rootCanvasRect != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(rootCanvasRect, screen, eventCam, out Vector2 rootLocal))
        {
            Vector3 relayWorld = rootCanvasRect.TransformPoint(rootLocal);
            Vector2 relayScreen = RectTransformUtility.WorldToScreenPoint(eventCam, relayWorld);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, relayScreen, eventCam, out anchored))
            {
                return true;
            }
        }

        Vector3 inverseLocal = rect.InverseTransformPoint(world);
        if (float.IsFinite(inverseLocal.x) && float.IsFinite(inverseLocal.y))
        {
            anchored = new Vector2(inverseLocal.x, inverseLocal.y);
            return true;
        }

        return false;
    }

    private static Camera ResolveCanvasEventCamera(RectTransform rect)
    {
        Canvas canvas = rect.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        if (canvas.worldCamera != null)
        {
            return canvas.worldCamera;
        }

        return Camera.main;
    }

    private async UniTask ShowApStampAsync(CancellationToken token)

    {

        RectTransform rect = ResolveApBadgeRect();

        if (rect == null)

        {

            return;

        }



        CanvasGroup group = EnsureApBadgeCanvasGroup(rect);

        rect.DOKill(false);

        rect.localEulerAngles = Vector3.zero;

        rect.gameObject.SetActive(true);

        rect.localScale = Vector3.one * _apStampStartScale;

        if (group != null)

        {

            group.DOKill(false);

            group.alpha = 0f;

        }



        Sequence seq = DOTween.Sequence().SetUpdate(true);

        seq.Append(rect.DOScale(Vector3.one, _apStampDuration).SetEase(Ease.OutBack));

        if (group != null && _apStampFadeDuration > 0f)

        {

            seq.Join(

                group

                    .DOFade(1f, _apStampFadeDuration)

                    .SetEase(Ease.OutQuad));

        }

        else if (group != null)

        {

            group.alpha = 1f;

        }

        if (_apStampPunchDuration > 0f)

        {

            seq.Append(rect.DOPunchScale(new Vector3(0.08f, 0.08f, 0f), _apStampPunchDuration, 4, 0.6f));

            if (_apStampShakeStrength > 0f)

            {

                seq.Join(

                    rect.DOShakeRotation(

                        _apStampPunchDuration,

                        new Vector3(0f, 0f, _apStampShakeStrength),

                        12,

                        90f,

                        false));

            }

        }



        await seq.ToUniTask(cancellationToken: token);

    }



    private async UniTask ShowHealBonusLabelAsync(BattleSettingsSO settings, CancellationToken token)

    {

        if (_apBadgeLabel == null || settings == null)

        {

            return;

        }



        _apBadgeLabel.text = FormatMultiplierText(settings.AllPerfectHealBonusMultiplier);

        _apBadgeLabel.gameObject.SetActive(true);



        RectTransform rt = _apBadgeLabel.rectTransform;

        if (rt == null)

        {

            return;

        }



        rt.DOKill(false);

        CanvasGroup labelGroup = _apBadgeLabel.GetComponent<CanvasGroup>();

        if (labelGroup != null)

        {

            labelGroup.DOKill(false);

            labelGroup.alpha = 0f;

            rt.localScale = Vector3.one;

            await labelGroup.DOFade(1f, 0.12f).SetUpdate(true).ToUniTask(cancellationToken: token);

            return;

        }



        rt.localScale = Vector3.one;

    }



    private RectTransform ResolveApBadgeRect()

    {

        if (_apBadgeRect != null)

        {

            return _apBadgeRect;

        }



        return _apBadgeLabel != null ? _apBadgeLabel.rectTransform : null;

    }



    private CanvasGroup ResolveApBadgeGroup(RectTransform rect)

    {

        if (_apBadgeGroup != null)

        {

            return _apBadgeGroup;

        }



        return rect != null ? rect.GetComponent<CanvasGroup>() : null;

    }



    private CanvasGroup EnsureApBadgeCanvasGroup(RectTransform rect)

    {

        CanvasGroup group = ResolveApBadgeGroup(rect);

        if (group != null || rect == null)

        {

            return group;

        }



        group = rect.GetComponent<CanvasGroup>();

        if (group == null)

        {

            group = rect.gameObject.AddComponent<CanvasGroup>();

            group.blocksRaycasts = false;

            group.interactable = false;

        }



        return group;

    }



    private void HideApBadge()

    {

        if (_apBadgeRect != null)

        {

            _apBadgeRect.DOKill(false);

            _apBadgeRect.localEulerAngles = Vector3.zero;

            _apBadgeRect.localScale = Vector3.one;

            _apBadgeRect.gameObject.SetActive(false);

        }



        CanvasGroup stampGroup = ResolveApBadgeGroup(_apBadgeRect);

        if (stampGroup != null)

        {

            stampGroup.DOKill(false);

            stampGroup.alpha = 1f;

        }



        if (_apBadgeLabel == null)

        {

            return;

        }



        _apBadgeLabel.DOKill(false);

        CanvasGroup labelGroup = _apBadgeLabel.GetComponent<CanvasGroup>();

        if (labelGroup != null)

        {

            labelGroup.DOKill(false);

            labelGroup.alpha = 1f;

        }



        _apBadgeLabel.gameObject.SetActive(false);

    }



    private void EnsureAbsorbIconRoot()

    {

        if (_absorbIconRoot != null)

        {

            _absorbIconRoot.SetAsLastSibling();

            return;

        }



        Transform existing = transform.Find("MergeLabelsFront");

        if (existing != null)

        {

            _absorbIconRoot = existing as RectTransform;

            _absorbIconRoot.SetAsLastSibling();

        }

    }



    private void BringAbsorbIconToFront(QteAbsorbNoteIconView icon)

    {

        if (icon == null)

        {

            return;

        }



        EnsureAbsorbIconRoot();

        if (_absorbIconRoot == null)

        {

            return;

        }



        icon.IconRect.SetParent(_absorbIconRoot, false);

        icon.IconRect.SetAsLastSibling();

    }



    private void PlayImpactJuice(

        RectTransform target,

        Vector3 restScale,

        float duration,

        float peakScaleX,

        float peakScaleY,

        float bounceYOffset)

    {

        if (target == null || duration <= 0f)

        {

            return;

        }



        target.DOKill(false);

        Vector3 squashScale = new Vector3(

            restScale.x * peakScaleX,

            restScale.y * peakScaleY,

            restScale.z);

        float squashIn = duration * 0.35f;

        float squashOut = Mathf.Max(duration - squashIn, 0.01f);



        Sequence seq = DOTween.Sequence();

        seq.SetUpdate(true);

        seq.Append(target.DOScale(squashScale, squashIn).SetEase(Ease.OutQuad));

        seq.Append(target.DOScale(restScale, squashOut).SetEase(Ease.OutBack));



        if (bounceYOffset > 0f)

        {

            seq.Insert(

                0f,

                target.DOPunchAnchorPos(new Vector2(0f, bounceYOffset), duration, 1, 0.45f)

                    .SetUpdate(true));

        }

    }



    private static string FormatMultiplierText(float value) =>
        QteOutcomeCalculator.FormatProductMultiplierDisplay(value);



    private static void ApplyAbsorbIconStyle(QteAbsorbNoteIconView icon)

    {

        if (icon == null)

        {

            return;

        }



        if (icon.TryGetComponent(out Image image))

        {

            image.raycastTarget = false;

        }



        if (!icon.TryGetComponent(out CanvasGroup group))

        {

            group = icon.gameObject.AddComponent<CanvasGroup>();

        }



        group.blocksRaycasts = false;

        group.interactable = false;

    }



    private QteAbsorbNoteIconView RentAbsorbIcon()

    {

        for (int i = 0; i < _absorbIconPool.Count; i++)

        {

            QteAbsorbNoteIconView icon = _absorbIconPool[i];

            if (icon == null)

            {

                continue;

            }



            if (!icon.gameObject.activeSelf)

            {

                icon.PrepareForAbsorbFlight();

                return icon;

            }

        }



        QteAbsorbNoteIconView created = CreateAbsorbIconInstance();

        if (created != null)

        {

            _absorbIconPool.Add(created);

        }

        return created;

    }



    private Vector3 GetMergeSparkleOriginWorldPosition()

    {

        if (_mainMultiplierLabel != null)

        {

            RectTransform labelRt = _mainMultiplierLabel.rectTransform;

            return labelRt.TransformPoint(labelRt.rect.center);

        }



        return GetAbsorbTargetWorldPosition();

    }



    private void SpawnMergeRadialSparkles(QteJudgment judgment)

    {

        QtePresenter.EnsureQteCanvasScale(transform);
        Canvas.ForceUpdateCanvases();

        if (_absorbSparklePrefab == null

            || !TryGetAbsorbSparkleSettings(judgment, out QteAbsorbSparkleJudgmentSettings sparkleSettings)

            || sparkleSettings.MergeRadialSparkleCount <= 0)

        {

            return;

        }



        RectTransform flyParent = GetAbsorbFlyParent();

        if (flyParent == null)

        {

            return;

        }



        if (!TryWorldToAnchoredInRect(flyParent, GetMergeSparkleOriginWorldPosition(), out Vector2 center))

        {

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[QteLiveMultiplierView] マージ Sparkle の座標変換に失敗しました。",
                this);
#endif
            return;

        }



        float distanceMin = Mathf.Min(

            sparkleSettings.MergeRadialBurstDistanceMin,

            sparkleSettings.MergeRadialBurstDistanceMax);

        float distanceMax = Mathf.Max(

            sparkleSettings.MergeRadialBurstDistanceMin,

            sparkleSettings.MergeRadialBurstDistanceMax);

        float startScale = GetMergeRadialSparkleStartScale(sparkleSettings);



        for (int i = 0; i < sparkleSettings.MergeRadialSparkleCount; i++)

        {

            Vector2 direction = SampleRandomUnitDirection();

            float distance = UnityEngine.Random.Range(distanceMin, distanceMax);

            Vector2 end = center + direction * distance;

            Color tint = ResolveSparkleTint(judgment);



            QteAbsorbSparkleView sparkle = RentSparkle();

            if (sparkle == null)

            {

                continue;

            }



            sparkle.PlayBurstAt(

                flyParent,

                center,

                end,

                _mergeRadialBurstDuration,

                startScale,

                _sparkleEndScale,

                _mergeRadialMoveEase,

                _sparkleScaleEase,

                tint);

        }

    }



    private void SpawnTrailSparkleAt(Vector2 anchoredPosition, QteJudgment judgment)

    {

        if (_absorbSparklePrefab == null

            || !TryGetAbsorbSparkleSettings(judgment, out QteAbsorbSparkleJudgmentSettings sparkleSettings)

            || sparkleSettings.TrailSparkleCount <= 0)

        {

            return;

        }



        RectTransform flyParent = GetAbsorbFlyParent();

        if (flyParent == null)

        {

            return;

        }



        QteAbsorbSparkleView sparkle = RentSparkle();

        if (sparkle == null)

        {

            return;

        }



        Vector2 spawnPosition = anchoredPosition + SampleTrailSparkleOffset();



        sparkle.PlayAt(

            flyParent,

            spawnPosition,

            _sparkleFadeDuration,

            GetTrailSparkleStartScale(sparkleSettings),

            _sparkleEndScale,

            _sparkleScaleEase,

            ResolveSparkleTint(judgment));

    }



    private bool TryGetAbsorbSparkleSettings(

        QteJudgment judgment,

        out QteAbsorbSparkleJudgmentSettings settings)

    {

        switch (judgment)

        {

            case QteJudgment.Good:

                settings = _goodAbsorbSparkle;

                return true;

            case QteJudgment.Perfect:

                settings = _perfectAbsorbSparkle;

                return true;

            default:

                settings = default;

                return false;

        }

    }



    private float GetTrailSparkleStartScale(QteAbsorbSparkleJudgmentSettings settings)

    {

        return _sparkleStartScale * settings.TrailStartScaleMultiplier;

    }



    private float GetMergeRadialSparkleStartScale(QteAbsorbSparkleJudgmentSettings settings)

    {

        return _sparkleStartScale * settings.MergeRadialStartScaleMultiplier;

    }



    private Color ResolveSparkleTint(QteJudgment judgment)

    {

        if (judgment != QteJudgment.Perfect)

        {

            return Color.white;

        }



        int pick = UnityEngine.Random.Range(0, 3);

        if (pick == 0)

        {

            return _perfectPastelColor1;

        }



        if (pick == 1)

        {

            return _perfectPastelColor2;

        }



        return _perfectPastelColor3;

    }



    private static Vector2 SampleRandomUnitDirection()

    {

        Vector2 direction = UnityEngine.Random.insideUnitCircle;

        if (direction.sqrMagnitude < 0.0001f)

        {

            return Vector2.up;

        }



        return direction.normalized;

    }



    private Vector2 SampleTrailSparkleOffset()

    {

        if (_trailSparkleSpreadRadius <= 0f)

        {

            return Vector2.zero;

        }



        return UnityEngine.Random.insideUnitCircle * _trailSparkleSpreadRadius;

    }



    private QteAbsorbSparkleView RentSparkle()

    {

        for (int i = 0; i < _sparklePool.Count; i++)

        {

            QteAbsorbSparkleView sparkle = _sparklePool[i];

            if (sparkle != null && !sparkle.gameObject.activeSelf)

            {

                return sparkle;

            }

        }



        if (_absorbSparklePrefab == null)

        {

            return null;

        }



        EnsureAbsorbIconRoot();

        Transform parent = _absorbIconRoot != null ? _absorbIconRoot : transform;

        QteAbsorbSparkleView created = Instantiate(_absorbSparklePrefab, parent);

        created.gameObject.SetActive(false);

        ApplySparkleStyle(created);

        _sparklePool.Add(created);

        return created;

    }



    private static void ApplySparkleStyle(QteAbsorbSparkleView sparkle)

    {

        if (sparkle == null)

        {

            return;

        }



        if (sparkle.TryGetComponent(out Image image))

        {

            image.raycastTarget = false;

        }



        if (!sparkle.TryGetComponent(out CanvasGroup group))

        {

            group = sparkle.gameObject.AddComponent<CanvasGroup>();

        }



        group.blocksRaycasts = false;

        group.interactable = false;

    }



    private void DestroyAbsorbIcons()

    {

        DestroySparkles();



        for (int i = 0; i < _absorbIconPool.Count; i++)

        {

            QteAbsorbNoteIconView icon = _absorbIconPool[i];

            if (icon == null)

            {

                continue;

            }



            icon.CancelAbsorbSilently();

            Destroy(icon.gameObject);

        }



        _absorbIconPool.Clear();

    }



    private void DestroySparkles()

    {

        for (int i = 0; i < _sparklePool.Count; i++)

        {

            QteAbsorbSparkleView sparkle = _sparklePool[i];

            if (sparkle == null)

            {

                continue;

            }



            sparkle.KillTweens();

            Destroy(sparkle.gameObject);

        }



        _sparklePool.Clear();

    }

}


