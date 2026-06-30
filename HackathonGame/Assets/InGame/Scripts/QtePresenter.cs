using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// スキルQTEの再生・入力収集・判定一覧の生成（QTE用合成 clip / ジングル clip 時間基準・TaikoScroll）。
/// </summary>
public sealed class QtePresenter : MonoBehaviour
{
    /// <summary>BGM スクラッチ演出の既定秒数（InGameManager の QTE 用フェード遅延と揃える）。</summary>
    public const float DefaultBgmScratchLeadSeconds = 0.2f;

    private const float QteHandoffVolumeScale = 0.4f;

    private readonly struct SavedLoopBgm
    {
        public readonly AudioClip Clip;
        public readonly AudioResource Resource;
        public readonly bool WasPlaying;

        public bool HasLoopAudio => Clip != null || Resource != null;

        public SavedLoopBgm(AudioClip clip, AudioResource resource, bool wasPlaying)
        {
            Clip = clip;
            Resource = resource;
            WasPlaying = wasPlaying;
        }
    }

    [SerializeField]
    private GameObject _taikoScrollLayerRoot;

    [SerializeField]
    private TaikoScrollQteRunner _taikoRunner;

    [SerializeField]
    private QteTaikoLayerIntroView _layerIntro;

    [SerializeField]
    private QteLiveMultiplierView _liveMultiplier;

    [SerializeField]
    private PlayerView _playerView;

    [SerializeField]
    private BattleCameraController _battleCamera;

    [SerializeField]
    private TargetSelectView _targetSelectView;

    [SerializeField]
    private QteTapButtonGuideView _tapButtonGuide;

    [SerializeField]
    private AudioSource _jingleSource;

    [SerializeField]
    private float _jingleScheduleLeadSeconds = 0.05f;

    [SerializeField]
    private float _bgmScratchSeconds = 0.2f;

    [SerializeField]
    private float _bgmScratchPitchMin = 0.25f;

    [SerializeField]
    private float _bgmScratchPitchPeak = 0.85f;

    [SerializeField]
    private bool _playBgmScratchSe = true;

    [SerializeField]
    [Range(0f, 1f)]
    private float _bgmScratchSeVolume = 1f;

    [SerializeField]
    [Tooltip("全ノート判定後、ループ BGM へ戻すクロスフェード秒数。")]
    private float _qteLoopRestoreFadeSeconds = 0.25f;

    /// <summary>
    /// コマンド確定直後に BGM スクラッチ SE と pitch 演出を開始する（待機なしで即完了）。
    /// ループ BGM の pitch スクラッチは呼び出し側をブロックしない。
    /// </summary>
    public UniTask PlayBgmScratchOnConfirmAsync(CancellationToken token)
    {
        SoundManager soundManager = SoundManager.EnsureInstance();
        AudioSource bgmSource = soundManager.GetAudibleBgmSource();
        if (bgmSource == null)
        {
            return UniTask.CompletedTask;
        }

        soundManager.FocusBgmChannel(bgmSource);

        if (_playBgmScratchSe)
        {
            soundManager.PlaySe(InGameSeKey.QteBgmScratch, _bgmScratchSeVolume);
        }

        soundManager.PlayBgmLoopScratchAsync(
            bgmSource,
            _bgmScratchPitchMin,
            _bgmScratchPitchPeak,
            _bgmScratchSeconds,
            token).Forget();

        return UniTask.CompletedTask;
    }

