using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Inspector で参照した Panel を表示・アニメーションのみ行うカットイン View。
/// Sprite / サイズ / Image はエディター配置のまま。実行時に参照を自動解決しない。
/// </summary>
public sealed class WaveTransitionCutInView : MonoBehaviour
{
    [FormerlySerializedAs("_sweepStrip")]
    [SerializeField]
    private WaveTransitionCutInPanel _battleStartSweep;

    [SerializeField]
    private WaveTransitionCutInPanel _nextWaveSweep;

    [FormerlySerializedAs("_waveMark")]
    [SerializeField]
    private WaveTransitionCutInPanel[] _waveMarks;

    [SerializeField]
    private SweepMotionSettings _sweepMotion = new SweepMotionSettings();

    [SerializeField]
    private WaveMarkRevealSettings _waveMarkReveal = new WaveMarkRevealSettings();

    private Sequence _sweepSequence;
    private Sequence _waveMarkSequence;
    private WaveTransitionCutInPanel _activeSweepPanel;
    private WaveTransitionCutInPanel _activeWaveMarkPanel;

    private void Awake()
    {
        HideAll();
    }

    private void OnDestroy()
    {
        KillAllTweens();
    }

    /// <summary>演出中断時に Tween を止め、全 Panel のレイアウトを復帰して非表示にする。</summary>
    public void AbortAndReset()
    {
        if (!IsAlive)
        {
            return;
        }

        KillAllTweens();
        _activeSweepPanel = null;
        _activeWaveMarkPanel = null;
        HideAll();
    }

    /// <summary>BattleStart または NextWave の Sweep を左外→中央→右外へアニメーション。</summary>
    public async UniTask PlaySweepStripAsync(bool isBattleStart, CancellationToken token)
    {
        KillSweepTweens();

        WaveTransitionCutInPanel panel = isBattleStart ? _battleStartSweep : _nextWaveSweep;
        if (panel == null)
        {
            Debug.LogWarning(
                $"[WaveTransitionCutInView] {(isBattleStart ? "BattleStartImage" : "NextWaveText")} が未設定です。ヒエラルキーに Panel を配置し Inspector で参照してください。",
                this);
            return;
        }

        _activeSweepPanel = panel;
        RectTransform stripRect = panel.Root;
        CanvasGroup stripGroup = panel.CanvasGroup;
        if (stripRect == null)
        {
            _activeSweepPanel = null;
            return;
        }

        EnsureRootActive();
        HidePanelsExcept(panel);
        panel.PrepareForAnimation();

        Vector3 centerLocalPos = panel.RestLocalPosition;
        Vector3 restScale = panel.RestLocalScale;
        Vector3 startLocalPos = centerLocalPos + new Vector3(_sweepMotion.EnterOffsetX, 0f, 0f);
        Vector3 endLocalPos = centerLocalPos + new Vector3(_sweepMotion.ExitOffsetX, 0f, 0f);
        stripRect.localPosition = startLocalPos;
        stripRect.localScale = restScale;

        if (stripGroup != null)
        {
            stripGroup.alpha = _sweepMotion.FadeInDuration > 0f ? 0f : 1f;
        }

        _sweepSequence = DOTween.Sequence();
        _sweepSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _sweepSequence.SetUpdate(true);

        if (stripGroup != null && _sweepMotion.FadeInDuration > 0f)
        {
            _sweepSequence.Append(
                stripGroup.DOFade(1f, _sweepMotion.FadeInDuration).SetEase(Ease.Linear));
        }

        if (_sweepMotion.EnterDuration > 0f)
        {
            _sweepSequence.Append(
                stripRect.DOLocalMove(centerLocalPos, _sweepMotion.EnterDuration).SetEase(_sweepMotion.EnterEase));
        }
        else
        {
            stripRect.localPosition = centerLocalPos;
        }

        if (_sweepMotion.HoldDuration > 0f)
        {
            _sweepSequence.AppendInterval(_sweepMotion.HoldDuration);
        }

        if (_sweepMotion.ExitDuration > 0f)
        {
            _sweepSequence.Append(
                stripRect.DOLocalMove(endLocalPos, _sweepMotion.ExitDuration).SetEase(_sweepMotion.ExitEase));
        }
        else
        {
            stripRect.localPosition = endLocalPos;
        }

        bool completed = false;
        _sweepSequence.OnComplete(() => completed = true);
        _sweepSequence.OnKill(() => completed = true);

        try
        {
            await UniTask.WaitUntil(() => completed, cancellationToken: token);
        }
        finally
        {
            if (_activeSweepPanel != null)
            {
                HideSweepPanel(_activeSweepPanel);
                _activeSweepPanel = null;
            }
        }
    }

