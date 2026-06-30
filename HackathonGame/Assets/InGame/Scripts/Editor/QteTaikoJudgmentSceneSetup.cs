#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 太鼓 QTE 判定表示・空間判定帯のシーンセットアップ。
/// </summary>
public static class QteTaikoJudgmentSceneSetup
{
    private const string MenuDisplayPath = "InGame/Setup Taiko Judgment Display";
    private const string MenuZonesPath = "InGame/Setup Taiko Judgment Zones";

    private const string PrefabDir = "Assets/InGame/Prefabs";
    private const string PrefabPerfect = PrefabDir + "/QteJudgmentPerfect.prefab";
    private const string PrefabGood = PrefabDir + "/QteJudgmentGood.prefab";
    private const string PrefabMiss = PrefabDir + "/QteJudgmentMiss.prefab";
    private const string PrefabMissLegacy = PrefabDir + "/QteJudgmentBad.prefab";

    private const float DefaultGoodZoneWidth = 360f;
    private const float DefaultPerfectZoneWidth = 100f;
    private const float DefaultZoneHeight = 120f;

    [MenuItem(MenuDisplayPath)]
    public static void SetupTaikoJudgmentDisplay()
    {
        if (!TryGetTaikoHierarchy(out QteTaikoView taikoView, out Transform noteContainer, out Transform judgmentContainer))
        {
            return;
        }

        EnsureJudgmentPopups(judgmentContainer);
        SetupJudgmentZonesInternal(taikoView, judgmentContainer, noteContainer);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[QteTaikoJudgmentSceneSetup] 判定表示のセットアップが完了しました。");
    }

    [MenuItem(MenuZonesPath)]
    public static void SetupTaikoJudgmentZones()
    {
        if (!TryGetTaikoHierarchy(out QteTaikoView taikoView, out Transform noteContainer, out Transform judgmentContainer))
        {
            return;
        }

        SetupJudgmentZonesInternal(taikoView, judgmentContainer, noteContainer);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[QteTaikoJudgmentSceneSetup] 判定帯のセットアップが完了しました。");
    }

