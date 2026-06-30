using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// バトル終了後の Result オーバーレイ（勝利・敗北カットイン / 旧報酬 UI）。
/// </summary>
public sealed class ResultView : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private GameObject _canvasRoot;

    [SerializeField]
    private VictoryCutInView _victoryCutInView;

    [SerializeField]
    private GameObject _victoryRoot;

    [SerializeField]
    private GameObject _defeatRoot;

    [SerializeField]
    private TMP_Text _outcomeText;

    [SerializeField]
    private GameObject _rewardsBlock;

    [SerializeField]
    private Button _toTitleButton;

    [SerializeField]
    private Button _toStageButton;

    [Header("カットイン → ResultScene")]
    [Tooltip("カットイン入場後、ResultScene へ遷移するまでの待ち時間（秒）。退場フェードは行わない。")]
    [SerializeField]
    private float _autoTransitionHoldSeconds = 0.6f;

    [SerializeField]
    private string _resultSceneName = SceneNames.Result;

    [Tooltip("Build Settings に無いシーンへの遷移を Editor でスキップする。")]
    [SerializeField]
    private bool _skipLoadIfSceneMissingInEditor = true;

    private bool _isNavigating;

    private void Awake()
    {
        if (_canvasRoot == null)
        {
            _canvasRoot = transform.parent != null ? transform.parent.gameObject : gameObject;
        }

        if (_victoryCutInView == null)
        {
            _victoryCutInView = GetComponent<VictoryCutInView>();
        }

        if (_toTitleButton != null)
        {
            _toTitleButton.onClick.AddListener(OnToTitleClicked);
        }

        if (_toStageButton != null)
        {
            _toStageButton.onClick.AddListener(OnToStageClicked);
        }

        // 初回 Show で Canvas を有効化した直後に Awake が走るため、ここで Hide しない。
        // 非表示の初期状態はシーン上の ResultCanvas 非アクティブと InGameManager.InitializeBattleAsync の Hide に任せる。
    }

    /// <summary>
    /// 勝利カットインを再生し、ホールド後に退場フェードなしで ResultScene へ自動遷移する。
    /// </summary>
    public UniTask ShowVictoryCutInAsync(CancellationToken token)
    {
        return ShowResultCutInAsync(isVictory: true, token);
    }

    /// <summary>
    /// 敗北カットインを再生し、ホールド後に退場フェードなしで ResultScene へ自動遷移する。
    /// </summary>
    public UniTask ShowDefeatCutInAsync(CancellationToken token)
    {
        return ShowResultCutInAsync(isVictory: false, token);
    }

    private async UniTask ShowResultCutInAsync(bool isVictory, CancellationToken token)
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;
        SetButtonsInteractable(false);
        HideLegacyResultUi();

        if (_canvasRoot != null)
        {
            _canvasRoot.SetActive(true);
            if (_canvasRoot.transform is RectTransform canvasRect
                && canvasRect.localScale.sqrMagnitude < 0.01f)
            {
                canvasRect.localScale = Vector3.one;
            }
        }

        if (_victoryCutInView == null)
        {
            Debug.LogError("[ResultView] VictoryCutInView が未設定です。", this);
            _isNavigating = false;
            return;
        }

        await _victoryCutInView.PlayIntroAsync(token, isVictory);

        if (_autoTransitionHoldSeconds > 0f)
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(_autoTransitionHoldSeconds),
                DelayType.UnscaledDeltaTime,
                cancellationToken: token);
        }

        _victoryCutInView.KillTweens();

        string stageId = BattleStageSession.LastPlayedStageId;
        BattleResultSession.SetPending(isVictory, stageId);

        if (SoundManager.Instance != null)
        {
            await SoundManager.Instance.FadeOutAndStopActiveBgmAsync(0f, token);
        }

        if (!TryLoadResultScene())
        {
            _isNavigating = false;
        }
    }

    /// <summary>
    /// 非表示にする。
    /// </summary>
    public void Hide()
    {
        _victoryCutInView?.KillTweens();
        HideVictoryCutInUi();
        HideLegacyResultUi();
        SetCutInRootsInactive();

        if (_canvasRoot != null)
        {
            _canvasRoot.SetActive(false);
        }

        _isNavigating = false;
    }

    private bool TryLoadResultScene()
    {
        if (string.IsNullOrEmpty(_resultSceneName))
        {
            Debug.LogError("[ResultView] 遷移先シーン名が空です。", this);
            return false;
        }

#if UNITY_EDITOR
        if (_skipLoadIfSceneMissingInEditor && !IsSceneInBuildSettings(_resultSceneName))
        {
            Debug.LogWarning(
                $"[ResultView] '{_resultSceneName}' が Build Settings に無いため遷移をスキップしました（Editor のみ）。",
                this);
            return false;
        }
#endif

        if (SceneTransferManager.Instance == null)
        {
            Debug.LogError("[ResultView] SceneTransferManager が見つかりません。", this);
            return false;
        }

        SceneTransferManager.Instance.ClearHistory();
        SceneTransferManager.Instance.LoadNewScene(_resultSceneName, saveToHistory: false);
        return true;
    }

    private static bool IsSceneInBuildSettings(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
            {
                return true;
            }
        }

        return false;
    }

    private void HideLegacyResultUi()
    {
        // VictoryRoot / DefeatRoot はカットイン階層のためここでは触らない（VictoryCutInView が制御）。

        if (_outcomeText != null)
        {
            _outcomeText.gameObject.SetActive(false);
        }

        if (_rewardsBlock != null)
        {
            _rewardsBlock.SetActive(false);
        }

        if (_toTitleButton != null)
        {
            _toTitleButton.gameObject.SetActive(false);
        }

        if (_toStageButton != null)
        {
            _toStageButton.gameObject.SetActive(false);
        }
    }

    private void HideVictoryCutInUi()
    {
        _victoryCutInView?.KillTweens();
    }

    private void SetCutInRootsInactive()
    {
        if (_victoryRoot != null)
        {
            _victoryRoot.SetActive(false);
        }

        if (_defeatRoot != null)
        {
            _defeatRoot.SetActive(false);
        }
    }

    private void OnToTitleClicked()
    {
        Navigate(_toTitleButton, SceneNames.Title);
    }

    private void OnToStageClicked()
    {
        Navigate(_toStageButton, SceneNames.Stage);
    }

    private void Navigate(Button sourceButton, string sceneName)
    {
        if (_isNavigating)
        {
            return;
        }

        _isNavigating = true;
        SetButtonsInteractable(false);
        if (sourceButton != null)
        {
            sourceButton.interactable = false;
        }

        if (SceneTransferManager.Instance == null)
        {
            Debug.LogError("[ResultView] SceneTransferManager が見つかりません。", this);
            return;
        }

        SceneTransferManager.Instance.ClearHistory();
        SceneTransferManager.Instance.LoadNewScene(sceneName, saveToHistory: false);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (_toTitleButton != null)
        {
            _toTitleButton.interactable = interactable;
        }

        if (_toStageButton != null)
        {
            _toStageButton.interactable = interactable;
        }
    }
}
