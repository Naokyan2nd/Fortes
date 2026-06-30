using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// ウェーブ間遷移（余韻 → SweepStrip → グリッチ中スポーン → WaveMark → 復帰）。
/// 1ウェーブ目のバトル開始演出も担当する。
/// カットインの見た目はエディター配置、コードはアニメーションとタイムラインのみ。
/// </summary>
public sealed class WaveTransitionPresenter : MonoBehaviour
{
    [SerializeField]
    private InGameManager _inGameManager;

    [SerializeField]
    private WaveTransitionCutInView _cutInView;

    [SerializeField]
    private CameraGlitchManager _glitchManager;

    [Header("Wave transition timeline (seconds)")]
    [SerializeField]
    private float _afterClearBeatSeconds = 0.5f;

    [SerializeField]
    private float _cutInDelayAfterBeatSeconds = 0f;

    [SerializeField]
    private float _glitchHoldSeconds = 0.25f;

    [SerializeField]
    private float _glitchDecaySeconds = 0.2f;

    [SerializeField]
    [Range(0f, 1f)]
    private float _spawnAtGlitchHoldNormalized = 0.35f;

    [FormerlySerializedAs("_showWaveNumberAfterGlitch")]
    [SerializeField]
    private bool _showWaveMarkAfterGlitch = true;

    [Header("Battle start timeline (seconds)")]
    [SerializeField]
    private float _battleStartAfterBeatSeconds = 0.25f;

    [SerializeField]
    private float _battleStartCutInDelayAfterBeatSeconds = 0f;

    [SerializeField]
    private float _battleStartGlitchHoldSeconds = 0.35f;

    [SerializeField]
    private float _battleStartGlitchDecaySeconds = 0.2f;

    [SerializeField]
    [Range(0f, 1f)]
    private float _battleStartSpawnAtGlitchHoldNormalized = 0.35f;

    [FormerlySerializedAs("_battleStartShowWaveNumberAfterGlitch")]
    [SerializeField]
    private bool _battleStartShowWaveMarkAfterGlitch = true;

    /// <summary>
    /// 1ウェーブ目のバトル開始演出を再生する。敵スポーンと PostSpawn まで含む。
    /// </summary>
    public async UniTask PlayBattleStartAsync(int waveIndex, CancellationToken token)
    {
        await PlayIntroAsync(
            waveIndex,
            isBattleStart: true,
            _battleStartAfterBeatSeconds,
            _battleStartCutInDelayAfterBeatSeconds,
            _battleStartGlitchHoldSeconds,
            _battleStartGlitchDecaySeconds,
            _battleStartSpawnAtGlitchHoldNormalized,
            _battleStartShowWaveMarkAfterGlitch,
            flushPendingRemovals: false,
            token);
    }

    /// <summary>
    /// ウェーブ遷移演出を再生する。敵スポーンと PostSpawn まで含む。
    /// </summary>
    public async UniTask PlayTransitionAsync(int waveIndex, CancellationToken token)
    {
        await PlayIntroAsync(
            waveIndex,
            isBattleStart: false,
            _afterClearBeatSeconds,
            _cutInDelayAfterBeatSeconds,
            _glitchHoldSeconds,
            _glitchDecaySeconds,
            _spawnAtGlitchHoldNormalized,
            _showWaveMarkAfterGlitch,
            flushPendingRemovals: true,
            token);
    }

    private async UniTask PlayIntroAsync(
        int waveIndex,
        bool isBattleStart,
        float afterBeatSeconds,
        float cutInDelaySeconds,
        float glitchHoldSeconds,
        float glitchDecaySeconds,
        float spawnAtHoldNormalized,
        bool showWaveMarkAfterGlitch,
        bool flushPendingRemovals,
        CancellationToken token)
    {
        if (_inGameManager == null)
        {
            Debug.LogError("[WaveTransitionPresenter] InGameManager が未設定です。", this);
            return;
        }

        _inGameManager.SetBattleInputBlocked(true);

        try
        {
            if (flushPendingRemovals)
            {
                _inGameManager.FlushPendingEnemyRemovalsForTransition();
            }

            if (_cutInView != null)
            {
                _cutInView.transform.SetAsLastSibling();
            }

            int beatMs = Mathf.Max(0, Mathf.RoundToInt(afterBeatSeconds * 1000f));
            if (beatMs > 0)
            {
                await UniTask.Delay(beatMs, cancellationToken: token);
            }

            int cutInDelayMs = Mathf.Max(0, Mathf.RoundToInt(cutInDelaySeconds * 1000f));
            if (cutInDelayMs > 0)
            {
                await UniTask.Delay(cutInDelayMs, cancellationToken: token);
            }

            if (_cutInView != null)
            {
                await _cutInView.PlaySweepStripAsync(isBattleStart, token);
            }

            if (_glitchManager != null)
            {
                await _glitchManager.PlayWaveTransitionGlitchAsync(
                    glitchHoldSeconds,
                    glitchDecaySeconds,
                    spawnAtHoldNormalized,
                    _inGameManager.SpawnWaveEnemiesForTransition,
                    token);
            }
            else
            {
                _inGameManager.SpawnWaveEnemiesForTransition();
            }

            bool waveMarkPlayed = false;
            if (showWaveMarkAfterGlitch && _cutInView != null)
            {
                waveMarkPlayed = await _cutInView.PlayWaveMarkAsync(waveIndex, token);
            }

            await _inGameManager.WaveStartPostSpawnAsync(playWaveStartSe: !waveMarkPlayed, token);
            _inGameManager.MarkWaveReadyFromTransition();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
        }
        finally
        {
            SafeAbortCutInView();
            if (_inGameManager != null)
            {
                _inGameManager.SetBattleInputBlocked(false);
            }
        }
    }

    private void SafeAbortCutInView()
    {
        if (_cutInView == null || !_cutInView)
        {
            return;
        }

        _cutInView.AbortAndReset();
    }
}
