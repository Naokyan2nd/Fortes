using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 画面全体（カメラ）用のグリッチシェーダーのマテリアルを
/// C#から制御してダメージ演出などを作成するスクリプト。
/// </summary>
public class CameraGlitchManager : MonoBehaviour
{
    [Header("Settings - Material")]
    [Tooltip("Universal Renderer Featureにセットしてある、画面全体用のグリッチマテリアルをセットしてください。")]
    [SerializeField]
    private Material _glitchMaterial;

    [Header("Settings - Damage Trigger")]
    [Tooltip("ダメージ時の最大横揺れ幅。")]
    [SerializeField]
    private float _damageStrength = 0.08f;

    [Tooltip("ダメージ時の最大色収差の幅。")]
    [SerializeField]
    private float _damageAberration = 0.08f;

    [Tooltip("激しいバグ状態を一瞬キープする時間（秒）。")]
    [SerializeField]
    private float _holdDuration = 0.15f;

    [Tooltip("激しいバグから元の状態にしっとり戻るまでの時間（秒）。")]
    [SerializeField]
    private float _decayDuration = 0.3f;

    [Header("Settings - Wave Transition")]
    [SerializeField]
    private float _waveStrength = 0.2f;

    [SerializeField]
    private float _waveAberration = 0.15f;

    [Header("Settings - Default State (たまにバグる状態)")]
    [Tooltip("Blackboardのデフォルト値に合わせてください (例: 0.01)")]
    [SerializeField]
    private float _defaultStrength = 0.01f;

    [Tooltip("Blackboardのデフォルト値に合わせてください (例: 0.02)")]
    [SerializeField]
    private float _defaultAberration = 0.02f;

    [Tooltip("Blackboardのデフォルト値に合わせてください (0で常時ON, 0.8などでたまにON)")]
    [SerializeField]
    private float _defaultRarity = 0.8f;

    private static readonly int GlitchStrengthId = Shader.PropertyToID("_GlitchStrength");
    private static readonly int AberrationStrengthId = Shader.PropertyToID("_AberrationStrength");
    private static readonly int GlitchRarityId = Shader.PropertyToID("_GlitchRarity");

    private bool _waveGlitchRunning;

    private void Awake()
    {
        if (_glitchMaterial == null)
        {
            Debug.LogError("[CameraGlitchManager] グリッチマテリアルがセットされていません。");
            enabled = false;
            return;
        }

        ResetShaderState();
    }

    /// <summary>ダメージを受けた瞬間のグリッチ演出。</summary>
    [ContextMenu("Trigger Damage Glitch")]
    public void TriggerDamageGlitch()
    {
        if (!isActiveAndEnabled || _glitchMaterial == null)
        {
            return;
        }

        PlayDamageGlitchAsync(destroyCancellationToken).Forget();
    }

    /// <summary>
    /// ウェーブ遷移用の強めグリッチ。Hold 中の指定割合で onPeak を1回呼ぶ。
    /// </summary>
    public async UniTask PlayWaveTransitionGlitchAsync(
        float holdSeconds,
        float decaySeconds,
        float spawnAtHoldNormalized,
        Action onPeak,
        CancellationToken token)
    {
        if (_glitchMaterial == null)
        {
            onPeak?.Invoke();
            return;
        }

        while (_waveGlitchRunning)
        {
            await UniTask.Yield(token);
        }

        _waveGlitchRunning = true;
        try
        {
            float hold = Mathf.Max(0f, holdSeconds);
            float decay = Mathf.Max(0f, decaySeconds);
            float spawnNorm = Mathf.Clamp01(spawnAtHoldNormalized);
            bool peakInvoked = false;

            _glitchMaterial.SetFloat(GlitchRarityId, 0f);
            _glitchMaterial.SetFloat(GlitchStrengthId, _waveStrength);
            _glitchMaterial.SetFloat(AberrationStrengthId, _waveAberration);

            if (hold > 0f && spawnNorm > 0f)
            {
                int prePeakMs = Mathf.RoundToInt(hold * spawnNorm * 1000f);
                if (prePeakMs > 0)
                {
                    await UniTask.Delay(prePeakMs, cancellationToken: token);
                }

                if (!peakInvoked)
                {
                    peakInvoked = true;
                    onPeak?.Invoke();
                }

                int remainMs = Mathf.RoundToInt(hold * 1000f) - prePeakMs;
                if (remainMs > 0)
                {
                    await UniTask.Delay(remainMs, cancellationToken: token);
                }
            }
            else
            {
                if (!peakInvoked)
                {
                    peakInvoked = true;
                    onPeak?.Invoke();
                }

                if (hold > 0f)
                {
                    await UniTask.Delay(Mathf.RoundToInt(hold * 1000f), cancellationToken: token);
                }
            }

            float elapsed = 0f;
            while (elapsed < decay)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = decay > 0f ? Mathf.Clamp01(elapsed / decay) : 1f;

                _glitchMaterial.SetFloat(GlitchRarityId, Mathf.Lerp(0f, _defaultRarity, t));
                _glitchMaterial.SetFloat(GlitchStrengthId, Mathf.Lerp(_waveStrength, _defaultStrength, t));
                _glitchMaterial.SetFloat(AberrationStrengthId, Mathf.Lerp(_waveAberration, _defaultAberration, t));

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            ResetShaderState();
        }
        finally
        {
            _waveGlitchRunning = false;
        }
    }

    private async UniTaskVoid PlayDamageGlitchAsync(CancellationToken token)
    {
        if (_waveGlitchRunning)
        {
            return;
        }

        _waveGlitchRunning = true;
        try
        {
            _glitchMaterial.SetFloat(GlitchRarityId, 0f);
            _glitchMaterial.SetFloat(GlitchStrengthId, _damageStrength);
            _glitchMaterial.SetFloat(AberrationStrengthId, _damageAberration);

            int holdMs = Mathf.Max(0, Mathf.RoundToInt(_holdDuration * 1000f));
            if (holdMs > 0)
            {
                await UniTask.Delay(holdMs, cancellationToken: token);
            }

            float elapsed = 0f;
            while (elapsed < _decayDuration)
            {
                token.ThrowIfCancellationRequested();
                elapsed += Time.deltaTime;
                float t = _decayDuration > 0f ? Mathf.Clamp01(elapsed / _decayDuration) : 1f;

                _glitchMaterial.SetFloat(GlitchRarityId, Mathf.Lerp(0f, _defaultRarity, t));
                _glitchMaterial.SetFloat(GlitchStrengthId, Mathf.Lerp(_damageStrength, _defaultStrength, t));
                _glitchMaterial.SetFloat(AberrationStrengthId, Mathf.Lerp(_damageAberration, _defaultAberration, t));

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            ResetShaderState();
        }
        catch (OperationCanceledException)
        {
            // 破棄時
        }
        finally
        {
            _waveGlitchRunning = false;
        }
    }

    private void ResetShaderState()
    {
        if (_glitchMaterial == null)
        {
            return;
        }

        _glitchMaterial.SetFloat(GlitchRarityId, _defaultRarity);
        _glitchMaterial.SetFloat(GlitchStrengthId, _defaultStrength);
        _glitchMaterial.SetFloat(AberrationStrengthId, _defaultAberration);
    }
}
