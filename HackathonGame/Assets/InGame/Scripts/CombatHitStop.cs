using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// ヒットストップの種別（長さは <see cref="CombatHitStopPresenter"/> で切り替え）。
/// </summary>
public enum CombatHitStopKind
{
    /// <summary>プレイヤー攻撃の敵着弾。</summary>
    AttackImpact,

    /// <summary>敵攻撃のプレイヤー被弾。</summary>
    PlayerDamage,
}

/// <summary>
/// 着弾時の短いハードストップ（Time.timeScale = 0、UnscaledTime で復帰）。
/// 並列着弾（AP 全体攻撃など）は1本のフリーズにまとめる。
/// </summary>
public sealed class CombatHitStopPresenter : MonoBehaviour
{
    [SerializeField]
    private bool _enabled = true;

    [SerializeField]
    [Min(0f)]
    [Tooltip("プレイヤー攻撃の敵着弾時の停止時間（実時間・秒）。")]
    private float _attackImpactSeconds = 0.05f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("敵攻撃のプレイヤー被弾時の停止時間（実時間・秒）。")]
    private float _playerDamageSeconds = 0.03f;

    private readonly object _gate = new object();

    private bool _isFreezing;
    private float _savedTimeScale = 1f;
    private float _freezeEndUnscaledTime;
    private UniTaskCompletionSource _freezeTcs;

    /// <summary>
    /// 種別に応じたヒットストップを再生する。無効・0秒の場合は即完了。
    /// </summary>
    public UniTask PlayAsync(CombatHitStopKind kind, CancellationToken token)
    {
        if (!_enabled)
        {
            return UniTask.CompletedTask;
        }

        float duration = kind == CombatHitStopKind.AttackImpact
            ? _attackImpactSeconds
            : _playerDamageSeconds;
        if (duration <= 0f)
        {
            return UniTask.CompletedTask;
        }

        return RequestFreezeAsync(duration, token);
    }

    private UniTask RequestFreezeAsync(float unscaledSeconds, CancellationToken token)
    {
        UniTask freezeTask;
        lock (_gate)
        {
            float requestedEnd = Time.unscaledTime + unscaledSeconds;
            if (requestedEnd > _freezeEndUnscaledTime)
            {
                _freezeEndUnscaledTime = requestedEnd;
            }

            if (!_isFreezing)
            {
                _isFreezing = true;
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _freezeTcs = new UniTaskCompletionSource();
                RunFreezeAsync(_freezeTcs, token).Forget();
            }

            freezeTask = _freezeTcs.Task;
        }

        return freezeTask.AttachExternalCancellation(token);
    }

    private async UniTaskVoid RunFreezeAsync(UniTaskCompletionSource tcs, CancellationToken token)
    {
        try
        {
            while (true)
            {
                float end;
                lock (_gate)
                {
                    end = _freezeEndUnscaledTime;
                }

                if (Time.unscaledTime >= end)
                {
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        finally
        {
            EndFreeze(tcs);
        }
    }

    private void EndFreeze(UniTaskCompletionSource tcs)
    {
        lock (_gate)
        {
            if (_isFreezing)
            {
                Time.timeScale = _savedTimeScale;
                _isFreezing = false;
                _freezeEndUnscaledTime = 0f;
            }

            tcs.TrySetResult();
        }
    }

    private void OnDisable()
    {
        lock (_gate)
        {
            if (!_isFreezing)
            {
                return;
            }

            Time.timeScale = _savedTimeScale;
            _isFreezing = false;
            _freezeEndUnscaledTime = 0f;
            _freezeTcs?.TrySetResult();
        }
    }
}