    /// <summary>waveIndex に対応する WaveMark Panel を中央表示アニメーション。</summary>
    /// <returns>WaveMark を表示した場合 true。</returns>
    public async UniTask<bool> PlayWaveMarkAsync(int waveIndex, CancellationToken token)
    {
        KillWaveMarkTweens();

        WaveTransitionCutInPanel panel = ResolveWaveMarkPanel(waveIndex);
        if (panel == null)
        {
            Debug.LogWarning(
                $"[WaveTransitionCutInView] WaveMark（waveIndex={waveIndex}）が未設定です。WaveText_001 等を配置し _waveMarks に割当してください。",
                this);
            return false;
        }

        _activeWaveMarkPanel = panel;
        RectTransform markRect = panel.Root;
        CanvasGroup markGroup = panel.CanvasGroup;
        if (markRect == null)
        {
            _activeWaveMarkPanel = null;
            return false;
        }

        EnsureRootActive();
        HidePanelsExcept(panel);
        panel.PrepareForAnimation();

        Vector3 restScale = panel.RestLocalScale;
        markRect.anchoredPosition = panel.RestAnchoredPosition;
        markRect.localPosition = panel.RestLocalPosition;
        markRect.localScale = restScale * _waveMarkReveal.PopScaleFrom;

        if (markGroup != null)
        {
            markGroup.alpha = 0f;
        }

        _waveMarkSequence = DOTween.Sequence();
        _waveMarkSequence.SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        _waveMarkSequence.SetUpdate(true);
        _waveMarkSequence.AppendCallback(PlayBattleWaveStartSe);

        float revealDuration = _waveMarkReveal.FadeInDuration;
        if (markGroup != null)
        {
            _waveMarkSequence.Append(
                markGroup.DOFade(1f, revealDuration).SetEase(Ease.Linear));
            _waveMarkSequence.Join(
                markRect.DOScale(restScale, revealDuration).SetEase(_waveMarkReveal.FadeInEase));
        }
        else
        {
            _waveMarkSequence.Append(
                markRect.DOScale(restScale, revealDuration).SetEase(_waveMarkReveal.FadeInEase));
        }

        if (_waveMarkReveal.HoldDuration > 0f)
        {
            _waveMarkSequence.AppendInterval(_waveMarkReveal.HoldDuration);
        }

        if (markGroup != null)
        {
            _waveMarkSequence.Append(
                markGroup.DOFade(0f, _waveMarkReveal.FadeOutDuration).SetEase(Ease.Linear));
        }

        bool completed = false;
        _waveMarkSequence.OnComplete(() => completed = true);
        _waveMarkSequence.OnKill(() => completed = true);

        try
        {
            await UniTask.WaitUntil(() => completed, cancellationToken: token);
        }
        finally
        {
            if (_activeWaveMarkPanel != null)
            {
                HideWaveMarkPanel(_activeWaveMarkPanel);
                _activeWaveMarkPanel = null;
            }

            HideAll();
        }

        return true;
    }

    private static void PlayBattleWaveStartSe()
    {
        InGameSe.Play(InGameSeKey.BattleWaveStart);
    }

    private WaveTransitionCutInPanel ResolveWaveMarkPanel(int waveIndex)
    {
        if (_waveMarks == null || waveIndex < 0 || waveIndex >= _waveMarks.Length)
        {
            return null;
        }

        return _waveMarks[waveIndex];
    }

    private bool IsAlive => (Object)this != null && gameObject != null;

    private void EnsureRootActive()
    {
        if (!IsAlive)
        {
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    private void HideAll()
    {
        if (!IsAlive)
        {
            return;
        }

        HideAllPanels();

        if (!gameObject.activeSelf)
        {
            return;
        }

        gameObject.SetActive(false);
    }

    private void HideAllPanels()
    {
        HidePanelsExcept(null);
    }

    private void HidePanelsExcept(WaveTransitionCutInPanel except)
    {
        HidePanelIfNot(_battleStartSweep, except);
        HidePanelIfNot(_nextWaveSweep, except);

        if (_waveMarks != null)
        {
            for (int i = 0; i < _waveMarks.Length; i++)
            {
                HidePanelIfNot(_waveMarks[i], except);
            }
        }
    }

    private static void HidePanelIfNot(WaveTransitionCutInPanel panel, WaveTransitionCutInPanel except)
    {
        if (panel == null || panel == except)
        {
            return;
        }

        panel.HideAfterAnimation();
    }

    private void HideSweepPanel(WaveTransitionCutInPanel panel)
    {
        if (panel == null)
        {
            return;
        }

        if (_sweepSequence != null && _sweepSequence.IsActive())
        {
            _sweepSequence.Kill(false);
        }

        _sweepSequence = null;
        KillPanelTweens(panel);
        panel.HideAfterAnimation();
    }

    private void HideWaveMarkPanel(WaveTransitionCutInPanel panel)
    {
        if (panel == null)
        {
            return;
        }

        if (_waveMarkSequence != null && _waveMarkSequence.IsActive())
        {
            _waveMarkSequence.Kill(false);
        }

        _waveMarkSequence = null;
        KillPanelTweens(panel);
        panel.HideAfterAnimation();
    }

    private void KillAllTweens()
    {
        KillSweepTweens();
        KillWaveMarkTweens();
    }

    private void KillSweepTweens()
    {
        if (_sweepSequence != null && _sweepSequence.IsActive())
        {
            _sweepSequence.Kill(false);
        }

        _sweepSequence = null;
        KillPanelTweens(_activeSweepPanel);
        KillPanelTweens(_battleStartSweep);
        KillPanelTweens(_nextWaveSweep);
    }

    private void KillWaveMarkTweens()
    {
        if (_waveMarkSequence != null && _waveMarkSequence.IsActive())
        {
            _waveMarkSequence.Kill(false);
        }

        _waveMarkSequence = null;

        if (_waveMarks != null)
        {
            for (int i = 0; i < _waveMarks.Length; i++)
            {
                KillPanelTweens(_waveMarks[i]);
            }
        }

        KillPanelTweens(_activeWaveMarkPanel);
    }

    private static void KillPanelTweens(WaveTransitionCutInPanel panel)
    {
        if (panel == null)
        {
            return;
        }

        if (panel.Root != null)
        {
            panel.Root.DOKill(false);
        }

        if (panel.CanvasGroup != null)
        {
            panel.CanvasGroup.DOKill(false);
        }
    }
}
