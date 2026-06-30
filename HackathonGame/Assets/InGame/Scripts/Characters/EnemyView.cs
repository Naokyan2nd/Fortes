using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 敵1体分のスプライト・タップ通知・選択マーカー・HPスライダー同期（プレハブ用）。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class EnemyView : MonoBehaviour, IPointerDownHandler
{
    private const int BaseLayer = 0;

    [Header("Character animation")]
    [SerializeField]
    private Animator _animator;

    [SerializeField]
    [Min(0f)]
    [Tooltip("isAttacking 発火から Trail 発射までの秒数。")]
    private float _attackTrailDelaySeconds = 0.28f;

    [SerializeField]
    [Tooltip("被弾アニメ再生時は横パンチ DOTween をスキップする。")]
    private bool _suppressHitPunchWhenAnimated = true;

    [Header("Floating text")]
    [SerializeField]
    [Tooltip("未設定時は SpriteRenderer.bounds の上端中央。")]
    private Transform _floatingTextAnchor;

    [Header("Turn approach (DQ-style hop)")]
    [SerializeField]
    private float _turnJumpHeight = 0.35f;

    [SerializeField]
    [Tooltip("1段（上がりまたは下り）の秒数。2回ジャンプなら合計 4 区間分かかります。")]
    private float _turnJumpHalfDuration = 0.12f;

    [SerializeField]
    [Min(1)]
    private int _turnJumpCount = 2;

    [Header("Hit (player attack)")]
    [SerializeField]
    private float _hitPunchStrength = 0.18f;

    [SerializeField]
    private float _hitPunchDuration = 0.35f;

    [SerializeField]
    [Min(0)]
    private int _hitPunchVibrato = 10;

    [SerializeField]
    [Range(0f, 1f)]
    private float _hitPunchElasticity = 0.45f;

    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    [SerializeField]
    private GameObject _targetMarker;

    [SerializeField]
    private HpReactiveSliderBinder _hpSliderBinder;

    [Header("Player attack hit point")]
    [SerializeField]
    [Tooltip("プレイヤー攻撃 Trail の着弾点（子 CombatHitPoint 推奨）。未設定時は子名検索、なければ bounds 中心相当。")]
    private Transform _enemyCombatHitPoint;

    [Header("Attack trail (dark orb)")]
    [SerializeField]
    [Tooltip("闇の球の出現位置。未設定時はスプライト bounds 中心。")]
    private Transform _attackTrailEmitPoint;

    [SerializeField]
    private GameObject _attackTrailPrefab;

    [SerializeField]
    [Min(0.1f)]
    private float _attackTrailSpeed = 6f;

    [SerializeField]
    [Min(0f)]
    private float _attackTrailDurationMin = 0.2f;

    [SerializeField]
    [Min(0f)]
    private float _attackTrailDurationMax = 1.5f;

    [SerializeField]
    private Ease _attackTrailArrivalEase = Ease.Linear;

    [SerializeField]
    [Min(0f)]
    private float _attackTrailDestroyDelay = 0.15f;

    [Header("Sprite glitch desync")]
    [SerializeField]
    [Tooltip("スロットごとに _GlitchPahse へ加算する時間オフセット。")]
    private float _glitchPhasePerSlot = 2.17f;

    private static readonly int GlitchPhaseId = Shader.PropertyToID("_GlitchPahse");

    private EnemyModel _model;
    private MaterialPropertyBlock _materialPropertyBlock;
    private GameObject _activeAttackTrailInstance;
    private SpriteRenderer _displaySpriteRenderer;
    private IDisposable _selectionSubscription;
    private readonly Subject<int> _tapped = new Subject<int>();

    private void Awake()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        BoxCollider2D boxCollider2D = GetComponent<BoxCollider2D>();
        if (_spriteRenderer == null || boxCollider2D == null)
        {
            Debug.LogError("[EnemyView] SpriteRenderer / BoxCollider2D が不正です。", this);
        }

        if (_targetMarker != null)
        {
            _targetMarker.SetActive(false);
            DisableRaycastOnMarkerHierarchy(_targetMarker);
        }

        if (_enemyCombatHitPoint == null)
        {
            _enemyCombatHitPoint = transform.Find("CombatHitPoint");
        }

        ResolveCharacterAnimator();
        EnsureCharacterVisualBindings();
    }

    private void OnDestroy()
    {
        KillVisualTweens();
        DestroyActiveAttackTrailImmediate();
        ClearSelectionBind();
        if (_hpSliderBinder != null)
        {
            _hpSliderBinder.Unbind();
        }

        _tapped.Dispose();
    }

    /// <summary>スロット番号（モデルから取得）。</summary>
    public int SlotIndex => _model != null ? _model.SlotIndex : -1;

    /// <summary>FloatingText 用ワールドアンカー。</summary>
    public Transform FloatingTextAnchor => _floatingTextAnchor != null ? _floatingTextAnchor : transform;

    /// <summary>プレイヤー攻撃 Trail の着弾 Transform。</summary>
    public Transform EnemyCombatHitPoint => ResolveEnemyCombatHitPoint();

    /// <summary>FloatingText スポーン位置（ワールド）。</summary>
    public Vector3 GetFloatingTextWorldPosition()
    {
        if (_floatingTextAnchor != null)
        {
            return _floatingTextAnchor.position;
        }

        SpriteRenderer boundsSource = ResolveBoundsSpriteRenderer();
        if (boundsSource == null)
        {
            return transform.position;
        }

        Bounds bounds = boundsSource.bounds;
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }

    /// <summary>敵がタップされた通知（生存時のみ）。</summary>
    public Observable<int> OnTapped => _tapped;

    /// <summary>
    /// モデルを差し替える（nullで非表示）。
    /// </summary>
    /// <param name="model">敵モデル。</param>
    public void Setup(EnemyModel model)
    {
        KillVisualTweens();
        ClearSelectionBind();
        if (_hpSliderBinder != null)
        {
            _hpSliderBinder.Unbind();
        }

        _model = model;
        if (model == null)
        {
            gameObject.SetActive(false);
            return;
        }

        ResolveCharacterAnimator();
        EnsureCharacterVisualBindings();
        RefreshAnimatorPlayback();

        EnemyDataSO data = model.Data;
        gameObject.SetActive(true);
        if (data != null && data.Sprite != null && !HasAnimator)
        {
            _spriteRenderer.sprite = data.Sprite;
        }

        ApplyPerEnemyGlitchPhase(model);

        if (_hpSliderBinder != null)
        {
            _hpSliderBinder.Bind(model.CurrentHp, model.MaxHp);
        }

        _selectionSubscription = model.IsSelected.Subscribe(OnSelectedChanged);
        OnSelectedChanged(model.IsSelected.Value);
    }

    /// <summary>
    /// 共有マテリアルでもグリッチのタイミングが揃わないよう、敵ごとに時間オフセットを渡す。
    /// </summary>
    private void ApplyPerEnemyGlitchPhase(EnemyModel model)
    {
        float phase = model.SlotIndex * _glitchPhasePerSlot;
        string enemyId = model.Data != null ? model.Data.EnemyId : null;
        if (!string.IsNullOrEmpty(enemyId))
        {
            phase += (enemyId.GetHashCode() & 0xFFFF) * 0.0001f;
        }

        _materialPropertyBlock ??= new MaterialPropertyBlock();
        SpriteRenderer[] targets = ResolveGlitchSpriteRenderers();
        for (int i = 0; i < targets.Length; i++)
        {
            SpriteRenderer target = targets[i];
            if (target == null)
            {
                continue;
            }

            target.GetPropertyBlock(_materialPropertyBlock);
            _materialPropertyBlock.SetFloat(GlitchPhaseId, phase);
            target.SetPropertyBlock(_materialPropertyBlock);
        }
    }

    /// <summary>
    /// ターン開始時の近づき（Y方向に複数回ジャンプ）→ 攻撃モーション（isAttacking）→ Trail 遅延まで。
    /// </summary>
    public async UniTask PlayAttackFxAsync(CancellationToken token)
    {
        KillVisualTweens();
        Vector3 baseLocal = transform.localPosition;
        Sequence hop = DOTween.Sequence();
        for (int j = 0; j < _turnJumpCount; j++)
        {
            hop.Append(transform.DOLocalMoveY(baseLocal.y + _turnJumpHeight, _turnJumpHalfDuration).SetEase(Ease.OutQuad));
            hop.Append(transform.DOLocalMoveY(baseLocal.y, _turnJumpHalfDuration).SetEase(Ease.InQuad));
        }

        await WaitTweenAsync(hop, token);
        transform.localPosition = baseLocal;
        await PlayAttackWindupAsync(token);
    }

    /// <summary>攻撃モーション（isAttacking）開始から Trail 遅延まで。</summary>
    public async UniTask PlayAttackWindupAsync(CancellationToken token)
    {
        if (!HasAnimator)
        {
            return;
        }

        FireTrigger(EnemyAnimatorTriggers.Attacking);
        if (_attackTrailDelaySeconds > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_attackTrailDelaySeconds), cancellationToken: token);
        }
    }

    /// <summary>
    /// 闇の球 Trail をプレイヤー着弾点へ直進させ、到達まで待機する。
    /// </summary>
    public async UniTask PlayAttackTrailToPlayerAsync(Transform playerCombatHitPoint, CancellationToken token)
    {
        if (_attackTrailPrefab == null)
        {
            Debug.LogWarning("[EnemyView] Attack Trail Prefab が未設定のためスキップします。", this);
            return;
        }

        Vector3 startWorld = GetAttackTrailEmitWorldPosition();
        Vector3 endWorld = playerCombatHitPoint != null
            ? playerCombatHitPoint.position
            : startWorld;
        float distance = Vector3.Distance(startWorld, endWorld);
        if (distance < 0.001f)
        {
            return;
        }

        float duration = distance / _attackTrailSpeed;
        if (_attackTrailDurationMax > 0f)
        {
            duration = Mathf.Min(duration, _attackTrailDurationMax);
        }

        if (_attackTrailDurationMin > 0f)
        {
            duration = Mathf.Max(duration, _attackTrailDurationMin);
        }

        DestroyActiveAttackTrailImmediate();
        GameObject trailInstance = Instantiate(_attackTrailPrefab, startWorld, Quaternion.identity);
        _activeAttackTrailInstance = trailInstance;
        ApplyTrailFacing2D(trailInstance.transform, endWorld - startWorld);

        Tweener moveTween = trailInstance.transform
            .DOMove(endWorld, duration)
            .SetEase(_attackTrailArrivalEase);

        bool cancelled = false;
        try
        {
            await WaitTweenAsync(moveTween, token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }
        finally
        {
            if (trailInstance != null)
            {
                trailInstance.transform.DOKill(false);
            }
        }

        if (cancelled || token.IsCancellationRequested)
        {
            DestroyActiveAttackTrailImmediate();
            return;
        }

        if (_activeAttackTrailInstance == trailInstance)
        {
            _activeAttackTrailInstance = null;
        }

        FadeOutAndDestroyTrailAsync(trailInstance, destroyCancellationToken: CancellationToken.None).Forget();
    }

    /// <summary>
    /// プレイヤー攻撃の着弾 VFX（SkillData のプレハブを CombatHitPoint の子に生成。Play On Awake 任せ）。
    /// </summary>
    public void SpawnAttackHitVfx(GameObject prefab, float destroyDelay)
    {
        if (prefab == null)
        {
            return;
        }

        Transform anchor = ResolveEnemyCombatHitPoint();
        GameObject instance = Instantiate(prefab, anchor);
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        DestroyAttackHitVfxAfterDelayAsync(instance, destroyDelay, CancellationToken.None).Forget();
    }

    /// <summary>
    /// プレイヤー攻撃ヒット時の被弾モーション（isReceivingDamage）または横パンチ。
    /// </summary>
    public async UniTask PlayHitFxAsync(CancellationToken token)
    {
        if (!IsViewAliveForFx())
        {
            return;
        }

        bool useAnimator = HasAnimator;
        if (useAnimator)
        {
            FireTrigger(EnemyAnimatorTriggers.ReceivingDamage);
        }

        bool suppressPunch = useAnimator && _suppressHitPunchWhenAnimated;
        if (suppressPunch)
        {
            await WaitForStateCompleteAsync(BaseLayer, EnemyAnimatorStateNames.ReceiveDamage, token);
            return;
        }

        KillVisualTweens();
        if (!IsViewAliveForFx())
        {
            return;
        }

        Tweener punch = transform.DOPunchPosition(
            new Vector3(_hitPunchStrength, 0f, 0f),
            _hitPunchDuration,
            _hitPunchVibrato,
            _hitPunchElasticity);
        await WaitTweenAsync(punch, token);
    }

    private bool IsViewAliveForFx()
    {
        return this != null && ResolveBoundsSpriteRenderer() != null;
    }

    private bool HasAnimator => _animator != null && _animator.isActiveAndEnabled;

    /// <summary>
    /// PSB 差し替え後に Animator / 表示スプライトを再接続する。
    /// </summary>
    private void EnsureCharacterVisualBindings()
    {
        if (_animator != null)
        {
            _displaySpriteRenderer = FindPrimarySpriteRenderer(_animator.gameObject);
        }
        else
        {
            _displaySpriteRenderer = null;
            if (_spriteRenderer != null && _spriteRenderer.enabled)
            {
                _displaySpriteRenderer = _spriteRenderer;
            }
            else
            {
                _displaySpriteRenderer = FindPrimarySpriteRenderer(gameObject);
            }
        }
    }

    private void RefreshAnimatorPlayback()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.enabled = true;
        _animator.Rebind();
        _animator.Update(0f);
    }

    private SpriteRenderer ResolveBoundsSpriteRenderer()
    {
        if (_displaySpriteRenderer != null)
        {
            return _displaySpriteRenderer;
        }

        return _spriteRenderer;
    }

    private SpriteRenderer[] ResolveGlitchSpriteRenderers()
    {
        if (_animator != null)
        {
            return _animator.GetComponentsInChildren<SpriteRenderer>(true);
        }

        return _spriteRenderer != null ? new[] { _spriteRenderer } : Array.Empty<SpriteRenderer>();
    }

    private static SpriteRenderer FindPrimarySpriteRenderer(GameObject visualRoot)
    {
        if (visualRoot == null)
        {
            return null;
        }

        SpriteRenderer onRoot = visualRoot.GetComponent<SpriteRenderer>();
        if (onRoot != null && onRoot.enabled && onRoot.sprite != null)
        {
            return onRoot;
        }

        SpriteRenderer[] renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer candidate = renderers[i];
            if (candidate != null && candidate.enabled && candidate.sprite != null)
            {
                return candidate;
            }
        }

        return renderers.Length > 0 ? renderers[0] : null;
    }

    /// <summary>
    /// 攻撃クリップ用 Animator を解決する（PSB 子オブジェクト上の Enemy.controller / Elite.controller 等）。
    /// ルートの TargetMarker 用 Animator は isAttacking を持たないため除外する。
    /// </summary>
    private void ResolveCharacterAnimator()
    {
        if (_animator != null && EnemyAnimatorTriggers.HasAttackTrigger(_animator))
        {
            return;
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator candidate = animators[i];
            if (candidate != null && EnemyAnimatorTriggers.HasAttackTrigger(candidate))
            {
                _animator = candidate;
                return;
            }
        }

        if (_animator != null && !EnemyAnimatorTriggers.HasAttackTrigger(_animator))
        {
            _animator = null;
        }
    }

    private void FireTrigger(string triggerName)
    {
        if (_animator == null)
        {
            return;
        }

        _animator.ResetTrigger(triggerName);
        _animator.SetTrigger(triggerName);
    }

    private async UniTask WaitForStateCompleteAsync(int layer, string stateName, CancellationToken token)
    {
        if (!HasAnimator)
        {
            return;
        }

        await UniTask.WaitUntil(
            () => _animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName),
            cancellationToken: token);

        await UniTask.WaitUntil(
            () =>
            {
                AnimatorStateInfo info = _animator.GetCurrentAnimatorStateInfo(layer);
                if (!info.IsName(stateName))
                {
                    return true;
                }

                return info.normalizedTime >= 1f;
            },
            cancellationToken: token);
    }

    private async UniTask DestroyAttackHitVfxAfterDelayAsync(
        GameObject instance,
        float destroyDelay,
        CancellationToken token)
    {
        if (instance == null)
        {
            return;
        }

        if (destroyDelay > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(destroyDelay), cancellationToken: token);
        }

        if (instance == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        Destroy(instance);
    }

    /// <inheritdoc />
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_model == null || !_model.IsAlive())
        {
            return;
        }

        _tapped.OnNext(_model.SlotIndex);
    }

    private Transform ResolveEnemyCombatHitPoint()
    {
        if (_enemyCombatHitPoint != null)
        {
            return _enemyCombatHitPoint;
        }

        Transform found = transform.Find("CombatHitPoint");
        if (found != null)
        {
            return found;
        }

        return transform;
    }

    private Vector3 GetAttackTrailEmitWorldPosition()
    {
        if (_attackTrailEmitPoint != null)
        {
            return _attackTrailEmitPoint.position;
        }

        SpriteRenderer boundsSource = ResolveBoundsSpriteRenderer();
        if (boundsSource != null)
        {
            return boundsSource.bounds.center;
        }

        return transform.position;
    }

    private static void ApplyTrailFacing2D(Transform trailTransform, Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float angleZ = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        trailTransform.rotation = Quaternion.Euler(0f, 0f, angleZ);
    }

    private async UniTask FadeOutAndDestroyTrailAsync(GameObject trailInstance, CancellationToken destroyCancellationToken)
    {
        if (trailInstance == null)
        {
            return;
        }

        ParticleSystem[] particleSystems = trailInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (_attackTrailDestroyDelay > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_attackTrailDestroyDelay), cancellationToken: destroyCancellationToken);
        }

        if (trailInstance != null)
        {
            Destroy(trailInstance);
        }
    }

    private void DestroyActiveAttackTrailImmediate()
    {
        if (_activeAttackTrailInstance == null)
        {
            return;
        }

        if (_activeAttackTrailInstance.transform != null)
        {
            _activeAttackTrailInstance.transform.DOKill(false);
        }

        Destroy(_activeAttackTrailInstance);
        _activeAttackTrailInstance = null;
    }

    private void KillVisualTweens()
    {
        transform.DOKill(false);
        if (_animator != null)
        {
            _animator.transform.DOKill(false);
        }

        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill(false);
        }

        if (_displaySpriteRenderer != null && _displaySpriteRenderer != _spriteRenderer)
        {
            _displaySpriteRenderer.DOKill(false);
        }
    }

    private static async UniTask WaitTweenAsync(Tween tween, CancellationToken token)
    {
        if (tween == null)
        {
            return;
        }

        UniTaskCompletionSource utcs = new UniTaskCompletionSource();
        tween.OnComplete(() => utcs.TrySetResult());
        tween.OnKill(() => utcs.TrySetResult());
        await utcs.Task.AttachExternalCancellation(token);
    }

    private void OnSelectedChanged(bool selected)
    {
        if (_targetMarker != null)
        {
            _targetMarker.SetActive(selected);
        }
    }

    /// <summary>
    /// マーカーが敵本体の当たり判定を遮らないようにする。
    /// </summary>
    private static void DisableRaycastOnMarkerHierarchy(GameObject markerRoot)
    {
        Collider2D[] colliders = markerRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void ClearSelectionBind()
    {
        if (_selectionSubscription != null)
        {
            _selectionSubscription.Dispose();
            _selectionSubscription = null;
        }
    }
}
