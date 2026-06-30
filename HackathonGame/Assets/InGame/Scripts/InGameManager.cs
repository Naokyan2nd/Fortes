using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using R3;
using UnityEngine;

/// <summary>
/// インゲームバトル全体のステートマシンとオーケストレーション。
/// </summary>
public sealed class InGameManager : MonoBehaviour
{
    private const int EnemySlotCount = 3;

    [SerializeField]
    private BattleSettingsSO _battleSettings;

    [SerializeField]
    private StageConfigSO _fallbackStage;

    [SerializeField]
    [Tooltip("Normal / Rare / SuperRare など StageId で参照する全ステージ。")]
    private StageConfigSO[] _battleStages;

    [SerializeField]
    private SkillDataSO[] _skillSlots;

    [SerializeField]
    private Transform[] _enemySpawnPoints;

    [SerializeField]
    private TargetSelectView _targetSelectView;

    [SerializeField]
    private BattleCameraController _battleCamera;

    [SerializeField]
    private CommandPanelView _commandPanelView;

    [SerializeField]
    private HudView _hudView;

    [SerializeField]
    private WavePanelView _wavePanelView;

    [SerializeField]
    private PlayerView _playerView;

    [SerializeField]
    private ResultView _resultView;

    [SerializeField]
    private QtePresenter _qtePresenter;

    [SerializeField]
    private BattleHudSlideView _battleHudSlide;

