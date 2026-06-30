using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds <see cref="UIButtonClickSound"/> to all Buttons in out-of-game nav scenes on load.
/// </summary>
public static class OutGameUIButtonClickSoundBootstrap
{
    static readonly HashSet<string> TargetSceneNames = new()
    {
        SceneNames.Home,
        SceneNames.Craft,
        SceneNames.Outfit,
        SceneNames.Result,
        SceneNames.Scan,
        SceneNames.Stage,
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsTargetScene(scene.name))
        {
            return;
        }

        ApplyToScene(scene);
        ScheduleLateRefresh(scene);
    }

    static bool IsTargetScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && TargetSceneNames.Contains(sceneName);
    }

    static void ApplyToScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || !IsTargetScene(scene.name))
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

            UIButtonClickSound.EnsureOnButton(button);
        }
    }

    static void ScheduleLateRefresh(Scene scene)
    {
        if (!scene.IsValid() || !IsTargetScene(scene.name))
        {
            return;
        }

        var hostObject = new GameObject(nameof(OutGameUIButtonClickSoundBootstrap) + "_LateRefresh");
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
            StartCoroutine(RefreshRoutine());
        }

        IEnumerator RefreshRoutine()
        {
            for (int i = 0; i < 3; i++)
            {
                yield return null;
                ApplyToScene(_scene);
            }

            Destroy(gameObject);
        }
    }
}
