using System;
using System.Collections.Generic;
using DG.Tweening;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerModel / EnemyModel など、ReactiveProperty で表現された現在HPと最大HPを Slider に同期する。
/// 変化時は DOTween でスライダー（および任意の HP ラベル）を補間する。
/// </summary>
[RequireComponent(typeof(Slider))]
public sealed class HpReactiveSliderBinder : MonoBehaviour
{
    [SerializeField]
    private Slider _slider;

    [SerializeField]
    [Min(0f)]
    [Tooltip("0 で即時反映。ヒットストップ中は timeScale=0 のためゲージも一時停止する。")]
    private float _tweenDuration = 0.4f;

    [SerializeField]
    private Ease _tweenEase = Ease.OutCubic;

    private TextMeshProUGUI _hpLabel;
    private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
    private Tweener _sliderTween;
    private Tweener _hpLabelTween;
    private float _animatedHp;
    private int _targetCurrentHp;
    private int _targetMaxHp;
    private bool _hasDisplayState;

    private void Awake()
    {
        if (_slider == null)
        {
            _slider = GetComponent<Slider>();
        }

        if (_slider == null)
        {
            Debug.LogError("[HpReactiveSliderBinder] Slider が見つかりません。", this);
        }
    }

    private void OnDestroy()
    {
        Unbind();
    }

    /// <summary>
    /// HPのReactivePropertyにバインドする（再呼び出しで前回の購読は解除される）。
    /// </summary>
    /// <param name="currentHp">現在HP。</param>
    /// <param name="maxHp">最大HP。</param>
    /// <param name="hpLabel">任意。指定時はスライダーと同期して数値を補間表示する。</param>
    public void Bind(ReactiveProperty<int> currentHp, ReactiveProperty<int> maxHp, TextMeshProUGUI hpLabel = null)
    {
        Unbind();
        _hpLabel = hpLabel;
        if (_slider == null || currentHp == null || maxHp == null)
        {
            return;
        }

        SnapTo(currentHp.Value, maxHp.Value);
        _subscriptions.Add(currentHp.Subscribe(v => OnHpChanged(v, maxHp.Value)));
        _subscriptions.Add(maxHp.Subscribe(v => OnHpChanged(currentHp.Value, v)));
    }

    /// <summary>
    /// 表示をモデル値へ補間する（目標値が変わらない場合は何もしない）。
    /// </summary>
    public void TweenTo(int currentHp, int maxHp)
    {
        if (_slider == null)
        {
            return;
        }

        if (_hasDisplayState && _targetCurrentHp == currentHp && _targetMaxHp == maxHp)
        {
            return;
        }

        _targetCurrentHp = currentHp;
        _targetMaxHp = maxHp;
        float targetRatio = ToRatio(currentHp, maxHp);

        KillTweens();

        if (_tweenDuration <= 0f)
        {
            SnapTo(currentHp, maxHp);
            return;
        }

        _sliderTween = _slider
            .DOValue(targetRatio, _tweenDuration)
            .SetEase(_tweenEase)
            .SetLink(gameObject);

        if (_hpLabel != null)
        {
            float displayHp = _animatedHp;
            _hpLabelTween = DOTween
                .To(() => displayHp, value =>
                {
                    displayHp = value;
                    _animatedHp = value;
                    UpdateHpLabel(Mathf.RoundToInt(value), maxHp);
                }, currentHp, _tweenDuration)
                .SetEase(_tweenEase)
                .SetLink(gameObject);
        }

        _hasDisplayState = true;
    }

    private void OnHpChanged(int currentHp, int maxHp)
    {
        TweenTo(currentHp, maxHp);
    }

    /// <summary>
    /// 購読を解除し、スライダーを0に戻す。
    /// </summary>
    public void Unbind()
    {
        for (int i = 0; i < _subscriptions.Count; i++)
        {
            _subscriptions[i].Dispose();
        }

        _subscriptions.Clear();
        KillTweens();
        _hpLabel = null;
        _hasDisplayState = false;

        if (_slider != null)
        {
            _slider.value = 0f;
        }
    }

    private void SnapTo(int currentHp, int maxHp)
    {
        KillTweens();
        _targetCurrentHp = currentHp;
        _targetMaxHp = maxHp;
        _animatedHp = currentHp;
        _hasDisplayState = true;

        if (_slider != null)
        {
            _slider.value = ToRatio(currentHp, maxHp);
        }

        UpdateHpLabel(currentHp, maxHp);
    }

    private void KillTweens()
    {
        if (_sliderTween != null && _sliderTween.IsActive())
        {
            _sliderTween.Kill();
        }

        _sliderTween = null;

        if (_hpLabelTween != null && _hpLabelTween.IsActive())
        {
            _hpLabelTween.Kill();
        }

        _hpLabelTween = null;
    }

    private void UpdateHpLabel(int currentHp, int maxHp)
    {
        if (_hpLabel == null)
        {
            return;
        }

        _hpLabel.text = $"{currentHp} / {maxHp}";
    }

    private static float ToRatio(int currentHp, int maxHp)
    {
        if (maxHp <= 0)
        {
            return 0f;
        }

        float ratio = (float)currentHp / maxHp;
        if (ratio < 0f)
        {
            return 0f;
        }

        return ratio > 1f ? 1f : ratio;
    }
}
