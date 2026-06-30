using System;
using DG.Tweening;
using UnityEngine;

/// <summary>SweepStrip 横スイープ演出のタイミング。</summary>
[Serializable]
public sealed class SweepMotionSettings
{
    [Tooltip("入退場の X オフセット量。Y / サイズ / スケールは SweepStrip の RectTransform で指定。")]
    [SerializeField]
    private float _enterOffsetX = -1400f;

    [SerializeField]
    private float _exitOffsetX = 1400f;

    [SerializeField]
    private float _enterDuration = 0.25f;

    [SerializeField]
    private float _holdDuration = 0.6f;

    [SerializeField]
    private float _exitDuration = 0.35f;

    [SerializeField]
    private float _fadeInDuration = 0.05f;

    [SerializeField]
    private Ease _enterEase = Ease.OutQuad;

    [SerializeField]
    private Ease _exitEase = Ease.InQuad;

    public float EnterOffsetX => _enterOffsetX;
    public float ExitOffsetX => _exitOffsetX;
    public float EnterDuration => _enterDuration;
    public float HoldDuration => _holdDuration;
    public float ExitDuration => _exitDuration;
    public float FadeInDuration => _fadeInDuration;
    public Ease EnterEase => _enterEase;
    public Ease ExitEase => _exitEase;
}

/// <summary>WaveMark 中央表示演出のタイミング。</summary>
[Serializable]
public sealed class WaveMarkRevealSettings
{
    [Tooltip("表示開始時のスケール倍率（基準スケール = ヒエラルキーの localScale）。")]
    [SerializeField]
    private float _popScaleFrom = 1.25f;

    [SerializeField]
    private float _fadeInDuration = 0.1f;

    [SerializeField]
    private float _holdDuration = 0.35f;

    [SerializeField]
    private float _fadeOutDuration = 0.15f;

    [SerializeField]
    private Ease _fadeInEase = Ease.OutBack;

    public float PopScaleFrom => _popScaleFrom;
    public float FadeInDuration => _fadeInDuration;
    public float HoldDuration => _holdDuration;
    public float FadeOutDuration => _fadeOutDuration;
    public Ease FadeInEase => _fadeInEase;
}
