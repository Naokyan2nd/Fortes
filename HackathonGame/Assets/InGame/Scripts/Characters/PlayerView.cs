using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// プレイヤーの簡易演出待ち用。
/// </summary>
[DefaultExecutionOrder(10)]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class PlayerView : MonoBehaviour
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
    private bool _suppressDamagePunchWhenAnimated = true;

    [Header("Floating text")]
    [SerializeField]
    [Tooltip("未設定時は SpriteRenderer.bounds の上端中央。")]
    private Transform _floatingTextAnchor;

    [Header("Combat hit point (enemy attack)")]
    [SerializeField]
    [Tooltip("敵攻撃 Trail の着弾点・被弾ヒット VFX の発生アンカー（Player 直下の CombatHitPoint 推奨）。未設定時は子名 CombatHitPoint を検索、なければ bounds 中心。")]
    private Transform _combatHitPoint;

    [Header("Player attack trail emit")]
    [SerializeField]
    [Tooltip("単体攻撃 Trail の発射アンカー（子 AttackTrailEmit 推奨）。未設定時は子名を検索。")]
    private Transform _attackTrailEmitPoint;

    [Header("Damage hit feedback")]
    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    [SerializeField]
    private float _damagePunchStrength = 0.2f;

    [SerializeField]
    private float _damagePunchDuration = 0.4f;

    [SerializeField]
    [Min(0)]
    private int _damagePunchVibrato = 12;

    [SerializeField]
    [Range(0f, 1f)]
    private float _damagePunchElasticity = 0.45f;

    [SerializeField]
    [Tooltip("CombatHitPoint の子に生成。再生はプレハブ側の Play On Awake に任せる。")]
    private GameObject _damageHitVfxPrefab;

    [SerializeField]
    [Min(0f)]
    private float _damageHitVfxDestroyDelay = 1.2f;

    [Header("Heal feedback")]
    [SerializeField]
    [Tooltip("回復 VFX の発生アンカー（子 HealVfxAnchor 推奨）。未設定時は CombatHitPoint。")]
    private Transform _healVfxAnchor;

    [SerializeField]
    [Tooltip("Heal Vfx Anchor の子に生成。再生はプレハブ側の Play On Awake に任せる。")]
    private GameObject _healVfxPrefab;

    [SerializeField]
    [Min(0f)]
    private float _healVfxDestroyDelay = 1.2f;

    [Header("Multiplier charge")]
    [SerializeField]
    [Tooltip("QTE 倍率チャージの着弾点（Player 直下の子オブジェクト推奨）。未設定時は子名 MultiplierChargeAnchor を検索。")]
    private Transform _multiplierChargeAnchor;

    [SerializeField]
    [Range(0.05f, 1f)]
    private float _chargePunchStrength = 0.18f;

    [SerializeField]
    [Range(0.05f, 0.6f)]
    private float _chargePunchDuration = 0.22f;

    [SerializeField]
    [Min(1)]
    private int _chargePunchVibrato = 8;

    [SerializeField]
    [Range(0f, 1f)]
    private float _chargePunchElasticity = 0.55f;

    [SerializeField]
    [Tooltip("倍率チャージ確定時に MultiplierChargeAnchor の子へ生成。再生はプレハブ側の Play On Awake に任せる。")]
    private GameObject _multiplierChargeVfxPrefab;

    [SerializeField]
    [Min(0f)]
    private float _multiplierChargeVfxDestroyDelay = 1.2f;

    [Header("Target selection")]
    [SerializeField]
    private GameObject _targetMarker;

    private GameObject _boundVisualRoot;

    private void Awake()
    {
        EnsureVisualBindings();
        EnsureUtilityAnchors();
        EnsureTargetMarker();
        if (_boundVisualRoot != null)
        {
            RefreshAnimatorPlayback();
        }
    }

    /// <summary>回復スキル選択中のターゲットマーカー表示を切り替える。</summary>
    public void SetTargetMarkerVisible(bool visible)
    {
        if (_targetMarker != null)
        {
            _targetMarker.SetActive(visible);
        }
    }

    /// <summary>
    /// <see cref="PlayerInGameOutfitVisual"/> が装備プレハブを差し替えた後に Animator / SpriteRenderer を再接続する。
    /// </summary>
    public void BindVisualRoot(GameObject visualRoot)
    {
        _boundVisualRoot = visualRoot;
        EnsureVisualBindings();
        RefreshAnimatorPlayback();
    }

    void RefreshAnimatorPlayback()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.enabled = true;
        _animator.Rebind();
        _animator.Update(0f);
    }

    void EnsureVisualBindings()
    {
        if (_boundVisualRoot != null)
        {
            _animator = _boundVisualRoot.GetComponent<Animator>();
            if (_animator == null)
            {
                _animator = _boundVisualRoot.GetComponentInChildren<Animator>(true);
            }

            _spriteRenderer = FindPrimarySpriteRenderer(_boundVisualRoot);
        }
        else
        {
            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }
        }

        BoxCollider2D boxCollider2D = GetComponent<BoxCollider2D>();
        if (_spriteRenderer == null && _boundVisualRoot == null)
        {
            Debug.LogWarning("[PlayerView] SpriteRenderer が見つかりません。", this);
        }

        if (boxCollider2D == null)
        {
            Debug.LogError("[PlayerView] BoxCollider2D が未設定です。", this);
        }

    }

    static SpriteRenderer FindPrimarySpriteRenderer(GameObject visualRoot)
    {
        if (visualRoot == null)
        {
            return null;
        }

        SpriteRenderer onRoot = visualRoot.GetComponent<SpriteRenderer>();
        if (onRoot != null && onRoot.sprite != null)
        {
            return onRoot;
        }

        SpriteRenderer[] renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sprite != null)
            {
                return renderers[i];
            }
        }

        return renderers.Length > 0 ? renderers[0] : null;
    }

    void EnsureUtilityAnchors()
    {
        if (_combatHitPoint == null)
        {
            _combatHitPoint = transform.parent != null
                ? transform.parent.Find("CombatHitPoint")
                : transform.Find("CombatHitPoint");
        }

        if (_attackTrailEmitPoint == null)
        {
            _attackTrailEmitPoint = transform.parent != null
                ? transform.parent.Find("AttackTrailEmit")
                : transform.Find("AttackTrailEmit");
        }

        if (_healVfxAnchor == null)
        {
            _healVfxAnchor = transform.parent != null
                ? transform.parent.Find("HealVfxAnchor")
                : transform.Find("HealVfxAnchor");
        }

        if (_multiplierChargeAnchor == null)
        {
            _multiplierChargeAnchor = transform.parent != null
                ? transform.parent.Find("MultiplierChargeAnchor")
                : transform.Find("MultiplierChargeAnchor");
        }

        if (_floatingTextAnchor == null && transform.parent != null)
        {
            Transform anchor = transform.parent.Find("FloatingTextAnchor");
            if (anchor != null)
            {
                _floatingTextAnchor = anchor;
            }
        }
    }

    private void EnsureTargetMarker()
    {
        if (_targetMarker == null)
        {
            Transform found = transform.Find("TargetMarker");
            if (found != null)
            {
                _targetMarker = found.gameObject;
            }
        }

        if (_targetMarker == null)
        {
            return;
        }

        _targetMarker.SetActive(false);
        DisableRaycastOnMarkerHierarchy(_targetMarker);
    }

    private static void DisableRaycastOnMarkerHierarchy(GameObject markerRoot)
    {
        Collider2D[] colliders = markerRoot.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private void OnDestroy()
    {
        KillVisualTweens();
    }

    /// <summary>FloatingText 用ワールドアンカー。</summary>
    public Transform FloatingTextAnchor => _floatingTextAnchor != null ? _floatingTextAnchor : transform;

    /// <summary>敵攻撃の着弾・被弾 VFX 用 Transform（未設定時は本体）。</summary>
    public Transform CombatHitPoint => ResolveCombatHitPoint();

    /// <summary>敵攻撃 Trail の着弾ワールド座標。</summary>
    public Vector3 GetCombatHitWorldPosition()
    {
        return ResolveCombatHitPoint().position;
    }

    private Transform ResolveCombatHitPoint()
    {
        if (_combatHitPoint != null)
        {
            return _combatHitPoint;
        }

        return transform;
    }

    /// <summary>FloatingText スポーン位置（ワールド）。</summary>
    public Vector3 GetFloatingTextWorldPosition()
    {
        if (_floatingTextAnchor != null)
        {
            return _floatingTextAnchor.position;
        }

        if (_spriteRenderer == null)
        {
            return transform.position;
        }

        Bounds bounds = _spriteRenderer.bounds;
        return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
    }

    /// <summary>
    /// 発射点から敵着弾点までの Trail 飛行時間（秒）を算出する。
    /// </summary>
    public float ComputeAttackTrailFlightDuration(PlayerAttackTrailSettings settings, EnemyView enemyView)
    {
        if (settings == null || enemyView == null)
        {
            return 0.05f;
        }

        Transform emit = ResolveAttackTrailEmitPoint();
        Vector3 emitWorld = emit.position;
        Vector3 targetWorld = enemyView.EnemyCombatHitPoint.position;
        return settings.ComputeFlightDuration(emitWorld, targetWorld);
    }

    /// <summary>QTE 演奏ポーズ（isPlaying）。発火のみ。</summary>
    public UniTask PlayQtePerformAsync(CancellationToken token)
    {
        FireTrigger(PlayerAnimatorTriggers.Playing);
        return UniTask.CompletedTask;
    }

    /// <summary>敗北モーション（isLost）。発火のみ。</summary>
    public void PlayLost()
    {
        FireTrigger(PlayerAnimatorTriggers.Lost);
    }

    /// <summary>倍率チャージ吸収の着弾ワールド座標。</summary>
    public Vector3 GetMultiplierChargeWorldPosition()
    {
        Transform anchor = ResolveMultiplierChargeAnchor();
        if (anchor != null)
        {
            return anchor.position;
        }

        if (_spriteRenderer != null)
        {
            return _spriteRenderer.bounds.center;
        }

        return ResolveCombatHitPoint().position;
    }

    private Transform ResolveMultiplierChargeAnchor()
    {
        if (_multiplierChargeAnchor != null)
        {
            return _multiplierChargeAnchor;
        }

        Transform found = transform.Find("MultiplierChargeAnchor");
        if (found != null)
        {
            return found;
        }

        if (transform.parent != null)
        {
            found = transform.parent.Find("MultiplierChargeAnchor");
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>倍率チャージ確定時のワンショット VFX（アンカー子に生成）。</summary>
    public void SpawnMultiplierChargeVfx()
    {
        SpawnOneShotVfxAtAnchor(
            ResolveMultiplierChargeAnchor(),
            _multiplierChargeVfxPrefab,
            _multiplierChargeVfxDestroyDelay,
            "Multiplier Charge Vfx");
    }

    /// <summary>倍率チャージ到達時のスケールパンチ。</summary>
    public async UniTask PlayMultiplierChargeReactionAsync(CancellationToken token)
    {
        Transform punchTarget = ResolveMultiplierChargeAnchor();
        if (punchTarget == null)
        {
            punchTarget = _boundVisualRoot != null ? _boundVisualRoot.transform : transform;
        }
        KillVisualTweens();
        Tweener punch = punchTarget
            .DOPunchScale(
                Vector3.one * _chargePunchStrength,
                _chargePunchDuration,
                _chargePunchVibrato,
                _chargePunchElasticity)
            .SetUpdate(true);
        await WaitTweenAsync(punch, token);
    }

    /// <summary>回復 VFX（Heal Vfx Anchor の子に生成。Play On Awake 任せ）。</summary>
    public void SpawnHealVfx()
    {
        SpawnOneShotVfxAtAnchor(
            ResolveHealVfxAnchor(),
            _healVfxPrefab,
            _healVfxDestroyDelay,
            "Heal Vfx");
    }

    /// <summary>回復モーション（isHealing）と回復 VFX。モーション完了まで待機。</summary>
    public async UniTask PlayHealAsync(CancellationToken token)
    {
        SpawnHealVfx();

        if (!HasAnimator)
        {
            return;
        }

        FireTrigger(PlayerAnimatorTriggers.Healing);
        await WaitForStateCompleteAsync(BaseLayer, PlayerAnimatorStateNames.Heal, token);
    }

    /// <summary>被弾モーション（isReceivingDamage）とヒット VFX。</summary>
    public async UniTask PlayReceiveDamageAsync(CancellationToken token)
    {
        bool useAnimator = HasAnimator;
        if (useAnimator)
        {
            FireTrigger(PlayerAnimatorTriggers.ReceivingDamage);
        }

        bool suppressPunch = useAnimator && _suppressDamagePunchWhenAnimated;
        UniTask damageFxTask = PlayDamageHitFxAsync(suppressPunch, token);
        if (useAnimator)
        {
            await UniTask.WhenAll(
                damageFxTask,
                WaitForStateCompleteAsync(BaseLayer, PlayerAnimatorStateNames.ReceiveDamage, token));
        }
        else
        {
            await damageFxTask;
        }
    }

    /// <summary>攻撃モーション開始から Trail 遅延まで（全体攻撃の先頭 1 回用）。</summary>
    public async UniTask PlayAttackWindupAsync(CancellationToken token)
    {
        if (!HasAnimator)
        {
            return;
        }

        FireTrigger(PlayerAnimatorTriggers.Attacking);
        if (_attackTrailDelaySeconds > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_attackTrailDelaySeconds), cancellationToken: token);
        }
    }

    /// <summary>攻撃モーション → 遅延 → Trail → 着弾コールバック。</summary>
    public async UniTask PlayAttackWithTrailAsync(
        PlayerAttackTrailSettings settings,
        EnemyView enemyView,
        Func<CancellationToken, UniTask> onTrailArrivedAsync,
        CancellationToken token)
    {
        await PlayAttackWindupAsync(token);
        await PlayAttackTrailToEnemyCoreAsync(
            settings,
            enemyView,
            overrideFlightDuration: null,
            onTrailArrivedAsync,
            token);
    }

    /// <summary>
    /// 単体攻撃用の Trail を 1 本、敵着弾点へ直進させる。着弾コールバック完了まで待機する。
    /// </summary>
    public UniTask PlayAttackTrailToEnemyAsync(
        PlayerAttackTrailSettings settings,
        EnemyView enemyView,
        Func<CancellationToken, UniTask> onTrailArrivedAsync,
        CancellationToken token)
    {
        return PlayAttackTrailToEnemyCoreAsync(
            settings,
            enemyView,
            overrideFlightDuration: null,
            onTrailArrivedAsync,
            token);
    }

    /// <summary>
    /// 全体攻撃用の Trail を 1 本飛ばす（着弾コールバックなし）。
    /// <paramref name="overrideFlightDuration"/> 指定時は距離に関わらずその秒数で飛行する。
    /// </summary>
    public UniTask PlayAttackTrailToEnemyAsync(
        PlayerAttackTrailSettings settings,
        EnemyView enemyView,
        float? overrideFlightDuration,
        CancellationToken token)
    {
        return PlayAttackTrailToEnemyCoreAsync(
            settings,
            enemyView,
            overrideFlightDuration,
            onTrailArrivedAsync: null,
            token);
    }

    private async UniTask PlayAttackTrailToEnemyCoreAsync(
        PlayerAttackTrailSettings settings,
        EnemyView enemyView,
        float? overrideFlightDuration,
        Func<CancellationToken, UniTask> onTrailArrivedAsync,
        CancellationToken token)
    {
        if (settings == null || !settings.IsConfigured)
        {
            Debug.LogWarning("[PlayerView] Attack trail settings が未設定のためスキップします。", this);
            return;
        }

        if (enemyView == null)
        {
            Debug.LogWarning("[PlayerView] EnemyView が null のため Trail をスキップします。", this);
            return;
        }

        Transform enemyHitPoint = enemyView.EnemyCombatHitPoint;
        Transform emit = ResolveAttackTrailEmitPoint();
        Vector3 emitWorld = emit.position;
        Vector3 targetWorld = enemyHitPoint.position;
        float flightDuration = overrideFlightDuration ?? settings.ComputeFlightDuration(emitWorld, targetWorld);

        GameObject trailInstance = Instantiate(settings.TrailPrefab, emitWorld, Quaternion.identity);
        float scale = Random.Range(settings.ScaleMin, settings.ScaleMax);
        trailInstance.transform.localScale = Vector3.one * scale;

        bool cancelled = false;
        try
        {
            await FlyHomingTrailAsync(
                trailInstance.transform,
                enemyHitPoint,
                emitWorld,
                flightDuration,
                settings,
                token);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
        }

        if (cancelled || token.IsCancellationRequested)
        {
            if (trailInstance != null)
            {
                Destroy(trailInstance);
            }

            return;
        }

        await DestroyAttackTrailAfterDelayAsync(trailInstance, settings.DestroyDelay, token);

        if (onTrailArrivedAsync != null)
        {
            await onTrailArrivedAsync(token);
        }
    }

    /// <summary>
    /// ダメージを受けたときの横パンチ。
    /// </summary>
    public UniTask PlayDamageHitFxAsync(CancellationToken token)
    {
        return PlayDamageHitFxAsync(includePunch: true, token);
    }

    private async UniTask PlayDamageHitFxAsync(bool includePunch, CancellationToken token)
    {
        SpawnDamageHitVfx();
        if (!includePunch)
        {
            return;
        }

        KillVisualTweens();
        Tweener punch = transform.DOPunchPosition(
            new Vector3(_damagePunchStrength, 0f, 0f),
            _damagePunchDuration,
            _damagePunchVibrato,
            _damagePunchElasticity);
        await WaitTweenAsync(punch, token);
    }

    private bool HasAnimator => _animator != null;

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

    private Transform ResolveAttackTrailEmitPoint()
    {
        if (_attackTrailEmitPoint != null)
        {
            return _attackTrailEmitPoint;
        }

        Transform found = transform.Find("AttackTrailEmit");
        if (found != null)
        {
            return found;
        }

        return transform;
    }

    private Transform ResolveHealVfxAnchor()
    {
        if (_healVfxAnchor != null)
        {
            return _healVfxAnchor;
        }

        Transform parent = transform.parent;
        if (parent != null)
        {
            Transform foundOnParent = parent.Find("HealVfxAnchor");
            if (foundOnParent != null)
            {
                return foundOnParent;
            }
        }

        Transform found = transform.Find("HealVfxAnchor");
        if (found != null)
        {
            return found;
        }

        return ResolveCombatHitPoint();
    }

    /// <summary>
    /// 飛行時間で進行度を進め、着弾点 Transform を追尾しながら直進する。
    /// </summary>
    private static async UniTask FlyHomingTrailAsync(
        Transform trailTransform,
        Transform enemyHitPoint,
        Vector3 startPosition,
        float flightDuration,
        PlayerAttackTrailSettings settings,
        CancellationToken token)
    {
        if (trailTransform == null || enemyHitPoint == null)
        {
            return;
        }

        float duration = Mathf.Max(0.05f, flightDuration);
        Vector3 previousPosition = startPosition;
        trailTransform.position = startPosition;
        float elapsed = 0f;

        while (elapsed < duration && !token.IsCancellationRequested)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float moveT = settings.PathEase != Ease.Linear
                ? DOVirtual.EasedValue(0f, 1f, normalizedTime, settings.PathEase)
                : normalizedTime;

            Vector3 targetPosition = enemyHitPoint.position;
            Vector3 position = Vector3.Lerp(startPosition, targetPosition, moveT);

            trailTransform.position = position;
            ApplyTrailFacing2D(trailTransform, position - previousPosition);
            previousPosition = position;

            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        if (!token.IsCancellationRequested && trailTransform != null)
        {
            Vector3 finalTarget = enemyHitPoint.position;
            trailTransform.position = finalTarget;
            ApplyTrailFacing2D(trailTransform, finalTarget - previousPosition);
        }
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

    private static async UniTask DestroyAttackTrailAfterDelayAsync(
        GameObject trailInstance,
        float destroyDelay,
        CancellationToken token)
    {
        if (trailInstance == null)
        {
            return;
        }

        if (destroyDelay > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(destroyDelay), cancellationToken: token);
        }

        if (trailInstance == null)
        {
            return;
        }

        ParticleSystemStopBehavior stopBehavior = destroyDelay > 0f
            ? ParticleSystemStopBehavior.StopEmitting
            : ParticleSystemStopBehavior.StopEmittingAndClear;

        ParticleSystem[] particleSystems = trailInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Stop(true, stopBehavior);
        }

        Destroy(trailInstance);
    }

    private void SpawnDamageHitVfx()
    {
        SpawnOneShotVfxAtAnchor(
            ResolveCombatHitPoint(),
            _damageHitVfxPrefab,
            _damageHitVfxDestroyDelay,
            "Damage Hit Vfx");
    }

    private void SpawnOneShotVfxAtAnchor(
        Transform anchor,
        GameObject prefab,
        float destroyDelay,
        string label)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerView] {label} Prefab が未設定のためスキップします。", this);
            return;
        }

        if (anchor == null)
        {
            Debug.LogWarning($"[PlayerView] {label} Anchor が未設定のためスキップします。", this);
            return;
        }

        GameObject instance = Instantiate(prefab, anchor);
        instance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        DestroyOneShotVfxAfterDelayAsync(instance, destroyDelay, CancellationToken.None).Forget();
    }

    private async UniTask DestroyOneShotVfxAfterDelayAsync(
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

    private void KillVisualTweens()
    {
        transform.DOKill(false);
        if (_spriteRenderer != null)
        {
            _spriteRenderer.DOKill(false);
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
}