    [SerializeField]
    private float _bgmFadeSeconds = 0.4f;

    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("QTE中のBGM音量。フェード前の音量に対する倍率（0.5で半分、1で変化なし）。")]
    private float _bgmVolumeDuringQte = 0.5f;

    [Header("Result Presentation")]
    [SerializeField]
    private float _resultBgmFadeSeconds = 0.9f;

    [SerializeField]
    [Tooltip("BGMフェード開始後、リザルト SE を鳴らすまでの待ち（秒）。0 で即時。")]
    private float _resultSeDelayAfterFadeStart = 0.15f;

    [SerializeField]
    private InGameLogManager _log;

    [SerializeField]
    private CombatFloatingTextPresenter _floatingText;

    [SerializeField]
    private CameraGlitchManager _cameraGlitchManager;

    [SerializeField]
    private WaveTransitionPresenter _waveTransition;

    [Header("Tutorial")]
    [SerializeField]
    private BattleTutorialPresenter _battleTutorial;

    [SerializeField]
    [Tooltip("InGameTutorial シーンでは true。BattleStageSession を無視し Fallback Stage のみ使用。")]
    private bool _tutorialRun;

    private bool _tutorialAttackFocusPending;
    private bool _tutorialAttackOnlyActive;
    private bool _tutorialCapPlayerDamageUntilEnrageGuide;

    [Header("Command Input")]
    [SerializeField]
    [Tooltip("TwoStep: 攻撃は選択→確定→ターゲット操作。QuickConfirm: 攻撃ボタン1回で最左の生存敵へ即確定。")]
    private AttackCommandInputMode _attackCommandInputMode = AttackCommandInputMode.TwoStep;

    [Header("Enemy Batch Attack")]
    [SerializeField]
    private bool _useEnemyBatchAttack = true;

    [SerializeField]
    private float _enemyBatchHopStaggerSeconds = 0.05f;

    [Header("Enemy Enrage")]
    [SerializeField]
    [Tooltip("ラウンド3,5,7…（3以上の奇数）開始時に加算する火力倍率（最大3段まで）。")]
    private float _enrageMultiplierStep = 0.2f;

    [SerializeField]
    [Min(1)]
    private int _maxEnrageBuffStacks = 3;

    [SerializeField]
    private CombatHitStopPresenter _hitStop;

    private PlayerModel _player;
    private readonly EnemyModel[] _enemySlots = new EnemyModel[EnemySlotCount];
    private readonly EnemyView[] _spawnedEnemyViews = new EnemyView[EnemySlotCount];
    private readonly IDisposable[] _enemyDeathSubscriptions = new IDisposable[EnemySlotCount];
    private readonly Queue<ActionUnit> _actionQueue = new Queue<ActionUnit>();
    private int _waveIndex;
    private bool _battleEnded;
    private bool _pendingWaveAdvance;
    private bool _waveEnemiesReadyFromTransition;
    private bool _battleInputBlocked;
    private int _lastTargetSlot = -1;
    private bool _deferEnemyRemoval;
    private readonly List<int> _pendingEnemyRemovalSlots = new List<int>();
    private StageConfigSO _activeStage;
    private int _currentRoundCount;
    private float _enrageMultiplier = 1f;
    private int _enrageBuffStackCount;
    private bool _hasPlayedBeforeFirstQteTutorial;
    private bool _enrageAppliedLastRoundStart;

    private int WaveCount => _activeStage != null ? _activeStage.WaveCount : 0;

    private readonly struct ActionUnit
    {
        public readonly bool IsPlayer;
        public readonly int EnemySlot;

        private ActionUnit(bool isPlayer, int enemySlot)
        {
            IsPlayer = isPlayer;
            EnemySlot = enemySlot;
        }

        public static ActionUnit ForPlayer()
        {
            return new ActionUnit(true, -1);
        }

        public static ActionUnit ForEnemy(int slot)
        {
            return new ActionUnit(false, slot);
        }
    }

    private StageConfigSO ResolveActiveStage(out string noiseChildName, out string requestedStageId)
    {
        noiseChildName = null;
        requestedStageId = null;
        if (_tutorialRun)
        {
            requestedStageId = TutorialStageIds.TutorialStage;
            return _fallbackStage;
        }

        if (BattleStageSession.TryConsume(out string stageId))
        {
            requestedStageId = stageId;
            noiseChildName = BattleStageSession.ConsumePendingNoiseChildName();
            StageConfigSO resolved = FindStageConfigById(stageId);
            if (resolved != null)
            {
                return resolved;
            }

            Debug.LogWarning(
                $"[InGameManager] 未登録のステージ ID '{stageId}'。Fallback Stage を使用します。",
                this);
        }

        return _fallbackStage;
    }

    private StageConfigSO FindStageConfigById(string stageId)
    {
        if (string.IsNullOrEmpty(stageId))
        {
            return null;
        }

        if (_battleStages != null)
        {
            for (int i = 0; i < _battleStages.Length; i++)
            {
                StageConfigSO stage = _battleStages[i];
                if (stage != null && stage.StageId == stageId)
                {
                    return stage;
                }
            }
        }

        if (_fallbackStage != null && _fallbackStage.StageId == stageId)
        {
            return _fallbackStage;
        }

        return null;
    }

    private void Awake()
    {
        _activeStage = ResolveActiveStage(out string noiseChildName, out string requestedStageId);
        if (_activeStage != null)
        {
            if (string.IsNullOrEmpty(noiseChildName))
            {
                noiseChildName = BattleStageSession.CommittedBattleNoiseChildName;
            }

            string playedStageId = !string.IsNullOrEmpty(requestedStageId)
                ? requestedStageId
                : _activeStage.StageId;
            BattleStageSession.RecordPlayedBattle(playedStageId, noiseChildName);
        }

        if (_battleSettings == null)
        {
            Debug.LogError("[InGameManager] BattleSettingsSO が未設定です。", this);
        }

        if (_activeStage == null || WaveCount == 0)
        {
            Debug.LogError("[InGameManager] Stage が未設定です（Fallback Stage を Inspector に指定してください）。", this);
        }

        if (_skillSlots == null || _skillSlots.Length != 2)
        {
            Debug.LogError("[InGameManager] スキルは2枠（攻撃・回復）で指定してください。", this);
        }

        if (_enemySpawnPoints == null || _enemySpawnPoints.Length != EnemySlotCount)
        {
            Debug.LogError("[InGameManager] 敵スポーン点が未設定、または数が一致しません（3箇所）。", this);
        }

        ValidateWaveEnemyViewPrefabs();

        if (_playerView == null)
        {
            _playerView = FindFirstObjectByType<PlayerView>();
        }

        if (_targetSelectView == null || _commandPanelView == null || _hudView == null || _playerView == null || _resultView == null || _qtePresenter == null)
        {
            Debug.LogError("[InGameManager] View / Presenter 参照が不足しています。", this);
        }

        if (_cameraGlitchManager == null)
        {
            _cameraGlitchManager = FindFirstObjectByType<CameraGlitchManager>();
        }

        if (_waveTransition == null)
        {
            _waveTransition = FindFirstObjectByType<WaveTransitionPresenter>();
        }

        if (_battleTutorial == null)
        {
            _battleTutorial = FindFirstObjectByType<BattleTutorialPresenter>();
        }

        if (_battleTutorial == null && _tutorialRun)
        {
            var presenterGo = new GameObject("BattleTutorialPresenter");
            presenterGo.transform.SetParent(transform.parent != null ? transform.parent : transform);
            _battleTutorial = presenterGo.AddComponent<BattleTutorialPresenter>();
        }
    }

    private void ValidateWaveEnemyViewPrefabs()
    {
        if (_activeStage == null || _activeStage.Waves == null)
        {
            return;
        }

        IReadOnlyList<WaveConfigSO> waves = _activeStage.Waves;
        for (int w = 0; w < waves.Count; w++)
        {
            WaveConfigSO wave = waves[w];
            if (wave == null || wave.Enemies == null)
            {
                continue;
            }

            for (int e = 0; e < wave.Enemies.Length; e++)
            {
                EnemyDataSO enemyData = wave.Enemies[e];
                if (enemyData == null || enemyData.ViewPrefab != null)
                {
                    continue;
                }

                Debug.LogError(
                    $"[InGameManager] Wave[{w}] Enemies[{e}] ({enemyData.name}) の ViewPrefab が未設定です。",
                    this);
            }
        }
    }

    private void OnDestroy()
    {
        if (_tutorialAttackOnlyActive || _tutorialAttackFocusPending)
        {
            _battleTutorial?.HideAttackCommandFocus();
            _commandPanelView?.SetAttackArrowVisible(false);
        }

        DespawnAllEnemyInstances();
        ClearEnemySlots();
        if (_player != null)
        {
            _player.Dispose();
            _player = null;
        }
    }

    private void Start()
    {
        CancellationToken token = this.GetCancellationTokenOnDestroy();
        RunBattleRoutine(token).Forget();
    }

    /// <summary>
    /// バトルメインループ。
    /// </summary>
    /// <param name="token">破棄時キャンセル。</param>
    /// <returns>完了までのUniTask。</returns>
    private async UniTaskVoid RunBattleRoutine(CancellationToken token)
    {
        try
        {
            _log?.LogState("RunBattleRoutine", "開始");
            await InitializeBattleAsync(token);
            if (ShouldDelayInitialBgmForTutorialOpening())
            {
                await _battleTutorial.PlayOpeningWarningAsync(token);
                TryStartInitialBgm();
            }

            while (!_battleEnded)
            {
                if (!_waveEnemiesReadyFromTransition)
                {
                    await WaveStartAsync(token);
                }
                else
                {
                    _waveEnemiesReadyFromTransition = false;
                }

                if (_battleEnded)
                {
                    break;
                }

                await RunRoundLoopAsync(token);
                if (_battleEnded)
                {
                    break;
                }

                if (_pendingWaveAdvance)
                {
                    if (_waveTransition != null)
                    {
                        await _waveTransition.PlayTransitionAsync(_waveIndex, token);
                    }
                    else
                    {
                        SetBattleInputBlocked(true);
                        FlushPendingEnemyRemovalsForTransition();
                        SpawnWaveEnemiesForTransition();
                        await WaveStartPostSpawnAsync(playWaveStartSe: true, token);
                        MarkWaveReadyFromTransition();
                        SetBattleInputBlocked(false);
                    }

                    _pendingWaveAdvance = false;
                }
            }

            _log?.LogState("RunBattleRoutine", "終了");
        }
        catch (OperationCanceledException)
        {
            // 破棄時の正常キャンセル
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            SetBattleInputBlocked(false);
        }
    }

    private async UniTask InitializeBattleAsync(CancellationToken token)
    {
        _log?.LogState("InitializeBattle", "開始");
        if (ShouldDelayInitialBgmForTutorialOpening())
        {
            TryStopInitialBgm();
        }

        if (_resultView == null)
        {
            Debug.LogError("[InGameManager] ResultView が未設定です。", this);
            return;
        }

        _resultView.Hide();
        PlayerCombatStats playerStats = PlayerCombatStatsResolver.ResolveCurrent();
        _player = new PlayerModel(
            playerStats.MaxHp,
            _battleSettings.PlayerSpeed);
        if (_hudView == null)
        {
            Debug.LogError("[InGameManager] HudView が未設定です。", this);
            return;
        }

        _hudView.Bind(_player);
        _waveIndex = 0;
        _battleEnded = false;
        _pendingWaveAdvance = false;
        _waveEnemiesReadyFromTransition = false;
        _battleInputBlocked = false;
        _tutorialAttackFocusPending = _tutorialRun && IsActiveTutorialStage();
        _tutorialAttackOnlyActive = false;
        _tutorialCapPlayerDamageUntilEnrageGuide = IsActiveTutorialStage();
        _hasPlayedBeforeFirstQteTutorial = false;
        ResetSkillQteVariantHistories();
        if (_waveTransition != null)
        {
            SetBattleInputBlocked(true);
            _wavePanelView?.SetVisible(false);
        }

        await UniTask.Yield(token);
        if (!ShouldDelayInitialBgmForTutorialOpening())
        {
            TryStartInitialBgm();
        }

        _log?.LogState("InitializeBattle", "完了");
    }

    private bool ShouldDelayInitialBgmForTutorialOpening()
    {
        return _tutorialRun && IsActiveTutorialStage() && _battleTutorial != null;
    }

    private static void TryStartInitialBgm()
    {
        SoundManager soundManager = SoundManager.EnsureInstance();
        if (soundManager == null)
        {
            return;
        }

        soundManager.EnsureInitialBgmPlaying();
    }

    private static void TryStopInitialBgm()
    {
        SoundManager soundManager = SoundManager.EnsureInstance();
        if (soundManager == null)
        {
            return;
        }

        soundManager.StopInitialBgmImmediate();
    }

    private void ResetSkillQteVariantHistories()
    {
        if (_skillSlots == null)
        {
            return;
        }

        for (int i = 0; i < _skillSlots.Length; i++)
        {
            _skillSlots[i]?.ResetQteVariantHistory();
        }
    }

    private async UniTask WaveStartAsync(CancellationToken token)
    {
        if (_waveIndex < 0 || _waveIndex >= WaveCount)
        {
            Debug.LogError("[InGameManager] WaveIndex が範囲外です。", this);
            _battleEnded = true;
            return;
        }

        WaveConfigSO wave = _activeStage.GetWave(_waveIndex);
        if (wave == null || wave.Enemies == null)
        {
            Debug.LogError("[InGameManager] WaveConfig が不正です。", this);
            return;
        }

        string stageId = _activeStage.StageId ?? "?";
        _log?.Log(InGameLogCategory.Wave, $"WaveStart stageId={stageId} waveIndex={_waveIndex}");
        if (_waveIndex == 0 && _waveTransition != null)
        {
            await _waveTransition.PlayBattleStartAsync(_waveIndex, token);
            return;
        }

        SpawnWaveEnemiesForTransition();
        await WaveStartPostSpawnAsync(playWaveStartSe: true, token);
    }

    /// <summary>ウェーブ遷移のグリッチピーク中に次ウェーブ敵を生成する。</summary>
    public void SpawnWaveEnemiesForTransition()
    {
        if (_waveIndex < 0 || _waveIndex >= WaveCount)
        {
            Debug.LogError("[InGameManager] WaveIndex が範囲外です。", this);
            _battleEnded = true;
            return;
        }

        WaveConfigSO wave = _activeStage.GetWave(_waveIndex);
        if (wave == null || wave.Enemies == null)
        {
            Debug.LogError("[InGameManager] WaveConfig が不正です。", this);
            return;
        }

        ResetEnrageForNewWave();
        DespawnAllEnemyInstances();
        ClearEnemySlots();

        for (int i = 0; i < wave.Enemies.Length && i < EnemySlotCount; i++)
        {
            EnemyDataSO enemyData = wave.Enemies[i];
            if (enemyData == null)
            {
                continue;
            }

            if (_enemySpawnPoints[i] == null)
            {
                Debug.LogError($"[InGameManager] スポーン点 index {i} が null です。", this);
                continue;
            }

            EnemyView prefab = enemyData.ViewPrefab;
            if (prefab == null)
            {
                Debug.LogError(
                    $"[InGameManager] EnemyDataSO '{enemyData.name}' (enemyId={enemyData.EnemyId}) の ViewPrefab が未設定です。",
                    this);
                continue;
            }

            EnemyModel model = new EnemyModel(i, enemyData);
            _enemySlots[i] = model;
            EnemyView view = Instantiate(prefab, _enemySpawnPoints[i], false);
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.identity;
            _spawnedEnemyViews[i] = view;
            view.Setup(model);
            BindEnemyDeathWatcher(i, model);
            string enemyId = enemyData.EnemyId ?? "?";
            _log?.Log(
                InGameLogCategory.Wave,
                $"スポーン slot={i} enemyId={enemyId} hp={model.CurrentHp.Value}/{model.MaxHp.Value} speed={model.Speed.Value}");
        }
    }

    /// <summary>敵スポーン後のカメラ・UI・SE セットアップ。</summary>
    /// <param name="playWaveStartSe">false のとき BattleWaveStart は再生しない（WaveMark 表示時に既に鳴らした場合）。</param>
    public async UniTask WaveStartPostSpawnAsync(bool playWaveStartSe, CancellationToken token)
    {
        if (_tutorialRun)
        {
            Canvas hostCanvas = _commandPanelView != null
                ? _commandPanelView.GetComponentInParent<Canvas>(true)?.rootCanvas
                : null;
            BattleTutorialUiFactory.EnsureHostCanvasVisible(hostCanvas);
        }

        BindBattleCameraEnemyTargets();
        if (_targetSelectView != null)
        {
            await _targetSelectView.ResetStageAsync(token);
        }

        if (playWaveStartSe)
        {
            InGameSe.Play(InGameSeKey.BattleWaveStart);
        }

        _log?.LogState("WaveStart", $"waveIndex={_waveIndex} ターゲットUIリセット完了");
        RefreshWavePanel();
        await TryPlayWaveTutorialAsync(WaveTutorialMoment.AfterWaveStart, token);
        PrewarmUpcomingTutorialVideos(token);
    }

    private void PrewarmUpcomingTutorialVideos(CancellationToken token)
    {
        if (_battleTutorial == null || _activeStage == null)
        {
            return;
        }

        WaveConfigSO wave = _activeStage.GetWave(_waveIndex);
        if (wave == null)
        {
            return;
        }

        _battleTutorial.PrewarmWaveVideos(wave, token);
    }

    private void RefreshWavePanel()
    {
        if (_wavePanelView == null || _activeStage == null)
        {
            return;
        }

        _wavePanelView.Refresh(_waveIndex, WaveCount);
        _wavePanelView.SetVisible(true);
    }

    /// <summary>ウェーブ遷移で敵が既に配置済みであることを示す。</summary>
    public void MarkWaveReadyFromTransition()
    {
        _waveEnemiesReadyFromTransition = true;
    }

    /// <summary>遷移開始前に遅延撃破 Destroy を反映する。</summary>
    public void FlushPendingEnemyRemovalsForTransition()
    {
        FlushPendingEnemyRemovals();
    }

    /// <summary>コマンド入力の一括ブロック（ウェーブ遷移中など）。</summary>
    public void SetBattleInputBlocked(bool blocked)
    {
        _battleInputBlocked = blocked;
        if (_commandPanelView != null)
        {
            if (blocked)
            {
                _commandPanelView.SetAllInteractable(false);
            }
            else
            {
                RefreshCommandInteractable();
            }
        }
    }

    /// <summary>
    /// 生成済み敵インスタンスを破棄する。
    /// </summary>
    private void DespawnAllEnemyInstances()
    {
        for (int i = 0; i < _spawnedEnemyViews.Length; i++)
        {
            EnemyView view = _spawnedEnemyViews[i];
            if (view != null)
            {
                Destroy(view.gameObject);
                _spawnedEnemyViews[i] = null;
            }
        }
    }

    private void ClearEnemySlots()
    {
        for (int i = 0; i < EnemySlotCount; i++)
        {
            DisposeEnemyDeathSubscription(i);
            _enemySlots[i]?.Dispose();
            _enemySlots[i] = null;
        }
    }

    /// <summary>
    /// HPが0になったときに敵ビュー（子のUI含む）とモデルを破棄する購読を張る。
    /// </summary>
    /// <param name="slot">スロット。</param>
    /// <param name="model">敵モデル。</param>
    private void BindEnemyDeathWatcher(int slot, EnemyModel model)
    {
        DisposeEnemyDeathSubscription(slot);
        if (model == null)
        {
            return;
        }

        _enemyDeathSubscriptions[slot] = model.CurrentHp
            .Where(hp => hp <= 0)
            .Take(1)
            .Subscribe(_ => RemoveDeadEnemyAtSlot(slot));
    }

    /// <summary>
    /// 敵死亡時の購読を解除する。
    /// </summary>
    /// <param name="slot">スロット。</param>
    private void DisposeEnemyDeathSubscription(int slot)
    {
        if (slot < 0 || slot >= EnemySlotCount)
        {
            return;
        }

        IDisposable disposable = _enemyDeathSubscriptions[slot];
        if (disposable != null)
        {
            disposable.Dispose();
            _enemyDeathSubscriptions[slot] = null;
        }
    }

    /// <summary>
    /// 撃破時に敵オブジェクトをDestroyし、スロットを空にする（再生成は次ウェーブのInstantiateに任せる）。
    /// </summary>
    /// <param name="slot">スロット。</param>
    private void RemoveDeadEnemyAtSlot(int slot)
    {
        if (slot < 0 || slot >= EnemySlotCount)
        {
            return;
        }

        if (_enemySlots[slot] == null)
        {
            return;
        }

        if (_deferEnemyRemoval)
        {
            if (!_pendingEnemyRemovalSlots.Contains(slot))
            {
                _pendingEnemyRemovalSlots.Add(slot);
                NotifyEnemyDefeated(slot);
            }

            return;
        }

        NotifyEnemyDefeated(slot);
        DestroyEnemyAtSlot(slot);
    }

    private void NotifyEnemyDefeated(int slot)
    {
        EnemyModel deadModel = _enemySlots[slot];
        if (deadModel == null)
        {
            return;
        }

        string enemyIdLog = deadModel.Data != null ? deadModel.Data.EnemyId : "?";
        if (_lastTargetSlot == slot)
        {
            _lastTargetSlot = -1;
        }

        ClearAllEnemySelection();
        _log?.Log(InGameLogCategory.Enemy, $"撃破 slot={slot} enemyId={enemyIdLog}");
        InGameSe.Play(InGameSeKey.CombatEnemyDefeat);
    }

    private void DestroyEnemyAtSlot(int slot)
    {
        if (slot < 0 || slot >= EnemySlotCount)
        {
            return;
        }

        if (_enemySlots[slot] == null)
        {
            return;
        }

        DisposeEnemyDeathSubscription(slot);

        EnemyView view = _spawnedEnemyViews[slot];
        if (view != null)
        {
            Destroy(view.gameObject);
            _spawnedEnemyViews[slot] = null;
        }

        EnemyModel deadModel = _enemySlots[slot];
        deadModel.Dispose();
        _enemySlots[slot] = null;
    }

    private void FlushPendingEnemyRemovals()
    {
        for (int i = 0; i < _pendingEnemyRemovalSlots.Count; i++)
        {
            DestroyEnemyAtSlot(_pendingEnemyRemovalSlots[i]);
        }

        _pendingEnemyRemovalSlots.Clear();
    }

    /// <summary>
    /// 全生存敵のターゲット選択マーカーを消す。
    /// </summary>
    private void ClearAllEnemySelection()
    {
        for (int i = 0; i < EnemySlotCount; i++)
        {
            EnemyModel enemyModel = _enemySlots[i];
            if (enemyModel != null)
            {
                enemyModel.SetSelected(false);
            }
        }
    }

    private void ClearPlayerTargetMarker()
    {
        _playerView?.SetTargetMarkerVisible(false);
    }

    /// <summary>
    /// 敵・プレイヤー双方のターゲットマーカーを消す。
    /// </summary>
    private void ClearAllTargetMarkers()
    {
        ClearAllEnemySelection();
        ClearPlayerTargetMarker();
    }

    private void ResetEnrageForNewWave()
    {
        _currentRoundCount = 0;
        _enrageMultiplier = 1f;
        _enrageBuffStackCount = 0;
    }

    private void OnRoundStart()
    {
        _enrageAppliedLastRoundStart = false;
        _currentRoundCount++;
        if (!ShouldApplyEnrageThisRound(_currentRoundCount))
        {
            return;
        }

        _enrageMultiplier += _enrageMultiplierStep;
        _enrageBuffStackCount++;
        _enrageAppliedLastRoundStart = true;
        _log?.Log(
            InGameLogCategory.Combat,
            $"狂暴化 round={_currentRoundCount} stack={_enrageBuffStackCount} mult={_enrageMultiplier:F2}");
    }

    private bool ShouldApplyEnrageThisRound(int roundCount)
    {
        return roundCount >= 3
            && roundCount % 2 == 1
            && _enrageBuffStackCount < _maxEnrageBuffStacks;
    }

    private async UniTask RunRoundLoopAsync(CancellationToken token)
    {
        _log?.LogState("RunRoundLoop", "開始（ウェーブ内ラウンド処理）");
        while (!_battleEnded && !_pendingWaveAdvance)
        {
            OnRoundStart();
            if (_enrageAppliedLastRoundStart)
            {
                await TryPlayWaveTutorialAsync(WaveTutorialMoment.OnEnrageRoundStart, token);
            }

            BuildActionQueue();
            if (_actionQueue.Count == 0)
            {
                _log?.Log(InGameLogCategory.TurnOrder, "行動キューが空のため結果判定へ");
                await CheckResultAsync(token);
                if (_battleEnded || _pendingWaveAdvance)
                {
                    break;
                }

                continue;
            }

            while (_actionQueue.Count > 0 && !_battleEnded && !_pendingWaveAdvance)
            {
                ActionUnit unit = _actionQueue.Dequeue();
                if (IsUnitDead(unit))
                {
                    continue;
                }

                if (unit.IsPlayer)
                {
                    _log?.Log(InGameLogCategory.TurnOrder, "実行: プレイヤーターン");
                    await PlayerTurnAsync(token);
                }
                else
                {
                    List<int> batchSlots = CollectConsecutiveEnemySlots(unit);
                    _log?.Log(InGameLogCategory.TurnOrder, $"実行: 敵まとめ攻撃 slots=[{string.Join(",", batchSlots)}]");
                    await EnemyBatchTurnAsync(batchSlots, token);
                }

                await CheckResultAsync(token);
            }
        }
    }

    private void BuildActionQueue()
    {
        _actionQueue.Clear();
        List<ActionUnit> list = new List<ActionUnit>();
        if (_player.CurrentHp.Value > 0)
        {
            list.Add(ActionUnit.ForPlayer());
        }

        for (int i = 0; i < EnemySlotCount; i++)
        {
            EnemyModel enemyModel = _enemySlots[i];
            if (enemyModel != null && enemyModel.IsAlive())
            {
                list.Add(ActionUnit.ForEnemy(i));
            }
        }

        list.Sort(CompareActionOrder);
        for (int i = 0; i < list.Count; i++)
        {
            _actionQueue.Enqueue(list[i]);
        }

        if (_log != null && _log.IsEnabled(InGameLogCategory.TurnOrder))
        {
            _log.Log(InGameLogCategory.TurnOrder, "行動順キュー: " + DescribeActionQueue(list));
        }
    }

    private static string DescribeActionQueue(List<ActionUnit> ordered)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < ordered.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(" → ");
            }

            ActionUnit u = ordered[i];
            if (u.IsPlayer)
            {
                sb.Append("Player");
            }
            else
            {
                sb.Append("Enemy[");
                sb.Append(u.EnemySlot);
                sb.Append(']');
            }
        }

        return sb.Length == 0 ? "(空)" : sb.ToString();
    }

    private int CompareActionOrder(ActionUnit a, ActionUnit b)
    {
        int speedA = GetSpeed(a);
        int speedB = GetSpeed(b);
        int compare = speedB.CompareTo(speedA);
        if (compare != 0)
        {
            return compare;
        }

        return UnityEngine.Random.Range(0, 2) == 0 ? -1 : 1;
    }

    private int GetSpeed(ActionUnit unit)
    {
        if (unit.IsPlayer)
        {
            return _player.Speed.Value;
        }

        EnemyModel enemyModel = _enemySlots[unit.EnemySlot];
        if (enemyModel == null)
        {
            return 0;
        }

        return enemyModel.Speed.Value;
    }

    private bool IsUnitDead(ActionUnit unit)
    {
        if (unit.IsPlayer)
        {
            return _player.CurrentHp.Value <= 0;
        }

        EnemyModel enemyModel = _enemySlots[unit.EnemySlot];
        return enemyModel == null || !enemyModel.IsAlive();
    }

    private async UniTask PlayerTurnAsync(CancellationToken token)
    {
        _log?.LogState("PlayerTurn", "開始");
        RefreshCommandInteractable();
        if (_tutorialAttackFocusPending)
        {
            BeginTutorialAttackFocus();
        }

        (int skillSlot, int targetSlot) = await RunCommandAndTargetSelectionAsync(token);
        ClearAllTargetMarkers();
        _commandPanelView.ClearSelection();
        SkillDataSO skill = _skillSlots[skillSlot];
        string skillId = skill != null ? skill.SkillId : "?";
        _log?.Log(InGameLogCategory.Player, $"スキル決定 slot={skillSlot} id={skillId} category={skill.Category}");
        if (targetSlot >= 0)
        {
            _log?.Log(InGameLogCategory.Player, $"攻撃ターゲット slot={targetSlot}");
        }

        UniTask scratchTask = _qtePresenter.PlayBgmScratchOnConfirmAsync(token);

        bool isAttackSkill = skill != null && skill.Category == SkillCategory.Attack;
        UniTask preQteSetupTask = UniTask.CompletedTask;
        if (!isAttackSkill)
        {
            _battleCamera?.FocusDefault();
            if (_targetSelectView != null)
            {
                preQteSetupTask = _targetSelectView.ResetStageAsync(token);
            }
        }

        _commandPanelView.SetAllInteractable(false);

        try
        {
            AudioSource activeBgm = SoundManager.EnsureInstance().ActiveBgmSource;
            float bgmVolumeBeforeQte = activeBgm != null ? activeBgm.volume : 1f;
            UniTask fadeDownTask = FadeBgmVolumeAfterDelayAsync(
                bgmVolumeBeforeQte * _bgmVolumeDuringQte,
                _bgmFadeSeconds,
                QtePresenter.DefaultBgmScratchLeadSeconds,
                token);
            UniTask hideHudTask = _battleHudSlide != null
                ? _battleHudSlide.PlayHideAsync(token)
                : UniTask.CompletedTask;
            UniTask qtePerformTask = _playerView != null
                ? _playerView.PlayQtePerformAsync(token)
                : UniTask.CompletedTask;
            await UniTask.WhenAll(scratchTask, preQteSetupTask, hideHudTask, fadeDownTask, qtePerformTask);
            if (!_hasPlayedBeforeFirstQteTutorial)
            {
                _hasPlayedBeforeFirstQteTutorial = true;
                await TryPlayWaveTutorialAsync(WaveTutorialMoment.BeforeFirstQte, token);
            }

            IReadOnlyList<QteJudgment> judgments =
                await _qtePresenter.RunSkillQteAsync(skill, _battleSettings, token);
            await FadeBgmVolumeAsync(bgmVolumeBeforeQte, token);
            _log?.Log(InGameLogCategory.Qte, $"QTE id={skillId} 結果=[{FormatJudgmentList(judgments)}]");

            if (_battleHudSlide != null)
            {
                await _battleHudSlide.PlayShowAsync(token);
            }

            await ApplySkillOutcomesAsync(skill, judgments, targetSlot, token);
            _hudView.Refresh(_player);
            _log?.Log(InGameLogCategory.Player, $"ターン終了時 hp={_player.CurrentHp.Value}/{_player.MaxHp.Value}");
        }
        finally
        {
            _battleHudSlide?.KillAndReset();
        }

        _log?.LogState("PlayerTurn", "終了");
    }

    private List<int> CollectConsecutiveEnemySlots(ActionUnit firstEnemyUnit)
    {
        var slots = new List<int> { firstEnemyUnit.EnemySlot };
        if (!_useEnemyBatchAttack)
        {
            return slots;
        }

        while (_actionQueue.Count > 0)
        {
            ActionUnit peek = _actionQueue.Peek();
            if (peek.IsPlayer || IsUnitDead(peek))
            {
                break;
            }

            slots.Add(_actionQueue.Dequeue().EnemySlot);
        }

        return slots;
    }

    private async UniTask EnemyBatchTurnAsync(IReadOnlyList<int> slots, CancellationToken token)
    {
        var attackers = new List<(int slot, EnemyModel model, EnemyView view)>();
        for (int i = 0; i < slots.Count; i++)
        {
            int slot = slots[i];
            EnemyModel enemyModel = _enemySlots[slot];
            EnemyView enemyView = _spawnedEnemyViews[slot];
            if (enemyModel == null || !enemyModel.IsAlive() || enemyView == null)
            {
                continue;
            }

            attackers.Add((slot, enemyModel, enemyView));
        }

        if (attackers.Count == 0)
        {
            return;
        }

        var slotList = new StringBuilder();
        for (int i = 0; i < attackers.Count; i++)
        {
            if (i > 0)
            {
                slotList.Append(',');
            }

            slotList.Append(attackers[i].slot);
        }

        _log?.LogState("EnemyBatchTurn", $"slots=[{slotList}] count={attackers.Count} 開始");

        List<UniTask> hopTasks = new List<UniTask>(attackers.Count);
        for (int i = 0; i < attackers.Count; i++)
        {
            float delay = i * _enemyBatchHopStaggerSeconds;
            hopTasks.Add(PlayAttackFxAfterDelayAsync(attackers[i].view, delay, token));
        }

        await UniTask.WhenAll(hopTasks);

        if (_playerView != null)
        {
            Transform hitPoint = _playerView.CombatHitPoint;
            List<UniTask> trailTasks = new List<UniTask>(attackers.Count);
            for (int i = 0; i < attackers.Count; i++)
            {
                trailTasks.Add(attackers[i].view.PlayAttackTrailToPlayerAsync(hitPoint, token));
            }

            await UniTask.WhenAll(trailTasks);
        }

        int totalDamage = 0;
        var damageBreakdown = new StringBuilder();
        for (int i = 0; i < attackers.Count; i++)
        {
            int basePower = attackers[i].model.Data != null ? attackers[i].model.Data.AttackPower : 0;
            int damage = Mathf.FloorToInt(basePower * _enrageMultiplier);
            totalDamage += damage;
            if (i > 0)
            {
                damageBreakdown.Append('+');
            }

            damageBreakdown.Append(damage);
        }

        int hpBefore = _player.CurrentHp.Value;
        if (totalDamage > 0 && _hitStop != null)
        {
            await _hitStop.PlayAsync(CombatHitStopKind.PlayerDamage, token);
        }

        _player.ApplyDamage(totalDamage);
        if (totalDamage > 0)
        {
            _cameraGlitchManager?.TriggerDamageGlitch();
        }

        _hudView.Refresh(_player);
        if (totalDamage > 0 && _playerView != null)
        {
            ShowFloatingText(
                CombatFloatingTextKind.DamageToPlayer,
                totalDamage,
                _playerView.GetFloatingTextWorldPosition());
        }

        _log?.Log(
            InGameLogCategory.Combat,
            $"敵まとめ攻撃 slots=[{slotList}] damages=[{damageBreakdown}] mult={_enrageMultiplier:F2} total={totalDamage} playerHp {hpBefore}→{_player.CurrentHp.Value}");
        if (totalDamage > 0)
        {
            _battleCamera?.PlayDamageShake();
        }

        if (totalDamage > 0 && _playerView != null)
        {
            InGameSe.Play(InGameSeKey.CombatPlayerDamage);
            await _playerView.PlayReceiveDamageAsync(token);
        }

        _log?.LogState("EnemyBatchTurn", $"slots=[{slotList}] 終了");
    }

    private static async UniTask PlayAttackFxAfterDelayAsync(EnemyView view, float delay, CancellationToken token)
    {
        if (delay > 0f)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
        }

        await view.PlayAttackFxAsync(token);
    }

    private async UniTask CheckResultAsync(CancellationToken token)
    {
        if (_player.CurrentHp.Value <= 0)
        {
            _battleEnded = true;
            _log?.Log(InGameLogCategory.Result, "敗北（プレイヤー HP 0）");
            _playerView?.PlayLost();
            await ReturnToDefaultViewForVictoryResultAsync(token);
            await PrepareForResultPresentationAsync(token);
            InGameSe.Play(InGameSeKey.ResultDefeat);
            await _resultView.ShowDefeatCutInAsync(token);
            return;
        }

        if (AllEnemiesDead())
        {
            if (_waveIndex >= WaveCount - 1)
            {
                _battleEnded = true;
                _log?.Log(InGameLogCategory.Result, "勝利（全ウェーブ完了）");
                bool isTutorialStage = IsActiveTutorialStage();
                if (isTutorialStage)
                {
                    BattleTutorialProgress.MarkCompleted();
                    TitleIntroProgress.MarkCompleted();
                }
                else
                {
                    string defeatedNoiseName = BattleStageSession.CommittedBattleNoiseChildName;
                    if (string.IsNullOrEmpty(defeatedNoiseName))
                    {
                        defeatedNoiseName = BattleStageSession.LastPlayedNoiseChildName;
                    }

                    if (string.IsNullOrEmpty(defeatedNoiseName)
                        && StageBattleStageIds.TryResolveNoiseChildNameForStageId(
                            BattleStageSession.LastRequestedStageId,
                            out string noiseFromStageId))
                    {
                        defeatedNoiseName = noiseFromStageId;
                    }

                    bool markedDefeated = false;
                    if (!string.IsNullOrEmpty(defeatedNoiseName))
                    {
                        StageDefeatedNoiseRegistry.MarkDefeated(defeatedNoiseName);
                        markedDefeated = StageDefeatedNoiseRegistry.IsDefeated(defeatedNoiseName);
                        BattleStageSession.ClearCommittedBattleNoise();
                    }

                    if (markedDefeated)
                    {
                        OutGameScanNoiseRevealCount.ConsumeOneAfterBattleVictory(0f);
                    }
                    else
                    {
                        Debug.LogWarning(
                            "[InGameManager] 撃破した Noises 子名が未確定のため、距離リビールのみは行いません。"
                            + " StageScene で ToReady 後に戦闘したノイズがフォーカスされているか確認してください。",
                            this);
                    }

                    GrantVictoryBattleRewards();
                }

                await ReturnToDefaultViewForVictoryResultAsync(token);
                await PrepareForResultPresentationAsync(token);
                InGameSe.Play(InGameSeKey.ResultVictory);
                await _resultView.ShowVictoryCutInAsync(token);
                return;
            }

            _pendingWaveAdvance = true;
            _waveIndex++;
            InGameSe.Play(InGameSeKey.BattleWaveClear);
            _log?.Log(InGameLogCategory.Result, $"ウェーブ全滅 → 次ウェーブへ advance waveIndex={_waveIndex}");
            await UniTask.Yield(token);
        }
    }

    private bool IsActiveTutorialStage()
    {
        if (_activeStage != null && _activeStage.IsTutorialStage)
        {
            return true;
        }

        string stageId = _activeStage != null ? _activeStage.StageId : null;
        return TutorialStageIds.IsTutorialStageId(stageId);
    }

    private async UniTask TryPlayWaveTutorialAsync(WaveTutorialMoment moment, CancellationToken token)
    {
        if (_battleTutorial == null || _activeStage == null)
        {
            return;
        }

        WaveConfigSO wave = _activeStage.GetWave(_waveIndex);
        if (wave == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[InGameManager] チュートリアルをスキップ: wave が null (moment={moment}, waveIndex={_waveIndex})",
                this);
#endif
            return;
        }

        IReadOnlyList<BattleTutorialStepSO> steps = wave.GetTutorialStepsForMoment(moment);
        if (steps == null || steps.Count == 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                $"[InGameManager] チュートリアルステップ 0 件のためスキップ: moment={moment}, wave={wave.name}",
                this);
