using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 敵ステージ（キャラクター列）のフォーカス移動と、背景パララックスの同期起動。
/// </summary>
public sealed class TargetSelectView : MonoBehaviour
{
    [SerializeField]
    private Transform _enemyGroup;

    [SerializeField]
    private BattleParallaxBackgroundView _parallaxBackground;

    [Header("Focus mode")]
    [SerializeField]
    [Tooltip("ScrollEnemyAndParallax: 敵列を中央へ移動＋背景パララックス（従来）。ParallaxOnly: 敵は動かさず背景のみパララックス。")]
    private TargetSelectFocusMode _focusMode = TargetSelectFocusMode.ScrollEnemyAndParallax;

    [SerializeField]
    private float _characterScrollUnit = 1.5f;

    [SerializeField]
    private float _enemyMoveDuration = 0.3f;

    [SerializeField]
    private Ease _enemyMoveEase = Ease.OutCubic;

    private Vector3 _enemyBaseLocal;

    private void Awake()
    {
        if (_enemyGroup == null)
        {
            Debug.LogError("[TargetSelectView] _enemyGroup が未設定です。", this);
            return;
        }

        _enemyBaseLocal = _enemyGroup.localPosition;
    }

    /// <summary>
    /// 選択スロットに合わせてステージと背景を移動する。
    /// </summary>
    /// <param name="selectedIndex">敵スロット（0〜）。</param>
    /// <param name="token">キャンセル。</param>
    /// <returns>完了待ち。</returns>
    public async UniTask FocusEnemyAsync(int selectedIndex, CancellationToken token)
    {
        if (_enemyGroup == null)
        {
            return;
        }

        float scroll = -1f * selectedIndex * _characterScrollUnit;
        await RunFocusAsync(scroll, token);
    }

    /// <summary>
    /// ステージを初期位置へ戻す。
    /// </summary>
    /// <param name="token">キャンセル。</param>
    /// <returns>完了待ち。</returns>
    public async UniTask ResetStageAsync(CancellationToken token)
    {
        if (_enemyGroup == null)
        {
            return;
        }

        await RunFocusAsync(0f, token);
    }

    private async UniTask RunFocusAsync(float scrollSigned, CancellationToken token)
    {
        UniTask parallaxTask = _parallaxBackground != null
            ? _parallaxBackground.MoveToScrollAsync(scrollSigned, token)
            : UniTask.CompletedTask;

        if (_focusMode == TargetSelectFocusMode.ParallaxOnly)
        {
            await parallaxTask;
            return;
        }

        Vector3 enemyTarget = new Vector3(_enemyBaseLocal.x + scrollSigned, _enemyBaseLocal.y, _enemyBaseLocal.z);

        UniTaskCompletionSource enemyDone = new UniTaskCompletionSource();
        Tweener enemyTween = _enemyGroup.DOLocalMove(enemyTarget, _enemyMoveDuration).SetEase(_enemyMoveEase);
        enemyTween.OnComplete(() => enemyDone.TrySetResult());
        enemyTween.OnKill(() => enemyDone.TrySetResult());

        UniTask enemyTask = enemyDone.Task.AttachExternalCancellation(token);

        try
        {
            await UniTask.WhenAll(enemyTask, parallaxTask);
        }
        catch (OperationCanceledException)
        {
            if (enemyTween.IsActive())
            {
                enemyTween.Kill();
            }

            throw;
        }
    }
}