    /// <summary>
    /// 指定スキルのQTEを実行し、各ヒットの判定を返す。
    /// </summary>
    public async UniTask<IReadOnlyList<QteJudgment>> RunSkillQteAsync(
        SkillDataSO skill,
        BattleSettingsSO battleSettings,
        CancellationToken token)
    {
        List<QteJudgment> results = new List<QteJudgment>();
        if (battleSettings == null)
        {
            Debug.LogError("[QtePresenter] BattleSettingsSO が null です。", this);
            return results;
        }

        if (skill == null)
        {
            Debug.LogError("[QtePresenter] Skill が null です。", this);
            return results;
        }

        if (!skill.TryCreateQtePlaybackContext(out SkillQtePlaybackContext playbackContext))
        {
            Debug.LogError(
                $"[QtePresenter] Skill '{skill.SkillId}' に有効な QTE ジングルバリアントがありません。",
                this);
            return results;
        }

        AudioClip playbackClip = playbackContext.GetQtePlaybackClip();
        if (playbackClip == null)
        {
            Debug.LogError("[QtePresenter] 抽選バリアントの QteCombinedClip が未設定です。", this);
            return results;
        }

        SoundManager soundManager = SoundManager.EnsureInstance();
        AudioSource bgmSource = soundManager.GetAudibleBgmSource();
        if (bgmSource == null)
        {
            Debug.LogError("[QtePresenter] SoundManager の BGM AudioSource が未設定です。", this);
            return results;
        }

        soundManager.FocusBgmChannel(bgmSource);
        SavedLoopBgm savedLoop = CaptureLoopBgmState(bgmSource);

        if (_jingleSource != null)
        {
            _jingleSource.Stop();
        }

        await EnsureAudioClipLoadedAsync(playbackClip, token);

        double syncDsp = AudioSettings.dspTime + _jingleScheduleLeadSeconds;

        bool loopBgmRestored = false;
        bool qteUiTornDown = false;

        try
        {
            if (_taikoScrollLayerRoot != null)
            {
                _taikoScrollLayerRoot.SetActive(true);
                EnsureQteCanvasScale(_taikoScrollLayerRoot.transform);
            }

            SafeResetLiveMultiplier();
            _layerIntro?.PrepareForShow();
            UniTask introTask = _layerIntro != null
                ? _layerIntro.PlayIntroAsync(token)
                : UniTask.CompletedTask;

            double playbackStartDsp = StartQtePlaybackOnBgm(bgmSource, playbackClip, syncDsp);

            await introTask;

            SafeBeginLiveMultiplierSession(battleSettings);
            ShowTapButtonGuide();

            if (_taikoRunner == null)
            {
                Debug.LogError("[QtePresenter] TaikoScrollQteRunner が未設定です。", this);
                return TaikoScrollQteRunner.CreateAllMiss(playbackContext);
            }

            IReadOnlyList<QteJudgment> judgments = await _taikoRunner.RunAsync(
                playbackContext,
                battleSettings,
                bgmSource,
                playbackStartDsp,
                token);

            HideTapButtonGuide();

            UniTask restoreTask = soundManager.RestoreLoopBgmAfterQteAsync(
                savedLoop.Clip,
                savedLoop.Resource,
                savedLoop.WasPlaying,
                token,
                _qteLoopRestoreFadeSeconds);

            if (_liveMultiplier != null)
            {
                await UniTask.WhenAll(
                    restoreTask,
                    _liveMultiplier.PlayFinaleAsync(
                        judgments,
                        battleSettings,
                        playbackContext.Category,
                        token));
            }
            else
            {
                await restoreTask;
            }

            await CloseQteUiAfterFinaleAsync(token);
            loopBgmRestored = true;

            if (_liveMultiplier != null && _playerView != null)
            {
                _liveMultiplier.DetachToChargeOverlay();

                if (_taikoScrollLayerRoot != null)
                {
                    _taikoScrollLayerRoot.SetActive(false);
                }

                await PrepareCameraBeforeChargeAsync(playbackContext.Category, token);

                await _liveMultiplier.PlayChargeAbsorbToPlayerAsync(_playerView, judgments, token);

                // 倍率 UI はオーバーレイ上のまま非表示化。太鼓レイヤーへ戻してから SetActive(false) すると
                // 2 回目 QTE まで inactive の子になり表示が復活しない。
                _liveMultiplier.PrepareForNextQteAfterCharge();

                if (_taikoScrollLayerRoot != null)
                {
                    _taikoScrollLayerRoot.SetActive(false);
                }

                qteUiTornDown = true;
            }
            else
            {
                if (_taikoScrollLayerRoot != null)
                {
                    _taikoScrollLayerRoot.SetActive(false);
                }

                qteUiTornDown = SafeResetLiveMultiplier();
            }

            return judgments;
        }
        finally
        {
            HideTapButtonGuide();

            if (_jingleSource != null)
            {
                _jingleSource.Stop();
                _jingleSource.time = 0f;
            }

            _layerIntro?.KillAndReset();

            if (!qteUiTornDown)
            {
                SafeResetLiveMultiplier();

                if (_taikoScrollLayerRoot != null)
                {
                    _taikoScrollLayerRoot.SetActive(false);
                }
            }

            if (!loopBgmRestored && savedLoop.HasLoopAudio)
            {
                soundManager.RestoreLoopBgmAfterQteImmediate(
                    savedLoop.Clip,
                    savedLoop.Resource,
                    savedLoop.WasPlaying);
            }
        }
    }

    private async UniTask PrepareCameraBeforeChargeAsync(SkillCategory category, CancellationToken token)
    {
        if (_battleCamera != null)
        {
            await _battleCamera.FocusDefaultAsync(token);
        }

        if (category == SkillCategory.Attack && _targetSelectView != null)
        {
            await _targetSelectView.ResetStageAsync(token);
        }
    }