#endif
            return;
        }

        try
        {
            await _battleTutorial.TryPlayAsync(wave, moment, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _log?.Log(InGameLogCategory.Wave, $"チュートリアル表示がキャンセルされました: moment={moment}");
        }

        if (moment == WaveTutorialMoment.OnEnrageRoundStart)
        {
            _tutorialCapPlayerDamageUntilEnrageGuide = false;
        }
    }

    private void GrantVictoryBattleRewards()
    {
        if (IsActiveTutorialStage())
        {
            return;
        }

        if (PlayerBattleRewardManager.Instance == null)
        {
            return;
        }

        string stageId = BattleStageSession.LastPlayedStageId;
        if (string.IsNullOrEmpty(stageId))
        {
            stageId = BattleStageSession.LastRequestedStageId;
        }

        if (string.IsNullOrEmpty(stageId))
        {
            return;
        }

        BattleRewardCounts granted =
            PlayerBattleRewardManager.Instance.GrantVictoryRewardsForStageId(stageId);
        if (granted.IsEmpty)
        {
            return;
        }

        _log?.Log(
            InGameLogCategory.Result,
            $"バトル報酬付与 stage={stageId} → {granted}");
    }

    private async UniTask PrepareForResultPresentationAsync(CancellationToken token)
    {
        UniTask fadeOutTask = SoundManager.EnsureInstance()
            .FadeOutAndStopActiveBgmAsync(_resultBgmFadeSeconds, token);
        UniTask hideHudTask = _battleHudSlide != null
            ? _battleHudSlide.PlayHideAsync(token)
            : UniTask.CompletedTask;
        await UniTask.WhenAll(fadeOutTask, hideHudTask);

        if (_resultSeDelayAfterFadeStart > 0f)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(_resultSeDelayAfterFadeStart),
                cancellationToken: token);
        }
    }

    /// <summary>
    /// 勝利リザルト表示前にカメラ・ステージをデフォルトへ戻す。
    /// </summary>
    private async UniTask ReturnToDefaultViewForVictoryResultAsync(CancellationToken token)
    {
        if (_battleCamera != null)
        {
            await _battleCamera.FocusDefaultAsync(token);
        }

        if (_targetSelectView != null)
        {
            await _targetSelectView.ResetStageAsync(token);
        }
    }

    private bool AllEnemiesDead()
    {
        for (int i = 0; i < EnemySlotCount; i++)
        {
            EnemyModel enemyModel = _enemySlots[i];
            if (enemyModel != null && enemyModel.IsAlive())
            {
                return false;
            }
        }

        return true;
    }

    private void RefreshCommandInteractable()
    {
        for (int i = 0; i < _skillSlots.Length; i++)
        {
            ApplyCommandSlotInteractable(i, IsSkillSlotUsable(i));
        }

        _commandPanelView.RefreshAfterInteractableBatch();
    }

    private void ApplyCommandSlotInteractable(int slotIndex, bool interactable)
    {
        if (_commandPanelView == null)
        {
            return;
        }

        _commandPanelView.SetSlotInteractableWithoutRefresh(slotIndex, interactable);
    }

    private bool IsSkillSlotUsable(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _skillSlots.Length)
        {
            return false;
        }

        if (_tutorialAttackOnlyActive && slotIndex != CommandPanelView.AttackSlotIndex)
        {
            return false;
        }

        return _skillSlots[slotIndex] != null;
    }

    private void BeginTutorialAttackFocus()
    {
        if (!_tutorialAttackFocusPending || _commandPanelView == null)
        {
            return;
        }

        _tutorialAttackOnlyActive = true;
        RefreshCommandInteractable();

        RectTransform attackButton = _commandPanelView.GetSlotButtonRectTransform(CommandPanelView.AttackSlotIndex);
        _battleTutorial?.ShowAttackCommandFocus(attackButton);
        _commandPanelView.SetAttackArrowVisible(true);
    }

    private void EndTutorialAttackFocus()
    {
        if (!_tutorialAttackFocusPending && !_tutorialAttackOnlyActive)
        {
            return;
        }

        _tutorialAttackFocusPending = false;
        _tutorialAttackOnlyActive = false;
        _battleTutorial?.HideAttackCommandFocus();
        _commandPanelView?.SetAttackArrowVisible(false);
        RefreshCommandInteractable();
    }

    /// <summary>
    /// コマンド選択開始時の初期選択枠（常に未選択）。
    /// </summary>
    private int GetDefaultCommandSelectSlot()
    {
        return -1;
    }

    private async UniTask<(int skillSlot, int targetSlot)> RunCommandAndTargetSelectionAsync(CancellationToken token)
    {
        _commandPanelView.ClearSelection();
        _battleCamera?.FocusDefault();
        int selectedSlot = GetDefaultCommandSelectSlot();
        if (selectedSlot >= 0)
        {
            _commandPanelView.SetSelectedSlot(selectedSlot);
            UpdateCommandTargetMarkers(selectedSlot);
        }

        AttackTargetFocusState attackFocusState = new AttackTargetFocusState();

        while (true)
        {
            CommandSelectInput commandInput = await WaitCommandSelectInputAsync(
                selectedSlot,
                attackFocusState,
                token);
            if (commandInput.Kind == CommandSelectInputKind.ConfirmAttackTarget)
            {
                _lastTargetSlot = commandInput.Slot;
                return (selectedSlot, commandInput.Slot);
            }

            if (commandInput.Kind == CommandSelectInputKind.Cancel)
            {
                bool needStageReset = IsAttackSkillSlot(selectedSlot) && attackFocusState.HasFocused;
                selectedSlot = -1;
                attackFocusState.HasFocused = false;
                _commandPanelView.ClearSelection();
                ClearAllTargetMarkers();
                _battleCamera?.FocusDefault();
                if (needStageReset)
                {
                    await _targetSelectView.ResetStageAsync(token);
                }

                continue;
            }

            if (commandInput.Kind == CommandSelectInputKind.SelectOnly)
            {
                selectedSlot = commandInput.Slot;
                attackFocusState.HasFocused = false;
                _commandPanelView.SetSelectedSlot(selectedSlot);
                UpdateCommandTargetMarkers(selectedSlot);
                ApplyCameraForCommandSlot(selectedSlot);
                continue;
            }

            int skillSlot = commandInput.Slot;
            if (_tutorialAttackOnlyActive && IsAttackSkillSlot(skillSlot))
            {
                EndTutorialAttackFocus();
            }

            SkillDataSO skill = _skillSlots[skillSlot];
            if (skill == null || skill.Category != SkillCategory.Attack)
            {
                return (skillSlot, -1);
            }

            int targetSlot = ResolveAttackTargetSlotForConfirm();
            ApplySelectionToEnemies(targetSlot);
            if (!attackFocusState.HasFocused)
            {
                UniTask cameraTask = _battleCamera != null
                    ? _battleCamera.FocusEnemyAsync(targetSlot, token)
                    : UniTask.CompletedTask;
                UniTask stageTask = _targetSelectView != null
                    ? _targetSelectView.FocusEnemyAsync(targetSlot, token)
                    : UniTask.CompletedTask;
                await UniTask.WhenAll(cameraTask, stageTask);
            }
            else
            {
                _battleCamera?.FocusEnemy(targetSlot);
            }

            _lastTargetSlot = targetSlot;
            return (skillSlot, targetSlot);
        }
    }

    private bool IsAttackSkillSlot(int skillSlot)
    {
        if (skillSlot < 0 || skillSlot >= _skillSlots.Length)
        {
            return false;
        }

        SkillDataSO skill = _skillSlots[skillSlot];
        return skill != null && skill.Category == SkillCategory.Attack;
    }

    private sealed class AttackTargetFocusState
    {
        public bool HasFocused;
    }

    private async UniTask<CommandSelectInput> WaitCommandSelectInputAsync(
        int currentSelectedSlot,
        AttackTargetFocusState attackFocusState,
        CancellationToken token)
    {
        while (true)
        {
            CommandSelectInput input = await WaitCommandSelectInputOnceAsync(
                currentSelectedSlot,
                attackFocusState,
                token);
            if (!IsAttackSkillSlot(currentSelectedSlot))
            {
                return input;
            }

            if (input.Kind == CommandSelectInputKind.EnemySwitch)
            {
                ApplySelectionToEnemies(input.Slot);
                UniTask cameraTask = _battleCamera != null
                    ? _battleCamera.FocusEnemyAsync(input.Slot, token)
                    : UniTask.CompletedTask;
                UniTask stageTask = _targetSelectView != null
                    ? _targetSelectView.FocusEnemyAsync(input.Slot, token)
                    : UniTask.CompletedTask;
                await UniTask.WhenAll(cameraTask, stageTask);
                attackFocusState.HasFocused = true;
                continue;
            }

            return input;
        }
    }

    private async UniTask<CommandSelectInput> WaitCommandSelectInputOnceAsync(
        int currentSelectedSlot,
        AttackTargetFocusState attackFocusState,
        CancellationToken token)
    {
        UniTaskCompletionSource<CommandSelectInput> completionSource = new UniTaskCompletionSource<CommandSelectInput>();
        List<IDisposable> subscriptions = new List<IDisposable>();

        IDisposable skillSub = _commandPanelView.OnSkillSlotClicked.Subscribe(index =>
        {
            if (_battleInputBlocked)
            {
                return;
            }

            if (!IsSkillSlotUsable(index))
            {
                return;
            }

            if (currentSelectedSlot >= 0
                && index != currentSelectedSlot
                && _commandPanelView.IsSlotBackButton(index))
            {
                completionSource.TrySetResult(CommandSelectInput.Cancel());
                return;
            }

            if (_attackCommandInputMode == AttackCommandInputMode.QuickConfirm
                && IsAttackSkillSlot(index))
            {
                completionSource.TrySetResult(CommandSelectInput.Confirm(index));
                return;
            }

            if (currentSelectedSlot < 0 || index != currentSelectedSlot)
            {
                completionSource.TrySetResult(CommandSelectInput.SelectOnly(index));
                return;
            }

            completionSource.TrySetResult(CommandSelectInput.Confirm(index));
        });
        subscriptions.Add(skillSub);

        if (IsAttackSkillSlot(currentSelectedSlot)
            && _attackCommandInputMode == AttackCommandInputMode.TwoStep)
        {
            for (int i = 0; i < EnemySlotCount; i++)
            {
                EnemyView enemyView = _spawnedEnemyViews[i];
                if (enemyView == null)
                {
                    continue;
                }

                IDisposable enemySub = enemyView.OnTapped.Subscribe(slotIndex =>
                {
                    if (_battleInputBlocked)
                    {
                        return;
                    }

                    EnemyModel model = _enemySlots[slotIndex];
                    if (model == null || !model.IsAlive())
                    {
                        return;
                    }

                    int markedSlot = FindSelectedEnemySlot();
                    if (markedSlot < 0)
                    {
                        markedSlot = ComputeDefaultTargetSlot();
                    }

                    if (slotIndex == markedSlot && attackFocusState.HasFocused)
                    {
                        completionSource.TrySetResult(CommandSelectInput.ConfirmAttackTarget(slotIndex));
                        return;
                    }

                    completionSource.TrySetResult(CommandSelectInput.EnemySwitch(slotIndex));
                });
                subscriptions.Add(enemySub);
            }
        }

        try
        {
            return await completionSource.Task.AttachExternalCancellation(token);
        }
        finally
        {
            for (int i = 0; i < subscriptions.Count; i++)
            {
                subscriptions[i].Dispose();
            }
        }
    }

    private void BindBattleCameraEnemyTargets()
    {
        if (_battleCamera == null)
        {
            return;
        }

        Transform[] enemyTransforms = new Transform[EnemySlotCount];
        for (int i = 0; i < EnemySlotCount; i++)
        {
            EnemyView view = _spawnedEnemyViews[i];
            enemyTransforms[i] = view != null ? view.transform : null;
        }

        _battleCamera.BindEnemyTargets(enemyTransforms);
    }

    private void ApplyCameraForCommandSlot(int skillSlot)
    {
        if (_battleCamera == null)
        {
            return;
        }

        if (skillSlot < 0 || skillSlot >= _skillSlots.Length)
        {
            _battleCamera.FocusDefault();
            return;
        }

        SkillDataSO skill = _skillSlots[skillSlot];
        if (skill == null)
        {
            _battleCamera.FocusDefault();
            return;
        }

        if (skill.Category == SkillCategory.Attack)
        {
            int previewSlot = FindSelectedEnemySlot();
            if (previewSlot < 0)
            {
                previewSlot = ComputeDefaultTargetSlot();
            }

            _battleCamera.FocusEnemy(previewSlot);
            return;
        }

        if (skill.Category == SkillCategory.Heal)
        {
            _battleCamera.FocusPlayer();
            return;
        }

        _battleCamera.FocusDefault();
    }

    /// <summary>
    /// コマンド選択中のスキルに応じて敵またはプレイヤーのターゲットマーカーを表示する。
    /// </summary>
    private void UpdateCommandTargetMarkers(int skillSlot)
    {
        if (skillSlot < 0 || skillSlot >= _skillSlots.Length)
        {
            ClearAllTargetMarkers();
            return;
        }

        SkillDataSO skill = _skillSlots[skillSlot];
        if (skill != null && skill.Category == SkillCategory.Attack)
        {
            ClearPlayerTargetMarker();
            int previewSlot = FindSelectedEnemySlot();
            if (previewSlot < 0)
            {
                previewSlot = ComputeDefaultTargetSlot();
            }

            ApplySelectionToEnemies(previewSlot);
        }
        else if (skill != null && skill.Category == SkillCategory.Heal)
        {
            ClearAllEnemySelection();
            _playerView?.SetTargetMarkerVisible(true);
        }
        else
        {
            ClearAllTargetMarkers();
        }
    }

    private int ResolveAttackTargetSlotForConfirm()
    {
        if (_attackCommandInputMode == AttackCommandInputMode.QuickConfirm)
        {
            return ComputeLeftmostAliveEnemySlot();
        }

        int selected = FindSelectedEnemySlot();
        return selected >= 0 ? selected : ComputeDefaultTargetSlot();
    }

    /// <summary>
    /// 配列インデックスが最も小さい生存敵（画面上の左端想定）。
    /// </summary>
    private int ComputeLeftmostAliveEnemySlot()
    {
        for (int i = 0; i < EnemySlotCount; i++)
        {
            EnemyModel enemyModel = _enemySlots[i];
            if (enemyModel != null && enemyModel.IsAlive())
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// 前回ターゲット（生存時）→ 未選択ならインデックス最小の生存敵。
    /// </summary>
    private int ComputeDefaultTargetSlot()
    {
        if (_lastTargetSlot >= 0 && _lastTargetSlot < EnemySlotCount)
        {
            EnemyModel preferred = _enemySlots[_lastTargetSlot];
            if (preferred != null && preferred.IsAlive())
            {
                return _lastTargetSlot;
            }
        }

        for (int i = 0; i < EnemySlotCount; i++)
        {
            EnemyModel enemyModel = _enemySlots[i];
            if (enemyModel != null && enemyModel.IsAlive())
            {
                return i;
            }
        }

        return 0;
    }

    private int FindSelectedEnemySlot()
    {
        for (int i = 0; i < EnemySlotCount; i++)
        {
            EnemyModel enemyModel = _enemySlots[i];
            if (enemyModel != null && enemyModel.IsAlive() && enemyModel.IsSelected.Value)
            {
                return i;
            }
        }

        return -1;
    }

    private void ApplySelectionToEnemies(int selectedSlot)
    {
        for (int i = 0; i < EnemySlotCount; i++)
        {
            EnemyModel enemyModel = _enemySlots[i];
            if (enemyModel == null)
            {
                continue;
            }

            enemyModel.SetSelected(i == selectedSlot && enemyModel.IsAlive());
        }
    }

    private enum CommandSelectInputKind
    {
        SelectOnly,
        Confirm,
        Cancel,
        EnemySwitch,
        ConfirmAttackTarget,
    }

    private readonly struct CommandSelectInput
    {
        public CommandSelectInputKind Kind { get; }
        public int Slot { get; }

        private CommandSelectInput(CommandSelectInputKind kind, int slot)
        {
            Kind = kind;
            Slot = slot;
        }

        public static CommandSelectInput SelectOnly(int slot)
        {
            InGameSe.Play(InGameSeKey.UiCommandSelect);
            return new CommandSelectInput(CommandSelectInputKind.SelectOnly, slot);
        }

        public static CommandSelectInput Confirm(int slot)
        {
            InGameSe.Play(InGameSeKey.UiCommandConfirm);
            return new CommandSelectInput(CommandSelectInputKind.Confirm, slot);
        }

        public static CommandSelectInput Cancel()
        {
            InGameSe.Play(InGameSeKey.UiCommandBack);
            return new CommandSelectInput(CommandSelectInputKind.Cancel, -1);
        }

        public static CommandSelectInput EnemySwitch(int slot)
        {
            InGameSe.Play(InGameSeKey.UiTargetSwitch);
            return new CommandSelectInput(CommandSelectInputKind.EnemySwitch, slot);
        }

        public static CommandSelectInput ConfirmAttackTarget(int slot)
        {
            InGameSe.Play(InGameSeKey.UiTargetConfirm);
            return new CommandSelectInput(CommandSelectInputKind.ConfirmAttackTarget, slot);
        }
    }

    /// <summary>
    /// 着弾の瞬間に敵カメラへ切替（即時）。ステージ寄せは <see cref="FocusEnemyStageAsync"/>。
    /// </summary>
    private void FocusEnemyCameraForHit(int targetSlot)
    {
        _battleCamera?.FocusEnemy(targetSlot);
    }

    /// <summary>
    /// 敵スロットへステージ・パララックスを寄せる（Trail 飛行中は呼ばない）。
    /// </summary>
    private async UniTask FocusEnemyStageAsync(int targetSlot, CancellationToken token)
    {
        if (_targetSelectView != null)
        {
            await _targetSelectView.FocusEnemyAsync(targetSlot, token);
        }
    }

    /// <summary>
    /// 単体攻撃の着弾演出（カメラ切替 → 着弾 VFX ＋ダメージ一式 → ステージ寄せとパンチを並行）。
    /// </summary>
    private async UniTask PlayEnemyHitImpactPresentationAsync(
        int targetSlot,
        EnemyModel target,
        EnemyView targetView,
        PlayerAttackTrailSettings trailSettings,
        int damage,
        CancellationToken token)
    {
        if (!target.IsAlive() || targetView == null)
        {
            return;
        }

        FocusEnemyCameraForHit(targetSlot);
        SpawnEnemyAttackHitVfx(targetView, trailSettings);

        if (_hitStop != null)
        {
            await _hitStop.PlayAsync(CombatHitStopKind.AttackImpact, token);
        }

        _battleCamera?.PlayAttackHitShake();
        InGameSe.Play(InGameSeKey.CombatPlayerHit);
        int appliedDamage = ClampTutorialPlayerDamageToEnemy(damage, target);
        target.ApplyDamage(appliedDamage);
        ShowFloatingText(
            CombatFloatingTextKind.DamageToEnemy,
            appliedDamage,
            targetView.GetFloatingTextWorldPosition());

        UniTask stageTask = FocusEnemyStageAsync(targetSlot, token);
        await UniTask.WhenAll(stageTask, targetView.PlayHitFxAsync(token));
    }

    private static void SpawnEnemyAttackHitVfx(EnemyView targetView, PlayerAttackTrailSettings trailSettings)
    {
        if (trailSettings == null || targetView == null)
        {
            return;
        }

        targetView.SpawnAttackHitVfx(trailSettings.HitVfxPrefab, trailSettings.HitVfxDestroyDelay);
    }

    /// <summary>
    /// AP 全体攻撃の同時着弾（カメラ・ステージ切替なし、ヒットストップ・SE・シェイクは1回）。
    /// </summary>
    private async UniTask PlayAllTargetsHitImpactBatchAsync(
        IReadOnlyList<(EnemyModel target, EnemyView targetView)> targets,
        PlayerAttackTrailSettings trailSettings,
        int damage,
        CancellationToken token)
    {
        if (targets == null || targets.Count == 0)
        {
            return;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            EnemyModel target = targets[i].target;
            EnemyView targetView = targets[i].targetView;
            if (!target.IsAlive() || targetView == null)
            {
                continue;
            }

            SpawnEnemyAttackHitVfx(targetView, trailSettings);
        }

        if (damage > 0 && _hitStop != null)
        {
            await _hitStop.PlayAsync(CombatHitStopKind.AttackImpact, token);
        }

        if (damage > 0)
        {
            _battleCamera?.PlayAttackHitShake();
            InGameSe.Play(InGameSeKey.CombatPlayerHit);
        }

        List<UniTask> hitFxTasks = new List<UniTask>(targets.Count);
        for (int i = 0; i < targets.Count; i++)
        {
            EnemyModel target = targets[i].target;
            EnemyView targetView = targets[i].targetView;
            if (!target.IsAlive() || targetView == null)
            {
                continue;
            }

            int appliedDamage = ClampTutorialPlayerDamageToEnemy(damage, target);
            target.ApplyDamage(appliedDamage);
            ShowFloatingText(
                CombatFloatingTextKind.DamageToEnemy,
                appliedDamage,
                targetView.GetFloatingTextWorldPosition());
            hitFxTasks.Add(targetView.PlayHitFxAsync(token));
        }

        if (hitFxTasks.Count > 0)
        {
            await UniTask.WhenAll(hitFxTasks);
        }
    }

    private async UniTask PlaySingleTargetAttackOutcomeAsync(
        SkillDataSO skill,
        int targetSlot,
        int damage,
        float productMultiplier,
        CancellationToken token)
    {
        if (targetSlot < 0 || targetSlot >= EnemySlotCount || _enemySlots[targetSlot] == null)
        {
            _log?.Log(InGameLogCategory.Combat, $"単体攻撃（ターゲット無効）damage={damage} (x{productMultiplier:0.##})");
            return;
        }

        EnemyModel target = _enemySlots[targetSlot];
        string tid = target.Data != null ? target.Data.EnemyId : "?";
        EnemyView targetView = _spawnedEnemyViews[targetSlot];

        _deferEnemyRemoval = true;
        try
        {
            _battleCamera?.FocusDefault();
            if (_targetSelectView != null)
            {
                await _targetSelectView.ResetStageAsync(token);
            }

            int eHp = target.CurrentHp.Value;
            bool playHitPresentation = targetView != null && target.IsAlive();
            PlayerAttackTrailSettings trailSettings = skill.AttackTrailSettings;
            bool useAttackTrails = playHitPresentation
                && _playerView != null
                && trailSettings != null
                && trailSettings.IsConfigured;

            if (useAttackTrails)
            {
                await _playerView.PlayAttackWithTrailAsync(
                    trailSettings,
                    targetView,
                    ct => PlayEnemyHitImpactPresentationAsync(
                        targetSlot,
                        target,
                        targetView,
                        trailSettings,
                        damage,
                        ct),
                    token);
            }
            else if (playHitPresentation)
            {
                if (trailSettings != null && !trailSettings.IsConfigured)
                {
                    Debug.LogWarning("[InGameManager] Attack trail が未設定のため即ダメージにフォールバックします。");
                }

                await PlayEnemyHitImpactPresentationAsync(
                    targetSlot,
                    target,
                    targetView,
                    trailSettings,
                    damage,
                    token);
            }
            else
            {
                int appliedDamage = ClampTutorialPlayerDamageToEnemy(damage, target);
                target.ApplyDamage(appliedDamage);
            }

            _log?.Log(InGameLogCategory.Combat, $"単体攻撃 targetSlot={targetSlot} id={tid} damage={damage} (x{productMultiplier:0.##}) enemyHp {eHp}→{target.CurrentHp.Value}");
        }
        finally
        {
            _deferEnemyRemoval = false;
            FlushPendingEnemyRemovals();
            _battleCamera?.FocusDefault();
            if (_targetSelectView != null)
            {
                await _targetSelectView.ResetStageAsync(token);
            }
        }
    }

    private async UniTask PlayAllTargetAttackOutcomeAsync(
        SkillDataSO skill,
        int damage,
        float productMultiplier,
        CancellationToken token)
    {
        _deferEnemyRemoval = true;
        try
        {
            _battleCamera?.FocusDefault();
            if (_targetSelectView != null)
            {
                await _targetSelectView.ResetStageAsync(token);
            }

            var targets = new List<(EnemyModel target, EnemyView targetView)>();
            for (int slot = 0; slot < EnemySlotCount; slot++)
            {
                EnemyModel enemy = _enemySlots[slot];
                EnemyView enemyView = _spawnedEnemyViews[slot];
                if (enemy == null || !enemy.IsAlive() || enemyView == null)
                {
                    continue;
                }

                targets.Add((enemy, enemyView));
            }

            if (targets.Count == 0)
            {
                _log?.Log(InGameLogCategory.Combat, $"全体攻撃 damage={damage} (x{productMultiplier:0.##}) targets=0");
                return;
            }

            PlayerAttackTrailSettings trailSettings = skill.AttackTrailSettings;
            bool useAttackTrails = _playerView != null
                && trailSettings != null
                && trailSettings.IsConfigured;

            if (useAttackTrails)
            {
                await _playerView.PlayAttackWindupAsync(token);

                float maxFlightDuration = 0f;
                for (int i = 0; i < targets.Count; i++)
                {
                    float duration = _playerView.ComputeAttackTrailFlightDuration(
                        trailSettings,
                        targets[i].targetView);
                    if (duration > maxFlightDuration)
                    {
                        maxFlightDuration = duration;
                    }
                }

                List<UniTask> trailTasks = new List<UniTask>(targets.Count);
                for (int i = 0; i < targets.Count; i++)
                {
                    trailTasks.Add(_playerView.PlayAttackTrailToEnemyAsync(
                        trailSettings,
                        targets[i].targetView,
                        maxFlightDuration,
                        token));
                }

                await UniTask.WhenAll(trailTasks);
            }

            await PlayAllTargetsHitImpactBatchAsync(targets, trailSettings, damage, token);

            _log?.Log(InGameLogCategory.Combat, $"全体攻撃 damage={damage} (x{productMultiplier:0.##}) targets={targets.Count}");
        }
        finally
        {
            _deferEnemyRemoval = false;
            FlushPendingEnemyRemovals();
            _battleCamera?.FocusDefault();
            if (_targetSelectView != null)
            {
                await _targetSelectView.ResetStageAsync(token);
            }
        }
    }

    private async UniTask ApplySkillOutcomesAsync(SkillDataSO skill, IReadOnlyList<QteJudgment> judgments, int targetSlot, CancellationToken token)
    {
        float productMultiplier = QteOutcomeCalculator.ComputeProductMultiplier(judgments, _battleSettings);
        bool allPerfect = QteOutcomeCalculator.IsAllPerfect(judgments);

        if (skill.Category == SkillCategory.Attack)
        {
            int baseAttack = PlayerCombatStatsResolver.ResolveCurrent().Attack;
            int damage = QteOutcomeCalculator.ComputeAttackDamage(baseAttack, judgments, _battleSettings);
            damage = ClampTutorialPlayerAttackDamage(damage, baseAttack);
            if (allPerfect)
            {
                await PlayAllTargetAttackOutcomeAsync(skill, damage, productMultiplier, token);
            }
            else
            {
                await PlaySingleTargetAttackOutcomeAsync(skill, targetSlot, damage, productMultiplier, token);
            }

            return;
        }

        if (skill.Category == SkillCategory.Heal)
        {
            int heal = QteOutcomeCalculator.ComputeHealAmount(skill, judgments, _battleSettings);
            int hpBefore = _player.CurrentHp.Value;

            InGameSe.Play(InGameSeKey.CombatPlayerHeal);
            if (_playerView != null)
            {
                await _playerView.PlayHealAsync(token);
                _player.Heal(heal);
                ShowFloatingText(
                    CombatFloatingTextKind.HealHp,
                    heal,
                    _playerView.GetFloatingTextWorldPosition());
            }
            else
            {
                _player.Heal(heal);
            }

            string apTag = allPerfect ? " AP" : string.Empty;
            _log?.Log(InGameLogCategory.Combat, $"HP回復{apTag} heal={heal} (x{productMultiplier:0.##}) playerHp {hpBefore}→{_player.CurrentHp.Value}");
        }
    }

    /// <summary>
    /// 狂暴化チュートリアル前は QTE 倍率に関わらず1ヒットのダメージを上限値にクランプする。
    /// </summary>
    private int ClampTutorialPlayerAttackDamage(int computedDamage, int baseAttack)
    {
        if (!_tutorialCapPlayerDamageUntilEnrageGuide || _battleSettings == null)
        {
            return computedDamage;
        }

        int cap = _battleSettings.TutorialPlayerDamageCapPerHit;
        if (cap <= 0)
        {
            cap = Mathf.Max(1, baseAttack);
        }

        return Mathf.Min(computedDamage, cap);
    }

    /// <summary>
    /// 狂暴化チュートリアル前は敵を倒しきらないよう、与ダメージを残り HP - 1 にも制限する。
    /// </summary>
    private int ClampTutorialPlayerDamageToEnemy(int damage, EnemyModel enemy)
    {
        if (!_tutorialCapPlayerDamageUntilEnrageGuide || enemy == null || !enemy.IsAlive())
        {
            return damage;
        }

        int maxAllowed = Mathf.Max(0, enemy.CurrentHp.Value - 1);
        return Mathf.Min(damage, maxAllowed);
    }

    private void ShowFloatingText(CombatFloatingTextKind kind, int amount, Vector3 worldPosition)
    {
        if (_floatingText == null)
        {
            return;
        }

        _floatingText.Show(kind, amount, worldPosition);
    }

    private static string FormatJudgmentList(IReadOnlyList<QteJudgment> judgments)
    {
        if (judgments == null || judgments.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < judgments.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(judgments[i]);
        }

        return sb.ToString();
    }

    private async UniTask FadeBgmVolumeAsync(float endVolume, CancellationToken token)
    {
        await SoundManager.EnsureInstance()
            .FadeBgmVolumeAsync(endVolume, _bgmFadeSeconds, token);
    }

    private async UniTask FadeBgmVolumeAfterDelayAsync(
        float endVolume,
        float fadeSeconds,
        float delaySeconds,
        CancellationToken token)
    {
        if (delaySeconds > 0f)
        {
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delaySeconds),
                cancellationToken: token,
                delayTiming: PlayerLoopTiming.Update);
        }

        await SoundManager.EnsureInstance()
            .FadeBgmVolumeAsync(endVolume, fadeSeconds, token);
    }
}
