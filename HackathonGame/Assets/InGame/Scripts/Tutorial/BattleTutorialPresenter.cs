using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ウェーブ設定に基づきチュートリアルポップアップを順次表示する。
/// </summary>
public sealed class BattleTutorialPresenter : MonoBehaviour
{
    [SerializeField]
    private BattleTutorialPopupView _popupView;

    [SerializeField]
    private BattleTutorialOpeningView _openingView;

    [SerializeField]
    private BattleTutorialCommandFocusOverlay _commandFocusOverlay;

    [SerializeField]
    private InGameManager _inGameManager;

    private void Awake()
    {
        if (_inGameManager == null)
        {
            _inGameManager = FindFirstObjectByType<InGameManager>();
        }

        if (_popupView == null)
        {
            _popupView = FindFirstObjectByType<BattleTutorialPopupView>(FindObjectsInactive.Include);
        }

        if (_popupView == null && SceneManager.GetActiveScene().name == SceneNames.InGameTutorial)
        {
            _popupView = BattleTutorialUiFactory.CreatePopupUnderCanvas(null);
        }

        if (_openingView == null)
        {
            _openingView = FindFirstObjectByType<BattleTutorialOpeningView>(FindObjectsInactive.Include);
        }

        if (_openingView == null && SceneManager.GetActiveScene().name == SceneNames.InGameTutorial)
        {
            _openingView = BattleTutorialUiFactory.CreateOpeningOverlay(null);
        }

        if (_commandFocusOverlay == null)
        {
            _commandFocusOverlay = FindFirstObjectByType<BattleTutorialCommandFocusOverlay>(FindObjectsInactive.Include);
        }

        if (_commandFocusOverlay == null && SceneManager.GetActiveScene().name == SceneNames.InGameTutorial)
        {
            _commandFocusOverlay = BattleTutorialUiFactory.CreateCommandFocusOverlay(null);
        }
    }

    public void ShowAttackCommandFocus(RectTransform attackButtonRect)
    {
        if (attackButtonRect == null || _commandFocusOverlay == null)
        {
            return;
        }

        _commandFocusOverlay.Show(attackButtonRect);
    }

    public void HideAttackCommandFocus()
    {
        if (_commandFocusOverlay == null)
        {
            return;
        }

        _commandFocusOverlay.Hide();
    }

    /// <summary>チュートリアル入場時の WARNING オープニング（黒画面 → フェードアウト）。</summary>
    public async UniTask PlayOpeningWarningAsync(CancellationToken token)
    {
        if (_openingView == null)
        {
            return;
        }

        if (_inGameManager != null)
        {
            _inGameManager.SetBattleInputBlocked(true);
        }

        try
        {
            await _openingView.PlayAsync(token);
        }
        finally
        {
            if (_inGameManager != null)
            {
                _inGameManager.SetBattleInputBlocked(false);
            }
        }
    }

    public async UniTask TryPlayAsync(
        WaveConfigSO wave,
        WaveTutorialMoment moment,
        CancellationToken token)
    {
        if (wave == null || _popupView == null)
        {
            return;
        }

        IReadOnlyList<BattleTutorialStepSO> steps = wave.GetTutorialStepsForMoment(moment);
        if (steps == null || steps.Count == 0)
        {
            return;
        }

        await PrewarmVideoStepsAsync(steps, token);

        if (_inGameManager != null)
        {
            _inGameManager.SetBattleInputBlocked(true);
        }

        try
        {
            for (int i = 0; i < steps.Count; i++)
            {
                BattleTutorialStepSO step = steps[i];
                if (step == null)
                {
                    continue;
                }

                bool isLast = i == steps.Count - 1;
                try
                {
                    await _popupView.ShowStepAsync(step, isLast, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        finally
        {
            if (_inGameManager != null)
            {
                _inGameManager.SetBattleInputBlocked(false);
            }
        }
    }

    /// <summary>ウェーブ内の動画ステップを、プレイヤー操作前にバックグラウンドで読み込む。</summary>
    public void PrewarmWaveVideos(WaveConfigSO wave, CancellationToken token)
    {
        if (wave == null || _popupView == null)
        {
            return;
        }

        IReadOnlyList<BattleTutorialStepSO> steps = wave.TutorialSteps;
        if (steps == null || steps.Count == 0)
        {
            return;
        }

        PrewarmVideoStepsAsync(steps, token).Forget();
    }

    async UniTask PrewarmVideoStepsAsync(IReadOnlyList<BattleTutorialStepSO> steps, CancellationToken token)
    {
        if (_popupView == null)
        {
            return;
        }

        var tasks = new List<UniTask>(steps.Count);
        for (int i = 0; i < steps.Count; i++)
        {
            BattleTutorialStepSO step = steps[i];
            if (step == null || !step.UsesVideo || step.LoopVideo == null)
            {
                continue;
            }

            tasks.Add(_popupView.PrewarmLoopVideoAsync(step.LoopVideo, token));
        }

        if (tasks.Count > 0)
        {
            await UniTask.WhenAll(tasks);
        }
    }
}