    private async UniTask CloseQteUiAfterFinaleAsync(CancellationToken token)
    {
        if (_layerIntro != null)
        {
            await _layerIntro.PlayOutroAsync(token);
        }
    }

    private static SavedLoopBgm CaptureLoopBgmState(AudioSource bgmSource)
    {
        return new SavedLoopBgm(
            BgmLoopSync.GetLoopClip(bgmSource),
            bgmSource.resource,
            bgmSource.isPlaying);
    }

    private static double StartQtePlaybackOnBgm(AudioSource bgmSource, AudioClip playbackClip, double syncDsp)
    {
        float preserveVolume = bgmSource.volume;
        bgmSource.volume = preserveVolume * QteHandoffVolumeScale;

        bgmSource.Stop();
        bgmSource.loop = false;
        bgmSource.clip = playbackClip;
        bgmSource.time = 0f;
        bgmSource.pitch = 1f;
        bgmSource.volume = preserveVolume;

        if (AudioSettings.dspTime >= syncDsp)
        {
            bgmSource.Play();
            return AudioSettings.dspTime;
        }

        bgmSource.PlayScheduled(syncDsp);
        return syncDsp;
    }

    private static async UniTask EnsureAudioClipLoadedAsync(AudioClip clip, CancellationToken token)
    {
        if (clip.loadState == AudioDataLoadState.Loaded)
        {
            return;
        }

        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }

        await UniTask.WaitUntil(
            () => clip.loadState == AudioDataLoadState.Loaded
                || clip.loadState == AudioDataLoadState.Failed,
            cancellationToken: token,
            timing: PlayerLoopTiming.Update);

        if (clip.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogError($"[QtePresenter] AudioClip のロードに失敗しました: {clip.name}");
        }
    }

    public static float GetMultiplier(QteJudgment judgment, BattleSettingsSO battleSettings)
    {
        if (judgment == QteJudgment.Perfect)
        {
            return battleSettings.QtePerfectMultiplier;
        }

        if (judgment == QteJudgment.Good)
        {
            return battleSettings.QteGoodMultiplier;
        }

        return battleSettings.QteMissMultiplier;
    }

    /// <summary>
    /// Unity の破棄済み参照は C# の <c>?.</c> では null と判定されないため、明示的にチェックする。
    /// </summary>
    private void SafeBeginLiveMultiplierSession(BattleSettingsSO battleSettings)
    {
        if (_liveMultiplier != null)
        {
            _liveMultiplier.BeginSession(battleSettings);
        }
    }

    /// <returns>リセットを実行した場合 true。</returns>
    private bool SafeResetLiveMultiplier()
    {
        if (_liveMultiplier == null)
        {
            return false;
        }

        _liveMultiplier.ResetForNextQte();
        return true;
    }

    private void Awake()
    {
        if (SoundManager.Instance == null)
        {
            Debug.LogWarning("[QtePresenter] SoundManager がシーンにありません。再生時に自動生成されます。", this);
        }

        if (_taikoRunner == null)
        {
            Debug.LogWarning("[QtePresenter] TaikoScrollQteRunner が未設定です。", this);
        }

        if (_playerView == null)
        {
            Debug.LogWarning("[QtePresenter] PlayerView が未設定です。倍率チャージ演出をスキップします。", this);
        }

        if (_taikoScrollLayerRoot != null)
        {
            _taikoScrollLayerRoot.SetActive(false);
        }

        _layerIntro?.ResetToHiddenState();
        HideTapButtonGuide();
    }

    private void ShowTapButtonGuide()
    {
        if (_tapButtonGuide != null)
        {
            _tapButtonGuide.Show();
        }
    }

    private void HideTapButtonGuide()
    {
        if (_tapButtonGuide != null)
        {
            _tapButtonGuide.Hide();
        }
    }

    public static void EnsureQteCanvasScale(Transform from)
    {
        if (from != null)
        {
            Canvas[] canvases = from.GetComponentsInParent<Canvas>(includeInactive: true);
            for (int i = 0; i < canvases.Length; i++)
            {
                FixCanvasRectScale(canvases[i]);
            }
        }

        Canvas root = from != null ? from.GetComponentInParent<Canvas>(includeInactive: true)?.rootCanvas : null;
        if (root != null)
        {
            FixCanvasRectScale(root);
        }
    }

    private static void FixCanvasRectScale(Canvas canvas)
    {
        if (canvas == null || canvas.transform is not RectTransform canvasRect)
        {
            return;
        }

        if (canvasRect.localScale.sqrMagnitude < 0.01f)
        {
            canvasRect.localScale = Vector3.one;
        }
    }
}
