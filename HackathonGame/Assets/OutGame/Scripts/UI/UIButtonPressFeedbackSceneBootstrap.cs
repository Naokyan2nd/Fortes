using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds <see cref="UIButtonPressFeedback"/> to scene Buttons on load (press scale/color like HomeScene).
/// Home, Scan, Main, and DistanceDebug manage their own feedback and are skipped.
/// </summary>
public static class UIButtonPressFeedbackSceneBootstrap
{
    static readonly HashSet<string> ExcludedSceneNames = new()
    {
        SceneNames.Home,
        SceneNames.Scan,
        SceneNames.Main,
        SceneNames.InGameTutorial,
        SceneNames.DistanceDebug,
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToScene(scene);
        ScheduleLateRefresh(scene);
    }

    static void ScheduleLateRefresh(Scene scene)
    {
        if (!scene.IsValid() || ExcludedSceneNames.Contains(scene.name))
        {
            return;
        }

        var hostObject = new GameObject(nameof(UIButtonPressFeedbackSceneBootstrap) + "_LateRefresh");
        hostObject.hideFlags = HideFlags.HideAndDontSave;
        var host = hostObject.AddComponent<LateRefreshHost>();
        host.Begin(scene);
    }

    sealed class LateRefreshHost : MonoBehaviour
    {
        Scene _scene;

        public void Begin(Scene scene)
        {
            _scene = scene;
            StartCoroutine(RefreshNextFrame());
        }

        IEnumerator RefreshNextFrame()
        {
            yield return null;
            ApplyLateRefresh(_scene);
            Destroy(gameObject);
        }
    }

    static void ApplyLateRefresh(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || ExcludedSceneNames.Contains(scene.name))
        {
            return;
        }

        UIButtonPressFeedback[] feedbacks = Object.FindObjectsByType<UIButtonPressFeedback>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < feedbacks.Length; i++)
        {
            UIButtonPressFeedback feedback = feedbacks[i];
            if (feedback == null || feedback.gameObject.scene != scene)
            {
                continue;
            }

            Button button = feedback.GetComponent<Button>();
            if (ShouldSkipButton(button))
            {
                continue;
            }

            UIButtonPressFeedback.RestoreNormalVisual(button);
        }
    }

    static void ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || ExcludedSceneNames.Contains(scene.name))
        {
            return;
        }

        Button[] buttons = Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || button.gameObject.scene != scene)
            {
                continue;
            }

            if (ShouldSkipButton(button))
            {
                continue;
            }

            EnsureOnButton(button);
        }
    }

    static bool ShouldSkipButton(Button button)
    {
        return button != null && button.GetComponentInParent<CommandPanelView>() != null;
    }

    public static void EnsureOnButton(Button button)
    {
        if (button == null || ShouldSkipButton(button))
        {
            return;
        }

        if (button.GetComponent<UIButtonPressFeedback>() == null)
        {
            button.gameObject.AddComponent<UIButtonPressFeedback>();
        }

        UIButtonPressFeedback.RestoreNormalVisual(button);
    }
}