    private static bool TryGetTaikoHierarchy(
        out QteTaikoView taikoView,
        out Transform noteContainer,
        out Transform judgmentContainer)
    {
        taikoView = Object.FindFirstObjectByType<QteTaikoView>(FindObjectsInactive.Include);
        noteContainer = null;
        judgmentContainer = null;

        if (taikoView == null)
        {
            Debug.LogError("[QteTaikoJudgmentSceneSetup] QteTaikoView が見つかりません。");
            return false;
        }

        Transform layerRoot = taikoView.transform;
        noteContainer = FindChild(layerRoot, "TaikoNoteContainer");
        if (noteContainer == null)
        {
            Debug.LogError("[QteTaikoJudgmentSceneSetup] TaikoNoteContainer が見つかりません。");
            return false;
        }

        judgmentContainer = FindChild(layerRoot, "TaikoJudgmentContainer");
        if (judgmentContainer == null)
        {
            GameObject containerGo = Object.Instantiate(noteContainer.gameObject, layerRoot);
            containerGo.name = "TaikoJudgmentContainer";
            judgmentContainer = containerGo.transform;
            judgmentContainer.SetSiblingIndex(noteContainer.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(containerGo, "Create TaikoJudgmentContainer");
        }

        return true;
    }

    private static void EnsureJudgmentPopups(Transform judgmentContainer)
    {
        QteTaikoJudgmentPopupView perfectPrefab = EnsureJudgmentPrefab(
            PrefabPerfect,
            "QteJudgmentPerfect",
            new Color(1f, 0.92f, 0.25f, 1f));
        QteTaikoJudgmentPopupView goodPrefab = EnsureJudgmentPrefab(
            PrefabGood,
            "QteJudgmentGood",
            new Color(0.35f, 0.85f, 1f, 1f));
        QteTaikoJudgmentPopupView missPrefab = EnsureJudgmentPrefab(
            PrefabMiss,
            PrefabMissLegacy,
            "QteJudgmentMiss",
            new Color(0.75f, 0.35f, 0.35f, 1f));

        QteTaikoJudgmentDisplay display = judgmentContainer.GetComponent<QteTaikoJudgmentDisplay>();
        if (display == null)
        {
            display = Undo.AddComponent<QteTaikoJudgmentDisplay>(judgmentContainer.gameObject);
        }

        SerializedObject serializedDisplay = new SerializedObject(display);
        serializedDisplay.FindProperty("_container").objectReferenceValue = judgmentContainer as RectTransform;
        serializedDisplay.FindProperty("_perfectPrefab").objectReferenceValue = perfectPrefab;
        serializedDisplay.FindProperty("_goodPrefab").objectReferenceValue = goodPrefab;
        serializedDisplay.FindProperty("_missPrefab").objectReferenceValue = missPrefab;
        serializedDisplay.FindProperty("_displayDuration").floatValue = 0.8f;
        serializedDisplay.FindProperty("_poolSizePerType").intValue = 4;
        serializedDisplay.ApplyModifiedPropertiesWithoutUndo();

        TaikoScrollQteRunner runner = judgmentContainer.parent.GetComponent<TaikoScrollQteRunner>();
        if (runner != null)
        {
            SerializedObject serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty("_judgmentDisplay").objectReferenceValue = display;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetupJudgmentZonesInternal(
        QteTaikoView taikoView,
        Transform judgmentContainer,
        Transform noteContainer)
    {
        float judgmentLineX = 0f;
        TaikoScrollQteRunner runner = taikoView.GetComponent<TaikoScrollQteRunner>();
        if (runner != null)
        {
            SerializedObject serializedRunner = new SerializedObject(runner);
            SerializedProperty settingsProp = serializedRunner.FindProperty("_settings");
            if (settingsProp.objectReferenceValue is QteTaikoSettingsSO settings)
            {
                judgmentLineX = settings.JudgmentLineX;
            }
        }

        RectTransform goodZone = EnsureZoneRect(
            judgmentContainer,
            "Zone_Good",
            new Color(0.2f, 0.85f, 0.35f, 0.25f),
            DefaultGoodZoneWidth,
            DefaultZoneHeight,
            judgmentLineX,
            0);
        RectTransform perfectZone = EnsureZoneRect(
            judgmentContainer,
            "Zone_Perfect",
            new Color(1f, 0.9f, 0.2f, 0.35f),
            DefaultPerfectZoneWidth,
            DefaultZoneHeight,
            judgmentLineX,
            1);

        QteTaikoJudgmentZones zones = judgmentContainer.GetComponent<QteTaikoJudgmentZones>();
        if (zones == null)
        {
            zones = Undo.AddComponent<QteTaikoJudgmentZones>(judgmentContainer.gameObject);
        }

        SerializedObject serializedZones = new SerializedObject(zones);
        serializedZones.FindProperty("_perfectZone").objectReferenceValue = perfectZone;
        serializedZones.FindProperty("_goodZone").objectReferenceValue = goodZone;
        serializedZones.ApplyModifiedPropertiesWithoutUndo();

        if (runner != null)
        {
            SerializedObject serializedRunner = new SerializedObject(runner);
            serializedRunner.FindProperty("_judgmentZones").objectReferenceValue = zones;
            serializedRunner.ApplyModifiedPropertiesWithoutUndo();
        }

        AlignJudgmentContainerToNotes(noteContainer, judgmentContainer);
    }

    private static RectTransform EnsureZoneRect(
        Transform parent,
        string name,
        Color color,
        float width,
        float height,
        float anchoredX,
        int siblingIndex)
    {
        Transform existing = FindChild(parent, name);
        GameObject zoneGo;
        if (existing != null)
        {
            zoneGo = existing.gameObject;
        }
        else
        {
            zoneGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(zoneGo, "Create " + name);
            zoneGo.transform.SetParent(parent, false);
        }

        zoneGo.transform.SetSiblingIndex(siblingIndex);

        Image image = zoneGo.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = color;

        RectTransform rt = zoneGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, height);
        rt.anchoredPosition = new Vector2(anchoredX, 0f);

        return rt;
    }

    private static void AlignJudgmentContainerToNotes(Transform noteContainer, Transform judgmentContainer)
    {
        RectTransform noteRect = noteContainer as RectTransform;
        RectTransform judgmentRect = judgmentContainer as RectTransform;
        if (noteRect == null || judgmentRect == null)
        {
            return;
        }

        judgmentRect.anchorMin = noteRect.anchorMin;
        judgmentRect.anchorMax = noteRect.anchorMax;
        judgmentRect.pivot = noteRect.pivot;
        judgmentRect.anchoredPosition = noteRect.anchoredPosition;
        judgmentRect.sizeDelta = noteRect.sizeDelta;
        judgmentRect.localRotation = noteRect.localRotation;
        judgmentRect.localScale = noteRect.localScale;
    }

    private static QteTaikoJudgmentPopupView EnsureJudgmentPrefab(
        string path,
        string objectName,
        Color color)
    {
        return EnsureJudgmentPrefab(path, null, objectName, color);
    }

    private static QteTaikoJudgmentPopupView EnsureJudgmentPrefab(
        string path,
        string legacyPath,
        string objectName,
        Color color)
    {
        QteTaikoJudgmentPopupView existing = AssetDatabase.LoadAssetAtPath<QteTaikoJudgmentPopupView>(path);
        if (existing != null)
        {
            return existing;
        }

        if (!string.IsNullOrEmpty(legacyPath))
        {
            QteTaikoJudgmentPopupView legacy = AssetDatabase.LoadAssetAtPath<QteTaikoJudgmentPopupView>(legacyPath);
            if (legacy != null)
            {
                return legacy;
            }
        }

        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Image image = root.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = color;

        RectTransform rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 80f);

        QteTaikoJudgmentPopupView view = root.AddComponent<QteTaikoJudgmentPopupView>();
        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("_rectTransform").objectReferenceValue = rt;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            AssetDatabase.CreateFolder("Assets/InGame", "Prefabs");
        }

        QteTaikoJudgmentPopupView prefab = PrefabUtility.SaveAsPrefabAsset(root, path)
            .GetComponent<QteTaikoJudgmentPopupView>();
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
