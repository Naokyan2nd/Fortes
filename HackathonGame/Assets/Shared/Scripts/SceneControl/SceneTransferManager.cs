using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 画面遷移と履歴（戻る機能）を管理するマネージャー
/// ゲーム起動時に自動生成され、シーンをまたいで保持されます。
/// </summary>
public class SceneTransferManager : MonoBehaviour
{
    // どこからでもアクセスできる唯一のインスタンス（シングルトン）
    public static SceneTransferManager Instance { get; private set; }

    // 遷移したシーンの履歴を記録するスタック（戻るボタン用）
    private Stack<string> sceneHistory = new Stack<string>();

    private enum FrameRateTier
    {
        Standard = 60,
        HighRefresh = 120,
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject(nameof(SceneTransferManager));
        Instance = go.AddComponent<SceneTransferManager>();
        DontDestroyOnLoad(go);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigurePlatformFrameRateCap();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyFrameRateForScene(scene.name);
    }

    /// <summary>
    /// iOS 真机：允许 ProMotion；其它平台固定 60。
    /// </summary>
    private static void ConfigurePlatformFrameRateCap()
    {
        QualitySettings.vSyncCount = 0;
#if UNITY_IOS && !UNITY_EDITOR
        Application.targetFrameRate = GetDeviceMaxRefreshRate();
#else
        Application.targetFrameRate = (int)FrameRateTier.Standard;
#endif
    }

    private static int GetDeviceMaxRefreshRate()
    {
        RefreshRate ratio = Screen.currentResolution.refreshRateRatio;
        int hz = Mathf.RoundToInt((float)ratio.value);
        if (hz <= 0)
        {
            hz = (int)FrameRateTier.Standard;
        }

        return Mathf.Min(hz, (int)FrameRateTier.HighRefresh);
    }

    private static void ApplyFrameRateForScene(string sceneName)
    {
        QualitySettings.vSyncCount = 0;
        int target = ResolveTargetFrameRate(sceneName);
        Application.targetFrameRate = target;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[SceneTransferManager] Scene={sceneName} targetFrameRate={target}");
#endif
    }

    private static int ResolveTargetFrameRate(string sceneName)
    {
#if UNITY_IOS && !UNITY_EDITOR
        FrameRateTier tier = GetTierForScene(sceneName);
        int deviceMax = GetDeviceMaxRefreshRate();
        return Mathf.Min((int)tier, deviceMax);
#else
        return (int)FrameRateTier.Standard;
#endif
    }

    private static FrameRateTier GetTierForScene(string sceneName)
    {
        switch (sceneName)
        {
            case SceneNames.Home:
            case SceneNames.Battle:
                return FrameRateTier.HighRefresh;
            default:
                return FrameRateTier.Standard;
        }
    }

    /// <summary>
    /// 新しいシーンへ遷移する
    /// </summary>
    /// <param name="targetSceneName">遷移先のシーン名</param>
    /// <param name="saveToHistory">履歴に保存するかどうか（デフォルトはtrue）</param>
    public void LoadNewScene(string targetSceneName, bool saveToHistory = true)
    {
        DailyRolloverNavRedirect.TryResolveNavigationTarget(ref targetSceneName, ref saveToHistory);

        if (saveToHistory)
        {
            // 現在アクティブなシーン名を履歴スタックに保存
            string currentSceneName = SceneManager.GetActiveScene().name;
            sceneHistory.Push(currentSceneName);
        }

        // シーンをロード
        SceneManager.LoadScene(targetSceneName);
    }

    /// <summary>
    /// 一つ前のシーンに戻る
    /// </summary>
    public void GoBack()
    {
        if (DailyRolloverNavRedirect.TryRedirectGoBackToHome())
        {
            ClearHistory();
            SceneManager.LoadScene(SceneNames.Home);
            return;
        }

        if (sceneHistory.Count > 0)
        {
            // スタックから直前のシーン名を取り出す
            string previousScene = sceneHistory.Pop();
            SceneManager.LoadScene(previousScene);
        }
        else
        {
            Debug.LogWarning("戻る履歴がありません。デフォルトでHomeに移動します。");
            SceneManager.LoadScene(SceneNames.Home);
        }
    }

    /// <summary>
    /// 履歴の直前が target と一致すれば GoBack、そうでなければ履歴を増やさず target へ遷移する。
    /// </summary>
    public void ReturnToScene(string targetSceneName)
    {
        if (!string.IsNullOrEmpty(targetSceneName)
            && sceneHistory.Count > 0
            && sceneHistory.Peek() == targetSceneName)
        {
            GoBack();
            return;
        }

        LoadNewScene(targetSceneName, saveToHistory: false);
    }

    /// <summary>
    /// 履歴スタックを完全にクリアする（ResultからHomeに戻る時などに使用）
    /// </summary>
    public void ClearHistory()
    {
        sceneHistory.Clear();
    }
}
