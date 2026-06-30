using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// After daily rollover while the player is in an out-of-home nav scene, all Buttons temporarily navigate to Home.
/// Cleared when HomeScene loads; affected scenes get fresh button wiring on their next visit.
/// </summary>
public static class DailyRolloverNavRedirect
{
    static readonly HashSet<string> AffectedSceneNames = new()
    {
        SceneNames.Craft,
        SceneNames.Outfit,
        SceneNames.Result,
        SceneNames.Scan,
        SceneNames.Stage,
    };

    static bool s_redirectActive;
    static bool s_navInProgress;
    static DailyRolloverNavRedirectRunner s_runner;

    public static bool IsRedirectActive => s_redirectActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        HomeDailyDistanceSchedule.DailyRolloverApplied -= OnDailyRolloverApplied;
        HomeDailyDistanceSchedule.DailyRolloverApplied += OnDailyRolloverApplied;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureRunner();
    }

    public static bool IsAffectedScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && AffectedSceneNames.Contains(sceneName);
    }

    public static bool TryResolveNavigationTarget(ref string targetSceneName, ref bool saveToHistory)
    {
        if (!s_redirectActive || string.IsNullOrEmpty(targetSceneName))
        {
            return false;
        }

        string currentSceneName = SceneManager.GetActiveScene().name;
        if (!IsAffectedScene(currentSceneName) || targetSceneName == SceneNames.Home)
        {
            return false;
        }

        targetSceneName = SceneNames.Home;
        saveToHistory = false;
        return true;
    }

    public static bool TryRedirectGoBackToHome()
    {
        if (!s_redirectActive)
        {
            return false;
        }

        return IsAffectedScene(SceneManager.GetActiveScene().name);
    }

    public static void RequestNavigateHome(Button button)
    {
        if (!s_redirectActive || s_navInProgress)
        {
            return;
        }

        EnsureRunner();
        s_runner.StartCoroutine(NavigateHomeCoroutine(button));
    }

    static void OnDailyRolloverApplied()
    {
        if (!HomeDailyDistanceSchedule.IsEnabled)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!IsAffectedScene(activeScene.name))
        {
            return;
        }

        s_redirectActive = true;
        ApplyToScene(activeScene);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SceneNames.Home)
        {
            s_redirectActive = false;
            s_navInProgress = false;
            return;
        }

        if (s_redirectActive && IsAffectedScene(scene.name))
        {
            ApplyToScene(scene);
        }
    }

    internal static void TickActiveScene()
    {
        if (!s_redirectActive)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded || !IsAffectedScene(activeScene.name))
        {
            return;
        }

        ApplyToScene(activeScene);
    }

    static void ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || !IsAffectedScene(scene.name))
        {
            return;
        }

        Button[] buttons = UnityEngine.Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || button.gameObject.scene != scene)
            {
                continue;
            }

            DailyRolloverNavRedirectButtonHook.Install(button);
        }
    }

    static IEnumerator NavigateHomeCoroutine(Button button)
    {
        if (s_navInProgress)
        {
            yield break;
        }

        s_navInProgress = true;

        if (button != null)
        {
            button.interactable = false;
            UIButtonPressFeedback pressFeedback = button.GetComponent<UIButtonPressFeedback>();
            if (pressFeedback != null)
            {
                yield return pressFeedback.PlayClickConfirm();
            }
        }

        if (SceneTransferManager.Instance != null)
        {
            SceneTransferManager.Instance.ClearHistory();
            SceneTransferManager.Instance.LoadNewScene(SceneNames.Home, saveToHistory: false);
        }
        else
        {
            SceneManager.LoadScene(SceneNames.Home);
        }

        s_navInProgress = false;
    }

    static void EnsureRunner()
    {
        if (s_runner != null)
        {
            return;
        }

        var host = new GameObject(nameof(DailyRolloverNavRedirectRunner));
        UnityEngine.Object.DontDestroyOnLoad(host);
        s_runner = host.AddComponent<DailyRolloverNavRedirectRunner>();
    }
}

[DisallowMultipleComponent]
sealed class DailyRolloverNavRedirectRunner : MonoBehaviour
{
    void Update()
    {
        DailyRolloverNavRedirect.TickActiveScene();
    }
}

[DisallowMultipleComponent]
sealed class DailyRolloverNavRedirectButtonHook : MonoBehaviour
{
    Button _button;

    public static void Install(Button button)
    {
        if (button == null || !DailyRolloverNavRedirect.IsRedirectActive)
        {
            return;
        }

        DailyRolloverNavRedirectButtonHook hook = button.GetComponent<DailyRolloverNavRedirectButtonHook>();
        if (hook == null)
        {
            hook = button.gameObject.AddComponent<DailyRolloverNavRedirectButtonHook>();
        }

        hook.Refresh(button);
    }

    /// <summary>Re-applies redirect wiring so late-bound scene listeners (e.g. ScanButton) are overridden.</summary>
    public void Refresh(Button button)
    {
        if (button == null)
        {
            return;
        }

        _button = button;
        _button.onClick.RemoveAllListeners();
        _button.onClick.AddListener(HandleClick);
    }

    void HandleClick()
    {
        OutGameUiButtonClickSound.Play();
        DailyRolloverNavRedirect.RequestNavigateHome(_button);
    }
}
