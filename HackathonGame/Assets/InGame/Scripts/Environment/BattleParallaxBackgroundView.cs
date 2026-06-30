using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// バトル背景（Far/Mid/Near）のパララックス移動と演出速度を担当する。
/// </summary>
public sealed class BattleParallaxBackgroundView : MonoBehaviour
{
    [SerializeField]
    private Transform _farLayer;

    [SerializeField]
    private Transform _midLayer;

    [SerializeField]
    private Transform _nearLayer;

    [SerializeField]
    private float _parallaxFarMultiplier = 0.1f;

    [SerializeField]
    private float _parallaxMidMultiplier = 0.5f;

    [SerializeField]
    private float _parallaxNearMultiplier = 1.2f;

    [SerializeField]
    private float _moveDuration = 0.3f;

    [SerializeField]
    private Ease _moveEase = Ease.OutCubic;

    private Vector3 _farBaseLocal;
    private Vector3 _midBaseLocal;
    private Vector3 _nearBaseLocal;

    private void Awake()
    {
        if (_farLayer == null || _midLayer == null || _nearLayer == null)
        {
            Debug.LogError("[BattleParallaxBackgroundView] Far/Mid/Near の参照が未設定です。", this);
            return;
        }

        _farBaseLocal = _farLayer.localPosition;
        _midBaseLocal = _midLayer.localPosition;
        _nearBaseLocal = _nearLayer.localPosition;
    }

    /// <summary>
    /// キャラクター列と同じ符号のスクロール量に合わせ、各背景レイヤーを目標ローカル座標へTweenする。
    /// </summary>
    /// <param name="scrollSigned">
    /// 基準スクロール（例: -selectedIndex * キャラ側ScrollUnit）。0で初期位置へ戻す。
    /// </param>
    /// <param name="token">キャンセル。</param>
    /// <returns>Tween完了まで。</returns>
    public async UniTask MoveToScrollAsync(float scrollSigned, CancellationToken token)
    {
        if (_farLayer == null || _midLayer == null || _nearLayer == null)
        {
            return;
        }

        Vector3 farTarget = new Vector3(
            _farBaseLocal.x + scrollSigned * _parallaxFarMultiplier,
            _farBaseLocal.y,
            _farBaseLocal.z);
        Vector3 midTarget = new Vector3(
            _midBaseLocal.x + scrollSigned * _parallaxMidMultiplier,
            _midBaseLocal.y,
            _midBaseLocal.z);
        Vector3 nearTarget = new Vector3(
            _nearBaseLocal.x + scrollSigned * _parallaxNearMultiplier,
            _nearBaseLocal.y,
            _nearBaseLocal.z);

        UniTaskCompletionSource completionSource = new UniTaskCompletionSource();
        Sequence sequence = DOTween.Sequence();
        sequence.Join(_farLayer.DOLocalMove(farTarget, _moveDuration).SetEase(_moveEase));
        sequence.Join(_midLayer.DOLocalMove(midTarget, _moveDuration).SetEase(_moveEase));
        sequence.Join(_nearLayer.DOLocalMove(nearTarget, _moveDuration).SetEase(_moveEase));
        sequence.OnComplete(() => completionSource.TrySetResult());
        sequence.OnKill(() => completionSource.TrySetResult());
        try
        {
            await completionSource.Task.AttachExternalCancellation(token);
        }
        catch (OperationCanceledException)
        {
            if (sequence.IsActive())
            {
                sequence.Kill();
            }

            throw;
        }
    }
}
