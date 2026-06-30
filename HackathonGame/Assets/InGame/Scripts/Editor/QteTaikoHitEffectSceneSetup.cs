#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 太鼓 QTE タップヒットエフェクトの Prefab・シーンセットアップ。
/// </summary>
public static class QteTaikoHitEffectSceneSetup
{
    private const string MenuPath = "InGame/Setup Taiko Hit Effect";
    private const string PrefabPath = "Assets/InGame/Prefabs/UI/QteTaikoHitEffect.prefab";

    [MenuItem(MenuPath)]
    public static void SetupTaikoHitEffect()
    {
        QteTaikoView taikoView = Object.FindFirstObjectByType<QteTaikoView>(FindObjectsInactive.Include);
        if (taikoView == null)
        {
            Debug.LogError("[QteTaikoHitEffectSceneSetup] QteTaikoView が見つかりません。");
            return;
        }

        Transform noteContainer = FindChild(taikoView.transform, "TaikoNoteContainer");
        if (noteContainer == null)
        {
            Debug.LogError("[QteTaikoHitEffectSceneSetup] TaikoNoteContainer が見つかりません。");
            return;
        }

        QteTaikoHitEffectView prefab = EnsureHitEffectPrefab();
        QteTaikoHitEffectDisplay display = noteContainer.GetComponent<QteTaikoHitEffectDisplay>();
        if (display == null)
        {
            display = Undo.AddComponent<QteTaikoHitEffectDisplay>(noteContainer.gameObject);
        }

        SerializedObject serializedDisplay = new SerializedObject(display);
        serializedDisplay.FindProperty("_container").objectReferenceValue = noteContainer as RectTransform;
        serializedDisplay.FindProperty("_prefab").objectReferenceValue = prefab;
        serializedDisplay.FindProperty("_poolPrewarmCount").intValue = 4;
        serializedDisplay.ApplyModifiedPropertiesWithoutUndo();

        TaikoScrollQteRunner runner = taikoView.GetComponent<TaikoScrollQteRunner>();
        if (runner != null)
        {
            SerializedObject serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty("_hitEffectDisplay").objectReferenceValue = display;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[QteTaikoHitEffectSceneSetup] ヒットエフェクトのセットアップが完了しました。");
    }

    private static QteTaikoHitEffectView EnsureHitEffectPrefab()
    {
        QteTaikoHitEffectView existing = AssetDatabase.LoadAssetAtPath<QteTaikoHitEffectView>(PrefabPath);
        if (existing != null)
        {
            return existing;
        }

        if (!AssetDatabase.IsValidFolder("Assets/InGame/Prefabs/UI"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/InGame/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets/InGame", "Prefabs");
            }

            AssetDatabase.CreateFolder("Assets/InGame/Prefabs", "UI");
        }

        GameObject root = new GameObject(
            "QteTaikoHitEffect",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(QteTaikoHitEffectView));

        Image image = root.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = new Color(1f, 0.95f, 0.5f, 0.85f);

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(96f, 96f);

        QteTaikoHitEffectView view = root.GetComponent<QteTaikoHitEffectView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("_canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        serializedView.FindProperty("_rectTransform").objectReferenceValue = rt;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);

        QteTaikoHitEffectView prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath)
            .GetComponent<QteTaikoHitEffectView>();
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }
        }

        return null;
    }
}
#endif
